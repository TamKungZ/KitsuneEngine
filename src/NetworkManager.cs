using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace KitsuneEngine.Network
{
    public class NetworkManager : IDisposable
    {
        private NetManager? _netManager;
        private EventBasedNetListener _listener;
        private NetDataWriter _dataWriter;
        private NetPacketProcessor _packetProcessor;

        private readonly NetworkConfig _config;
        private readonly ConcurrentDictionary<uint, NetPeer> _peers;
        private readonly ConcurrentQueue<NetworkMessage> _incomingMessages;
        private readonly ConcurrentQueue<NetworkEventArgs> _networkEvents;
        private readonly Dictionary<MessageType, Action<NetworkMessage>> _messageHandlers;

        private uint _localPeerId = 0;
        private bool _isServer = false;
        private bool _isRunning = false;
        private CancellationTokenSource? _cancellationTokenSource;

        // Events
        public event NetworkEventHandler? OnConnected;
        public event NetworkEventHandler? OnDisconnected;
        public event NetworkEventHandler? OnMessageReceived;
        public event Action<string>? OnLogMessage;

        public bool IsServer => _isServer;
        public bool IsConnected => _netManager?.IsRunning == true && _localPeerId > 0;
        public uint LocalPeerId => _localPeerId;
        public int ConnectedPeers => _peers.Count;

        public NetworkManager(NetworkConfig? config = null)
        {
            _config = config ?? new NetworkConfig();
            _peers = new ConcurrentDictionary<uint, NetPeer>();
            _incomingMessages = new ConcurrentQueue<NetworkMessage>();
            _networkEvents = new ConcurrentQueue<NetworkEventArgs>();
            _messageHandlers = new Dictionary<MessageType, Action<NetworkMessage>>();
            _dataWriter = new NetDataWriter();
            _listener = new EventBasedNetListener();
            _packetProcessor = new NetPacketProcessor();

            SetupListeners();
            RegisterPacketTypes();
        }

        private void SetupListeners()
        {
            _listener.ConnectionRequestEvent += request =>
            {
                if (_peers.Count < _config.MaxConnections)
                {
                    request.AcceptIfKey("KitsuneEngine");
                    Log("Connection accepted");
                }
                else
                {
                    request.Reject();
                    Log("Connection rejected: Max connections reached");
                }
            };

            _listener.PeerConnectedEvent += peer =>
            {
                uint peerId = (uint)peer.Id;
                _peers[peerId] = peer;

                // Assign peer ID if we're the server
                if (_isServer)
                {
                    SendPeerId(peer, peerId);
                }

                Log($"Peer connected: {peer.Address}:{peer.Port}");
                _networkEvents.Enqueue(new NetworkEventArgs { PeerId = peerId });
                OnConnected?.Invoke(this, new NetworkEventArgs { PeerId = peerId });
            };

            _listener.PeerDisconnectedEvent += (peer, info) =>
            {
                uint peerId = (uint)peer.Id;
                _peers.TryRemove(peerId, out _);

                Log($"Peer disconnected: {peer.Address}:{peer.Port}, Reason: {info.Reason}");
                _networkEvents.Enqueue(new NetworkEventArgs { PeerId = peerId });
                OnDisconnected?.Invoke(this, new NetworkEventArgs { PeerId = peerId });
            };

            _listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
            {
                try
                {
                    var messageType = (MessageType)reader.GetByte();
                    var senderId = reader.GetUInt();
                    var targetId = reader.GetUInt();
                    var timestamp = DateTime.FromBinary(reader.GetLong());

                    var data = reader.GetRemainingBytes();

                    var message = new NetworkMessage
                    {
                        Type = messageType,
                        SenderId = senderId,
                        TargetId = targetId,
                        Channel = (byte)channel,
                        Data = data,
                        Timestamp = timestamp,
                        IsReliable = IsReliableDeliveryMethod(method)
                    };

                    _incomingMessages.Enqueue(message);
                }
                catch (Exception ex)
                {
                    Log($"Error receiving message: {ex.Message}");
                }
            };

            _listener.NetworkErrorEvent += (endPoint, error) =>
            {
                Log($"Network error: {error}");
            };
        }

        private bool IsReliableDeliveryMethod(LiteNetLib.DeliveryMethod method)
        {
            return method == LiteNetLib.DeliveryMethod.ReliableUnordered ||
                   method == LiteNetLib.DeliveryMethod.ReliableSequenced ||
                   method == LiteNetLib.DeliveryMethod.ReliableOrdered;
        }

        private void RegisterPacketTypes()
        {
            // Register serialization methods for custom types
            _packetProcessor.RegisterNestedType(WriteVector2, ReadVector2);
            _packetProcessor.RegisterNestedType(WriteVector3, ReadVector3);
            _packetProcessor.RegisterNestedType(WriteQuaternion, ReadQuaternion);

            // Register packet classes
            _packetProcessor.SubscribeReusable<ConnectPacket, NetPeer>(OnConnectPacket);
            _packetProcessor.SubscribeReusable<EntitySyncPacket, NetPeer>(OnEntitySyncPacket);
            _packetProcessor.SubscribeReusable<RPCPacket, NetPeer>(OnRPCPacket);
        }

        // Serialization methods for custom types
        private void WriteVector2(NetDataWriter writer, NetVector2 vector)
        {
            writer.Put(vector.X);
            writer.Put(vector.Y);
        }

        private NetVector2 ReadVector2(NetDataReader reader)
        {
            return new NetVector2(reader.GetFloat(), reader.GetFloat());
        }

        private void WriteVector3(NetDataWriter writer, NetVector3 vector)
        {
            writer.Put(vector.X);
            writer.Put(vector.Y);
            writer.Put(vector.Z);
        }

        private NetVector3 ReadVector3(NetDataReader reader)
        {
            return new NetVector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }

        private void WriteQuaternion(NetDataWriter writer, NetQuaternion quat)
        {
            writer.Put(quat.X);
            writer.Put(quat.Y);
            writer.Put(quat.Z);
            writer.Put(quat.W);
        }

        private NetQuaternion ReadQuaternion(NetDataReader reader)
        {
            return new NetQuaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }

        public void StartServer()
        {
            if (_isRunning) return;

            _isServer = true;
            _localPeerId = 1; // Server always has ID 1

            _netManager = new NetManager(_listener)
            {
                AutoRecycle = true,
                EnableStatistics = true,
                UnconnectedMessagesEnabled = true,
                BroadcastReceiveEnabled = true,
                IPv6Enabled = false,
                DisconnectTimeout = _config.Timeout,
                PingInterval = _config.PingInterval,
                UpdateTime = 15
            };

            _netManager.Start(_config.Port);
            _isRunning = true;

            Log($"Server started on port {_config.Port}");

            // Start update loop
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => UpdateLoop(_cancellationTokenSource.Token));
        }

        public void StartClient()
        {
            if (_isRunning) return;

            _isServer = false;

            _netManager = new NetManager(_listener)
            {
                AutoRecycle = true,
                EnableStatistics = true,
                UnconnectedMessagesEnabled = true,
                IPv6Enabled = false,
                DisconnectTimeout = _config.Timeout,
                PingInterval = _config.PingInterval,
                UpdateTime = 15
            };

            _netManager.Start();
            _isRunning = true;

            Log("Client started");

            // Start update loop
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => UpdateLoop(_cancellationTokenSource.Token));
        }

        public void Connect(string address, int port)
        {
            if (!_isRunning || _isServer) return;

            _netManager?.Connect(address, port, "KitsuneEngine");
            Log($"Connecting to {address}:{port}...");
        }

        public void Disconnect()
        {
            _netManager?.DisconnectAll();
            _peers.Clear();
            _localPeerId = 0;

            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = null;
            }

            Log("Disconnected");
        }

        private async Task UpdateLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    _netManager?.PollEvents();
                    ProcessNetworkEvents();
                    ProcessIncomingMessages();

                    if (_isServer)
                    {
                        UpdateServer();
                    }
                    else
                    {
                        UpdateClient();
                    }

                    await Task.Delay(16, cancellationToken); // ~60 FPS
                }
                catch (Exception ex)
                {
                    Log($"Update loop error: {ex.Message}");
                }
            }
        }

        private void ProcessNetworkEvents()
        {
            while (_networkEvents.TryDequeue(out var evt))
            {
                // Events are already invoked when they occur
            }
        }

        private void ProcessIncomingMessages()
        {
            while (_incomingMessages.TryDequeue(out var message))
            {
                OnMessageReceived?.Invoke(this, new NetworkEventArgs
                {
                    PeerId = message.SenderId,
                    Message = message
                });

                if (_messageHandlers.TryGetValue(message.Type, out var handler))
                {
                    handler(message);
                }
            }
        }

        private void UpdateServer()
        {
            // Server-specific updates
            // Send keep-alive, process timeouts, etc.
        }

        private void UpdateClient()
        {
            // Client-specific updates
            // Send input, request state, etc.
        }

        public void SendMessage(NetworkMessage message, DeliveryMethod method = DeliveryMethod.Reliable)
        {
            if (!_isRunning || _netManager == null) return;

            _dataWriter.Reset();
            _dataWriter.Put((byte)message.Type);
            _dataWriter.Put(message.SenderId);
            _dataWriter.Put(message.TargetId);
            _dataWriter.Put(message.Timestamp.ToBinary());
            _dataWriter.Put(message.Data);

            var netMethod = ConvertDeliveryMethod(method);

            if (message.TargetId == 0) // Broadcast
            {
                _netManager.SendToAll(_dataWriter, (byte)message.Channel, netMethod);
            }
            else if (_peers.TryGetValue(message.TargetId, out var peer))
            {
                peer.Send(_dataWriter, (byte)message.Channel, netMethod);
            }
        }

        private LiteNetLib.DeliveryMethod ConvertDeliveryMethod(DeliveryMethod method)
        {
            return method switch
            {
                DeliveryMethod.Unreliable => LiteNetLib.DeliveryMethod.Unreliable,
                DeliveryMethod.UnreliableSequenced => LiteNetLib.DeliveryMethod.Sequenced,
                DeliveryMethod.Reliable => LiteNetLib.DeliveryMethod.ReliableUnordered,
                DeliveryMethod.ReliableSequenced => LiteNetLib.DeliveryMethod.ReliableSequenced,
                DeliveryMethod.ReliableOrdered => LiteNetLib.DeliveryMethod.ReliableOrdered,
                _ => LiteNetLib.DeliveryMethod.ReliableUnordered
            };
        }

        public void SendRPC(string methodName, params object[] args)
        {
            var packet = new RPCPacket
            {
                MethodName = methodName,
                Arguments = args
            };

            SendPacket(packet, DeliveryMethod.ReliableOrdered);
        }

        private void SendPacket<T>(T packet, DeliveryMethod method) where T : class, new()
        {
            if (!_isRunning || _netManager == null) return;

            _dataWriter.Reset();
            _packetProcessor.Write(_dataWriter, packet);

            _netManager.SendToAll(_dataWriter, ConvertDeliveryMethod(method));
        }

        private void SendPeerId(NetPeer peer, uint peerId)
        {
            var packet = new ConnectPacket { AssignedId = peerId };
            SendToPeer(peer, packet, DeliveryMethod.ReliableOrdered);
        }

        private void SendToPeer<T>(NetPeer peer, T packet, DeliveryMethod method) where T : class, new()
        {
            _dataWriter.Reset();
            _packetProcessor.Write(_dataWriter, packet);

            peer.Send(_dataWriter, ConvertDeliveryMethod(method));
        }

        // Packet handlers
        private void OnConnectPacket(ConnectPacket packet, NetPeer peer)
        {
            _localPeerId = packet.AssignedId;
            Log($"Assigned peer ID: {_localPeerId}");
            OnConnected?.Invoke(this, new NetworkEventArgs { PeerId = _localPeerId });
        }

        private void OnEntitySyncPacket(EntitySyncPacket packet, NetPeer peer)
        {
            // Handle entity synchronization
        }

        private void OnRPCPacket(RPCPacket packet, NetPeer peer)
        {
            // Handle RPC calls
        }

        public void RegisterMessageHandler(MessageType type, Action<NetworkMessage> handler)
        {
            _messageHandlers[type] = handler;
        }

        public List<PeerInfo> GetConnectedPeers()
        {
            var list = new List<PeerInfo>();

            foreach (var kvp in _peers)
            {
                var peer = kvp.Value;
                list.Add(new PeerInfo
                {
                    Id = kvp.Key,
                    Address = peer.Address.ToString(),
                    Port = peer.Port,
                    State = PeerState.Connected,
                    Ping = peer.Ping,
                    LastPingTime = DateTime.Now,
                    PacketLoss = (int)(peer.Statistics.PacketLoss * 100)
                });
            }

            return list;
        }

        private void Log(string message)
        {
            OnLogMessage?.Invoke($"[Network] {message}");
        }

        public void Dispose()
        {
            Disconnect();
            _netManager?.Stop();
            _netManager = null;
        }
    }

    // Packet definitions
    public class ConnectPacket
    {
        public uint AssignedId { get; set; }
    }

    public class EntitySyncPacket
    {
        public uint EntityId { get; set; }
        public NetVector3 Position { get; set; }
        public NetQuaternion Rotation { get; set; }
        public NetVector3 Scale { get; set; }
        public byte[] StateData { get; set; } = Array.Empty<byte>();
    }

    public class RPCPacket
    {
        public string MethodName { get; set; } = string.Empty;
        public object[] Arguments { get; set; } = Array.Empty<object>();
    }
}