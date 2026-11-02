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
                throw new InvalidOperationException("記録するBlendShapeを1つ以上チェックしてください。");
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
            if (!clip) throw new ArgumentException("読み込むAnimationClipを指定してください。");
            var path = session.BindingPath(avatarRoot);
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var imported = bindings.Where(b => b.path == path && b.type == typeof(SkinnedMeshRenderer)
                    && b.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                .Select(b => new { Shape = session.Shapes.FirstOrDefault(s => "blendShape." + s.Name == b.propertyName),
                    Curve = AnimationUtility.GetEditorCurve(clip, b) })
                .Where(item => item.Shape != null && item.Curve != null && item.Curve.length > 0).ToList();
            if (imported.Count == 0)
                throw new InvalidOperationException("選択中メッシュに一致するBlendShapeカーブがありません。アバターを基準とするパスを確認してください。");
            foreach (var shape in session.Shapes) shape.Include = false;
            foreach (var item in imported)
            {
                item.Shape.Include = true;
                item.Shape.Value = item.Curve.Evaluate(clip.length);
                item.Shape.Transition = item.Curve.keys.Any(k => !Mathf.Approximately(k.value, item.Shape.Value));
            }
            var ignored = bindings.Length - imported.Count + AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
            return imported.Count + "項目を最終フレームから読み込みました。"
                + (ignored > 0 ? " 対象外のカーブ " + ignored + " 件は読み込んでいません。" : "")
                + " 再保存時は固定値／直線補間になります。";
        }
    }
}
