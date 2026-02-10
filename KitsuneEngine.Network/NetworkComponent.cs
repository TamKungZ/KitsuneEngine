using DefaultEcs;
using System;
using System.Collections.Generic;

namespace KitsuneEngine.Network
{
    public class NetworkComponent
    {
        public uint NetworkId { get; set; }
        public bool IsOwner { get; set; }
        public bool IsReplicated { get; set; } = true;
        public float UpdateRate { get; set; } = 0.1f; // 10 times per second
        public float LastUpdateTime { get; set; }
        public Dictionary<string, object> SyncProperties { get; } = new Dictionary<string, object>();
        public List<string> RpcQueue { get; } = new List<string>();

        [NonSerialized]
        public Entity Entity;
    }

    public class NetworkTransform
    {
        public uint NetworkId { get; set; }
        public NetVector3 Position { get; set; }
        public NetQuaternion Rotation { get; set; }
        public NetVector3 Scale { get; set; }
        public NetVector3 Velocity { get; set; }
        public NetVector3 AngularVelocity { get; set; }
        public bool Interpolate { get; set; } = true;
        public float InterpolationTime { get; set; } = 0.1f;

        // For interpolation
        public NetVector3 TargetPosition { get; set; }
        public NetQuaternion TargetRotation { get; set; }
        public float InterpolationProgress { get; set; }
    }
}