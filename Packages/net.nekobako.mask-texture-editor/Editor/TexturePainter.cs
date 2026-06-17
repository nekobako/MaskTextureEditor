using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace net.nekobako.MaskTextureEditor.Editor
{
    internal enum BrushMode
    {
        Circle,
        Triangle,
    }

    internal enum GradientShape
    {
        Line,
        Band,
        Radial,
    }

    internal class TexturePainter : ScriptableObject
    {
        private sealed class TriangleLookup
        {
            private const int k_GridSize = 32;

            public readonly Vector2[] Points;
            public readonly int[] Indices;
            public readonly Vector2[] PixelPoints;
            public readonly List<int>[] Grid;
            public readonly int[] VisitStamps;
            public readonly Vector2 TextureSize;

            private int m_VisitStamp = 0;

            public TriangleLookup(Vector2[] points, int[] indices, Vector2 textureSize)
            {
                Points = points;
                Indices = indices;
                TextureSize = textureSize;
                PixelPoints = new Vector2[points.Length];
                Grid = new List<int>[k_GridSize * k_GridSize];
                VisitStamps = new int[indices.Length / 3];

                for (var i = 0; i < points.Length; i++)
                {
                    PixelPoints[i] = new(points[i].x * textureSize.x, (1.0f - points[i].y) * textureSize.y);
                }

                for (var offset = 0; offset + 2 < indices.Length; offset += 3)
                {
                    var indexA = indices[offset];
                    var indexB = indices[offset + 1];
                    var indexC = indices[offset + 2];
                    if (indexA < 0 || indexA >= points.Length ||
                        indexB < 0 || indexB >= points.Length ||
                        indexC < 0 || indexC >= points.Length)
                    {
                        continue;
                    }

                    var min = Vector2.Min(points[indexA], Vector2.Min(points[indexB], points[indexC]));
                    var max = Vector2.Max(points[indexA], Vector2.Max(points[indexB], points[indexC]));
                    var minX = ToGrid(min.x);
                    var minY = ToGrid(min.y);
                    var maxX = ToGrid(max.x);
                    var maxY = ToGrid(max.y);
                    for (var y = minY; y <= maxY; y++)
                    {
                        for (var x = minX; x <= maxX; x++)
                        {
                            var cell = y * k_GridSize + x;
                            Grid[cell] ??= new();
                            Grid[cell].Add(offset);
                        }
                    }
                }
            }

            public void CollectCandidates(Vector2 center, float radius, Vector2 textureSize, List<int> candidates)
            {
                candidates.Clear();

                if (++m_VisitStamp == int.MaxValue)
                {
                    System.Array.Clear(VisitStamps, 0, VisitStamps.Length);
                    m_VisitStamp = 1;
                }

                var minX = ToGrid((center.x - radius) / textureSize.x);
                var minY = ToGrid(1.0f - (center.y + radius) / textureSize.y);
                var maxX = ToGrid((center.x + radius) / textureSize.x);
                var maxY = ToGrid(1.0f - (center.y - radius) / textureSize.y);
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var entries = Grid[y * k_GridSize + x];
                        if (entries == null)
                        {
                            continue;
                        }

                        foreach (var offset in entries)
                        {
                            var triangle = offset / 3;
                            if (VisitStamps[triangle] == m_VisitStamp)
                            {
                                continue;
                            }

                            VisitStamps[triangle] = m_VisitStamp;
                            candidates.Add(offset);
                        }
                    }
                }
            }

            private static int ToGrid(float value)
            {
                return Mathf.Clamp(Mathf.FloorToInt(value * k_GridSize), 0, k_GridSize - 1);
            }
        }

        private const string k_FillShaderName = "Hidden/MaskTextureEditor/Fill";
        private const string k_PaintShaderName = "Hidden/MaskTextureEditor/Paint";
        private const string k_TrianglePaintShaderName = "Hidden/MaskTextureEditor/TrianglePaint";
        private const string k_GradientShaderName = "Hidden/MaskTextureEditor/Gradient";
        private const string k_InverseShaderName = "Hidden/MaskTextureEditor/Inverse";
        private const float k_TriangleMaskPadding = 0.75f;

        private static readonly int s_ColorMaskPropertyId = Shader.PropertyToID("_ColorMask");
        private static readonly int s_ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int s_BrushSizePropertyId = Shader.PropertyToID("_BrushSize");
        private static readonly int s_BrushHardnessPropertyId = Shader.PropertyToID("_BrushHardness");
        private static readonly int s_BrushStrengthPropertyId = Shader.PropertyToID("_BrushStrength");
        private static readonly int s_BrushColorPropertyId = Shader.PropertyToID("_BrushColor");
        private static readonly int s_BrushPositionPropertyId = Shader.PropertyToID("_BrushPosition");
        private static readonly int s_TriangleMaskPropertyId = Shader.PropertyToID("_TriangleMask");
        private static readonly int s_SelectionMaskPropertyId = Shader.PropertyToID("_SelectionMask");
        private static readonly int s_UseSelectionMaskPropertyId = Shader.PropertyToID("_UseSelectionMask");
        private static readonly int s_StartPointPropertyId = Shader.PropertyToID("_StartPoint");
        private static readonly int s_EndPointPropertyId = Shader.PropertyToID("_EndPoint");
        private static readonly int s_StartValuePropertyId = Shader.PropertyToID("_StartValue");
        private static readonly int s_EndValuePropertyId = Shader.PropertyToID("_EndValue");
        private static readonly int s_CurveExponentPropertyId = Shader.PropertyToID("_CurveExponent");
        private static readonly int s_TextureSizePropertyId = Shader.PropertyToID("_TextureSize");
        private static readonly int s_GradientShapePropertyId = Shader.PropertyToID("_GradientShape");
        private static readonly int s_GradientWidthPropertyId = Shader.PropertyToID("_GradientWidth");
        private static readonly int s_GradientFeatherPropertyId = Shader.PropertyToID("_GradientFeather");

        [SerializeField]
        private RenderTexture m_Target = null!; // Initialize in Init

        [SerializeField]
        private RenderTexture m_Buffer = null!; // Initialize in Init

        [SerializeField]
        private RenderTexture m_TriangleMask = null!; // Initialize in Init

        [SerializeField]
        private RenderTexture m_GradientSource = null!; // Initialize in Init

        [SerializeField]
        private RenderTexture m_GradientPreview = null!; // Initialize in Init

        [SerializeField]
        private Material m_FillMaterial = null!; // Initialize in Init

        [SerializeField]
        private Material m_PaintMaterial = null!; // Initialize in Init

        [SerializeField]
        private Material m_TrianglePaintMaterial = null!; // Initialize in Init

        [SerializeField]
        private Material m_GradientMaterial = null!; // Initialize in Init

        [SerializeField]
        private Material m_InverseMaterial = null!; // Initialize in Init

        [SerializeField]
        private BrushMode m_BrushMode = BrushMode.Circle;

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
        private readonly HashSet<int> m_PaintedTriangles = new();
        private readonly List<int> m_TriangleCandidates = new();
        private readonly List<int> m_HitTriangles = new();
        private TriangleLookup? m_TriangleLookup = null;
        private bool m_StrokeModified = false;
        private bool m_IsGradientPreview = false;

        public RenderTexture Texture => m_Target;
        public Vector2 TextureSize => new(m_Target.width, m_Target.height);
        public bool IsGradientPreview => m_IsGradientPreview;

        public BrushMode BrushMode
        {
            get => m_BrushMode;
            set => m_BrushMode = value;
        }

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
            m_TriangleMask = new(size.x, size.y, 0, RenderTextureFormat.R8)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            m_GradientSource = new(size.x, size.y, 0)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            m_GradientPreview = new(size.x, size.y, 0)
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
            m_TrianglePaintMaterial = new(Shader.Find(k_TrianglePaintShaderName))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            m_GradientMaterial = new(Shader.Find(k_GradientShaderName))
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
            GUI.DrawTexture(rect, m_IsGradientPreview ? m_GradientPreview : m_Target);

            // Draw the brush
            if (brush)
            {
                Handles.matrix = Matrix4x4.TRS(
                    Event.current.mousePosition,
                    Quaternion.identity,
                    m_BrushSize * rect.size / TextureSize);

                Handles.color = new(GUI.color.r * m_BrushColor.r, GUI.color.g * m_BrushColor.g, GUI.color.b * m_BrushColor.b, GUI.color.a * m_BrushColor.a);
                Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 0.5f);

                if (m_BrushMode == BrushMode.Circle)
                {
                    Handles.color = new(GUI.color.r * m_BrushColor.r, GUI.color.g * m_BrushColor.g, GUI.color.b * m_BrushColor.b, GUI.color.a * m_BrushColor.a * m_BrushStrength);
                    Handles.DrawSolidDisc(Vector3.zero, Vector3.forward, 0.5f * m_BrushHardness);
                }

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

            m_TriangleMask.Release();
            m_TriangleMask.width = texture.width;
            m_TriangleMask.height = texture.height;
            m_TriangleMask.Create();
            m_TriangleLookup = null;

            ResizeRenderTexture(m_GradientSource, texture.width, texture.height);
            ResizeRenderTexture(m_GradientPreview, texture.width, texture.height);
            m_IsGradientPreview = false;

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

        public void Fill(Color color, Texture? selectionMask = null)
        {
            m_FillMaterial.SetInt(s_ColorMaskPropertyId, (int)m_ColorMask);
            m_FillMaterial.SetColor(s_ColorPropertyId, color);
            SetSelectionMask(m_FillMaterial, selectionMask);

            Graphics.Blit(m_Target, m_Buffer);
            Graphics.Blit(m_Buffer, m_Target, m_FillMaterial);
            RenderTexture.active = null;
        }

        public void BeginStroke(Vector2 position, Vector2[]? points, int[]? indices, Texture? selectionMask)
        {
            m_BrushPosition = position;
            m_PaintedTriangles.Clear();
            m_StrokeModified = false;

            if (m_BrushMode == BrushMode.Triangle)
            {
                m_HitTriangles.Clear();
                CollectHitTriangles(position, points, indices, m_HitTriangles);
                PaintTriangles(points, indices, m_HitTriangles, selectionMask);
            }
            else
            {
                PaintCircle(position, selectionMask);
            }
        }

        public void ContinueStroke(Vector2 position, Vector2[]? points, int[]? indices, Texture? selectionMask)
        {
            var delta = position - m_BrushPosition;
            var spacing = m_BrushSize / m_BrushDensity;
            var spacingSquared = spacing * spacing;
            m_HitTriangles.Clear();
            while (delta.sqrMagnitude >= spacingSquared)
            {
                m_BrushPosition += delta * (spacing / Mathf.Sqrt(delta.sqrMagnitude));
                if (m_BrushMode == BrushMode.Triangle)
                {
                    CollectHitTriangles(m_BrushPosition, points, indices, m_HitTriangles);
                }
                else
                {
                    PaintCircle(m_BrushPosition, selectionMask);
                }
                delta = position - m_BrushPosition;
            }

            if (m_BrushMode == BrushMode.Triangle)
            {
                PaintTriangles(points, indices, m_HitTriangles, selectionMask);
            }
        }

        public bool EndStroke()
        {
            return m_StrokeModified;
        }

        public void BeginGradient()
        {
            Graphics.Blit(m_Target, m_GradientSource);
            Graphics.Blit(m_Target, m_GradientPreview);
            RenderTexture.active = null;
            m_IsGradientPreview = true;
        }

        public bool UpdateGradient(
            Vector2 start,
            Vector2 end,
            float startValue,
            float endValue,
            float curveExponent,
            GradientShape shape,
            float width,
            float feather,
            Texture? selectionMask)
        {
            if (!m_IsGradientPreview)
            {
                return false;
            }

            if ((end - start).sqrMagnitude <= Mathf.Epsilon)
            {
                Graphics.Blit(m_GradientSource, m_GradientPreview);
                RenderTexture.active = null;
                return false;
            }

            m_GradientMaterial.SetInt(s_ColorMaskPropertyId, (int)m_ColorMask);
            SetSelectionMask(m_GradientMaterial, selectionMask);
            m_GradientMaterial.SetVector(s_StartPointPropertyId, start);
            m_GradientMaterial.SetVector(s_EndPointPropertyId, end);
            m_GradientMaterial.SetFloat(s_StartValuePropertyId, startValue);
            m_GradientMaterial.SetFloat(s_EndValuePropertyId, endValue);
            m_GradientMaterial.SetFloat(s_CurveExponentPropertyId, curveExponent);
            m_GradientMaterial.SetVector(s_TextureSizePropertyId, new(TextureSize.x, TextureSize.y, 0.0f, 0.0f));
            m_GradientMaterial.SetInt(s_GradientShapePropertyId, (int)shape);
            m_GradientMaterial.SetFloat(s_GradientWidthPropertyId, width);
            m_GradientMaterial.SetFloat(s_GradientFeatherPropertyId, feather);
            Graphics.Blit(m_GradientSource, m_GradientPreview, m_GradientMaterial);
            RenderTexture.active = null;
            return true;
        }

        public bool CommitGradient()
        {
            if (!m_IsGradientPreview)
            {
                return false;
            }

            Graphics.Blit(m_GradientPreview, m_Target);
            RenderTexture.active = null;
            m_IsGradientPreview = false;
            return true;
        }

        public void CancelGradient()
        {
            m_IsGradientPreview = false;
        }

        private void PaintCircle(Vector2 position, Texture? selectionMask)
        {
            if (m_BrushStrength <= 0.0f)
            {
                return;
            }

            m_PaintMaterial.SetInt(s_ColorMaskPropertyId, (int)m_ColorMask);
            m_PaintMaterial.SetFloat(s_BrushSizePropertyId, m_BrushSize);
            m_PaintMaterial.SetFloat(s_BrushHardnessPropertyId, m_BrushHardness);
            m_PaintMaterial.SetFloat(s_BrushStrengthPropertyId, m_BrushStrength);
            m_PaintMaterial.SetColor(s_BrushColorPropertyId, m_BrushColor);
            m_PaintMaterial.SetVector(s_BrushPositionPropertyId, new(position.x, TextureSize.y - position.y));
            SetSelectionMask(m_PaintMaterial, selectionMask);

            Graphics.Blit(m_Target, m_Buffer);
            Graphics.Blit(m_Buffer, m_Target, m_PaintMaterial);
            RenderTexture.active = null;
            m_StrokeModified = true;
        }

        private void CollectHitTriangles(Vector2 position, Vector2[]? points, int[]? indices, List<int> hitTriangles)
        {
            if (m_BrushStrength <= 0.0f || points == null || indices == null)
            {
                return;
            }

            if (m_TriangleLookup == null ||
                m_TriangleLookup.Points != points ||
                m_TriangleLookup.Indices != indices ||
                m_TriangleLookup.TextureSize != TextureSize)
            {
                m_TriangleLookup = new(points, indices, TextureSize);
            }

            var radius = m_BrushSize * 0.5f;
            m_TriangleLookup.CollectCandidates(position, radius, TextureSize, m_TriangleCandidates);
            foreach (var offset in m_TriangleCandidates)
            {
                var triangle = offset / 3;
                if (m_PaintedTriangles.Contains(triangle))
                {
                    continue;
                }

                var a = m_TriangleLookup.PixelPoints[indices[offset]];
                var b = m_TriangleLookup.PixelPoints[indices[offset + 1]];
                var c = m_TriangleLookup.PixelPoints[indices[offset + 2]];
                if (!CircleIntersectsTriangle(position, radius, a, b, c))
                {
                    continue;
                }

                m_PaintedTriangles.Add(triangle);
                hitTriangles.Add(offset);
            }
        }

        private void PaintTriangles(
            Vector2[]? points,
            int[]? indices,
            List<int> hitTriangles,
            Texture? selectionMask)
        {
            if (points == null || indices == null || hitTriangles.Count == 0)
            {
                return;
            }

            m_TrianglePaintMaterial.SetInt(s_ColorMaskPropertyId, (int)m_ColorMask);
            m_TrianglePaintMaterial.SetColor(s_BrushColorPropertyId, m_BrushColor);
            m_TrianglePaintMaterial.SetFloat(s_BrushStrengthPropertyId, m_BrushStrength);
            m_TrianglePaintMaterial.SetTexture(s_TriangleMaskPropertyId, m_TriangleMask);
            SetSelectionMask(m_TrianglePaintMaterial, selectionMask);

            var previous = RenderTexture.active;
            RenderTexture.active = m_TriangleMask;
            GL.Clear(false, true, Color.clear);
            GL.PushMatrix();
            GL.LoadOrtho();
            m_TrianglePaintMaterial.SetPass(0);
            GL.Begin(GL.TRIANGLES);
            foreach (var offset in hitTriangles)
            {
                EmitPaddedUvTriangle(points, indices, offset, TextureSize, k_TriangleMaskPadding);
            }
            GL.End();
            GL.PopMatrix();

            Graphics.Blit(m_Target, m_Buffer);
            Graphics.Blit(m_Buffer, m_Target, m_TrianglePaintMaterial, 1);
            RenderTexture.active = previous;
            m_StrokeModified = true;
        }

        private static bool CircleIntersectsTriangle(Vector2 center, float radius, Vector2 a, Vector2 b, Vector2 c)
        {
            var min = Vector2.Min(a, Vector2.Min(b, c));
            var max = Vector2.Max(a, Vector2.Max(b, c));
            if (center.x + radius < min.x || center.x - radius > max.x ||
                center.y + radius < min.y || center.y - radius > max.y)
            {
                return false;
            }

            if (Mathf.Abs(Cross(b - a, c - a)) < Mathf.Epsilon)
            {
                return false;
            }

            if (PointInTriangle(center, a, b, c))
            {
                return true;
            }

            var radiusSquared = radius * radius;
            return
                (a - center).sqrMagnitude <= radiusSquared ||
                (b - center).sqrMagnitude <= radiusSquared ||
                (c - center).sqrMagnitude <= radiusSquared ||
                DistanceToSegmentSquared(center, a, b) <= radiusSquared ||
                DistanceToSegmentSquared(center, b, c) <= radiusSquared ||
                DistanceToSegmentSquared(center, c, a) <= radiusSquared;
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

        private static float DistanceToSegmentSquared(Vector2 point, Vector2 a, Vector2 b)
        {
            var segment = b - a;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
            {
                return (point - a).sqrMagnitude;
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared);
            return (point - (a + segment * t)).sqrMagnitude;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        internal static void EmitPaddedUvTriangle(
            Vector2[] points,
            int[] indices,
            int offset,
            Vector2 textureSize,
            float padding)
        {
            var a = points[indices[offset]];
            var b = points[indices[offset + 1]];
            var c = points[indices[offset + 2]];

            if (padding > 0.0f && textureSize.x > 0.0f && textureSize.y > 0.0f)
            {
                var pixelScale = new Vector2(textureSize.x, textureSize.y);
                var pixelA = Vector2.Scale(a, pixelScale);
                var pixelB = Vector2.Scale(b, pixelScale);
                var pixelC = Vector2.Scale(c, pixelScale);
                var center = (pixelA + pixelB + pixelC) / 3.0f;

                pixelA = PadTriangleVertex(pixelA, center, padding);
                pixelB = PadTriangleVertex(pixelB, center, padding);
                pixelC = PadTriangleVertex(pixelC, center, padding);

                a = new(pixelA.x / textureSize.x, pixelA.y / textureSize.y);
                b = new(pixelB.x / textureSize.x, pixelB.y / textureSize.y);
                c = new(pixelC.x / textureSize.x, pixelC.y / textureSize.y);
            }

            GL.Vertex3(a.x, a.y, 0.0f);
            GL.Vertex3(b.x, b.y, 0.0f);
            GL.Vertex3(c.x, c.y, 0.0f);
        }

        private static Vector2 PadTriangleVertex(Vector2 vertex, Vector2 center, float padding)
        {
            var direction = vertex - center;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return vertex;
            }

            return vertex + direction.normalized * padding;
        }

        private static void ResizeRenderTexture(RenderTexture texture, int width, int height)
        {
            texture.Release();
            texture.width = width;
            texture.height = height;
            texture.Create();
        }

        private static void SetSelectionMask(Material material, Texture? selectionMask)
        {
            material.SetTexture(s_SelectionMaskPropertyId, selectionMask);
            material.SetFloat(s_UseSelectionMaskPropertyId, selectionMask != null ? 1.0f : 0.0f);
        }

        public void Inverse(Texture? selectionMask = null)
        {
            m_InverseMaterial.SetInt(s_ColorMaskPropertyId, (int)m_ColorMask);
            SetSelectionMask(m_InverseMaterial, selectionMask);

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
            if (m_TriangleMask != null)
            {
                DestroyImmediate(m_TriangleMask);
                m_TriangleMask = null!; // Reset
            }
            if (m_GradientSource != null)
            {
                DestroyImmediate(m_GradientSource);
                m_GradientSource = null!; // Reset
            }
            if (m_GradientPreview != null)
            {
                DestroyImmediate(m_GradientPreview);
                m_GradientPreview = null!; // Reset
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
            if (m_TrianglePaintMaterial != null)
            {
                DestroyImmediate(m_TrianglePaintMaterial);
                m_TrianglePaintMaterial = null!; // Reset
            }
            if (m_GradientMaterial != null)
            {
                DestroyImmediate(m_GradientMaterial);
                m_GradientMaterial = null!; // Reset
            }
            if (m_InverseMaterial != null)
            {
                DestroyImmediate(m_InverseMaterial);
                m_InverseMaterial = null!; // Reset
            }
        }
    }
}
