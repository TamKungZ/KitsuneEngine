using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;

namespace KitsuneEngine.Network
{
    public class NetworkManager : IDisposable
    {
        private NetManager? _netManager;
        private EventBasedNetListener _listener;

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
            _listener = new EventBasedNetListener();

            SetupListeners();
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
                    _networkEvents.Enqueue(new NetworkEventArgs { PeerId = peerId });
                    OnConnected?.Invoke(this, new NetworkEventArgs { PeerId = peerId });
                }

                Log($"Peer connected: {peer.Address}:{peer.Port}");
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

        public void StartServer()
        {
            StartServer(_config.Port);
        }

        public void StartServer(int port)
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

            _netManager.Start(port);
            _isRunning = true;

            Log($"Server started on port {port}");

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
                ProcessInternalMessage(message);

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

        private void ProcessInternalMessage(NetworkMessage message)
        {
            switch (message.Type)
            {
                case MessageType.Connect:
                    if (_isServer)
                    {
                        return;
                    }

                    var connectPacket = Deserialize<ConnectPacket>(message.Data);
                    _localPeerId = connectPacket.AssignedId;
                    Log($"Assigned peer ID: {_localPeerId}");
                    _networkEvents.Enqueue(new NetworkEventArgs { PeerId = _localPeerId });
                    OnConnected?.Invoke(this, new NetworkEventArgs { PeerId = _localPeerId });
                    break;
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

            var writer = new NetDataWriter();
            writer.Put((byte)message.Type);
            writer.Put(message.SenderId);
            writer.Put(message.TargetId);
            writer.Put(message.Timestamp.ToBinary());
            writer.Put(message.Data);

            var netMethod = ConvertDeliveryMethod(method);

            if (message.TargetId == 0) // Broadcast
            {
                _netManager.SendToAll(writer, (byte)message.Channel, netMethod);
            }
            else if (_peers.TryGetValue(message.TargetId, out var peer))
            {
                peer.Send(writer, (byte)message.Channel, netMethod);
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

            SendMessage(new NetworkMessage
            {
                Type = MessageType.RPC,
                SenderId = _localPeerId,
                TargetId = 0,
                Channel = 0,
                Timestamp = DateTime.UtcNow,
                Data = Serialize(packet)
            }, DeliveryMethod.ReliableOrdered);
        }

        private void SendPeerId(NetPeer peer, uint peerId)
        {
            var packet = new ConnectPacket { AssignedId = peerId };
            SendMessage(new NetworkMessage
            {
                Type = MessageType.Connect,
                SenderId = _localPeerId,
                TargetId = peerId,
                Channel = 0,
                Timestamp = DateTime.UtcNow,
                Data = Serialize(packet)
            }, DeliveryMethod.ReliableOrdered);
        }

        private byte[] Serialize<T>(T obj)
        {
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));
        }

        private T Deserialize<T>(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            return JsonSerializer.Deserialize<T>(json)!;
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
