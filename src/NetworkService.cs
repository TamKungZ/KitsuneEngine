using DefaultEcs;
using System;

namespace KitsuneEngine.Network
{
    public class NetworkService : IDisposable
    {
        private readonly NetworkManager _networkManager;
        private readonly NetworkSystem _networkSystem;

        public NetworkManager Manager => _networkManager;
        public bool IsServer => _networkManager.IsServer;
        public bool IsConnected => _networkManager.IsConnected;

        public event Action<string>? OnLogMessage
        {
            add => _networkManager.OnLogMessage += value;
            remove => _networkManager.OnLogMessage -= value;
        }

        public NetworkService(World world, NetworkConfig? config = null)
        {
            _networkManager = new NetworkManager(config);
            _networkSystem = new NetworkSystem(world, _networkManager);
        }

        public void StartServer(int port = 7777)
        {
            _networkManager.StartServer();
        }

        public void StartClient(string address = "127.0.0.1", int port = 7777)
        {
            _networkManager.StartClient();
            _networkManager.Connect(address, port);
        }

        public void Disconnect()
        {
            _networkManager.Disconnect();
        }

        public void SendRPC(string methodName, params object[] args)
        {
            _networkManager.SendRPC(methodName, args);
        }

        public void Update(float deltaTime)
        {
            // Network system update handled by ECS
            // Additional network service updates can go here
        }

        public void Dispose()
        {
            _networkSystem?.Dispose();
            _networkManager?.Dispose();
        }
    }
}