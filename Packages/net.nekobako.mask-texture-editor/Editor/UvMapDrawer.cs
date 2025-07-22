using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace net.nekobako.MaskTextureEditor.Editor
{
    internal class UvMapDrawer : ScriptableObject
    {
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

        private void Awake()
        {
            // Set HideFlags to keep the state when reloading a scene or domain
            hideFlags = HideFlags.HideAndDontSave;
        }

        public void Init(Renderer? renderer, int? slot)
        {
            m_Renderer = renderer;
            m_Slot = slot ?? 0;
        }

        public void Draw(Rect rect)
        {
            var (mesh, material) = CollectMeshAndMaterial();
            if (m_IsDirty ||
                m_Mesh != mesh ||
                m_MeshDirtyCount != EditorUtility.GetDirtyCount(m_Mesh))
            {
                m_IsDirty = false;
                m_Mesh = mesh;
                m_MeshDirtyCount = EditorUtility.GetDirtyCount(m_Mesh);
                (m_Points, m_LineIndices) = CollectPointsAndLineIndices();
                m_Buffer = m_Points != null ? new Vector3[m_Points.Length] : null;
            }

            if (material != null && material.mainTexture != null)
            {
                // Draw the main texture if present
                EditorGUI.DrawTextureTransparent(rect, material.mainTexture);
            }

            if (m_Points != null && m_Buffer != null && m_LineIndices != null)
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
                    var material = m_Slot < meshRenderer.sharedMaterials.Length ? meshRenderer.sharedMaterials[m_Slot] : null;
                    return (mesh, material);
                }
                case SkinnedMeshRenderer skinnedMeshRenderer:
                {
                    var mesh = skinnedMeshRenderer.sharedMesh;
                    var material = m_Slot < skinnedMeshRenderer.sharedMaterials.Length ? skinnedMeshRenderer.sharedMaterials[m_Slot] : null;
                    return (mesh, material);
                }
                default:
                {
                    return (null, null);
                }
            }
        }

        private (Vector2[]?, int[]?) CollectPointsAndLineIndices()
        {
            if (m_Mesh == null)
            {
                return (null, null);
            }

            var topology = m_Mesh.GetTopology(Mathf.Min(m_Slot, m_Mesh.subMeshCount - 1)) switch
            {
                MeshTopology.Lines => 2,
                MeshTopology.Triangles => 3,
                MeshTopology.Quads => 4,
                _ => 0,
            };
            if (topology == 0)
            {
                return (null, null);
            }

            var lines = new HashSet<(int, int)>();
            var indices = m_Mesh.GetIndices(Mathf.Min(m_Slot, m_Mesh.subMeshCount - 1));
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

            return (m_Mesh.uv, lineIndices.ToArray());
        }
    }
}
