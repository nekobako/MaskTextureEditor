using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace net.nekobako.MaskTextureEditor.Editor
{
    internal class UvMapDrawer : ScriptableObject
    {
        private const string k_NormalOverlayShaderName = "Hidden/MaskTextureEditor/NormalOverlay";

        [SerializeField]
        private Renderer? m_Renderer = null;

        [SerializeField]
        private int m_Slot = 0;

        public Renderer? Renderer
        {
            get => m_Renderer;
            set
            {
                if (m_Renderer != value)
                {
                    m_IsDirty = true;
                }
                m_Renderer = value;
            }
        }

        public int Slot
        {
            get => m_Slot;
            set
            {
                if (m_Slot != value)
                {
                    m_IsDirty = true;
                }
                m_Slot = value;
            }
        }

        private bool m_IsDirty = false;
        private Mesh? m_Mesh = null;
        private int m_MeshDirtyCount = 0;
        private Vector2[]? m_Points = null;
        private Vector3[]? m_Buffer = null;
        private int[]? m_LineIndices = null;
        private int[]? m_TriangleIndices = null;
        private Color[]? m_NormalColors = null;

        [SerializeField]
        private RenderTexture m_NormalOverlay = null!;

        [SerializeField]
        private Material m_NormalOverlayMaterial = null!;

        private void Awake()
        {
            // Set HideFlags to keep the state when reloading a scene or domain
            hideFlags = HideFlags.HideAndDontSave;
            EnsureNormalOverlayMaterial();
        }

        public void Init(Renderer? renderer, int? slot)
        {
            m_Renderer = renderer;
            m_Slot = slot ?? 0;
        }

        public void Draw(Rect rect, Vector2Int textureSize, bool showUvWireframe, bool showNormalOverlay, float normalOverlayOpacity)
        {
            var (mesh, material) = CollectMeshAndMaterial();
            if (m_IsDirty ||
                m_Mesh != mesh ||
                m_MeshDirtyCount != EditorUtility.GetDirtyCount(m_Mesh))
            {
                m_IsDirty = false;
                m_Mesh = mesh;
                m_MeshDirtyCount = EditorUtility.GetDirtyCount(m_Mesh);
                (m_Points, m_LineIndices, m_TriangleIndices, m_NormalColors) = CollectMeshData();
                m_Buffer = m_Points != null ? new Vector3[m_Points.Length] : null;
                ReleaseNormalOverlay();
            }

            if (material != null && material.mainTexture != null)
            {
                // Draw the main texture if present
                EditorGUI.DrawTextureTransparent(rect, material.mainTexture);
            }

            if (showNormalOverlay && TryRenderNormalOverlay(textureSize))
            {
                var color = GUI.color;
                GUI.color = new(color.r, color.g, color.b, color.a * normalOverlayOpacity);
                GUI.DrawTexture(rect, m_NormalOverlay, ScaleMode.StretchToFill, true);
                GUI.color = color;
            }

            if (showUvWireframe && m_Points != null && m_Buffer != null && m_LineIndices != null)
            {
                // Draw line shadows for visibility
                for (var i = 0; i < m_Buffer.Length; i++)
                {
                    m_Buffer[i] = Rect.NormalizedToPoint(rect, new(m_Points[i].x, 1.0f - m_Points[i].y));
                }
                Handles.color = GUI.color * Color.black;
                Handles.DrawLines(m_Buffer, m_LineIndices);

                // Draw lines with offset
                for (var i = 0; i < m_Buffer.Length; i++)
                {
                    m_Buffer[i] -= Vector3.one;
                }
                Handles.color = GUI.color * Color.white;
                Handles.DrawLines(m_Buffer, m_LineIndices);
            }
        }

        private bool TryRenderNormalOverlay(Vector2Int textureSize)
        {
            if (m_Points == null ||
                m_TriangleIndices == null ||
                m_NormalColors == null ||
                textureSize.x <= 0 ||
                textureSize.y <= 0)
            {
                return false;
            }

            if (m_NormalOverlay != null &&
                m_NormalOverlay.width == textureSize.x &&
                m_NormalOverlay.height == textureSize.y)
            {
                return true;
            }

            ReleaseNormalOverlay();
            EnsureNormalOverlayMaterial();
            m_NormalOverlay = new(textureSize.x, textureSize.y, 0, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            m_NormalOverlay.Create();

            var previous = RenderTexture.active;
            RenderTexture.active = m_NormalOverlay;
            GL.Clear(false, true, Color.clear);
            GL.PushMatrix();
            GL.LoadOrtho();
            m_NormalOverlayMaterial.SetPass(0);
            GL.Begin(GL.TRIANGLES);
            for (var offset = 0; offset + 2 < m_TriangleIndices.Length; offset += 3)
            {
                GL.Color(m_NormalColors[offset]);
                GL.Vertex3(m_Points[m_TriangleIndices[offset]].x, m_Points[m_TriangleIndices[offset]].y, 0.0f);
                GL.Color(m_NormalColors[offset + 1]);
                GL.Vertex3(m_Points[m_TriangleIndices[offset + 1]].x, m_Points[m_TriangleIndices[offset + 1]].y, 0.0f);
                GL.Color(m_NormalColors[offset + 2]);
                GL.Vertex3(m_Points[m_TriangleIndices[offset + 2]].x, m_Points[m_TriangleIndices[offset + 2]].y, 0.0f);
            }
            GL.End();
            GL.PopMatrix();
            RenderTexture.active = previous;
            return true;
        }

        private void EnsureNormalOverlayMaterial()
        {
            if (m_NormalOverlayMaterial != null)
            {
                return;
            }

            m_NormalOverlayMaterial = new(Shader.Find(k_NormalOverlayShaderName))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        public bool TryGetTriangles(out Vector2[] points, out int[] indices)
        {
            if (m_Points != null && m_TriangleIndices != null)
            {
                points = m_Points;
                indices = m_TriangleIndices;
                return true;
            }

            points = null!;
            indices = null!;
            return false;
        }

        private (Mesh?, Material?) CollectMeshAndMaterial()
        {
            if (m_Renderer == null)
            {
                return (null, null);
            }

            switch (m_Renderer)
            {
                case MeshRenderer meshRenderer:
                {
                    var mesh = meshRenderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                    var material = m_Slot >= 0 && m_Slot < meshRenderer.sharedMaterials.Length
                        ? meshRenderer.sharedMaterials[m_Slot]
                        : null;
                    return (mesh, material);
                }
                case SkinnedMeshRenderer skinnedMeshRenderer:
                {
                    var mesh = skinnedMeshRenderer.sharedMesh;
                    var material = m_Slot >= 0 && m_Slot < skinnedMeshRenderer.sharedMaterials.Length
                        ? skinnedMeshRenderer.sharedMaterials[m_Slot]
                        : null;
                    return (mesh, material);
                }
                default:
                {
                    return (null, null);
                }
            }
        }

        private (Vector2[]?, int[]?, int[]?, Color[]?) CollectMeshData()
        {
            if (m_Mesh == null)
            {
                return (null, null, null, null);
            }

            if (m_Mesh.subMeshCount == 0)
            {
                return (null, null, null, null);
            }

            var subMesh = Mathf.Clamp(m_Slot, 0, m_Mesh.subMeshCount - 1);
            var meshTopology = m_Mesh.GetTopology(subMesh);
            var topology = meshTopology switch
            {
                MeshTopology.Lines => 2,
                MeshTopology.Triangles => 3,
                MeshTopology.Quads => 4,
                _ => 0,
            };
            if (topology == 0)
            {
                return (null, null, null, null);
            }

            var lines = new HashSet<(int, int)>();
            var indices = m_Mesh.GetIndices(subMesh);
            for (var i = 0; i < indices.Length; i++)
            {
                var indexA = indices[i / topology * topology + (i + 0) % topology];
                var indexB = indices[i / topology * topology + (i + 1) % topology];
                lines.Add(indexA < indexB ? (indexA, indexB) : (indexB, indexA));
            }

            var lineIndices = new List<int>();
            foreach (var (a, b) in lines)
            {
                lineIndices.Add(a);
                lineIndices.Add(b);
            }

            if (meshTopology != MeshTopology.Triangles)
            {
                return (m_Mesh.uv, lineIndices.ToArray(), null, null);
            }

            var vertices = m_Mesh.vertices;
            var normals = m_Mesh.normals;
            var hasVertexNormals = normals.Length == vertices.Length;
            var normalColors = new Color[indices.Length];
            for (var offset = 0; offset + 2 < indices.Length; offset += 3)
            {
                var a = vertices[indices[offset]];
                var b = vertices[indices[offset + 1]];
                var c = vertices[indices[offset + 2]];
                var cross = Vector3.Cross(b - a, c - a);
                if (cross.sqrMagnitude <= Mathf.Epsilon)
                {
                    normalColors[offset] = Color.clear;
                    normalColors[offset + 1] = Color.clear;
                    normalColors[offset + 2] = Color.clear;
                    continue;
                }

                var faceNormal = cross.normalized;
                for (var corner = 0; corner < 3; corner++)
                {
                    var normal = hasVertexNormals ? normals[indices[offset + corner]] : faceNormal;
                    if (normal.sqrMagnitude <= Mathf.Epsilon)
                    {
                        normal = faceNormal;
                    }
                    normalColors[offset + corner] = EncodeNormal(normal);
                }
            }

            return (m_Mesh.uv, lineIndices.ToArray(), indices, normalColors);
        }

        private static Color EncodeNormal(Vector3 normal)
        {
            normal.Normalize();
            return new(
                    normal.x * 0.5f + 0.5f,
                    normal.y * 0.5f + 0.5f,
                    normal.z * 0.5f + 0.5f,
                    1.0f);
        }

        private void ReleaseNormalOverlay()
        {
            if (m_NormalOverlay == null)
            {
                return;
            }

            DestroyImmediate(m_NormalOverlay);
            m_NormalOverlay = null!;
        }

        private void OnDestroy()
        {
            ReleaseNormalOverlay();

            if (m_NormalOverlayMaterial != null)
            {
                DestroyImmediate(m_NormalOverlayMaterial);
                m_NormalOverlayMaterial = null!;
            }
        }
    }
}
