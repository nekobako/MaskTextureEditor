using UnityEditor;
using UnityEngine;

namespace net.nekobako.MaskTextureEditor.Editor
{
    internal static class Menu
    {
        private const string k_CreateAndOpenMenuPath = "Tools/Mask Texture Editor/New Texture";
        private const string k_SelectAndOpenMenuPath = "Tools/Mask Texture Editor/Open Texture";
        private const string k_CreateAndOpenForRendererMenuPath = "CONTEXT/Renderer/Mask Texture Editor/New Texture";
        private const string k_SelectAndOpenForRendererMenuPath = "CONTEXT/Renderer/Mask Texture Editor/Open Texture";
        private const string k_CreateAndOpenForGameObjectMenuPath = "GameObject/Mask Texture Editor/New Texture";
        private const string k_SelectAndOpenForGameObjectMenuPath = "GameObject/Mask Texture Editor/Open Texture";
        private const string k_OpenTextureMenuPath = "Assets/Edit with Mask Texture Editor";

        private static readonly Vector2Int s_DefaultTextureSize = new(1024, 1024);
        private static readonly Color s_DefaultTextureColor = Color.white;

        [MenuItem(k_CreateAndOpenMenuPath)]
        private static void CreateAndOpen(MenuCommand command)
        {
            CreateAndOpen();
        }

        [MenuItem(k_SelectAndOpenMenuPath)]
        private static void SelectAndOpen(MenuCommand command)
        {
            SelectAndOpen();
        }

        [MenuItem(k_CreateAndOpenForRendererMenuPath, true)]
        [MenuItem(k_SelectAndOpenForRendererMenuPath, true)]
        private static bool ValidateForRenderer(MenuCommand command)
        {
            return command.context is MeshRenderer or SkinnedMeshRenderer;
        }

        [MenuItem(k_CreateAndOpenForRendererMenuPath)]
        private static void CreateAndOpenForRenderer(MenuCommand command)
        {
            if (command.context is Renderer renderer)
            {
                CreateAndOpen(renderer);
            }
        }

        [MenuItem(k_SelectAndOpenForRendererMenuPath)]
        private static void SelectAndOpenForRenderer(MenuCommand command)
        {
            if (command.context is Renderer renderer)
            {
                SelectAndOpen(renderer);
            }
        }

        [MenuItem(k_CreateAndOpenForGameObjectMenuPath, true)]
        [MenuItem(k_SelectAndOpenForGameObjectMenuPath, true)]
        private static bool ValidateForGameObject(MenuCommand command)
        {
            return Selection.gameObjects.Length == 1
                && Selection.activeGameObject.TryGetComponent<Renderer>(out var renderer)
                && renderer is MeshRenderer or SkinnedMeshRenderer;
        }

        [MenuItem(k_CreateAndOpenForGameObjectMenuPath)]
        private static void CreateAndOpenForGameObject(MenuCommand command)
        {
            if (Selection.activeGameObject.TryGetComponent<Renderer>(out var renderer))
            {
                CreateAndOpen(renderer);
            }
        }

        [MenuItem(k_SelectAndOpenForGameObjectMenuPath)]
        private static void SelectAndOpenForGameObject(MenuCommand command)
        {
            if (Selection.activeGameObject.TryGetComponent<Renderer>(out var renderer))
            {
                SelectAndOpen(renderer);
            }
        }

        private static void CreateAndOpen(Renderer? renderer = null, int? slot = null)
        {
            var texture = Utility.CreateTexture(s_DefaultTextureSize, s_DefaultTextureColor);
            if (texture != null)
            {
                Window.TryOpen(texture, renderer, slot);
            }
        }

        private static void SelectAndOpen(Renderer? renderer = null, int? slot = null)
        {
            var texture = Utility.SelectTexture();
            if (texture != null)
            {
                Window.TryOpen(texture, renderer, slot);
            }
        }

        [MenuItem(k_OpenTextureMenuPath, true)]
        private static bool ValidateTexture(MenuCommand command)
        {
            return Selection.objects.Length == 1
                && Selection.activeObject is Texture2D;
        }

        [MenuItem(k_OpenTextureMenuPath)]
        private static void OpenTexture(MenuCommand command)
        {
            if (Selection.activeObject is Texture2D texture)
            {
                Window.TryOpen(texture);
            }
        }
    }
}
