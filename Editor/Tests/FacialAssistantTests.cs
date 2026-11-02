using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace TY.FXFacialAssistant.Tests
{
    public sealed class FacialAssistantTests
    {
        private Scene scene;
        private GameObject root;
        private Mesh mesh;
        private SkinnedMeshRenderer body;
        private readonly List<Object> temporaryObjects = new List<Object>();
        private string folder;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            root = new GameObject("TestAvatar") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(root, scene);
            var go = new GameObject("Body");
            go.transform.SetParent(root.transform, false);
            mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.up, Vector3.right }, triangles = new[] { 0, 1, 2 } };
            mesh.AddBlendShapeFrame("Smile", 100f, new[] { Vector3.up, Vector3.zero, Vector3.zero }, null, null);
            mesh.AddBlendShapeFrame("Blink", 100f, new[] { Vector3.zero, Vector3.right, Vector3.zero }, null, null);
            body = go.AddComponent<SkinnedMeshRenderer>();
            body.sharedMesh = mesh;
            body.SetBlendShapeWeight(0, 12f);
            folder = "Assets/__TY_FXFacialAssistantTests_" + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.ClosePreviewScene(scene);
            Object.DestroyImmediate(mesh);
            foreach (var item in temporaryObjects) if (item) Object.DestroyImmediate(item);
            temporaryObjects.Clear();
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        }

        private FacialMeshSession Capture() => FacialMeshSession.Capture(body, root.transform);

        private T Track<T>(T item) where T : Object { temporaryObjects.Add(item); return item; }

        [Test]
        public void CaptureAndEditDoNotMutateSourceWeights()
        {
            var session = Capture();
            Assert.That(session.Shapes.Select(s => s.Include), Is.All.True);
            Assert.That(session.Shapes[0].DefaultValue, Is.EqualTo(12f));
            session.Shapes[0].Value = 85f;
            Assert.That(body.GetBlendShapeWeight(0), Is.EqualTo(12f));
        }

        [Test]
        public void BlacklistInitializesOnlyMatchingChecksWithoutChangingValues()
        {
            mesh.AddBlendShapeFrame("vrc.Blink", 100f, new[] { Vector3.up, Vector3.zero, Vector3.zero }, null, null);
            body.SetBlendShapeWeight(2, 30f);
            var blacklist = new BlendShapeBlacklist(FacialBlacklistDefaults.Rules);
            var session = FacialMeshSession.Capture(body, root.transform, blacklist);
            Assert.That(session.Shapes[0].Include, Is.True);
            Assert.That(session.Shapes[2].Include, Is.False);
            Assert.That(session.Shapes[2].DefaultInclude, Is.False);
            Assert.That(session.Shapes[2].Value, Is.EqualTo(30f));
            Assert.That(session.HasEdits, Is.False);
            var clip = Track(FacialAnimation.Create(session, root.transform, "Test"));
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b => b.propertyName == "blendShape.vrc.Blink"), Is.False);
            session.Shapes[2].Include = true;
            Assert.That(session.HasEdits, Is.True);
            Assert.That(body.GetBlendShapeWeight(2), Is.EqualTo(30f));
        }

        [Test]
        public void ApplyingBlacklistPreservesOtherChoicesAndAllExpressionValues()
        {
            var session = Capture();
            session.Shapes[0].Value = 90f;
            session.Shapes[1].Include = false;
            Assert.That(session.ApplyBlacklist(new BlendShapeBlacklist("SMILE")), Is.EqualTo(1));
            Assert.That(session.Shapes[0].Include, Is.False);
            Assert.That(session.Shapes[1].Include, Is.False);
            Assert.That(session.Shapes[0].Value, Is.EqualTo(90f));
            Assert.That(session.ApplyBlacklist(new BlendShapeBlacklist("")), Is.Zero);
            Assert.That(session.Shapes[0].Include, Is.False);
        }

        [Test]
        public void BodyDiscoveryIncludesInactiveMeshesAndRejectsMeshesWithoutShapes()
        {
            body.gameObject.SetActive(false);
            Assert.That(FacialMeshSession.FindBody(root), Is.SameAs(body));
            body.sharedMesh = null;
            Assert.That(FacialMeshSession.FindBody(root), Is.Null);
        }

        [Test]
        public void RejectMeshOutsideAvatar()
        {
            var other = new GameObject("Other");
            other.transform.SetParent(root.transform, false);
            Assert.Throws<ArgumentException>(() => FacialMeshSession.Capture(body, other.transform));
        }

        [Test]
        public void RejectAmbiguousSiblingPath()
        {
            var sibling = new GameObject("Body");
            sibling.transform.SetParent(root.transform);
            Assert.Throws<InvalidOperationException>(() => Capture());
        }

        [Test]
        public void RootRendererUsesEmptyAnimationPath()
        {
            body.transform.SetParent(root.transform);
            Assert.That(FacialMeshSession.Capture(body, body.transform).BindingPath(body.transform), Is.Empty);
        }

        [Test]
        public void MeshReplacementInvalidatesSession()
        {
            var session = Capture();
            body.sharedMesh = Track(new Mesh());
            Assert.That(session.IsValid, Is.False);
        }

        [Test]
        public void FixedClipIncludesCheckedZeroAndExcludesUncheckedShape()
        {
            var session = Capture();
            session.Shapes[0].Include = false;
            var clip = Track(FacialAnimation.Create(session, root.transform, "Test"));
            var bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.That(bindings.Length, Is.EqualTo(1));
            Assert.That(bindings[0].path, Is.EqualTo("Body"));
            Assert.That(bindings[0].propertyName, Is.EqualTo("blendShape.Blink"));
            var curve = AnimationUtility.GetEditorCurve(clip, bindings[0]);
            Assert.That(curve.Evaluate(0.1f), Is.Zero);
            Assert.That(clip.frameRate, Is.EqualTo(60f));
        }

        [Test]
        public void TransitionUsesCapturedDefaultAndReachesExpressionAtTenFrames()
        {
            var session = Capture();
            session.Shapes[0].Value = 92f;
            session.Shapes[0].Transition = true;
            var clip = Track(FacialAnimation.Create(session, root.transform, "Test"));
            var binding = AnimationUtility.GetCurveBindings(clip).Single(b => b.propertyName == "blendShape.Smile");
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            Assert.That(curve.Evaluate(0f), Is.EqualTo(12f).Within(0.001));
            Assert.That(curve.Evaluate(5f / 60f), Is.EqualTo(52f).Within(0.001));
            Assert.That(curve.Evaluate(10f / 60f), Is.EqualTo(92f).Within(0.001));
            Assert.That(clip.length, Is.EqualTo(10f / 60f).Within(0.001));
        }

        [Test]
        public void EmptySelectionCannotSaveAnimation()
        {
            var session = Capture();
            foreach (var shape in session.Shapes) shape.Include = false;
            Assert.Throws<InvalidOperationException>(() => FacialAnimation.Create(session, root.transform, "Test"));
        }

        [Test]
        public void ImportReadsFinalFrameAndDoesNotChangeCapturedDefault()
        {
            var session = Capture();
            var clip = Track(new AnimationClip());
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Body", typeof(SkinnedMeshRenderer), "blendShape.Smile"),
                AnimationCurve.Linear(0f, 30f, 1f, 90f));
            FacialAnimation.Load(clip, session, root.transform);
            Assert.That(session.Shapes[0].Value, Is.EqualTo(90f));
            Assert.That(session.Shapes[0].DefaultValue, Is.EqualTo(12f));
            Assert.That(session.Shapes[0].Transition, Is.True);
            Assert.That(session.Shapes[1].Include, Is.False);
            Assert.That(body.GetBlendShapeWeight(0), Is.EqualTo(12f));
        }

        [Test]
        public void UnmatchedImportLeavesEditingStateUnchanged()
        {
            var session = Capture();
            session.Shapes[0].Value = 60f;
            var clip = Track(new AnimationClip());
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Other", typeof(SkinnedMeshRenderer), "blendShape.Smile"),
                AnimationCurve.Constant(0f, 1f, 50f));
            Assert.Throws<InvalidOperationException>(() => FacialAnimation.Load(clip, session, root.transform));
            Assert.That(session.Shapes[0].Value, Is.EqualTo(60f));
            Assert.That(session.Shapes[1].Include, Is.True);
        }

        [TestCase("Happy", "Assets/Expressions/FA_Happy.anim", "Assets/Expressions/FA_Happy.png")]
        [TestCase("FA_笑顔", "Assets/Expressions/FA_笑顔.anim", "Assets/Expressions/FA_笑顔.png")]
        public void OutputNamesUseSameFolderAndPrefixes(string input, string expectedClip, string expectedIcon)
        {
            var paths = new FacialSavePaths("Assets/Expressions", input);
            Assert.That(paths.ClipPath, Is.EqualTo(expectedClip));
            Assert.That(paths.IconPath, Is.EqualTo(expectedIcon));
        }

        [TestCase("../Face")]
        [TestCase("A:B")]
        [TestCase("")]
        [TestCase("FA_")]
        [TestCase("Name.")]
        public void InvalidSaveNamesAreRejected(string name)
            => Assert.Throws<ArgumentException>(() => new FacialSavePaths("Assets/Expressions", name));

        [TestCase("Assets/../Packages")]
        [TestCase("Packages/Expressions")]
        [TestCase("Assets//Expressions")]
        public void SaveFolderCannotEscapeAssets(string path)
            => Assert.Throws<ArgumentException>(() => new FacialSavePaths(path, "Name"));

        [Test]
        public void IconRecoveryHandlesTransparentOpaqueAndPartialCoverage()
        {
            Assert.That(FacialPreview.RecoverTransparency(Color.black, Color.white, false).a, Is.Zero);
            var opaque = FacialPreview.RecoverTransparency(Color.red, Color.red, false);
            Assert.That(opaque.a, Is.EqualTo(1f));
            var partial = FacialPreview.RecoverTransparency(new Color(0.4f, 0.1f, 0.2f), new Color(0.9f, 0.6f, 0.7f), false);
            Assert.That(partial.a, Is.EqualTo(0.5f).Within(0.001));
            Assert.That(partial.r, Is.EqualTo(0.8f).Within(0.001));
        }

        [Test]
        public void LinearIconRecoveryEncodesRgbAfterRecoveringCoverage()
        {
            var partial = FacialPreview.RecoverTransparency(new Color(0.2f, 0.05f, 0.1f), new Color(0.7f, 0.55f, 0.6f), true);
            Assert.That(partial.a, Is.EqualTo(0.5f).Within(0.001));
            Assert.That(partial.r, Is.EqualTo(Mathf.LinearToGammaSpace(0.4f)).Within(0.001));
        }

        [Test]
        public void StateListSupportsNestedMachinesAndPreservesBlendTrees()
        {
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
            var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Nested.controller");
            var machine = controller.layers[0].stateMachine;
            machine.AddState("Root");
            var nested = machine.AddStateMachine("Sub");
            nested.AddState("Child");
            var deep = nested.AddStateMachine("Deep");
            var blendState = deep.AddState("BlendTree");
            var blendTree = new BlendTree { name = "KeepMe" };
            AssetDatabase.AddObjectToAsset(blendTree, controller);
            blendState.motion = blendTree;
            var window = ScriptableObject.CreateInstance<FXFacialAssistantWindow>();
            try
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(FXFacialAssistantWindow).GetField("controller", flags).SetValue(window, controller);
                var parent = new UnityEngine.UIElements.VisualElement();
                typeof(FXFacialAssistantWindow).GetMethod("AddStateMachine", flags).Invoke(window, new object[] { parent, machine });
                var foldouts = UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.Foldout>(parent).ToList();
                Assert.That(foldouts.Select(f => f.text), Is.EquivalentTo(new[] { "Sub", "Deep" }));
                Assert.That(blendState.motion, Is.SameAs(blendTree));
                Assert.Throws<InvalidOperationException>(() => FxControllerAccess.Assign(controller, blendState, null));
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void FailedIconReplacementRestoresExistingClipAndKeepsOriginalPng()
        {
            var session = Capture();
            var original = Track(FacialAnimation.Create(session, root.transform, "FA_Rollback"));
            var icon = Track(new Texture2D(256, 256, TextureFormat.RGBA32, false));
            var paths = new FacialSavePaths(folder, "Rollback");
            FacialAssetWriter.Save(paths, original, icon);
            var bytes = System.IO.File.ReadAllBytes(paths.IconPath);
            session.Shapes[0].Value = 99f;
            var replacement = Track(FacialAnimation.Create(session, root.transform, "FA_Rollback"));
            System.IO.File.SetAttributes(paths.IconPath, System.IO.FileAttributes.ReadOnly);
            try
            {
                Assert.Catch(() => FacialAssetWriter.Save(paths, replacement, icon));
                var restored = AssetDatabase.LoadAssetAtPath<AnimationClip>(paths.ClipPath);
                Assert.That(restored.name, Is.EqualTo("FA_Rollback"));
                var binding = AnimationUtility.GetCurveBindings(restored).Single(b => b.propertyName == "blendShape.Smile");
                Assert.That(AnimationUtility.GetEditorCurve(restored, binding).Evaluate(0f), Is.EqualTo(12f));
                Assert.That(System.IO.File.ReadAllBytes(paths.IconPath), Is.EqualTo(bytes));
            }
            finally { System.IO.File.SetAttributes(paths.IconPath, System.IO.FileAttributes.Normal); }
        }

        [Test]
        public void SavingTwicePreservesGuidsAndWrites256PixelTransparentIcon()
        {
            var clip = Track(FacialAnimation.Create(Capture(), root.transform, "FA_Test"));
            var icon = Track(new Texture2D(256, 256, TextureFormat.RGBA32, false));
            icon.SetPixels(Enumerable.Repeat(Color.clear, 256 * 256).ToArray());
            icon.Apply();
            var paths = new FacialSavePaths(folder, "Test");
            FacialAssetWriter.Save(paths, clip, icon);
            var clipGuid = AssetDatabase.AssetPathToGUID(paths.ClipPath);
            var iconGuid = AssetDatabase.AssetPathToGUID(paths.IconPath);
            FacialAssetWriter.Save(paths, clip, icon);
            Assert.That(AssetDatabase.AssetPathToGUID(paths.ClipPath), Is.EqualTo(clipGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(paths.IconPath), Is.EqualTo(iconGuid));
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(paths.IconPath);
            Assert.That(texture.width, Is.EqualTo(256));
            Assert.That(texture.height, Is.EqualTo(256));
            var importer = (TextureImporter)AssetImporter.GetAtPath(paths.IconPath);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            var defaults = importer.GetDefaultPlatformTextureSettings();
            Assert.That(defaults.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ));
            Assert.That(defaults.maxTextureSize, Is.EqualTo(256));
            Assert.That(defaults.format, Is.EqualTo(TextureImporterFormat.Automatic));
            Assert.That(defaults.crunchedCompression, Is.False);
        }

        [Test]
        public void IconSettingsRemoveIncompatiblePlatformOverrides()
        {
            var paths = new FacialSavePaths(folder, "PlatformSettings");
            var icon = Track(new Texture2D(256, 256, TextureFormat.RGBA32, false));
            FacialAssetWriter.SaveIcon(paths, icon);
            var importer = (TextureImporter)AssetImporter.GetAtPath(paths.IconPath);
            foreach (var platform in new[] { "Standalone", "Android", "iPhone" })
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = platform, overridden = true, maxTextureSize = 4096,
                    format = TextureImporterFormat.RGBA32, textureCompression = TextureImporterCompression.Uncompressed
                });
            ExpressionMenuIconImportSettings.Apply(importer);
            foreach (var platform in new[] { "Standalone", "Android", "iPhone" })
                Assert.That(importer.GetPlatformTextureSettings(platform).overridden, Is.False, platform);
        }


        [Test]
        public void AnimationOnlySaveDoesNotCreateOrOverwriteIcon()
        {
            var paths = new FacialSavePaths(folder, "Independent");
            var clip = Track(FacialAnimation.Create(Capture(), root.transform, paths.ClipName));
            FacialAssetWriter.SaveAnimation(paths, clip);
            Assert.That(System.IO.File.Exists(paths.ClipPath), Is.True);
            Assert.That(System.IO.File.Exists(paths.IconPath), Is.False);
            var icon = Track(new Texture2D(256, 256, TextureFormat.RGBA32, false));
            FacialAssetWriter.SaveIcon(paths, icon);
            var bytes = System.IO.File.ReadAllBytes(paths.IconPath);
            FacialAssetWriter.SaveAnimation(paths, clip);
            Assert.That(System.IO.File.ReadAllBytes(paths.IconPath), Is.EqualTo(bytes));
        }

        [Test]
        public void IconOnlySaveDoesNotCreateOrOverwriteAnimation()
        {
            var paths = new FacialSavePaths(folder, "Independent");
            var icon = Track(new Texture2D(256, 256, TextureFormat.RGBA32, false));
            FacialAssetWriter.SaveIcon(paths, icon);
            Assert.That(System.IO.File.Exists(paths.IconPath), Is.True);
            Assert.That(System.IO.File.Exists(paths.ClipPath), Is.False);
            var clip = Track(FacialAnimation.Create(Capture(), root.transform, paths.ClipName));
            FacialAssetWriter.SaveAnimation(paths, clip);
            var bytes = System.IO.File.ReadAllBytes(paths.ClipPath);
            var guid = AssetDatabase.AssetPathToGUID(paths.ClipPath);
            FacialAssetWriter.SaveIcon(paths, icon);
            Assert.That(System.IO.File.ReadAllBytes(paths.ClipPath), Is.EqualTo(bytes));
            Assert.That(AssetDatabase.AssetPathToGUID(paths.ClipPath), Is.EqualTo(guid));
        }

        [Test]
        public void FxAssignmentSupportsUndoAndLeavesTransitionsUntouched()
        {
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
            var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Test.controller");
            controller.layers = new[] { new AnimatorControllerLayer { name = "Left Hand", stateMachine = controller.layers[0].stateMachine } };
            var state = controller.layers[0].stateMachine.AddState("Smile");
            var child = controller.layers[0].stateMachine.AddStateMachine("Nested").AddState("Other");
            var transition = state.AddTransition(child);
            transition.hasExitTime = true;
            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, folder + "/Expression.anim");
            Undo.IncrementCurrentGroup();
            FxControllerAccess.Assign(controller, state, clip);
            Undo.FlushUndoRecordObjects();
            Assert.That(state.motion, Is.SameAs(clip));
            Assert.That(state.transitions.Single(), Is.SameAs(transition));
            Undo.PerformUndo();
            Assert.That(state.motion, Is.Null);
            Assert.That(state.transitions.Single().hasExitTime, Is.True);
            Undo.ClearUndo(state);
            Assert.That(FxControllerAccess.FindLayer(controller, "Missing"), Is.Null);
        }

        [Test]
        public void PreviewCopiesRenderingOnlyAndKeepsSourceUntouched()
        {
            var avatar = root.AddComponent<VRCAvatarDescriptor>();
            avatar.ViewPosition = new Vector3(0f, 1.4f, 0f);
            var before = body.GetBlendShapeWeight(0);
            var session = Capture();
            session.Shapes[0].Value = 80f;
            using (var preview = new FacialPreview(avatar)) preview.Apply(session);
            Assert.That(body.GetBlendShapeWeight(0), Is.EqualTo(before));
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(2));
        }
    }
}
