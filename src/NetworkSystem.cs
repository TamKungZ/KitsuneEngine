using DefaultEcs;
using DefaultEcs.System;
using System;
using System.Collections.Generic;
using System.Numerics;
using KitsuneEngine.Core;

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

            // Add transform if entity has it
            if (component.Entity.Has<Transform>())
            {
                var transform = component.Entity.Get<Transform>();
                packet.Position = transform.Position;
                packet.Rotation = transform.Rotation;
                packet.Scale = transform.Scale;
            }

            // Add sync properties
            // TODO: Serialize SyncProperties

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
                    // Update entity transform
                    if (entity.Has<Transform>())
                    {
                        var transform = entity.Get<Transform>();

                        // Interpolate if needed
                        if (entity.Has<NetworkTransform>())
                        {
                            var netTransform = entity.Get<NetworkTransform>();
                            netTransform.TargetPosition = packet.Position;
                            netTransform.TargetRotation = packet.Rotation;
                            netTransform.InterpolationProgress = 0;
                        }
                        else
                        {
                            transform.Position = packet.Position;
                            transform.Rotation = packet.Rotation;
                        }
                    }

                    // Update sync properties
                    if (entity.Has<NetworkComponent>())
                    {
                        var component = entity.Get<NetworkComponent>();
                        // TODO: Deserialize and update SyncProperties
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling RPC: {ex.Message}");
            }
        }

        private void HandleSceneSync(NetworkMessage message)
        {
            // Synchronize scene state
        }

        private void HandleInputSync(NetworkMessage message)
        {
            // Synchronize input state
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
            }

            foreach (var entity in entitiesToRemove)
            {
                entity.Dispose();
            }
        }

        private byte[] Serialize<T>(T obj)
        {
            // TODO: Implement serialization (MessagePack, JSON, etc.)
            return System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(obj));
        }

        private T Deserialize<T>(byte[] data)
        {
            // TODO: Implement deserialization
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