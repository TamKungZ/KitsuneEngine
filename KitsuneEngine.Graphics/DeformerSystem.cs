using System.Numerics;

namespace KitsuneEngine.Graphics;

public interface IMeshDeformer
{
    string Name { get; }
    bool Enabled { get; set; }
    void Apply(DeformationMesh mesh, float timeSeconds, float deltaTimeSeconds);
}

public sealed class DeformerSystem
{
    private readonly List<IMeshDeformer> _deformers = new();

    public IReadOnlyList<IMeshDeformer> Deformers => _deformers;
    public bool AutoResetToOriginal { get; set; } = true;

    public void Add(IMeshDeformer deformer)
    {
        ArgumentNullException.ThrowIfNull(deformer);
        _deformers.Add(deformer);
    }

    public bool Remove(IMeshDeformer deformer)
    {
        return _deformers.Remove(deformer);
    }

    public void Clear()
    {
        _deformers.Clear();
    }

    public void Update(DeformationMesh mesh, float timeSeconds, float deltaTimeSeconds)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (AutoResetToOriginal)
            mesh.ResetToOriginal();

        for (int i = 0; i < _deformers.Count; i++)
        {
            var deformer = _deformers[i];
            if (!deformer.Enabled)
                continue;

            deformer.Apply(mesh, timeSeconds, deltaTimeSeconds);
        }

        RecomputeNormals(mesh);
    }

    private static void RecomputeNormals(DeformationMesh mesh)
    {
        for (int i = 0; i < mesh.WorkingVertices.Length; i++)
        {
            var v = mesh.WorkingVertices[i];
            v.Normal = Vector3.Zero;
            mesh.WorkingVertices[i] = v;
        }

        for (int i = 0; i < mesh.Indices.Length; i += 3)
        {
            int i0 = mesh.Indices[i];
            int i1 = mesh.Indices[i + 1];
            int i2 = mesh.Indices[i + 2];

            var p0 = mesh.WorkingVertices[i0].Position;
            var p1 = mesh.WorkingVertices[i1].Position;
            var p2 = mesh.WorkingVertices[i2].Position;

            var normal = Vector3.Cross(p1 - p0, p2 - p0);
            if (normal.LengthSquared() > 0f)
                normal = Vector3.Normalize(normal);

            AccumulateNormal(mesh, i0, normal);
            AccumulateNormal(mesh, i1, normal);
            AccumulateNormal(mesh, i2, normal);
        }

        for (int i = 0; i < mesh.WorkingVertices.Length; i++)
        {
            var v = mesh.WorkingVertices[i];
            if (v.Normal.LengthSquared() <= 0f)
                v.Normal = Vector3.UnitZ;
            else
                v.Normal = Vector3.Normalize(v.Normal);

            mesh.WorkingVertices[i] = v;
        }
    }

    private static void AccumulateNormal(DeformationMesh mesh, int index, Vector3 normal)
    {
        var v = mesh.WorkingVertices[index];
        v.Normal += normal;
        mesh.WorkingVertices[index] = v;
    }
}
