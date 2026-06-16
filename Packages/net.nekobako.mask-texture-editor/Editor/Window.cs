using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using CustomLocalization4EditorExtension;
#if MTE_NDMF
using nadena.dev.ndmf.preview;
#endif

namespace net.nekobako.MaskTextureEditor.Editor
{
    public class Window : EditorWindow
    {
        private enum ToolMode
        {
            Paint = 0,
            Gradient = 2,
        }

        private enum OperationScope
        {
            WholeTexture,
            UvIslandUnderCursor,
        }

        private static class Styles
        {
            public static readonly GUIStyle Toolbar = new("Toolbar")
            {
                fixedHeight = 0.0f,
            };
            public static readonly GUIStyle Toggle = new("button")
            {
                fixedHeight = 0.0f,
            };
            public static readonly GUIStyle Button = new("button")
            {
                fixedHeight = 24.0f,
            };
            public static readonly GUIStyle CanvasToolbar = new(EditorStyles.toolbar)
            {
                fixedHeight = 24.0f,
            };
        }

        private static class Events
        {
            public static bool Paint => Event.current.button == 0
                && !Event.current.alt
                && !Event.current.shift
                && !(Event.current.control || Event.current.command);
            public static bool ViewScale => Event.current.alt && !Event.current.shift && !(Event.current.control || Event.current.command);
            public static bool ViewOpacity => !Event.current.alt && Event.current.shift && !(Event.current.control || Event.current.command);
            public static bool BrushSize => !Event.current.alt && !Event.current.shift && (Event.current.control || Event.current.command);
            public static bool BrushHardness => Event.current.alt && !Event.current.shift && (Event.current.control || Event.current.command);
            public static bool BrushStrength => !Event.current.alt && Event.current.shift && (Event.current.control || Event.current.command);
            public static bool BrushDensity => Event.current.alt && Event.current.shift && (Event.current.control || Event.current.command);
            public static bool GradientWidth => !Event.current.alt && !Event.current.shift && (Event.current.control || Event.current.command);
            public static bool GradientCurve => Event.current.alt && !Event.current.shift && (Event.current.control || Event.current.command);
            public static bool GradientFeather => !Event.current.alt && Event.current.shift && (Event.current.control || Event.current.command);
        }

        private const float k_ViewScaleMin = 0.1f;
        private const float k_ViewScaleMax = 10.0f;
        private const float k_ViewScaleFactor = 0.1f;
        private const float k_ViewOpacityMin = 0.0f;
        private const float k_ViewOpacityMax = 1.0f;
        private const float k_ViewOpacityFactor = 0.01f;
        private const float k_BrushSizeMin = 10.0f;
        private const float k_BrushSizeMax = 1000.0f;
        private const float k_BrushSizeFactor = 0.1f;
        private const float k_BrushHardnessMin = 0.0f;
        private const float k_BrushHardnessMax = 1.0f;
        private const float k_BrushHardnessFactor = 0.01f;
        private const float k_BrushStrengthMin = 0.0f;
        private const float k_BrushStrengthMax = 1.0f;
        private const float k_BrushStrengthFactor = 0.01f;
        private const float k_BrushDensityMin = 1.0f;
        private const float k_BrushDensityMax = 100.0f;
        private const float k_BrushDensityFactor = 0.1f;
        private const float k_GradientCurveMin = 0.25f;
        private const float k_GradientCurveMax = 8.0f;
        private const float k_GradientCurveFactor = 0.1f;
        private const float k_GradientWidthMin = 1.0f;
        private const float k_GradientWidthMax = 512.0f;
        private const float k_GradientWidthFactor = 0.1f;
        private const float k_GradientFeatherMin = 0.0f;
        private const float k_GradientFeatherMax = 512.0f;
        private const float k_GradientFeatherFactor = 0.1f;
        private const float k_SidebarWidth = 320.0f;
        private const float k_CanvasToolbarCompactWidth = 800.0f;

        [SerializeField]
        private Texture2D m_Texture = null!; // Initialize in Open

        [SerializeField]
        private UvMapDrawer m_UvMapDrawer = null!; // Initialize in Open

        [SerializeField]
        private TexturePainter m_TexturePainter = null!; // Initialize in Open

        [SerializeField]
        private TextureUndoStack m_TextureUndoStack = null!; // Initialize in Open

        [SerializeField]
        private bool m_IsLockRenderer = false;

        [SerializeField]
        private bool m_IsLockSlot = false;

        [SerializeField]
        private string m_Token = string.Empty;

        [SerializeField]
        private Vector2 m_ViewPosition = Vector2.zero;

        [SerializeField]
        private float m_ViewScale = 1.0f;

        [SerializeField]
        private float m_ViewOpacity = 0.5f;

        [SerializeField]
        private bool m_ShowUvPreview = true;

        [SerializeField]
        private bool m_ShowNormalOverlay = true;

        [SerializeField]
        private float m_NormalOverlayOpacity = 0.35f;

        [SerializeField]
        private ToolMode m_ToolMode = ToolMode.Paint;

        [SerializeField]
        private OperationScope m_PaintScope = OperationScope.WholeTexture;

        [SerializeField]
        private OperationScope m_GradientScope = OperationScope.UvIslandUnderCursor;

        [SerializeField]
        private GradientShape m_GradientShape = GradientShape.Band;

        [SerializeField]
        private float m_GradientStartValue = 0.0f;

        [SerializeField]
        private float m_GradientEndValue = 1.0f;

        [SerializeField]
        private float m_GradientCurveExponent = 1.0f;

        [SerializeField]
        private float m_GradientWidth = 128.0f;

        [SerializeField]
        private float m_GradientFeather = 32.0f;

        [SerializeField]
        private Vector2 m_SidebarScrollPosition = Vector2.zero;

        [SerializeField]
        private bool m_RequestResetView = true;

        [SerializeField]
        private int m_SavedTextureInstanceId = 0;

        private bool m_IsPaintingStroke = false;
        private bool m_IsGradientDragging = false;
        private bool m_GradientModified = false;
        private Vector2 m_GradientStart = Vector2.zero;
        private Vector2 m_GradientEnd = Vector2.zero;

#if MTE_NDMF
        public static readonly PublishedValue<bool> IsOpen = new(false);

        public static Texture2D? ObserveTextureFor(ComputeContext context, Renderer? renderer = null, int? slot = null, string? token = null)
        {
            return ObserveTextureFor(context, null, renderer, slot, token);
        }

        public static Texture2D? ObserveTextureFor(ComputeContext context, Texture2D? texture = null, Renderer? renderer = null, int? slot = null, string? token = null)
        {
            if (!context.Observe(IsOpen))
            {
                return null;
            }

            var window = GetWindow<Window>(string.Empty, false);
            if (!context.Observe(window, x =>
                (texture == null || x.m_Texture == texture) &&
                (renderer == null || x.m_UvMapDrawer.Renderer == renderer) &&
                (slot == null || x.m_UvMapDrawer.Slot == slot) &&
                (token == null || x.m_Token == token)))
            {
                return null;
            }

            return window.m_TextureUndoStack.ObservePeek(context);
        }
#endif

        private static bool s_IsOpen = false;

        public static bool IsOpenFor(Renderer? renderer = null, int? slot = null, string? token = null)
        {
            return IsOpenFor(null, renderer, slot, token);
        }

        public static bool IsOpenFor(Texture2D? texture = null, Renderer? renderer = null, int? slot = null, string? token = null)
        {
            if (!s_IsOpen)
            {
                return false;
            }

            var window = GetWindow<Window>(string.Empty, false);
            return
                (texture == null || window.m_Texture == texture) &&
                (renderer == null || window.m_UvMapDrawer.Renderer == renderer) &&
                (slot == null || window.m_UvMapDrawer.Slot == slot) &&
                (token == null || window.m_Token == token);
        }

        internal static void RepaintIfOpen()
        {
            if (HasOpenInstances<Window>())
            {
                GetWindow<Window>(string.Empty, false).Repaint();
            }
        }

        public static void TryOpen(Texture2D texture, Renderer? renderer = null, int? slot = null, string? token = null)
        {
            TryClose();

            if (HasOpenInstances<Window>())
            {
                return;
            }

            var window = CreateInstance<Window>();
            window.Init(texture, renderer, slot, token);
        }

        public static void TryClose()
        {
            if (!HasOpenInstances<Window>())
            {
                return;
            }

            var window = GetWindow<Window>();
            if (!window.hasUnsavedChanges)
            {
                window.Close();
                return;
            }

            switch (EditorUtility.DisplayDialogComplex(
                CL4EE.Tr("title"),
                window.saveChangesMessage,
                CL4EE.Tr("save-changes-button-save"),
                CL4EE.Tr("save-changes-button-cancel"),
                CL4EE.Tr("save-changes-button-discard")))
            {
                case 0:
                    {
                        window.SaveChanges();
                        window.Close();
                        return;
                    }
                case 2:
                    {
                        window.Close();
                        return;
                    }
            }
        }

        private void Init(Texture2D texture, Renderer? renderer = null, int? slot = null, string? token = null)
        {
            m_Texture = texture;

            m_UvMapDrawer = CreateInstance<UvMapDrawer>();
            m_UvMapDrawer.Init(renderer, slot);

            m_TexturePainter = CreateInstance<TexturePainter>();
            m_TexturePainter.Init(texture);

            m_TextureUndoStack = CreateInstance<TextureUndoStack>();
            m_TextureUndoStack.Init(m_TexturePainter.Texture);

            m_IsLockRenderer = renderer != null;
            m_IsLockSlot = slot != null;
            m_Token = token ?? string.Empty;
            m_SavedTextureInstanceId = m_TextureUndoStack.Peek().GetInstanceID();

            minSize = new(400.0f, 400.0f);
            position = new(0.0f, 0.0f, 1000.0f, 600.0f);

            Show();

            s_IsOpen = m_Texture != null;
#if MTE_NDMF
            IsOpen.Value = m_Texture != null;
#endif
        }

        private void OnEnable()
        {
            s_IsOpen = m_Texture != null;
#if MTE_NDMF
            IsOpen.Value = m_Texture != null;
#endif
        }

        private void OnDisable()
        {
            if (m_TexturePainter != null)
            {
                CancelActiveOperation();
            }
            s_IsOpen = false;
#if MTE_NDMF
            IsOpen.Value = false;
#endif
        }

        private void OnGUI()
        {
            wantsMouseMove = true;
            titleContent.text = CL4EE.Tr("title");
            hasUnsavedChanges = m_SavedTextureInstanceId != m_TextureUndoStack.Peek().GetInstanceID();
            saveChangesMessage = CL4EE.Tr("save-changes-message");

            var horizontalLayout = position.width > position.height;
            using var horizontal = horizontalLayout ? new EditorGUILayout.HorizontalScope() : null;

            using (new EditorGUILayout.VerticalScope(
                Styles.Toolbar,
                horizontalLayout ? GUILayout.Width(k_SidebarWidth) : GUILayout.ExpandWidth(true)))
            {
                DrawSidebar(horizontalLayout);
            }

            using var canvas = new EditorGUILayout.VerticalScope();
            DrawCanvasToolbar();
            DrawCanvas();
        }

        private void DrawCanvas()
        {
            var scrollRect = GUILayoutUtility.GetRect(
                0.0f,
                0.0f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            var viewportRect = new Rect(
                scrollRect.position.x,
                scrollRect.position.y,
                scrollRect.size.x - GUI.skin.verticalScrollbar.fixedWidth,
                scrollRect.size.y - GUI.skin.horizontalScrollbar.fixedHeight);

            // Add margins of half of the viewport on each side to make it easier to edit the edges of the texture
            var viewRect = new Rect(Vector2.zero, m_TexturePainter.TextureSize * m_ViewScale + viewportRect.size);
            var contentRect = new Rect(viewportRect.size * 0.5f, m_TexturePainter.TextureSize * m_ViewScale);
            var mousePosition = Event.current.mousePosition;

            using (var scroll = new GUI.ScrollViewScope(scrollRect, m_ViewPosition, viewRect, true, true)
            {
                handleScrollWheel = false,
            })
            {
                m_ViewPosition = scroll.scrollPosition;

                DrawContents(contentRect, viewportRect.Contains(mousePosition));
            }

            HandleEvents(viewportRect);
        }

        private void DrawSidebar(bool expand)
        {
            EditorGUIUtility.labelWidth = 100.0f;

            EditorGUILayout.LabelField(CL4EE.Tr("texture"), EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    m_Texture,
                    typeof(Texture2D),
                    true,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(
                m_SidebarScrollPosition,
                GUILayout.ExpandHeight(expand)))
            {
                m_SidebarScrollPosition = scroll.scrollPosition;

                EditorGUILayout.Space();
                DrawTargetSettings();
                EditorGUILayout.Space();
                DrawToolSettings();
                EditorGUILayout.Space();
                DrawTextureOperations();
            }

            if (!expand)
            {
                EditorGUILayout.Space();
            }

            if (GUILayout.Button(CL4EE.Tr("save"), Styles.Button, GUILayout.Height(42.0f)))
            {
                SaveChanges();
            }

            CL4EE.DrawLanguagePicker();
        }

        private void DrawTargetSettings()
        {
            using (new EditorGUI.DisabledScope(m_IsLockRenderer))
            {
                m_UvMapDrawer.Renderer = EditorGUILayout.ObjectField(
                    CL4EE.Tr("renderer"),
                    m_UvMapDrawer.Renderer, typeof(Renderer), true) as Renderer;
            }

            using (new EditorGUI.DisabledScope(m_IsLockSlot))
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(CL4EE.Tr("slot"));

                var texts = Enumerable.Range(0, m_UvMapDrawer.Renderer != null ? m_UvMapDrawer.Renderer.sharedMaterials.Length : 0)
                    .DefaultIfEmpty()
                    .Select(x => x.ToString())
                    .ToArray();
                var index = Mathf.Clamp(m_UvMapDrawer.Slot, 0, texts.Length - 1);
                m_UvMapDrawer.Slot = GUILayout.Toolbar(index, texts);
            }
        }

        private void DrawToolSettings()
        {
            EditorGUILayout.LabelField(CL4EE.Tr("tool-mode"), EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                var modes = new[] { ToolMode.Paint, ToolMode.Gradient };
                var texts = new[]
                {
                    CL4EE.Tr("tool-mode-paint"),
                    CL4EE.Tr("tool-mode-gradient"),
                };
                var index = Array.IndexOf(modes, m_ToolMode);
                index = Mathf.Max(index, 0);
                var selected = modes[GUILayout.Toolbar(index, texts)];
                if (selected != m_ToolMode)
                {
                    CancelActiveOperation();
                    m_ToolMode = selected;
                }
            }

            EditorGUILayout.Space();
            switch (m_ToolMode)
            {
                case ToolMode.Paint:
                    DrawPaintSettings();
                    break;
                case ToolMode.Gradient:
                    DrawGradientSettings();
                    break;
            }
        }

        private void DrawColorChannels()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(CL4EE.Tr("color-mask"));

                var mask = ColorWriteMask.Alpha;
                mask |= GUILayout.Toggle((m_TexturePainter.ColorMask & ColorWriteMask.Red) != 0, "R", Styles.Toggle) ? ColorWriteMask.Red : 0;
                mask |= GUILayout.Toggle((m_TexturePainter.ColorMask & ColorWriteMask.Green) != 0, "G", Styles.Toggle) ? ColorWriteMask.Green : 0;
                mask |= GUILayout.Toggle((m_TexturePainter.ColorMask & ColorWriteMask.Blue) != 0, "B", Styles.Toggle) ? ColorWriteMask.Blue : 0;
                m_TexturePainter.ColorMask = mask;
            }
        }

        private OperationScope DrawScopePopup(OperationScope scope, string uvIslandLabel)
        {
            var scopes = new[] { OperationScope.WholeTexture, OperationScope.UvIslandUnderCursor };
            var texts = new[] { CL4EE.Tr("scope-whole-texture"), CL4EE.Tr(uvIslandLabel) };
            var index = Array.IndexOf(scopes, scope);
            index = Mathf.Max(index, 0);
            return scopes[EditorGUILayout.Popup(CL4EE.Tr("scope"), index, texts)];
        }

        private void DrawPaintSettings()
        {
            DrawColorChannels();
            m_PaintScope = DrawScopePopup(m_PaintScope, "scope-uv-island-under-brush");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(CL4EE.Tr("brush-mode"));

                var modes = new[] { BrushMode.Circle, BrushMode.Triangle };
                var texts = new[] { CL4EE.Tr("brush-mode-circle"), CL4EE.Tr("brush-mode-triangle") };
                var index = Array.IndexOf(modes, m_TexturePainter.BrushMode);
                m_TexturePainter.BrushMode = modes[GUILayout.Toolbar(index, texts)];
            }

            m_TexturePainter.BrushSize = EditorGUILayout.Slider(
                new GUIContent(CL4EE.Tr("brush-size"), "Ctrl + Mouse Wheel"),
                m_TexturePainter.BrushSize, k_BrushSizeMin, k_BrushSizeMax);

            using (new EditorGUI.DisabledScope(m_TexturePainter.BrushMode == BrushMode.Triangle))
            {
                m_TexturePainter.BrushHardness = EditorGUILayout.Slider(
                    new GUIContent(CL4EE.Tr("brush-hardness"), "Ctrl + Alt + Mouse Wheel"),
                    m_TexturePainter.BrushHardness, k_BrushHardnessMin, k_BrushHardnessMax);
            }

            m_TexturePainter.BrushStrength = EditorGUILayout.Slider(
                new GUIContent(CL4EE.Tr("brush-strength"), "Ctrl + Shift + Mouse Wheel"),
                m_TexturePainter.BrushStrength, k_BrushStrengthMin, k_BrushStrengthMax);

            m_TexturePainter.BrushDensity = EditorGUILayout.Slider(
                new GUIContent(CL4EE.Tr("brush-density"), "Ctrl + Alt + Shift + Mouse Wheel"),
                m_TexturePainter.BrushDensity, k_BrushDensityMin, k_BrushDensityMax);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(CL4EE.Tr("brush-color"));

                var colors = new[] { Color.black, Color.white };
                var texts = new[] { CL4EE.Tr("black"), CL4EE.Tr("white") };
                var index = Array.IndexOf(colors, m_TexturePainter.BrushColor);
                m_TexturePainter.BrushColor = colors[GUILayout.Toolbar(index, texts)];
            }
        }

        private void DrawGradientSettings()
        {
            DrawColorChannels();
            m_GradientScope = DrawScopePopup(m_GradientScope, "scope-uv-island-under-drag");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(CL4EE.Tr("gradient-shape"));

                var shapes = new[] { GradientShape.Line, GradientShape.Band, GradientShape.Radial };
                var texts = new[]
                {
                    CL4EE.Tr("gradient-shape-line"),
                    CL4EE.Tr("gradient-shape-band"),
                    CL4EE.Tr("gradient-shape-radial"),
                };
                var index = Array.IndexOf(shapes, m_GradientShape);
                index = Mathf.Max(index, 0);
                m_GradientShape = shapes[GUILayout.Toolbar(index, texts)];
            }

            m_GradientStartValue = EditorGUILayout.Slider(
                new GUIContent(CL4EE.Tr("gradient-start-value"), CL4EE.Tr("gradient-value-tooltip")),
                m_GradientStartValue,
                0.0f,
                1.0f);
            m_GradientEndValue = EditorGUILayout.Slider(
                new GUIContent(CL4EE.Tr("gradient-end-value"), CL4EE.Tr("gradient-value-tooltip")),
                m_GradientEndValue,
                0.0f,
                1.0f);
            m_GradientCurveExponent = EditorGUILayout.Slider(
                new GUIContent(CL4EE.Tr("gradient-curve"), "Ctrl + Alt + Mouse Wheel"),
                m_GradientCurveExponent,
                k_GradientCurveMin,
                k_GradientCurveMax);

            if (m_GradientShape == GradientShape.Band)
            {
                m_GradientWidth = EditorGUILayout.Slider(
                    new GUIContent(CL4EE.Tr("gradient-width"), "Ctrl + Mouse Wheel"),
                    m_GradientWidth,
                    k_GradientWidthMin,
                    k_GradientWidthMax);
            }

            if (m_GradientShape == GradientShape.Band ||
                m_GradientShape == GradientShape.Radial)
            {
                m_GradientFeather = EditorGUILayout.Slider(
                    new GUIContent(CL4EE.Tr("gradient-feather"), "Ctrl + Shift + Mouse Wheel"),
                    m_GradientFeather,
                    k_GradientFeatherMin,
                    k_GradientFeatherMax);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(CL4EE.Tr("gradient-curve-linear"), EditorStyles.miniButtonLeft))
                {
                    m_GradientCurveExponent = 1.0f;
                }
                if (GUILayout.Button(CL4EE.Tr("gradient-curve-smooth"), EditorStyles.miniButtonMid))
                {
                    m_GradientCurveExponent = 2.0f;
                }
                if (GUILayout.Button(CL4EE.Tr("gradient-curve-sharp"), EditorStyles.miniButtonRight))
                {
                    m_GradientCurveExponent = 4.0f;
                }
            }

            if (GUILayout.Button(CL4EE.Tr("gradient-reverse"), Styles.Button))
            {
                (m_GradientStartValue, m_GradientEndValue) = (m_GradientEndValue, m_GradientStartValue);
            }

            EditorGUILayout.HelpBox(
                m_GradientScope == OperationScope.UvIslandUnderCursor
                    ? CL4EE.Tr("gradient-drag-island-hint")
                    : CL4EE.Tr("gradient-global-hint"),
                MessageType.Info);
        }

        private void DrawTextureOperations()
        {
            EditorGUILayout.LabelField(CL4EE.Tr("texture-operations"), EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(CL4EE.Tr("fill-black"), Styles.Button))
                {
                    CancelActiveOperation();
                    m_TexturePainter.Fill(Color.black);
                    m_TextureUndoStack.Record();
                }
                if (GUILayout.Button(CL4EE.Tr("fill-white"), Styles.Button))
                {
                    CancelActiveOperation();
                    m_TexturePainter.Fill(Color.white);
                    m_TextureUndoStack.Record();
                }
            }

            if (GUILayout.Button(CL4EE.Tr("inverse"), Styles.Button))
            {
                CancelActiveOperation();
                m_TexturePainter.Inverse();
                m_TextureUndoStack.Record();
            }
        }

        private void DrawCanvasToolbar()
        {
            var canvasWidth = position.width > position.height
                ? position.width - k_SidebarWidth
                : position.width;
            if (canvasWidth < k_CanvasToolbarCompactWidth)
            {
                using (new EditorGUILayout.HorizontalScope(Styles.CanvasToolbar))
                {
                    DrawViewControls();
                    GUILayout.FlexibleSpace();
                }
                using (new EditorGUILayout.HorizontalScope(Styles.CanvasToolbar))
                {
                    DrawPreviewControls();
                    GUILayout.FlexibleSpace();
                }
                return;
            }

            using (new EditorGUILayout.HorizontalScope(Styles.CanvasToolbar))
            {
                DrawViewControls();
                GUILayout.Space(12.0f);
                DrawPreviewControls();
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawViewControls()
        {
            if (GUILayout.Button(new GUIContent(CL4EE.Tr("reset-view-short"), CL4EE.Tr("reset-view")), EditorStyles.toolbarButton, GUILayout.Width(42.0f)))
            {
                m_RequestResetView = true;
            }

            GUILayout.Label(new GUIContent(CL4EE.Tr("zoom"), CL4EE.Tr("view-scale")), GUILayout.Width(40.0f));
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var prev = m_ViewScale;
                m_ViewScale = GUILayout.HorizontalSlider(
                    m_ViewScale,
                    k_ViewScaleMin,
                    k_ViewScaleMax,
                    GUILayout.Width(110.0f));
                if (check.changed)
                {
                    m_ViewPosition -= m_ViewPosition * (1.0f - m_ViewScale / prev);
                }
            }
            GUILayout.Label($"{m_ViewScale * 100.0f:0}%", GUILayout.Width(42.0f));

            GUILayout.Space(10.0f);
            GUILayout.Label(CL4EE.Tr("mask-opacity"), GUILayout.Width(92.0f));
            m_ViewOpacity = GUILayout.HorizontalSlider(
                m_ViewOpacity,
                k_ViewOpacityMin,
                k_ViewOpacityMax,
                GUILayout.Width(110.0f));
        }

        private void DrawPreviewControls()
        {
            m_ShowUvPreview = GUILayout.Toggle(
                m_ShowUvPreview,
                CL4EE.Tr("uv-preview-short"),
                EditorStyles.toolbarButton,
                GUILayout.Width(36.0f));
            GUILayout.Space(4.0f);
            m_ShowNormalOverlay = GUILayout.Toggle(
                m_ShowNormalOverlay,
                CL4EE.Tr("normal-overlay-short"),
                EditorStyles.toolbarButton,
                GUILayout.Width(70.0f));

            GUILayout.Space(10.0f);
            using (new EditorGUI.DisabledScope(!m_ShowNormalOverlay))
            {
                GUILayout.Label(CL4EE.Tr("normal-opacity-short"), GUILayout.Width(102.0f));
                m_NormalOverlayOpacity = GUILayout.HorizontalSlider(
                    m_NormalOverlayOpacity,
                    0.0f,
                    1.0f,
                    GUILayout.Width(90.0f));
            }
        }

        private void DrawContents(Rect rect, bool brush)
        {
            GUI.color = new(1.0f, 1.0f, 1.0f, 1.0f);
            m_UvMapDrawer.Draw(
                rect,
                Vector2Int.RoundToInt(m_TexturePainter.TextureSize),
                m_ShowUvPreview,
                m_ShowNormalOverlay,
                m_NormalOverlayOpacity,
                m_UvMapDrawer.HasActiveIsland && (m_IsPaintingStroke || m_IsGradientDragging));

            GUI.color = new(1.0f, 1.0f, 1.0f, m_ViewOpacity);
            m_TexturePainter.Draw(rect, brush && m_ToolMode == ToolMode.Paint);

            if (m_IsGradientDragging)
            {
                var start = Rect.NormalizedToPoint(rect, new(m_GradientStart.x, 1.0f - m_GradientStart.y));
                var end = Rect.NormalizedToPoint(rect, new(m_GradientEnd.x, 1.0f - m_GradientEnd.y));
                Handles.color = Color.cyan;
                Handles.DrawAAPolyLine(3.0f, start, end);
                Handles.DrawSolidDisc(start, Vector3.forward, 4.0f);
                Handles.DrawWireDisc(end, Vector3.forward, 6.0f);
            }
        }

        private void HandleEvents(Rect rect)
        {
            var control = GUIUtility.GetControlID(FocusType.Passive);
            var type = Event.current.GetTypeForControl(control);
            switch (type)
            {
                case EventType.Repaint:
                    {
                        if (m_RequestResetView)
                        {
                            // Fit the view to the window
                            m_ViewScale = Mathf.Min(
                                rect.size.x / m_TexturePainter.TextureSize.x,
                                rect.size.y / m_TexturePainter.TextureSize.y);
                            m_ViewScale = Mathf.Clamp(m_ViewScale, k_ViewScaleMin, k_ViewScaleMax);
                            m_ViewPosition = m_TexturePainter.TextureSize * (m_ViewScale * 0.5f);
                            m_RequestResetView = false;
                            Repaint();
                        }
                        break;
                    }
                case EventType.MouseMove:
                    {
                        Repaint();
                        break;
                    }
                case EventType.MouseDown when rect.Contains(Event.current.mousePosition):
                    {
                        // Set HotControl to continue dragging outside the window
                        GUIUtility.hotControl = control;

                        if (Events.Paint)
                        {
                            var pos = Event.current.mousePosition - rect.center + m_ViewPosition;
                            var texturePosition = pos / m_ViewScale;
                            var uv = TexturePositionToUv(texturePosition);
                            if (m_ToolMode == ToolMode.Gradient)
                            {
                                if (!BeginOperationScope(m_GradientScope, uv))
                                {
                                    GUIUtility.hotControl = 0;
                                    Repaint();
                                    break;
                                }

                                m_GradientStart = uv;
                                m_GradientEnd = m_GradientStart;
                                m_GradientModified = false;
                                m_IsGradientDragging = true;
                                m_TexturePainter.BeginGradient();
                            }
                            else
                            {
                                if (!BeginOperationScope(m_PaintScope, uv))
                                {
                                    GUIUtility.hotControl = 0;
                                    Repaint();
                                    break;
                                }

                                // Paint the texture
                                GetUvTriangles(out var points, out var indices);
                                m_TexturePainter.BeginStroke(
                                    texturePosition,
                                    points,
                                    indices,
                                    GetActiveIslandMask());
                                m_IsPaintingStroke = true;
                            }
                        }
                        Repaint();
                        break;
                    }
                case EventType.MouseUp when GUIUtility.hotControl == control:
                    {
                        // Unset HotControl to allow other controls to respond
                        GUIUtility.hotControl = 0;

                        if (m_IsPaintingStroke)
                        {
                            if (m_TexturePainter.EndStroke())
                            {
                                m_TextureUndoStack.Record();
                            }
                            m_IsPaintingStroke = false;
                            m_UvMapDrawer.ClearActiveIsland();
                        }
                        else if (m_IsGradientDragging)
                        {
                            if (m_GradientModified && m_TexturePainter.CommitGradient())
                            {
                                m_TextureUndoStack.Record();
                            }
                            else
                            {
                                m_TexturePainter.CancelGradient();
                            }
                            m_IsGradientDragging = false;
                            m_GradientModified = false;
                            m_UvMapDrawer.ClearActiveIsland();
                        }
                        Repaint();
                        break;
                    }
                case EventType.MouseDrag when GUIUtility.hotControl == control:
                    {
                        var delta = Event.current.delta;
                        if (m_IsPaintingStroke)
                        {
                            // Paint the texture
                            var pos = Event.current.mousePosition - rect.center + m_ViewPosition;
                            GetUvTriangles(out var points, out var indices);
                            m_TexturePainter.ContinueStroke(
                                pos / m_ViewScale,
                                points,
                                indices,
                                GetActiveIslandMask());
                        }
                        else if (m_IsGradientDragging)
                        {
                            var pos = Event.current.mousePosition - rect.center + m_ViewPosition;
                            m_GradientEnd = TexturePositionToUv(pos / m_ViewScale);
                            m_GradientModified = m_TexturePainter.UpdateGradient(
                                m_GradientStart,
                                m_GradientEnd,
                                m_GradientStartValue,
                                m_GradientEndValue,
                                m_GradientCurveExponent,
                                m_GradientShape,
                                m_GradientWidth,
                                m_GradientFeather,
                                GetActiveIslandMask());
                        }
                        else
                        {
                            // Move the view
                            m_ViewPosition -= delta;
                        }
                        Repaint();
                        break;
                    }
                case EventType.ScrollWheel when rect.Contains(Event.current.mousePosition):
                    {
                        var delta = Event.current.delta.x + Event.current.delta.y;
                        if (Events.ViewScale)
                        {
                            // Scale the view
                            var prev = m_ViewScale;
                            m_ViewScale *= 1.0f - delta * k_ViewScaleFactor;
                            m_ViewScale = Mathf.Clamp(m_ViewScale, k_ViewScaleMin, k_ViewScaleMax);
                            m_ViewPosition -= m_ViewPosition * (1.0f - m_ViewScale / prev);
                        }
                        else if (Events.ViewOpacity)
                        {
                            // Adjust the view opacity
                            m_ViewOpacity -= delta * k_ViewOpacityFactor;
                            m_ViewOpacity = Mathf.Clamp(m_ViewOpacity, k_ViewOpacityMin, k_ViewOpacityMax);
                        }
                        else if (m_ToolMode == ToolMode.Paint && Events.BrushSize)
                        {
                            // Adjust the brush size
                            m_TexturePainter.BrushSize *= 1.0f - delta * k_BrushSizeFactor;
                            m_TexturePainter.BrushSize = Mathf.Clamp(m_TexturePainter.BrushSize, k_BrushSizeMin, k_BrushSizeMax);
                        }
                        else if (m_ToolMode == ToolMode.Paint && Events.BrushHardness)
                        {
                            // Adjust the brush hardness
                            m_TexturePainter.BrushHardness -= delta * k_BrushHardnessFactor;
                            m_TexturePainter.BrushHardness = Mathf.Clamp(m_TexturePainter.BrushHardness, k_BrushHardnessMin, k_BrushHardnessMax);
                        }
                        else if (m_ToolMode == ToolMode.Paint && Events.BrushStrength)
                        {
                            // Adjust the brush strength
                            m_TexturePainter.BrushStrength -= delta * k_BrushStrengthFactor;
                            m_TexturePainter.BrushStrength = Mathf.Clamp(m_TexturePainter.BrushStrength, k_BrushStrengthMin, k_BrushStrengthMax);
                        }
                        else if (m_ToolMode == ToolMode.Paint && Events.BrushDensity)
                        {
                            // Adjust the brush density
                            m_TexturePainter.BrushDensity *= 1.0f - delta * k_BrushDensityFactor;
                            m_TexturePainter.BrushDensity = Mathf.Clamp(m_TexturePainter.BrushDensity, k_BrushDensityMin, k_BrushDensityMax);
                        }
                        else if (m_ToolMode == ToolMode.Gradient && m_GradientShape == GradientShape.Band && Events.GradientWidth)
                        {
                            // Adjust the gradient band width
                            m_GradientWidth *= 1.0f - delta * k_GradientWidthFactor;
                            m_GradientWidth = Mathf.Clamp(m_GradientWidth, k_GradientWidthMin, k_GradientWidthMax);
                        }
                        else if (m_ToolMode == ToolMode.Gradient && Events.GradientCurve)
                        {
                            // Adjust the gradient curve
                            m_GradientCurveExponent *= 1.0f - delta * k_GradientCurveFactor;
                            m_GradientCurveExponent = Mathf.Clamp(m_GradientCurveExponent, k_GradientCurveMin, k_GradientCurveMax);
                        }
                        else if (m_ToolMode == ToolMode.Gradient &&
                            (m_GradientShape == GradientShape.Band || m_GradientShape == GradientShape.Radial) &&
                            Events.GradientFeather)
                        {
                            // Adjust the gradient feather
                            m_GradientFeather = Mathf.Max(1.0f, m_GradientFeather) * (1.0f - delta * k_GradientFeatherFactor);
                            m_GradientFeather = Mathf.Clamp(m_GradientFeather, k_GradientFeatherMin, k_GradientFeatherMax);
                        }
                        else
                        {
                            // Scale the view around the mouse position
                            var prev = m_ViewScale;
                            m_ViewScale *= 1.0f - delta * k_ViewScaleFactor;
                            m_ViewScale = Mathf.Clamp(m_ViewScale, k_ViewScaleMin, k_ViewScaleMax);
                            var pos = Event.current.mousePosition - rect.center + m_ViewPosition;
                            m_ViewPosition -= pos * (1.0f - m_ViewScale / prev);
                        }
                        Repaint();
                        break;
                    }
                case EventType.KeyDown when Event.current.keyCode == KeyCode.Escape:
                    {
                        CancelActiveOperation();
                        GUIUtility.hotControl = 0;
                        Event.current.Use();
                        Repaint();
                        break;
                    }
                case EventType.ValidateCommand:
                    {
                        Repaint();
                        break;
                    }
            }

            // Ensure non-negative position to avoid scrollbars flickering
            m_ViewPosition = Vector2.Max(m_ViewPosition, Vector2.zero);
        }

        private Vector2 TexturePositionToUv(Vector2 position)
        {
            var textureSize = m_TexturePainter.TextureSize;
            return new(
                position.x / textureSize.x,
                1.0f - position.y / textureSize.y);
        }

        private bool BeginOperationScope(OperationScope scope, Vector2 uv)
        {
            if (scope == OperationScope.WholeTexture)
            {
                m_UvMapDrawer.ClearActiveIsland();
                return true;
            }

            if (m_UvMapDrawer.TryFindIsland(uv, out var island))
            {
                m_UvMapDrawer.SetActiveIsland(island);
                return true;
            }

            m_UvMapDrawer.ClearActiveIsland();
            ShowNotification(new GUIContent(CL4EE.Tr("uv-island-not-found")));
            return false;
        }

        private Texture? GetActiveIslandMask()
        {
            if (!m_UvMapDrawer.HasActiveIsland)
            {
                return null;
            }

            var textureSize = Vector2Int.RoundToInt(m_TexturePainter.TextureSize);
            return m_UvMapDrawer.TryGetSelectionMask(textureSize, out var selectionMask)
                ? selectionMask
                : null;
        }

        private void CancelActiveOperation()
        {
            m_IsPaintingStroke = false;
            m_IsGradientDragging = false;
            m_GradientModified = false;
            m_TexturePainter.CancelGradient();
            m_UvMapDrawer.ClearActiveIsland();
        }

        private void GetUvTriangles(out Vector2[]? points, out int[]? indices)
        {
            if (m_TexturePainter.BrushMode == BrushMode.Triangle &&
                m_UvMapDrawer.TryGetTriangles(out var uvPoints, out var triangleIndices))
            {
                points = uvPoints;
                indices = triangleIndices;
                return;
            }

            points = null;
            indices = null;
        }

        public override void SaveChanges()
        {
            CancelActiveOperation();
            base.SaveChanges();

            var path = AssetDatabase.GetAssetPath(m_Texture);

            var texture = new Texture2D(0, 0);
            m_TexturePainter.Save(texture);

            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());

                AssetDatabase.ImportAsset(path);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.isReadable = true;
                importer.SaveAndReimport();

                m_SavedTextureInstanceId = m_TextureUndoStack.Peek().GetInstanceID();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(
                    CL4EE.Tr("error-title"),
                    CL4EE.Tr("error-message-save-failed"),
                    "OK");

                Debug.LogError(e);
            }
            finally
            {
                DestroyImmediate(texture);
            }
        }

        private void OnDestroy()
        {
            if (m_Texture != null)
            {
                m_Texture = null!; // Reset
            }
            if (m_UvMapDrawer != null)
            {
                DestroyImmediate(m_UvMapDrawer);
                m_UvMapDrawer = null!; // Reset
            }
            if (m_TexturePainter != null)
            {
                DestroyImmediate(m_TexturePainter);
                m_TexturePainter = null!; // Reset
            }
            if (m_TextureUndoStack != null)
            {
                DestroyImmediate(m_TextureUndoStack);
                m_TextureUndoStack = null!; // Reset
            }
        }
    }
}
