using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CustomLocalization4EditorExtension;
#if MTE_NDMF
using nadena.dev.ndmf.preview;
#endif

namespace net.nekobako.MaskTextureEditor.Editor
{
    public class Window : EditorWindow
    {
        [AssemblyCL4EELocalization]
        private static Localization Localization => new("1b6661244fd2dc646af94ebf766dd8aa", "ja-jp");

        private static class Styles
        {
            public static readonly GUIStyle Toolbar = new("Toolbar")
            {
                fixedHeight = 0.0f,
            };
            public static readonly GUIStyle Button = new("button")
            {
                fixedHeight = 24.0f,
            };
            public static readonly GUIStyle LargeButton = new("button")
            {
                fixedHeight = 48.0f,
            };
        }

        private static class Events
        {
            public static bool Paint => Event.current.button == 0
                && !Event.current.control && !Event.current.command
                && !Event.current.alt
                && !Event.current.shift;
            public static bool ViewScale => Event.current.control || Event.current.command;
            public static bool ViewOpacity => Event.current.alt;
            public static bool BrushSize => Event.current.shift;
        }

        private const float k_ViewScaleMin = 0.1f;
        private const float k_ViewScaleMax = 10.0f;
        private const float k_ViewScaleFactor = 0.1f;
        private const float k_ViewOpacityMin = 0.1f;
        private const float k_ViewOpacityMax = 1.0f;
        private const float k_ViewOpacityFactor = 0.01f;
        private const float k_BrushSizeMin = 10.0f;
        private const float k_BrushSizeMax = 1000.0f;
        private const float k_BrushSizeFactor = 0.1f;

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
        private bool m_RequestResetView = true;

        [SerializeField]
        private int m_SavedTextureInstanceId = 0;

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

        private void OnEnable()
        {
            IsOpen.Value = m_Texture != null;
        }

        private void OnDisable()
        {
            IsOpen.Value = false;
        }
#endif

        public static bool IsOpenFor(Renderer? renderer = null, int? slot = null, string? token = null)
        {
            return IsOpenFor(null, renderer, slot, token);
        }

        public static bool IsOpenFor(Texture2D? texture = null, Renderer? renderer = null, int? slot = null, string? token = null)
        {
            if (!HasOpenInstances<Window>())
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

            IsOpen.Value = m_Texture != null;
        }

        private void OnGUI()
        {
            wantsMouseMove = true;
            titleContent.text = CL4EE.Tr("title");
            hasUnsavedChanges = m_SavedTextureInstanceId != m_TextureUndoStack.Peek().GetInstanceID();
            saveChangesMessage = CL4EE.Tr("save-changes-message");

            using var horizontal = position.width > position.height ? new EditorGUILayout.HorizontalScope() : null;

            using (new EditorGUILayout.VerticalScope(Styles.Toolbar, horizontal != null ? GUILayout.Width(400.0f) : GUILayout.ExpandWidth(true)))
            {
                DrawToolbar(horizontal != null);
            }

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

        private void DrawToolbar(bool expand)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    CL4EE.Tr("texture"),
                    m_Texture, typeof(Texture2D), true, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            EditorGUILayout.Space();

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

            EditorGUILayout.Space();

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var prev = m_ViewScale;
                m_ViewScale = EditorGUILayout.Slider(
                    CL4EE.Tr("view-scale"),
                    m_ViewScale, k_ViewScaleMin, k_ViewScaleMax);
                if (check.changed)
                {
                    m_ViewPosition -= m_ViewPosition * (1.0f - m_ViewScale / prev);
                }
            }

            m_ViewOpacity = EditorGUILayout.Slider(
                CL4EE.Tr("view-opacity"),
                m_ViewOpacity, k_ViewOpacityMin, k_ViewOpacityMax);

            EditorGUILayout.Space();

            m_TexturePainter.BrushSize = EditorGUILayout.Slider(
                CL4EE.Tr("brush-size"),
                m_TexturePainter.BrushSize, k_BrushSizeMin, k_BrushSizeMax);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(CL4EE.Tr("brush-color"));

                var colors = new[] { Color.black, Color.white };
                var texts = new[] { CL4EE.Tr("black"), CL4EE.Tr("white") };
                var index = Array.IndexOf(colors, m_TexturePainter.BrushColor);
                m_TexturePainter.BrushColor = colors[GUILayout.Toolbar(index, texts)];
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(CL4EE.Tr("fill-black"), Styles.Button))
                {
                    m_TexturePainter.Fill(Color.black);
                    m_TextureUndoStack.Record();
                }
                if (GUILayout.Button(CL4EE.Tr("fill-white"), Styles.Button))
                {
                    m_TexturePainter.Fill(Color.white);
                    m_TextureUndoStack.Record();
                }
                if (GUILayout.Button(CL4EE.Tr("inverse"), Styles.Button))
                {
                    m_TexturePainter.Inverse();
                    m_TextureUndoStack.Record();
                }
            }

            if (GUILayout.Button(CL4EE.Tr("reset-view"), Styles.Button))
            {
                m_RequestResetView = true;
            }

            if (GUILayout.Button(CL4EE.Tr("save"), Styles.LargeButton))
            {
                SaveChanges();
            }

            if (expand)
            {
                GUILayout.FlexibleSpace();
            }
            else
            {
                EditorGUILayout.Space();
            }

            CL4EE.DrawLanguagePicker();
        }

        private void DrawContents(Rect rect, bool brush)
        {
            GUI.color = new(1.0f, 1.0f, 1.0f, 1.0f);
            m_UvMapDrawer.Draw(rect);

            GUI.color = new(1.0f, 1.0f, 1.0f, m_ViewOpacity);
            m_TexturePainter.Draw(rect, brush);
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
                        // Paint the texture
                        var pos = Event.current.mousePosition - rect.center + m_ViewPosition;
                        m_TexturePainter.Paint(pos / m_ViewScale, pos / m_ViewScale);
                    }
                    Repaint();
                    break;
                }
                case EventType.MouseUp when GUIUtility.hotControl == control:
                {
                    // Unset HotControl to allow other controls to respond
                    GUIUtility.hotControl = 0;

                    if (Events.Paint)
                    {
                        m_TextureUndoStack.Record();
                    }
                    Repaint();
                    break;
                }
                case EventType.MouseDrag when GUIUtility.hotControl == control:
                {
                    var delta = Event.current.delta;
                    if (Events.Paint)
                    {
                        // Paint the texture
                        var pos = Event.current.mousePosition - rect.center + m_ViewPosition;
                        m_TexturePainter.Paint((pos - delta) / m_ViewScale, pos / m_ViewScale);
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
                    else if (Events.BrushSize)
                    {
                        // Adjust the brush size
                        m_TexturePainter.BrushSize *= 1.0f - delta * k_BrushSizeFactor;
                        m_TexturePainter.BrushSize = Mathf.Clamp(m_TexturePainter.BrushSize, k_BrushSizeMin, k_BrushSizeMax);
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
                case EventType.ValidateCommand:
                {
                    Repaint();
                    break;
                }
            }

            // Ensure non-negative position to avoid scrollbars flickering
            m_ViewPosition = Vector2.Max(m_ViewPosition, Vector2.zero);
        }

        public override void SaveChanges()
        {
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
