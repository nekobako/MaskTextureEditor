using UnityEngine;

namespace net.nekobako.MaskTextureEditor.Editor
{
    internal sealed class MeshUvData
    {
        public Vector2[] Points { get; }
        public int[] LineIndices { get; }
        public int[]? TriangleIndices { get; }
        public Color[]? NormalColors { get; }
        public UvTopology? Topology { get; }

        public MeshUvData(
            Vector2[] points,
            int[] lineIndices,
            int[]? triangleIndices,
            Color[]? normalColors)
        {
            Points = points;
            LineIndices = lineIndices;
            TriangleIndices = triangleIndices;
            NormalColors = normalColors;
            Topology = triangleIndices != null ? new(points, triangleIndices) : null;
        }
    }
}
