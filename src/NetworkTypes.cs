using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;

namespace KitsuneEngine.Network
{
    // Network message types
    public enum MessageType : byte
    {
        Connect = 1,
        Disconnect = 2,
        Data = 3,
        Ping = 4,
        Pong = 5,
        RPC = 6,
        SceneSync = 7,
        EntitySync = 8,
        InputSync = 9
    }

    // Network delivery method
    public enum DeliveryMethod : byte
    {
        Unreliable,
        UnreliableSequenced,
        Reliable,
        ReliableSequenced,
        ReliableOrdered
    }

    // Network peer state
    public enum PeerState : byte
    {
        Disconnected,
        Connecting,
        Connected,
        Disconnecting
    }

    // Network configuration
    public class NetworkConfig
    {
        public string Address { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 7777;
        public int MaxConnections { get; set; } = 32;
        public int PingInterval { get; set; } = 1000; // ms
        public int Timeout { get; set; } = 5000; // ms
        public bool UseEncryption { get; set; } = true;
        public bool EnableCompression { get; set; } = true;
    }

    // Network peer information
    public class PeerInfo
    {
        public uint Id { get; set; }
        public string Address { get; set; } = string.Empty;
        public int Port { get; set; }
        public PeerState State { get; set; }
        public float Ping { get; set; }
        public DateTime LastPingTime { get; set; }
        public int PacketLoss { get; set; }
    }

    // Network message structure
    public class NetworkMessage
    {
        public MessageType Type { get; set; }
        public uint SenderId { get; set; }
        public uint TargetId { get; set; } // 0 = broadcast
        public byte Channel { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public DateTime Timestamp { get; set; }
        public bool IsReliable { get; set; }
    }

    // RPC method signature
    [AttributeUsage(AttributeTargets.Method)]
    public class RPCAttribute : Attribute
    {
        public bool ServerOnly { get; set; }
        public bool ClientOnly { get; set; }
        public bool Broadcast { get; set; }
    }

    // Serializable Vector2 for networking
    [Serializable]
    public struct NetVector2
    {
        public float X { get; set; }
        public float Y { get; set; }

        public NetVector2(float x, float y) { X = x; Y = y; }
        public static implicit operator Vector2(NetVector2 v) => new Vector2(v.X, v.Y);
        public static implicit operator NetVector2(Vector2 v) => new NetVector2(v.X, v.Y);
    }

    // Serializable Vector3 for networking
    [Serializable]
    public struct NetVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public NetVector3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static implicit operator Vector3(NetVector3 v) => new Vector3(v.X, v.Y, v.Z);
        public static implicit operator NetVector3(Vector3 v) => new NetVector3(v.X, v.Y, v.Z);
    }

    // Serializable Quaternion for networking
    [Serializable]
    public struct NetQuaternion
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }

        public NetQuaternion(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
        public static implicit operator Quaternion(NetQuaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);
        public static implicit operator NetQuaternion(Quaternion q) => new NetQuaternion(q.X, q.Y, q.Z, q.W);
    }

    // Network events
    public class NetworkEventArgs : EventArgs
    {
        public uint PeerId { get; set; }
        public NetworkMessage? Message { get; set; }
    }

    public delegate void NetworkEventHandler(object sender, NetworkEventArgs e);
}