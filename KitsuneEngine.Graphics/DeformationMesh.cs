using System.Numerics;

namespace KitsuneEngine.Graphics;

public struct DeformationVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 UV;

    public DeformationVertex(Vector3 position, Vector3 normal, Vector2 uv)
    {
        Position = position;
        Normal = normal;
        UV = uv;
    }
}

public sealed class DeformationMesh
{
    public DeformationVertex[] OriginalVertices { get; }
    public DeformationVertex[] WorkingVertices { get; }
    public int[] Indices { get; }

    public int VertexCount => WorkingVertices.Length;

    public DeformationMesh(DeformationVertex[] vertices, int[] indices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);

        OriginalVertices = new DeformationVertex[vertices.Length];
        WorkingVertices = new DeformationVertex[vertices.Length];
        Array.Copy(vertices, OriginalVertices, vertices.Length);
        Array.Copy(vertices, WorkingVertices, vertices.Length);

        Indices = new int[indices.Length];
        Array.Copy(indices, Indices, indices.Length);
    }

    public void ResetToOriginal()
    {
        Array.Copy(OriginalVertices, WorkingVertices, OriginalVertices.Length);
    }

    public static DeformationMesh CreateGrid(int columns, int rows, float width, float height)
    {
        if (columns < 2 || rows < 2)
            throw new ArgumentOutOfRangeException(nameof(columns), "Grid requires at least 2x2 vertices.");

        var vertices = new DeformationVertex[columns * rows];
        var indices = new List<int>((columns - 1) * (rows - 1) * 6);

        float dx = width / (columns - 1);
        float dy = height / (rows - 1);

        int v = 0;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                float px = -width * 0.5f + x * dx;
                float py = -height * 0.5f + y * dy;
                var uv = new Vector2((float)x / (columns - 1), (float)y / (rows - 1));

                vertices[v++] = new DeformationVertex(new Vector3(px, py, 0f), Vector3.UnitZ, uv);
            }
        }

        for (int y = 0; y < rows - 1; y++)
        {
            for (int x = 0; x < columns - 1; x++)
            {
                int i0 = y * columns + x;
                int i1 = i0 + 1;
                int i2 = i0 + columns;
                int i3 = i2 + 1;

                indices.Add(i0); indices.Add(i1); indices.Add(i2);
                indices.Add(i1); indices.Add(i3); indices.Add(i2);
            }
        }

        return new DeformationMesh(vertices, indices.ToArray());
    }
}
