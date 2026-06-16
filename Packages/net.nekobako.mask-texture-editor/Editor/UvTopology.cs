using System;
using System.Collections.Generic;
using UnityEngine;

namespace net.nekobako.MaskTextureEditor.Editor
{
    internal sealed class UvTopology
    {
        private const float k_UvTolerance = 0.00001f;

        private readonly struct UvKey : IEquatable<UvKey>, IComparable<UvKey>
        {
            private readonly int m_X;
            private readonly int m_Y;

            public UvKey(Vector2 point)
            {
                m_X = Mathf.RoundToInt(point.x / k_UvTolerance);
                m_Y = Mathf.RoundToInt(point.y / k_UvTolerance);
            }

            public int CompareTo(UvKey other)
            {
                var x = m_X.CompareTo(other.m_X);
                return x != 0 ? x : m_Y.CompareTo(other.m_Y);
            }

            public bool Equals(UvKey other)
            {
                return m_X == other.m_X && m_Y == other.m_Y;
            }

            public override bool Equals(object? obj)
            {
                return obj is UvKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (m_X * 397) ^ m_Y;
                }
            }
        }

        private readonly struct UvEdge : IEquatable<UvEdge>
        {
            private readonly UvKey m_A;
            private readonly UvKey m_B;

            public UvEdge(Vector2 a, Vector2 b)
            {
                var keyA = new UvKey(a);
                var keyB = new UvKey(b);
                if (keyA.CompareTo(keyB) <= 0)
                {
                    m_A = keyA;
                    m_B = keyB;
                }
                else
                {
                    m_A = keyB;
                    m_B = keyA;
                }
            }

            public bool Equals(UvEdge other)
            {
                return m_A.Equals(other.m_A) && m_B.Equals(other.m_B);
            }

            public override bool Equals(object? obj)
            {
                return obj is UvEdge other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (m_A.GetHashCode() * 397) ^ m_B.GetHashCode();
                }
            }
        }

        private readonly Vector2[] m_Points;
        private readonly int[] m_Indices;
        private readonly int[] m_IslandByTriangle;
        private readonly List<int[]> m_Islands = new();

        public int IslandCount => m_Islands.Count;

        public UvTopology(Vector2[] points, int[] indices)
        {
            m_Points = points;
            m_Indices = indices;

            var triangleCount = indices.Length / 3;
            var parents = new int[triangleCount];
            var ranks = new byte[triangleCount];
            var valid = new bool[triangleCount];
            var edges = new Dictionary<UvEdge, int>();
            for (var triangle = 0; triangle < triangleCount; triangle++)
            {
                parents[triangle] = triangle;

                var offset = triangle * 3;
                var indexA = indices[offset];
                var indexB = indices[offset + 1];
                var indexC = indices[offset + 2];
                if (!IsValidIndex(indexA) || !IsValidIndex(indexB) || !IsValidIndex(indexC))
                {
                    continue;
                }

                var pointA = m_Points[indexA];
                var pointB = m_Points[indexB];
                var pointC = m_Points[indexC];
                if (Mathf.Abs(Cross(pointB - pointA, pointC - pointA)) <= k_UvTolerance * k_UvTolerance)
                {
                    continue;
                }

                valid[triangle] = true;
                ConnectEdge(new(pointA, pointB), triangle, edges, parents, ranks);
                ConnectEdge(new(pointB, pointC), triangle, edges, parents, ranks);
                ConnectEdge(new(pointC, pointA), triangle, edges, parents, ranks);
            }

            m_IslandByTriangle = new int[triangleCount];
            Array.Fill(m_IslandByTriangle, -1);
            var islandByRoot = new Dictionary<int, int>();
            var islandTriangles = new List<List<int>>();
            for (var triangle = 0; triangle < triangleCount; triangle++)
            {
                if (!valid[triangle])
                {
                    continue;
                }

                var root = Find(triangle, parents);
                if (!islandByRoot.TryGetValue(root, out var island))
                {
                    island = islandTriangles.Count;
                    islandByRoot.Add(root, island);
                    islandTriangles.Add(new());
                }

                m_IslandByTriangle[triangle] = island;
                islandTriangles[island].Add(triangle);
            }

            foreach (var triangles in islandTriangles)
            {
                m_Islands.Add(triangles.ToArray());
            }
        }

        public bool TryFindIsland(Vector2 point, out int island)
        {
            for (var triangle = 0; triangle < m_IslandByTriangle.Length; triangle++)
            {
                if (m_IslandByTriangle[triangle] < 0)
                {
                    continue;
                }

                var offset = triangle * 3;
                if (PointInTriangle(
                    point,
                    m_Points[m_Indices[offset]],
                    m_Points[m_Indices[offset + 1]],
                    m_Points[m_Indices[offset + 2]]))
                {
                    island = m_IslandByTriangle[triangle];
                    return true;
                }
            }

            island = -1;
            return false;
        }

        public IReadOnlyList<int> GetIslandTriangles(int island)
        {
            return island >= 0 && island < m_Islands.Count
                ? m_Islands[island]
                : Array.Empty<int>();
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < m_Points.Length;
        }

        private static void ConnectEdge(
            UvEdge edge,
            int triangle,
            Dictionary<UvEdge, int> edges,
            int[] parents,
            byte[] ranks)
        {
            if (edges.TryGetValue(edge, out var neighbor))
            {
                Union(triangle, neighbor, parents, ranks);
            }
            else
            {
                edges.Add(edge, triangle);
            }
        }

        private static int Find(int value, int[] parents)
        {
            while (parents[value] != value)
            {
                parents[value] = parents[parents[value]];
                value = parents[value];
            }
            return value;
        }

        private static void Union(int a, int b, int[] parents, byte[] ranks)
        {
            var rootA = Find(a, parents);
            var rootB = Find(b, parents);
            if (rootA == rootB)
            {
                return;
            }

            if (ranks[rootA] < ranks[rootB])
            {
                parents[rootA] = rootB;
            }
            else if (ranks[rootA] > ranks[rootB])
            {
                parents[rootB] = rootA;
            }
            else
            {
                parents[rootB] = rootA;
                ranks[rootA]++;
            }
        }

        private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            var ab = Cross(b - a, point - a);
            var bc = Cross(c - b, point - b);
            var ca = Cross(a - c, point - c);
            return
                (ab >= 0.0f && bc >= 0.0f && ca >= 0.0f) ||
                (ab <= 0.0f && bc <= 0.0f && ca <= 0.0f);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}
