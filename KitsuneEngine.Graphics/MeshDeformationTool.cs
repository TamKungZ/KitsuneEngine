using System.Numerics;

namespace KitsuneEngine.Graphics;

public sealed class MeshDeformationTool
{
    public DeformationMesh Mesh { get; }
    public DeformerSystem System { get; } = new();
    public float TimeSeconds { get; private set; }

    public MeshDeformationTool(DeformationMesh mesh)
    {
        Mesh = mesh;
    }

    public void Update(float deltaTimeSeconds)
    {
        TimeSeconds += deltaTimeSeconds;
        System.Update(Mesh, TimeSeconds, deltaTimeSeconds);
    }

    public ReadOnlySpan<DeformationVertex> GetDeformedVertices()
    {
        return Mesh.WorkingVertices;
    }

    public static MeshDeformationTool CreateDefaultGridTool(int columns = 32, int rows = 32, float width = 2f, float height = 2f)
    {
        var mesh = DeformationMesh.CreateGrid(columns, rows, width, height);
        var tool = new MeshDeformationTool(mesh);

        tool.System.Add(new WaveDeformer());
        tool.System.Add(new NoiseDeformer());

        return tool;
    }
}
