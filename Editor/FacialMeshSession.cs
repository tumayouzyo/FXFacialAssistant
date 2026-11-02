using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TY.FXFacialAssistant
{
    [Serializable]
    internal sealed class BlendShapeValue
    {
        public string Name;
        public float DefaultValue;
        public float CapturedValue;
        public bool HasCapturedValue;
        public float Value;
        public bool Include = true;
        public bool DefaultInclude = true;
        public bool Transition;
    }

    [Serializable]
    internal sealed class FacialMeshSession
    {
        [SerializeField] private SkinnedMeshRenderer renderer;
        [SerializeField] private Mesh mesh;
        [SerializeField] private List<BlendShapeValue> shapes = new List<BlendShapeValue>();

        public SkinnedMeshRenderer Renderer => renderer;
        public IReadOnlyList<BlendShapeValue> Shapes => shapes;
        public bool IsValid => renderer && mesh && renderer.sharedMesh == mesh
            && shapes.Count == mesh.blendShapeCount;

        public static SkinnedMeshRenderer FindBody(GameObject avatar)
        {
            return avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(r => r.name == "Body" && r.sharedMesh && r.sharedMesh.blendShapeCount > 0);
        }

        public static FacialMeshSession Capture(SkinnedMeshRenderer source, Transform avatarRoot, BlendShapeBlacklist blacklist = null)
        {
            if (!source || !source.sharedMesh || source.sharedMesh.blendShapeCount == 0)
                throw FacialErrors.Argument(FacialProblem.Mesh, "BlendShapeを持つSkinnedMeshRendererを指定してください。");
            ValidateBindingPath(source.transform, avatarRoot);
            var session = new FacialMeshSession { renderer = source, mesh = source.sharedMesh };
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < source.sharedMesh.blendShapeCount; i++)
            {
                var name = source.sharedMesh.GetBlendShapeName(i);
                if (!names.Add(name))
                    throw new InvalidOperationException(FacialLocalization.Format("同名のBlendShapeがあるため記録できません: {0}", name));
                var value = source.GetBlendShapeWeight(i);
                var include = blacklist == null || !blacklist.Matches(name);
                session.shapes.Add(new BlendShapeValue { Name = name, DefaultValue = value, Value = value,
                    CapturedValue = value, HasCapturedValue = true, Include = include, DefaultInclude = include });
            }
            return session;
        }

        public bool HasEdits => shapes.Any(s => s.Value != s.DefaultValue || s.Include != s.DefaultInclude || s.Transition);

        public int ApplyBlacklist(BlendShapeBlacklist blacklist)
        {
            var disabled = 0;
            foreach (var shape in shapes.Where(s => blacklist.Matches(s.Name)))
            {
                if (shape.Include) disabled++;
                shape.Include = false;
                shape.DefaultInclude = false;
            }
            return disabled;
        }

        public void ApplyTo(SkinnedMeshRenderer previewRenderer)
        {
            if (!IsValid || !previewRenderer || previewRenderer.sharedMesh != mesh)
                throw FacialErrors.Operation(FacialProblem.Preview, "メッシュが変更されました。アバターを再読み込みしてください。");
            for (var i = 0; i < shapes.Count; i++)
                previewRenderer.SetBlendShapeWeight(i, shapes[i].Value);
        }

        public string BindingPath(Transform avatarRoot)
        {
            if (!IsValid)
                throw FacialErrors.Operation(FacialProblem.Mesh, "有効なメッシュを選択してください。");
            ValidateBindingPath(renderer.transform, avatarRoot);
            return AnimationUtility.CalculateTransformPath(renderer.transform, avatarRoot);
        }

        internal static void ValidateBindingPath(Transform target, Transform root)
        {
            if (!target || !root || (target != root && !target.IsChildOf(root)))
                throw new ArgumentException("指定したアバター配下のメッシュを選択してください。");
            for (var node = target; node != root; node = node.parent)
            {
                if (node.name.Contains("/"))
                    throw new InvalidOperationException("オブジェクト名に / があるためAnimationのパスを作れません。");
                var sameName = 0;
                foreach (Transform sibling in node.parent)
                    if (sibling.name == node.name) sameName++;
                if (sameName > 1)
                    throw new InvalidOperationException(FacialLocalization.Format("同じ階層に同名オブジェクトがあるためAnimationの対象を特定できません: {0}", node.name));
            }
            if (target.GetComponents<SkinnedMeshRenderer>().Length != 1)
                throw new InvalidOperationException("同じオブジェクトに複数のSkinnedMeshRendererがあるため記録できません。");
        }
    }
}
