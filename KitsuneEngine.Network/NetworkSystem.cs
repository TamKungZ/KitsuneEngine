using DefaultEcs;
using DefaultEcs.System;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace KitsuneEngine.Network
{
    public class NetworkSystem : AComponentSystem<float, NetworkComponent>, IDisposable
    {
        private readonly NetworkManager _networkManager;
        private readonly World _world;
        private readonly Dictionary<uint, Entity> _networkedEntities;
        private float _time;

        public NetworkSystem(World world, NetworkManager networkManager)
            : base(world)
        {
            _world = world;
            _networkManager = networkManager;
            _networkedEntities = new Dictionary<uint, Entity>();

            SetupNetworkEvents();
        }

        private void SetupNetworkEvents()
        {
            _networkManager.OnMessageReceived += OnNetworkMessage;
            _networkManager.OnConnected += OnNetworkConnected;
            _networkManager.OnDisconnected += OnNetworkDisconnected;
        }

        protected override void Update(float deltaTime, Span<NetworkComponent> components)
        {
            _time += deltaTime;

            foreach (ref var component in components)
            {
                if (!component.IsReplicated) continue;

                // Update network transforms
                if (_time - component.LastUpdateTime >= component.UpdateRate)
                {
                    SyncEntity(component);
                    component.LastUpdateTime = _time;
                }

                // Process RPC queue
                ProcessRpcQueue(component);
            }
        }

        private void SyncEntity(NetworkComponent component)
        {
            if (!component.IsOwner || !_networkManager.IsConnected) return;

            // Create sync packet
            var packet = new EntitySyncPacket
            {
                EntityId = component.NetworkId
            };

            // Sync properties from dictionary
            if (component.SyncProperties.ContainsKey("Position"))
            {
                packet.Position = (NetVector3)component.SyncProperties["Position"];
            }
            if (component.SyncProperties.ContainsKey("Rotation"))
            {
                packet.Rotation = (NetQuaternion)component.SyncProperties["Rotation"];
            }
            if (component.SyncProperties.ContainsKey("Scale"))
            {
                packet.Scale = (NetVector3)component.SyncProperties["Scale"];
            }

            // Send over network
            var message = new NetworkMessage
            {
                Type = MessageType.EntitySync,
                SenderId = _networkManager.LocalPeerId,
                TargetId = 0, // Broadcast
                Data = Serialize(packet),
                Timestamp = DateTime.Now
            };

            _networkManager.SendMessage(message, DeliveryMethod.UnreliableSequenced);
        }

        private void ProcessRpcQueue(NetworkComponent component)
        {
            foreach (var rpc in component.RpcQueue)
            {
                _networkManager.SendRPC(rpc);
            }
            component.RpcQueue.Clear();
        }

        private void OnNetworkMessage(object? sender, NetworkEventArgs e)
        {
            if (e.Message == null) return;

            switch (e.Message.Type)
            {
                case MessageType.EntitySync:
                    HandleEntitySync(e.Message);
                    break;
                case MessageType.RPC:
                    HandleRPC(e.Message);
                    break;
                case MessageType.SceneSync:
                    HandleSceneSync(e.Message);
                    break;
                case MessageType.InputSync:
                    HandleInputSync(e.Message);
                    break;
            }
        }

        private void HandleEntitySync(NetworkMessage message)
        {
            try
            {
                var packet = Deserialize<EntitySyncPacket>(message.Data);

                if (_networkedEntities.TryGetValue(packet.EntityId, out var entity))
                {
                    // Update entity through sync properties
                    if (entity.Has<NetworkComponent>())
                    {
                        var component = entity.Get<NetworkComponent>();
                        
                        // Update sync properties
                        component.SyncProperties["Position"] = packet.Position;
                        component.SyncProperties["Rotation"] = packet.Rotation;
                        component.SyncProperties["Scale"] = packet.Scale;

                        // If there's a NetworkTransform, update target values
                        if (entity.Has<NetworkTransform>())
                        {
                            var netTransform = entity.Get<NetworkTransform>();
                            netTransform.TargetPosition = packet.Position;
                            netTransform.TargetRotation = packet.Rotation;
                            netTransform.InterpolationProgress = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling entity sync: {ex.Message}");
            }
        }

        private void HandleRPC(NetworkMessage message)
        {
            try
            {
                var packet = Deserialize<RPCPacket>(message.Data);
                // Invoke RPC method on appropriate entities
                Console.WriteLine($"RPC received: {packet.MethodName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling RPC: {ex.Message}");
            }
        }

        private void HandleSceneSync(NetworkMessage message)
        {
            // Synchronize scene state
            Console.WriteLine("Scene sync received");
        }

        private void HandleInputSync(NetworkMessage message)
        {
            // Synchronize input state
            Console.WriteLine("Input sync received");
        }

        private void OnNetworkConnected(object? sender, NetworkEventArgs e)
        {
            Console.WriteLine($"Network connected with ID: {e.PeerId}");

            if (_networkManager.IsServer)
            {
                // Send initial state to new client
                SendInitialState(e.PeerId);
            }
        }

        private void OnNetworkDisconnected(object? sender, NetworkEventArgs e)
        {
            Console.WriteLine($"Network disconnected: {e.PeerId}");

            // Clean up entities owned by disconnected peer
            RemovePeerEntities(e.PeerId);
        }

        private void SendInitialState(uint peerId)
        {
            // Send all networked entities to new client
            foreach (var entity in _world.GetEntities().With<NetworkComponent>().AsEnumerable())
            {
                var component = entity.Get<NetworkComponent>();
                SyncEntity(component);
            }
        }

        private void RemovePeerEntities(uint peerId)
        {
            var entitiesToRemove = new List<Entity>();

            foreach (var entity in _world.GetEntities().With<NetworkComponent>().AsEnumerable())
            {
                var component = entity.Get<NetworkComponent>();
                // TODO: Check if entity is owned by disconnected peer
                // This would require tracking owner ID in NetworkComponent
            }

            foreach (var entity in entitiesToRemove)
            {
                entity.Dispose();
            }
        }

        private byte[] Serialize<T>(T obj)
        {
            // Using System.Text.Json for serialization
            return System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(obj));
        }

        private T Deserialize<T>(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            return System.Text.Json.JsonSerializer.Deserialize<T>(json)!;
        }

        public new void Dispose()
        {
            _networkManager.OnMessageReceived -= OnNetworkMessage;
            _networkManager.OnConnected -= OnNetworkConnected;
            _networkManager.OnDisconnected -= OnNetworkDisconnected;
            base.Dispose();
        }
    }
}
