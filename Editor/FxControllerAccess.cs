using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace TY.FXFacialAssistant
{
    internal static class FxControllerAccess
    {
        public static void ValidateCurrent(VRCAvatarDescriptor avatar, AnimatorController expected)
        {
            var current = Resolve(avatar, out var message);
            if (!current || current != expected)
                throw FacialErrors.Operation(FacialProblem.Fx, FacialLocalization.Format("FX設定が変わりました。アバターを再読み込みしてください。 {0}", FacialLocalization.Text(message)));
        }
        public static AnimatorController Resolve(VRCAvatarDescriptor avatar, out string message)
        {
            message = "";
            if (!avatar) { message = "Hierarchyからアバターを指定してください。"; return null; }
            if (!avatar.customizeAnimationLayers || avatar.baseAnimationLayers == null)
            { message = "Avatar DescriptorのPlayable Layersがカスタマイズされていません。"; return null; }
            var fx = avatar.baseAnimationLayers.FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            if (fx.isDefault || !fx.animatorController)
            { message = "Avatar DescriptorにカスタムFX Controllerが設定されていません。"; return null; }
            var controller = fx.animatorController as AnimatorController;
            if (!controller)
            { message = "AnimatorOverrideControllerには未対応です。通常のAnimatorControllerを指定してください。"; return null; }
            var path = AssetDatabase.GetAssetPath(controller);
            if (string.IsNullOrEmpty(path))
            { message = "FX Controllerがアセットとして保存されていません。"; return null; }
            return controller;
        }

        public static string DefaultFolder(AnimatorController controller)
        {
            if (!controller) return "";
            return Path.GetDirectoryName(AssetDatabase.GetAssetPath(controller)).Replace('\\', '/') + "/Facial";
        }

        public static AnimatorControllerLayer FindLayer(AnimatorController controller, string name)
        {
            return controller ? controller.layers.FirstOrDefault(l => l.name == name) : null;
        }

        public static void Assign(AnimatorController controller, AnimatorState state, AnimationClip clip)
        {
            if (!controller || !state) throw new ArgumentException("FX Controllerとステートを再読み込みしてください。");
            var path = AssetDatabase.GetAssetPath(controller);
            if (AssetDatabase.GetAssetPath(state) != path)
                throw new InvalidOperationException("ステートが指定したFX Controllerに属していません。");
            if (!(path.StartsWith("Assets/", StringComparison.Ordinal) && AssetDatabase.IsOpenForEdit(controller)))
                throw new InvalidOperationException("FX Controllerが編集可能なAssetsフォルダ内にありません。");
            if (state.motion && !(state.motion is AnimationClip))
                throw new InvalidOperationException("BlendTreeなどのMotionは、このツールでは置き換えません。");
            if (state.motion == clip) return;
            Undo.RecordObject(state, "Assign facial animation");
            state.motion = clip;
            EditorUtility.SetDirty(state);
            AssetDatabase.SaveAssetIfDirty(state);
        }
    }
}
