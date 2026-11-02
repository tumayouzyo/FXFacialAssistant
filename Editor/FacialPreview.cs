using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace TY.FXFacialAssistant
{
    [Serializable]
    internal sealed class FacialCameraSettings
    {
        public float Distance = 0.65f;
        public float Height = 0.06f;
        public float FocalLength = 85f;
        public float Yaw;

        public float FieldOfView => Camera.FocalLengthToFieldOfView(FocalLength, 24f);
    }

    internal sealed class FacialPreview : IDisposable
    {
        private readonly PreviewRenderUtility utility;
        private readonly Dictionary<Transform, Transform> transforms = new Dictionary<Transform, Transform>();
        private readonly Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer> renderers
            = new Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer>();
        private readonly GameObject root;
        private readonly Vector3 headPosition;
        private readonly float scale;

        public string Note { get; }

        public FacialPreview(VRCAvatarDescriptor avatar)
        {
            utility = new PreviewRenderUtility();
            try
            {
                root = new GameObject(avatar.name + " Facial Preview") { hideFlags = HideFlags.HideAndDontSave };
                utility.AddSingleGO(root);
                root.transform.localScale = avatar.transform.lossyScale;
                transforms.Add(avatar.transform, root.transform);
                foreach (Transform child in avatar.transform) CopyTransform(child, root.transform);
                CopyRenderers(avatar.gameObject);
                var animator = avatar.GetComponent<Animator>();
                var head = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                if (head && transforms.TryGetValue(head, out var previewHead))
                    headPosition = previewHead.position;
                else
                {
                    headPosition = root.transform.TransformPoint(avatar.ViewPosition);
                    Note = "Headボーンが取得できないため、DescriptorのView Positionを基準に表示しています。";
                }
                scale = Mathf.Max(0.05f, Vector3.Distance(root.transform.position, headPosition) / 1.4f);
                utility.ambientColor = new Color(0.65f, 0.65f, 0.65f);
                utility.lights[0].intensity = 1f;
                utility.lights[0].color = Color.white;
                utility.lights[0].transform.rotation = Quaternion.Euler(30f, 210f, 0f);
                utility.lights[1].intensity = 0.5f;
                utility.lights[1].color = Color.white;
                utility.lights[1].transform.rotation = Quaternion.Euler(340f, 40f, 0f);
                var camera = utility.camera;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.nearClipPlane = 0.01f * scale;
                camera.farClipPlane = 30f * scale;
                camera.cullingMask = ~0;
            }
            catch
            {
                utility.Cleanup();
                throw;
            }
        }

        private void CopyTransform(Transform source, Transform parent)
        {
            var copy = new GameObject(source.name) { hideFlags = HideFlags.HideAndDontSave };
            copy.transform.SetParent(parent, false);
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            copy.transform.localScale = source.localScale;
            copy.SetActive(source.gameObject.activeSelf);
            transforms.Add(source, copy.transform);
            foreach (Transform child in source) CopyTransform(child, copy.transform);
        }

        private void CopyRenderers(GameObject avatar)
        {
            foreach (var source in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!source.sharedMesh) continue;
                var copy = transforms[source.transform].gameObject.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = source.sharedMesh;
                copy.bones = source.bones.Select(b => b && transforms.TryGetValue(b, out var target) ? target : null).ToArray();
                copy.rootBone = source.rootBone && transforms.TryGetValue(source.rootBone, out var bone) ? bone : null;
                copy.localBounds = source.localBounds;
                copy.quality = source.quality;
                copy.updateWhenOffscreen = true;
                for (var i = 0; i < source.sharedMesh.blendShapeCount; i++)
                    copy.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
                CopyRendererProperties(source, copy);
                renderers.Add(source, copy);
            }
            foreach (var source in avatar.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = source.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                var go = transforms[source.transform].gameObject;
                go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                CopyRendererProperties(source, go.AddComponent<MeshRenderer>());
            }
        }

        private static void CopyRendererProperties(Renderer source, Renderer target)
        {
            target.sharedMaterials = source.sharedMaterials;
            target.enabled = source.enabled;
            target.forceRenderingOff = source.forceRenderingOff;
            target.shadowCastingMode = ShadowCastingMode.Off;
            target.receiveShadows = false;
            target.lightProbeUsage = LightProbeUsage.Off;
            target.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var properties = new MaterialPropertyBlock();
            source.GetPropertyBlock(properties);
            target.SetPropertyBlock(properties);
            for (var i = 0; i < source.sharedMaterials.Length; i++)
            {
                properties.Clear();
                source.GetPropertyBlock(properties, i);
                if (!properties.isEmpty) target.SetPropertyBlock(properties, i);
            }
        }

        public void Apply(FacialMeshSession session)
        {
            if (!session.IsValid || !renderers.TryGetValue(session.Renderer, out var target))
                throw new InvalidOperationException("プレビューを再読み込みしてください。");
            session.ApplyTo(target);
        }

        public Texture Render(FacialCameraSettings settings, int pixels)
        {
            ConfigureCamera(settings);
            utility.camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            return RenderFrame(pixels);
        }

        private void ConfigureCamera(FacialCameraSettings settings)
        {
            var target = headPosition + Vector3.up * (settings.Height * scale);
            var offset = Quaternion.AngleAxis(settings.Yaw, Vector3.up) * Vector3.forward * (settings.Distance * scale);
            utility.camera.transform.SetPositionAndRotation(target + offset, Quaternion.LookRotation(-offset, Vector3.up));
            utility.camera.fieldOfView = settings.FieldOfView;
            utility.camera.aspect = 1f;
        }

        private Texture RenderFrame(int pixels)
        {
            var size = pixels / EditorGUIUtility.pixelsPerPoint;
            utility.BeginPreview(new Rect(0f, 0f, size, size), GUIStyle.none);
            try { utility.Render(false, false); }
            finally { utility.EndPreview(); }
            return utility.camera.targetTexture;
        }

        public Texture2D Capture(FacialCameraSettings settings)
        {
            ConfigureCamera(settings);
            Texture2D black = null;
            Texture2D white = null;
            Texture2D result = null;
            try
            {
                // Two backgrounds recover coverage even when a material does not write useful alpha.
                utility.camera.backgroundColor = Color.black;
                black = ReadTexture((RenderTexture)RenderFrame(512));
                utility.camera.backgroundColor = Color.white;
                white = ReadTexture((RenderTexture)RenderFrame(512));
                var darkPixels = black.GetPixels();
                var lightPixels = white.GetPixels();
                var pixels = new Color[256 * 256];
                for (var y = 0; y < 256; y++)
                for (var x = 0; x < 256; x++)
                {
                    var i = y * 2 * 512 + x * 2;
                    var dark = (darkPixels[i] + darkPixels[i + 1] + darkPixels[i + 512] + darkPixels[i + 513]) * 0.25f;
                    var light = (lightPixels[i] + lightPixels[i + 1] + lightPixels[i + 512] + lightPixels[i + 513]) * 0.25f;
                    pixels[y * 256 + x] = RecoverTransparency(dark, light, QualitySettings.activeColorSpace == ColorSpace.Linear);
                }
                result = new Texture2D(256, 256, TextureFormat.RGBA32, false, false)
                    { hideFlags = HideFlags.HideAndDontSave };
                result.SetPixels(pixels);
                result.Apply();
                return result;
            }
            catch
            {
                if (result) Object.DestroyImmediate(result);
                throw;
            }
            finally
            {
                if (black) Object.DestroyImmediate(black);
                if (white) Object.DestroyImmediate(white);
                utility.camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
        }

        internal static Color RecoverTransparency(Color black, Color white, bool linear)
        {
            // PreviewRenderUtility uses a non-sRGB target; Linear projects need encoding only after unpremultiplication.
            var dark = black;
            var light = white;
            var alpha = Mathf.Clamp01(1f - Mathf.Max(light.r - dark.r, light.g - dark.g, light.b - dark.b));
            if (alpha < 1f / 255f) return Color.clear;
            var color = new Color(Mathf.Clamp01(dark.r / alpha), Mathf.Clamp01(dark.g / alpha),
                Mathf.Clamp01(dark.b / alpha), alpha);
            if (linear) { color = color.gamma; color.a = alpha; }
            return color;
        }

        private static Texture2D ReadTexture(RenderTexture source)
        {
            var previous = RenderTexture.active;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false)
                { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                RenderTexture.active = source;
                texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                texture.Apply();
                return texture;
            }
            catch { Object.DestroyImmediate(texture); throw; }
            finally { RenderTexture.active = previous; }
        }

        public void Dispose() => utility.Cleanup();
    }
}
