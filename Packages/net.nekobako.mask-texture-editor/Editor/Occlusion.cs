using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CustomLocalization4EditorExtension;
using Object = UnityEngine.Object;

namespace net.nekobako.MaskTextureEditor.Editor
{
    internal static class Occlusion
    {
        public static bool Paint(Renderer targetRenderer, int slot, Renderer occluderRenderer, float offset, TexturePainter painter)
        {
            Mesh? target = null;
            Mesh? occluder = null;
            GameObject? temporary = null;
            var backfaces = Physics.queriesHitBackfaces;
            try
            {
                if (!(offset > 0) || float.IsInfinity(offset))
                    throw new InvalidOperationException(CL4EE.Tr("occlusion-error-offset"));
                if (targetRenderer == occluderRenderer)
                    throw new InvalidOperationException(CL4EE.Tr("occlusion-error-same-renderer"));

                target = SnapshotMesh(targetRenderer);
                occluder = SnapshotMesh(occluderRenderer);
                if (slot < 0 || slot >= target.subMeshCount)
                    throw new InvalidOperationException(CL4EE.Tr("occlusion-error-no-submesh"));
                var vertices = target.vertices;
                var uv = target.uv;
                if (uv.Length != vertices.Length)
                    throw new InvalidOperationException(CL4EE.Tr("occlusion-error-no-uv"));
                var indices = target.GetTriangles(slot);
                var covered = new List<int>();
                temporary = new GameObject(nameof(Occlusion)) { hideFlags = HideFlags.HideAndDontSave };
                var collider = temporary.AddComponent<MeshCollider>();
                collider.sharedMesh = occluder;
                Physics.queriesHitBackfaces = true;

                for (var i = 0; i < indices.Length; i += 3)
                {
                    if (i % 768 == 0 && EditorUtility.DisplayCancelableProgressBar(
                        CL4EE.Tr("auto-detect-occlusion"), CL4EE.Tr("occlusion-progress"), (float)i / indices.Length))
                        return false;
                    if (!TriangleOccluded(collider, vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]], offset))
                        continue;
                    covered.Add(indices[i]);
                    covered.Add(indices[i + 1]);
                    covered.Add(indices[i + 2]);
                }

                if (covered.Count == 0)
                    return false;
                painter.PaintOcclusion(uv, covered);
                return true;
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(CL4EE.Tr("auto-detect-occlusion"), e.Message, CL4EE.Tr("ok"));
                Debug.LogException(e);
                return false;
            }
            finally
            {
                Physics.queriesHitBackfaces = backfaces;
                EditorUtility.ClearProgressBar();
                if (temporary != null) Object.DestroyImmediate(temporary);
                if (target != null) Object.DestroyImmediate(target);
                if (occluder != null) Object.DestroyImmediate(occluder);
            }
        }

        private static Mesh SnapshotMesh(Renderer renderer)
        {
            var source = renderer switch
            {
                SkinnedMeshRenderer skinned => skinned.sharedMesh,
                MeshRenderer regular when regular.TryGetComponent<MeshFilter>(out var filter) => filter.sharedMesh,
                _ => null,
            };
            if (renderer == null || source == null)
                throw new InvalidOperationException(CL4EE.Tr("occlusion-error-renderer"));
            for (var i = 0; i < source.subMeshCount; i++)
                if (source.GetTopology(i) != MeshTopology.Triangles)
                    throw new InvalidOperationException(CL4EE.Tr("occlusion-error-topology"));

            var mesh = renderer is SkinnedMeshRenderer ? new Mesh() : Object.Instantiate(source);
            try
            {
                mesh.hideFlags = HideFlags.HideAndDontSave;
                if (renderer is SkinnedMeshRenderer skinned)
                    skinned.BakeMesh(mesh, false);
                var localToWorld = renderer.localToWorldMatrix;
                var vertices = mesh.vertices;
                for (var i = 0; i < vertices.Length; i++)
                    vertices[i] = localToWorld.MultiplyPoint3x4(vertices[i]);
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                return mesh;
            }
            catch
            {
                Object.DestroyImmediate(mesh);
                throw;
            }
        }

        private static bool TriangleOccluded(MeshCollider collider, Vector3 a, Vector3 b, Vector3 c, float offset)
        {
            var normal = Vector3.Cross(b - a, c - a);
            var length = normal.magnitude;
            if (length == 0f)
                return false;
            normal /= length;
            return SampleOccluded(collider, a, normal, offset)
                && SampleOccluded(collider, b, normal, offset)
                && SampleOccluded(collider, c, normal, offset)
                && SampleOccluded(collider, (a + b + c) / 3, normal, offset);
        }

        private static bool SampleOccluded(MeshCollider collider, Vector3 point, Vector3 normal, float offset)
        {
            return collider.Raycast(new Ray(point + normal * offset, -normal), out _, offset * 1.1f);
        }
    }
}
