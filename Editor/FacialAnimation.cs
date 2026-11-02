using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TY.FXFacialAssistant
{
    internal static class FacialAnimation
    {
        public const float FrameRate = 60f;
        public const float TransitionEnd = 10f / FrameRate;

        public static AnimationClip Create(FacialMeshSession session, Transform avatarRoot, string name)
        {
            var path = session.BindingPath(avatarRoot);
            if (!session.Shapes.Any(s => s.Include))
                throw FacialErrors.Operation(FacialProblem.RecordingSelection, "記録するBlendShapeを1つ以上チェックしてください。");
            var clip = new AnimationClip { name = name, frameRate = FrameRate };
            try
            {
                foreach (var shape in session.Shapes.Where(s => s.Include))
                {
                    var curve = shape.Transition
                        ? AnimationCurve.Linear(0f, shape.DefaultValue, TransitionEnd, shape.Value)
                        : AnimationCurve.Constant(0f, 1f / FrameRate, shape.Value);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(
                        path, typeof(SkinnedMeshRenderer), "blendShape." + shape.Name), curve);
                }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                return clip;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(clip);
                throw;
            }
        }

        public static string Load(AnimationClip clip, FacialMeshSession session, Transform avatarRoot)
        {
            if (!clip) throw FacialErrors.Argument(FacialProblem.LoadClip, "読み込むAnimationClipを指定してください。");
            var path = session.BindingPath(avatarRoot);
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var imported = bindings.Where(b => b.path == path && b.type == typeof(SkinnedMeshRenderer)
                    && b.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                .Select(b => new { Shape = session.Shapes.FirstOrDefault(s => "blendShape." + s.Name == b.propertyName),
                    Curve = AnimationUtility.GetEditorCurve(clip, b) })
                .Where(item => item.Shape != null && item.Curve != null && item.Curve.length > 0).ToList();
            foreach (var shape in session.Shapes)
            {
                shape.Include = false;
                shape.Value = 0f;
                shape.Transition = false;
            }
            foreach (var item in imported)
            {
                item.Shape.Include = true;
                item.Shape.Value = item.Curve.Evaluate(clip.length);
                item.Shape.Transition = item.Curve.keys.Any(k => !Mathf.Approximately(k.value, item.Shape.Value));
            }
            var ignored = bindings.Length - imported.Count + AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
            return FacialLocalization.Format("{0}項目を最終フレームから読み込みました。", imported.Count)
                + (ignored > 0 ? FacialLocalization.Format(" 対象外のカーブ {0} 件は読み込んでいません。", ignored) : "")
                + FacialLocalization.Text(" 再保存時は固定値／直線補間になります。");
        }

        public static void SetDefault(AnimationClip clip, FacialMeshSession session, Transform root, bool apply)
        {
            var path = session.BindingPath(root);
            foreach (var shape in session.Shapes)
            {
                if (!shape.HasCapturedValue) { shape.CapturedValue = shape.DefaultValue; shape.HasCapturedValue = true; }
                var curve = clip ? AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(
                    path, typeof(SkinnedMeshRenderer), "blendShape." + shape.Name)) : null;
                shape.DefaultValue = curve != null && curve.length > 0 ? curve.Evaluate(clip.length)
                    : clip ? 0f : shape.CapturedValue;
                if (apply) shape.Value = shape.DefaultValue;
            }
        }
    }
}
