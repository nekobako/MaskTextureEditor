using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using CustomLocalization4EditorExtension;
using Object = UnityEngine.Object;

namespace net.nekobako.MaskTextureEditor.Editor
{
    public static class Utility
    {
        public static Texture2D? CreateTexture(Vector2Int size, Color color)
        {
            var path = EditorUtility.SaveFilePanelInProject(
                CL4EE.Tr("create"),
                string.Empty, "png", string.Empty);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var painter = ScriptableObject.CreateInstance<TexturePainter>();
            painter.Init(size, color);

            var texture = new Texture2D(0, 0);
            painter.Save(texture);

            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());

                AssetDatabase.ImportAsset(path);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.isReadable = true;
                importer.SaveAndReimport();

                var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (loaded == null)
                {
                    throw new("Failed to load a created texture.");
                }

                return loaded;
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(
                    CL4EE.Tr("error-title"),
                    CL4EE.Tr("error-message-create-failed"),
                    "OK");

                Debug.LogError(e);
            }
            finally
            {
                Object.DestroyImmediate(painter);
                Object.DestroyImmediate(texture);
            }

            return null;
        }

        public static Texture2D? SelectTexture()
        {
            var path = EditorUtility.OpenFilePanel(
                CL4EE.Tr("select"),
                "Assets", "png");
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            try
            {
                var assetsFolderPath = Path.GetFullPath("Assets").Replace('\\', '/');
                var packagesFolderPath = Path.GetFullPath("Packages").Replace('\\', '/');
                path =
                    path.StartsWith(assetsFolderPath) ? path[(assetsFolderPath.Length - "Assets".Length)..] :
                    path.StartsWith(packagesFolderPath) ? path[(packagesFolderPath.Length - "Packages".Length)..] :
                    null;
                if (string.IsNullOrEmpty(path))
                {
                    throw new("The texture is not in the Assets folder or the Packages folder.");
                }

                var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (loaded == null)
                {
                    throw new("Failed to load a selected texture.");
                }

                return loaded;
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(
                    CL4EE.Tr("error-title"),
                    CL4EE.Tr("error-message-select-failed"),
                    "OK");

                Debug.LogError(e);
            }

            return null;
        }
    }
}
