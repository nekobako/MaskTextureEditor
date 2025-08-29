using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace net.nekobako.MaskTextureEditor.Editor
{
    internal class TexturePainter : ScriptableObject
    {
        private const string k_FillShaderName = "Hidden/MaskTextureEditor/Fill";
        private const string k_PaintShaderName = "Hidden/MaskTextureEditor/Paint";
        private const string k_InverseShaderName = "Hidden/MaskTextureEditor/Inverse";

        private static readonly int s_ColorMaskPropertyId = Shader.PropertyToID("_ColorMask");
        private static readonly int s_ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int s_BrushSizePropertyId = Shader.PropertyToID("_BrushSize");
        private static readonly int s_BrushHardnessPropertyId = Shader.PropertyToID("_BrushHardness");
        private static readonly int s_BrushStrengthPropertyId = Shader.PropertyToID("_BrushStrength");
        private static readonly int s_BrushColorPropertyId = Shader.PropertyToID("_BrushColor");
        private static readonly int s_BrushPositionPropertyId = Shader.PropertyToID("_BrushPosition");

        [SerializeField]
        private RenderTexture m_Target = null!; // Initialize in Init

        [SerializeField]
        private RenderTexture m_Buffer = null!; // Initialize in Init

        [SerializeField]
        private Material m_FillMaterial = null!; // Initialize in Init

        [SerializeField]
        private Material m_PaintMaterial = null!; // Initialize in Init

        [SerializeField]
        private Material m_InverseMaterial = null!; // Initialize in Init

        [SerializeField]
        private ColorWriteMask m_ColorMask = ColorWriteMask.All;

        [SerializeField]
        private float m_BrushSize = 100.0f;

        [SerializeField]
        private float m_BrushHardness = 1.0f;

        [SerializeField]
        private float m_BrushStrength = 1.0f;

        [SerializeField]
        private float m_BrushDensity = 10.0f;

        [SerializeField]
        private Color m_BrushColor = Color.black;

        private Vector2 m_BrushPosition = Vector2.zero;

        public RenderTexture Texture => m_Target;
        public Vector2 TextureSize => new(m_Target.width, m_Target.height);

        public ColorWriteMask ColorMask
        {
            get => m_ColorMask;
            set => m_ColorMask = value;
        }

        public float BrushSize
        {
            get => m_BrushSize;
            set => m_BrushSize = value;
        }

        public float BrushHardness
        {
            get => m_BrushHardness;
            set => m_BrushHardness = value;
        }

        public float BrushStrength
        {
            get => m_BrushStrength;
            set => m_BrushStrength = value;
        }

        public float BrushDensity
        {
            get => m_BrushDensity;
            set => m_BrushDensity = value;
        }

        public Color BrushColor
        {
            get => m_BrushColor;
            set => m_BrushColor = value;
        }

        private void Awake()
        {
            // Set HideFlags to keep the state when reloading a scene or domain
            hideFlags = HideFlags.HideAndDontSave;
        }

        public void Init(Texture2D texture)
        {
            Init();
            Load(texture);
        }

        public void Init(Vector2Int size, Color color)
        {
            Init(size);
            Fill(color);
        }

        private void Init(Vector2Int size = default)
        {
            // Set HideFlags to keep the state when reloading a scene or domain
            m_Target = new(size.x, size.y, 0)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            m_Buffer = new(size.x, size.y, 0)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            m_FillMaterial = new(Shader.Find(k_FillShaderName))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            m_PaintMaterial = new(Shader.Find(k_PaintShaderName))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            m_InverseMaterial = new(Shader.Find(k_InverseShaderName))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        public void Draw(Rect rect, bool brush)
        {
            // Draw the texture
            var color = GUI.color;
            color.r = (m_ColorMask & ColorWriteMask.Red) != 0 ? color.r : 0.0f;
            color.g = (m_ColorMask & ColorWriteMask.Green) != 0 ? color.g : 0.0f;
            color.b = (m_ColorMask & ColorWriteMask.Blue) != 0 ? color.b : 0.0f;
            GUI.color = color;
            GUI.DrawTexture(rect, m_Target);

            // Draw the brush
            if (brush)
            {
                Handles.matrix = Matrix4x4.TRS(
                    Event.current.mousePosition,
                    Quaternion.identity,
                    m_BrushSize * rect.size / TextureSize);

                Handles.color = new(GUI.color.r * m_BrushColor.r, GUI.color.g * m_BrushColor.g, GUI.color.b * m_BrushColor.b, GUI.color.a * m_BrushColor.a);
                Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 0.5f);

                Handles.color = new(GUI.color.r * m_BrushColor.r, GUI.color.g * m_BrushColor.g, GUI.color.b * m_BrushColor.b, GUI.color.a * m_BrushColor.a * m_BrushStrength);
                Handles.DrawSolidDisc(Vector3.zero, Vector3.forward, 0.5f * m_BrushHardness);

                Handles.matrix = Matrix4x4.identity;
            }
        }

        public void Load(Texture2D texture)
        {
            m_Target.Release();
            m_Target.width = texture.width;
            m_Target.height = texture.height;
            m_Target.Create();

            m_Buffer.Release();
            m_Buffer.width = texture.width;
            m_Buffer.height = texture.height;
            m_Buffer.Create();

            Graphics.Blit(texture, m_Target);
            RenderTexture.active = null;
        }

        public void Save(Texture2D texture)
        {
            RenderTexture.active = m_Target;

            texture.Reinitialize(m_Target.width, m_Target.height);
            texture.ReadPixels(new(Vector2.zero, TextureSize), 0, 0);
            texture.Apply();

            RenderTexture.active = null;
        }

        public void Fill(Color color)
        {
            m_FillMaterial.SetInt(s_ColorMaskPropertyId, (int)m_ColorMask);
            m_FillMaterial.SetColor(s_ColorPropertyId, color);

            Graphics.Blit(m_Target, m_Buffer);
            Graphics.Blit(m_Buffer, m_Target, m_FillMaterial);
            RenderTexture.active = null;
        }

        public void Paint(Vector2 position, bool stroke)
        {
            m_PaintMaterial.SetInt(s_ColorMaskPropertyId, (int)m_ColorMask);
            m_PaintMaterial.SetFloat(s_BrushSizePropertyId, m_BrushSize);
            m_PaintMaterial.SetFloat(s_BrushHardnessPropertyId, m_BrushHardness);
            m_PaintMaterial.SetFloat(s_BrushStrengthPropertyId, m_BrushStrength);
            m_PaintMaterial.SetColor(s_BrushColorPropertyId, m_BrushColor);

            if (stroke)
            {
                var delta = position - m_BrushPosition;
                for (var i = 0; i < Mathf.FloorToInt(delta.magnitude / (m_BrushSize / m_BrushDensity)); i++)
                {
                    m_BrushPosition += delta.normalized * (m_BrushSize / m_BrushDensity);
                    m_PaintMaterial.SetVector(s_BrushPositionPropertyId, new(m_BrushPosition.x, TextureSize.y - m_BrushPosition.y));

                    Graphics.Blit(m_Target, m_Buffer);
                    Graphics.Blit(m_Buffer, m_Target, m_PaintMaterial);
                }
            }
            else
            {
                m_BrushPosition = position;
                m_PaintMaterial.SetVector(s_BrushPositionPropertyId, new(m_BrushPosition.x, TextureSize.y - m_BrushPosition.y));

                Graphics.Blit(m_Target, m_Buffer);
                Graphics.Blit(m_Buffer, m_Target, m_PaintMaterial);
            }

            RenderTexture.active = null;
        }

        public void Inverse()
        {
            m_InverseMaterial.SetInt(s_ColorMaskPropertyId, (int)m_ColorMask);

            Graphics.Blit(m_Target, m_Buffer);
            Graphics.Blit(m_Buffer, m_Target, m_InverseMaterial);
            RenderTexture.active = null;
        }

        private void OnDestroy()
        {
            if (m_Target != null)
            {
                DestroyImmediate(m_Target);
                m_Target = null!; // Reset
            }
            if (m_Buffer != null)
            {
                DestroyImmediate(m_Buffer);
                m_Buffer = null!; // Reset
            }
            if (m_FillMaterial != null)
            {
                DestroyImmediate(m_FillMaterial);
                m_FillMaterial = null!; // Reset
            }
            if (m_PaintMaterial != null)
            {
                DestroyImmediate(m_PaintMaterial);
                m_PaintMaterial = null!; // Reset
            }
            if (m_InverseMaterial != null)
            {
                DestroyImmediate(m_InverseMaterial);
                m_InverseMaterial = null!; // Reset
            }
        }
    }
}
