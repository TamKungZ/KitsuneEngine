using System.Numerics;

namespace KitsuneEngine.Core
{
    public struct Transform
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public Transform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public Transform(Vector3 position) : this(position, Quaternion.Identity, Vector3.One) { }
        public Transform() : this(Vector3.Zero, Quaternion.Identity, Vector3.One) { }

        public Matrix4x4 GetMatrix()
        {
            return Matrix4x4.CreateScale(Scale) *
                   Matrix4x4.CreateFromQuaternion(Rotation) *
                   Matrix4x4.CreateTranslation(Position);
        }

        public Vector3 Forward => Vector3.Transform(Vector3.UnitZ, Rotation);
        public Vector3 Up => Vector3.Transform(Vector3.UnitY, Rotation);
        public Vector3 Right => Vector3.Transform(Vector3.UnitX, Rotation);
    }
}