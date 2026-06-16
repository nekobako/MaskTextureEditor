using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace net.nekobako.MaskTextureEditor.Editor
{
    internal class UvMapDrawer : ScriptableObject
    {
        private const string k_NormalOverlayShaderName = "Hidden/MaskTextureEditor/NormalOverlay";
        private const string k_SelectionMaskShaderName = "Hidden/MaskTextureEditor/SelectionOverlay";
        private const float k_SelectionOverlayOpacity = 0.4f;

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
        private MeshUvData? m_Data = null;
        private Vector3[]? m_Buffer = null;

        [SerializeField]
        private int m_ActiveIsland = -1;

        public bool HasActiveIsland => m_ActiveIsland >= 0;

        [SerializeField]
        private RenderTexture m_NormalOverlay = null!;

        [SerializeField]
        private Material m_NormalOverlayMaterial = null!;

        [SerializeField]
        private RenderTexture m_SelectionMask = null!;

        [SerializeField]
        private Material m_SelectionMaskMaterial = null!;

        private void Awake()
        {
            // Set HideFlags to keep the state when reloading a scene or domain
            hideFlags = HideFlags.HideAndDontSave;
            EnsureNormalOverlayMaterial();
            EnsureSelectionMaskMaterial();
        }

        public void Init(Renderer? renderer, int? slot)
        {
            m_Renderer = renderer;
            m_Slot = slot ?? 0;
        }

        public void Draw(
            Rect rect,
            Vector2Int textureSize,
            bool showUvWireframe,
            bool showNormalOverlay,
            float normalOverlayOpacity,
            bool showSelectionOverlay)
        {
            var (mesh, material) = CollectMeshAndMaterial();
            if (m_IsDirty ||
                m_Mesh != mesh ||
                m_MeshDirtyCount != EditorUtility.GetDirtyCount(m_Mesh))
            {
                m_IsDirty = false;
                m_Mesh = mesh;
                m_MeshDirtyCount = EditorUtility.GetDirtyCount(m_Mesh);
                m_Data = CollectMeshData();
                m_Buffer = m_Data != null ? new Vector3[m_Data.Points.Length] : null;
                m_ActiveIsland = -1;
                ReleaseNormalOverlay();
                ReleaseSelectionMask();
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

            if (showSelectionOverlay && TryGetSelectionMask(textureSize, out var selectionMask))
            {
                var color = GUI.color;
                GUI.color = new(0.0f, 0.75f, 1.0f, color.a * k_SelectionOverlayOpacity);
                GUI.DrawTexture(rect, selectionMask, ScaleMode.StretchToFill, true);
                GUI.color = color;
            }

            if (showUvWireframe && m_Data != null && m_Buffer != null)
            {
                // Draw line shadows for visibility
                for (var i = 0; i < m_Buffer.Length; i++)
                {
                    m_Buffer[i] = Rect.NormalizedToPoint(rect, new(m_Data.Points[i].x, 1.0f - m_Data.Points[i].y));
                }
                Handles.color = GUI.color * Color.black;
                Handles.DrawLines(m_Buffer, m_Data.LineIndices);

                // Draw lines with offset
                for (var i = 0; i < m_Buffer.Length; i++)
                {
                    m_Buffer[i] -= Vector3.one;
                }
                Handles.color = GUI.color * Color.white;
                Handles.DrawLines(m_Buffer, m_Data.LineIndices);
            }
        }

        private bool TryRenderNormalOverlay(Vector2Int textureSize)
        {
            if (m_Data?.TriangleIndices == null ||
                m_Data.NormalColors == null ||
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
            for (var offset = 0; offset + 2 < m_Data.TriangleIndices.Length; offset += 3)
            {
                GL.Color(m_Data.NormalColors[offset]);
                GL.Vertex3(m_Data.Points[m_Data.TriangleIndices[offset]].x, m_Data.Points[m_Data.TriangleIndices[offset]].y, 0.0f);
                GL.Color(m_Data.NormalColors[offset + 1]);
                GL.Vertex3(m_Data.Points[m_Data.TriangleIndices[offset + 1]].x, m_Data.Points[m_Data.TriangleIndices[offset + 1]].y, 0.0f);
                GL.Color(m_Data.NormalColors[offset + 2]);
                GL.Vertex3(m_Data.Points[m_Data.TriangleIndices[offset + 2]].x, m_Data.Points[m_Data.TriangleIndices[offset + 2]].y, 0.0f);
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

        public bool TryGetSelectionMask(Vector2Int textureSize, out RenderTexture selectionMask)
        {
            if (m_ActiveIsland < 0 ||
                m_Data?.TriangleIndices == null ||
                m_Data.Topology == null ||
                textureSize.x <= 0 ||
                textureSize.y <= 0)
            {
                selectionMask = null!;
                return false;
            }

            if (m_SelectionMask != null &&
                m_SelectionMask.width == textureSize.x &&
                m_SelectionMask.height == textureSize.y)
            {
                selectionMask = m_SelectionMask;
                return true;
            }

            ReleaseSelectionMask();
            EnsureSelectionMaskMaterial();
            m_SelectionMask = new(textureSize.x, textureSize.y, 0, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            m_SelectionMask.Create();

            var previous = RenderTexture.active;
            RenderTexture.active = m_SelectionMask;
            GL.Clear(false, true, Color.clear);
            GL.PushMatrix();
            GL.LoadOrtho();
            m_SelectionMaskMaterial.SetPass(0);
            GL.Begin(GL.TRIANGLES);
            foreach (var triangle in m_Data.Topology.GetIslandTriangles(m_ActiveIsland))
            {
                var offset = triangle * 3;
                GL.Vertex3(m_Data.Points[m_Data.TriangleIndices[offset]].x, m_Data.Points[m_Data.TriangleIndices[offset]].y, 0.0f);
                GL.Vertex3(m_Data.Points[m_Data.TriangleIndices[offset + 1]].x, m_Data.Points[m_Data.TriangleIndices[offset + 1]].y, 0.0f);
                GL.Vertex3(m_Data.Points[m_Data.TriangleIndices[offset + 2]].x, m_Data.Points[m_Data.TriangleIndices[offset + 2]].y, 0.0f);
            }
            GL.End();
            GL.PopMatrix();
            RenderTexture.active = previous;
            selectionMask = m_SelectionMask;
            return true;
        }

        private void EnsureSelectionMaskMaterial()
        {
            if (m_SelectionMaskMaterial != null)
            {
                return;
            }

            m_SelectionMaskMaterial = new(Shader.Find(k_SelectionMaskShaderName))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        public bool TryGetTriangles(out Vector2[] points, out int[] indices)
        {
            if (m_Data?.TriangleIndices != null)
            {
                points = m_Data.Points;
                indices = m_Data.TriangleIndices;
                return true;
            }

            points = null!;
            indices = null!;
            return false;
        }

        public bool TryFindIsland(Vector2 point, out int island)
        {
            if (m_Data?.Topology != null && m_Data.Topology.TryFindIsland(point, out island))
            {
                return true;
            }

            island = -1;
            return false;
        }

        public bool SetActiveIsland(int island)
        {
            if (m_ActiveIsland == island)
            {
                return false;
            }

            m_ActiveIsland = island;
            ReleaseSelectionMask();
            return true;
        }

        public void ClearActiveIsland()
        {
            if (m_ActiveIsland < 0)
            {
                return;
            }

            m_ActiveIsland = -1;
            ReleaseSelectionMask();
        }

        public bool TryGetActiveIsland(out MeshUvData data, out IReadOnlyList<int> triangles)
        {
            if (m_ActiveIsland >= 0 && m_Data?.Topology != null)
            {
                data = m_Data;
                triangles = m_Data.Topology.GetIslandTriangles(m_ActiveIsland);
                return true;
            }

            data = null!;
            triangles = null!;
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

        private MeshUvData? CollectMeshData()
        {
            if (m_Mesh == null)
            {
                return null;
            }

            if (m_Mesh.subMeshCount == 0)
            {
                return null;
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
                return null;
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
                return new(m_Mesh.uv, lineIndices.ToArray(), null, null);
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

            return new(m_Mesh.uv, lineIndices.ToArray(), indices, normalColors);
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

        private void ReleaseSelectionMask()
        {
            if (m_SelectionMask == null)
            {
                return;
            }

            DestroyImmediate(m_SelectionMask);
            m_SelectionMask = null!;
        }

        private void OnDestroy()
        {
            ReleaseNormalOverlay();
            ReleaseSelectionMask();

            if (m_NormalOverlayMaterial != null)
            {
                DestroyImmediate(m_NormalOverlayMaterial);
                m_NormalOverlayMaterial = null!;
            }
            if (m_SelectionMaskMaterial != null)
            {
                DestroyImmediate(m_SelectionMaskMaterial);
                m_SelectionMaskMaterial = null!;
            }
        }
    }
}
