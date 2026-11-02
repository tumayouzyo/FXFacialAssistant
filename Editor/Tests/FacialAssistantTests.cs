using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
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
        public void CleanupPreservesQuotedNamesCommentsAndBlockScalars()
        {
            var yaml = "%YAML 1.1\r\n--- !u!1102 &100\r\nAnimatorState:\r\n"
                + "  m_Name: 'Face {fileID: 999}'\r\n"
                + "  description: \"Face\n    fake: {fileID: 999}\n  end\"\r\n"
                + "  literal: |-\r\n    m_Motion: {fileID: 999}\r\n"
                + "  # note: {fileID: 999}\r\n"
                + "  m_Motion: {fileID: 999}\r\n"
                + "  refs:\r\n  - {fileID: 888}\r\n"
                + "  external: {fileID: 999, guid: abc, type: 2}\r\n";
            var expected = yaml.Replace("  m_Motion: {fileID: 999}\r\n  refs:", "  m_Motion: {fileID: 0}\r\n  refs:")
                .Replace("  - {fileID: 888}", "  - {fileID: 0}");
            Assert.That(FxControllerMaintenance.RemoveMissingLocalReferences(yaml, out var count), Is.EqualTo(expected));
            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void TranslationPreservesPathsAndFormatsOnlyTemplate()
        {
            const string path = "Assets/保存/FA_設定.anim";
            Assert.That(FacialLocalization.TranslateText(path, true), Is.EqualTo(path));
            var template = FacialLocalization.TranslateText("{0}\n\n既存のファイルを上書きしますか？", true);
            Assert.That(string.Format(template, path), Is.EqualTo(path + "\n\nOverwrite the existing file?"));
        }

        [Test]
        public void RecycledShapeRowEditsOnlyItsCurrentBinding()
        {
            var first = new BlendShapeValue { Name = "First", Value = 10 };
            var second = new BlendShapeValue { Name = "Second", Value = 20 };
            var previewChanges = 0;
            var row = new FacialShapeRow(() => previewChanges++, () => { });
            var host = Track(UnityEngine.ScriptableObject.CreateInstance<FXFacialAssistantWindow>());
            host.ShowUtility();
            host.rootVisualElement.Add(row);
            row.Bind(first, false);
            row.Q<Slider>().value = 40;
            row.Bind(second, false);
            row.Q<Slider>().value = 70;
            Assert.That(first.Value, Is.EqualTo(40));
            Assert.That(second.Value, Is.EqualTo(70));
            row.Unbind();
            row.Q<Slider>().value = 90;
            Assert.That(second.Value, Is.EqualTo(70));
            Assert.That(previewChanges, Is.EqualTo(2));
        }

        [Test]
        public void RecreatingUiAndChangingPrefixPreservePreviewInstance()
        {
            var previous = FacialToolPreferences.Prefix;
            var window = Track(UnityEngine.ScriptableObject.CreateInstance<FXFacialAssistantWindow>());
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var type = typeof(FXFacialAssistantWindow);
            type.GetField("avatar", flags).SetValue(window, root.AddComponent<VRCAvatarDescriptor>());
            type.GetField("session", flags).SetValue(window, Capture());
            try
            {
                window.CreateGUI();
                var preview = type.GetField("preview", flags).GetValue(window);
                window.CreateGUI();
                Assert.That(type.GetField("preview", flags).GetValue(window), Is.SameAs(preview));
                Assert.That(window.rootVisualElement.Q<ListView>("shapeList").itemsSource.Count, Is.EqualTo(2));
                FacialToolPreferences.Prefix = !previous;
                Assert.That(type.GetField("preview", flags).GetValue(window), Is.SameAs(preview));
            }
            finally { FacialToolPreferences.Prefix = previous; }
        }

        [Test]
        public void CleanupRejectsStaleFxBeforeOpeningConfirmation()
        {
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
            var old = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Old.controller");
            var current = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Current.controller");
            var avatar = root.AddComponent<VRCAvatarDescriptor>();
            avatar.customizeAnimationLayers = true;
            avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer
                { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = current, isDefault = false } };
            Assert.Throws<InvalidOperationException>(() => FxControllerAccess.ValidateCurrent(avatar, old));
            Assert.DoesNotThrow(() => FxControllerAccess.ValidateCurrent(avatar, current));
            var window = Track(UnityEngine.ScriptableObject.CreateInstance<FXFacialAssistantWindow>());
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(FXFacialAssistantWindow).GetField("avatar", flags).SetValue(window, avatar);
            typeof(FXFacialAssistantWindow).GetField("controller", flags).SetValue(window, old);
            var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                typeof(FXFacialAssistantWindow).GetMethod("CleanupController", flags).Invoke(window, null));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void RenderFailureStopsUntilPreviewIsRebuilt()
        {
            var window = Track(UnityEngine.ScriptableObject.CreateInstance<FXFacialAssistantWindow>());
            var type = typeof(FXFacialAssistantWindow);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            type.GetField("avatar", flags).SetValue(window, root.AddComponent<VRCAvatarDescriptor>());
            type.GetField("session", flags).SetValue(window, Capture());
            window.ShowUtility();
            window.CreateGUI();
            try
            {
                body.sharedMesh = null;
                type.GetField("previewDirty", flags).SetValue(window, true);
                type.GetField("nextRenderTime", flags).SetValue(window, 0d);
                type.GetMethod("RenderPreview", flags).Invoke(window, null);
                Assert.That(type.GetField("previewFaulted", flags).GetValue(window), Is.True);
                var area = window.rootVisualElement.Q("statusArea");
                var firstMessage = area[0];
                for (var i = 0; i < 5; i++) type.GetMethod("RenderPreview", flags).Invoke(window, null);
                Assert.That(area[0], Is.SameAs(firstMessage));
                body.sharedMesh = mesh;
                type.GetMethod("RebuildPreview", flags).Invoke(window, null);
                Assert.That(type.GetField("previewFaulted", flags).GetValue(window), Is.False);
            }
            finally { body.sharedMesh = mesh; }
        }

        [Test]
        public void StatusErrorClearsOnlyAfterItsConditionIsResolvedAndInfoPersists()
        {
            var window = Track(UnityEngine.ScriptableObject.CreateInstance<FXFacialAssistantWindow>());
            window.CreateGUI();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var type = typeof(FXFacialAssistantWindow);
            type.GetField("saveFolder", flags).SetValue(window, "Assets/Expressions");
            type.GetField("animationName", flags).SetValue(window, "");
            var run = type.GetMethod("Try", flags);
            Action fail = () => throw FacialErrors.Argument(FacialProblem.SaveName, "文面を変更しても回復判定には影響しません。");
            run.Invoke(window, new object[] { fail });
            var area = window.rootVisualElement.Q("statusArea");
            Assert.That(area.childCount, Is.EqualTo(1));
            run.Invoke(window, new object[] { (Action)(() => { }) });
            Assert.That(area.childCount, Is.EqualTo(1), "Unrelated successful actions must preserve the error.");
            type.GetMethod("RenderPreview", flags).Invoke(window, null);
            Assert.That(area.childCount, Is.EqualTo(1));
            type.GetField("animationName", flags).SetValue(window, "Smile");
            type.GetMethod("RenderPreview", flags).Invoke(window, null);
            Assert.That(area.childCount, Is.Zero);
            type.GetMethod("ShowStatus", flags).Invoke(window, new object[] { "Saved", HelpBoxMessageType.Info });
            type.GetMethod("RenderPreview", flags).Invoke(window, null);
            Assert.That(area.childCount, Is.EqualTo(1));
        }

        [Test]
        public void FillEmptyIncludesNestedStatesAndPreservesExistingClipsWithUndo()
        {
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
            var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Empty.controller");
            controller.AddLayer("Left Hand");
            var machine = FxControllerAccess.FindLayer(controller, "Left Hand").stateMachine;
            var empty = machine.AddState("Empty");
            var nested = machine.AddStateMachine("Nested").AddState("NestedEmpty");
            var existing = machine.AddState("Existing");
            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, folder + "/Keep.anim");
            existing.motion = clip;
            Assert.That(FxControllerMaintenance.FillEmpty(controller, "Left Hand"), Is.EqualTo(2));
            Assert.That(empty.motion.name, Is.EqualTo("proxy_empty"));
            Assert.That(nested.motion, Is.EqualTo(empty.motion));
            Assert.That(existing.motion, Is.EqualTo(clip));
            Undo.PerformUndo();
            Assert.That(empty.motion, Is.Null);
            Assert.That(nested.motion, Is.Null);
            Assert.That(existing.motion, Is.EqualTo(clip));
        }

        [Test]
        public void EnglishUiContainsNoUntranslatedJapaneseAndDoesNotTranslateFileNames()
        {
            var previous = FacialToolPreferences.English;
            FXFacialAssistantWindow window = null;
            FacialBlacklistSettingsWindow settings = null;
            try
            {
                FacialToolPreferences.English = true;
                window = UnityEngine.ScriptableObject.CreateInstance<FXFacialAssistantWindow>();
                AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
                var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Tooltips.controller");
                controller.AddLayer("Left Hand");
                controller.AddLayer("Right Hand");
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.customizeAnimationLayers = true;
                avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer
                    { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = controller, isDefault = false } };
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(FXFacialAssistantWindow).GetField("avatar", flags).SetValue(window, avatar);
                typeof(FXFacialAssistantWindow).GetField("session", flags).SetValue(window, Capture());
                window.CreateGUI();
                settings = UnityEngine.ScriptableObject.CreateInstance<FacialBlacklistSettingsWindow>();
                settings.CreateGUI();
                var field = window.rootVisualElement.Q<UnityEngine.UIElements.TextField>("animationName");
                Assert.That(field.value, Is.Empty, "Translating a label must not change the input value.");
                field.SetValueWithoutNotify("設定_表情");
                FacialLocalization.Translate(window.rootVisualElement);
                Assert.That(field.value, Is.EqualTo("設定_表情"));
                var buttons = window.rootVisualElement.Query<Button>().ToList();
                var emptyButtons = buttons.Where(button => button.text == "Fill empty").ToArray();
                Assert.That(emptyButtons.Length, Is.EqualTo(2));
                Assert.That(emptyButtons.All(button => button.tooltip.Contains("proxy_empty") && button.tooltip.Contains("Ctrl+Z")), Is.True);
                Assert.That(buttons.Single(button => button.text == "Clean up").tooltip, Does.Contain("Broken text PPtr"));
                Assert.That(window.rootVisualElement.Q<Button>("highlightFolderButton").tooltip, Does.Contain("does not create"));
                typeof(FXFacialAssistantWindow).GetField("customFolder", flags).SetValue(window, true);
                typeof(FXFacialAssistantWindow).GetField("saveFolder", flags).SetValue(window, folder + "/Missing");
                typeof(FXFacialAssistantWindow).GetMethod("HighlightSaveFolder", flags).Invoke(window, null);
                Assert.That(AssetDatabase.IsValidFolder(folder + "/Missing"), Is.False);
                foreach (var tree in new[] { window.rootVisualElement, settings.rootVisualElement })
                {
                foreach (var element in tree.Query<VisualElement>().ToList())
                    Assert.That(System.Text.RegularExpressions.Regex.IsMatch(element.tooltip ?? "", "[ぁ-龯]"), Is.False, element.tooltip);
                foreach (var text in tree.Query<UnityEngine.UIElements.TextElement>().ToList())
                {
                    if (text.GetFirstAncestorOfType<UnityEngine.UIElements.TextField>() != null || text.text == "日本語") continue;
                    Assert.That(System.Text.RegularExpressions.Regex.IsMatch(text.text ?? "", "[ぁ-龯]"), Is.False, text.text);
                }
                }
            }
            finally
            {
                if (window) UnityEngine.Object.DestroyImmediate(window);
                if (settings) UnityEngine.Object.DestroyImmediate(settings);
                FacialToolPreferences.English = previous;
            }
        }

        [Test]
        public void DefaultClipDefinesTransitionStartAndMissingShapesAreZero()
        {
            var session = Capture();
            var clip = Track(new AnimationClip());
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Body", typeof(SkinnedMeshRenderer), "blendShape.Blink"),
                AnimationCurve.Constant(0f, 1f, 40f));
            session.Shapes[0].Value = 80f;
            FacialAnimation.SetDefault(clip, session, root.transform, false);
            Assert.That(session.Shapes[0].Value, Is.EqualTo(80f));
            Assert.That(session.Shapes[0].DefaultValue, Is.Zero);
            Assert.That(session.Shapes[1].DefaultValue, Is.EqualTo(40f));
            session.Shapes[0].Transition = true;
            var generated = Track(FacialAnimation.Create(session, root.transform, "Test"));
            var curve = AnimationUtility.GetEditorCurve(generated, AnimationUtility.GetCurveBindings(generated)[0]);
            Assert.That(curve.Evaluate(0), Is.Zero);
            FacialAnimation.SetDefault(clip, session, root.transform, true);
            Assert.That(session.Shapes[1].Value, Is.EqualTo(40f));
            FacialAnimation.SetDefault(null, session, root.transform, true);
            Assert.That(session.Shapes[0].Value, Is.EqualTo(12f));
        }

        [Test]
        public void CleanupOnlyClearsDanglingLocalReferences()
        {
            var yaml = "%YAML 1.1\n--- !u!91 &100\nlocal: {fileID: 200}\nmissing: {fileID: 999}\nexternal: {fileID: 888, guid: abc, type: 2}\n--- !u!1102 &200\nnull: {fileID: 0}\n";
            var result = FxControllerMaintenance.RemoveMissingLocalReferences(yaml, out var count);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(result, Does.Contain("local: {fileID: 200}"));
            Assert.That(result, Does.Contain("missing: {fileID: 0}"));
            Assert.That(result, Does.Contain("{fileID: 888, guid: abc, type: 2}"));
            Assert.That(FxControllerMaintenance.RemoveMissingLocalReferences(result, out count), Is.EqualTo(result));
            Assert.That(count, Is.Zero);
        }

        [Test]
        public void PrefixCanBeDisabledWithoutStrippingUserName()
        {
            var paths = new FacialSavePaths("Assets/Expressions", "Smile", false);
            Assert.That(paths.ClipPath, Is.EqualTo("Assets/Expressions/Smile.anim"));
            Assert.That(new FacialSavePaths("Assets/Expressions", "FA_Smile", false).ClipName, Is.EqualTo("FA_Smile"));
        }

        [Test]
        public void RenderUpdatesBlendShapesWithoutWaitingForAnotherEditorFrame()
        {
            var avatar = root.AddComponent<VRCAvatarDescriptor>();
            avatar.ViewPosition = new Vector3(0.4f, 0.4f, 0f);
            body.sharedMaterial = Track(new Material(Shader.Find("Unlit/Color")));
            body.sharedMaterial.color = Color.red;
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 0 };
            var session = Capture();
            var settings = new FacialCameraSettings { Distance = 2f, FocalLength = 20f, Height = 0f };
            using (var preview = new FacialPreview(avatar))
            {
                Color32[] Read()
                {
                    var texture = preview.Render(settings, 128) as RenderTexture;
                    Assert.That(texture, Is.Not.Null);
                    var old = RenderTexture.active;
                    var readback = Track(new Texture2D(128, 128));
                    try
                    {
                        RenderTexture.active = texture;
                        readback.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                        readback.Apply();
                        return readback.GetPixels32();
                    }
                    finally { RenderTexture.active = old; }
                }
                session.Shapes[0].Value = 0;
                preview.Apply(session);
                var initial = Read();
                Assert.That(initial.Any(c => c.r > 100 && c.g < 50), Is.True, "Test mesh must be visible.");
                session.Shapes[0].Value = 100;
                preview.Apply(session);
                var changed = Read();
                Assert.That(changed.SequenceEqual(initial), Is.False);
                session.Shapes[0].Value = 0;
                preview.Apply(session);
                Assert.That(Read().SequenceEqual(initial), Is.True);
                settings.Yaw = 30;
                Assert.That(Read().SequenceEqual(initial), Is.False);
                Assert.That(body.GetBlendShapeWeight(0), Is.EqualTo(12));
            }
        }

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
            Assert.That(session.Shapes[1].Value, Is.Zero);
            Assert.That(body.GetBlendShapeWeight(0), Is.EqualTo(12f));
        }

        [Test]
        public void UnmatchedImportOverwritesAllShapesWithZero()
        {
            var session = Capture();
            session.Shapes[0].Value = 60f;
            var clip = Track(new AnimationClip());
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Other", typeof(SkinnedMeshRenderer), "blendShape.Smile"),
                AnimationCurve.Constant(0f, 1f, 50f));
            FacialAnimation.Load(clip, session, root.transform);
            Assert.That(session.Shapes.All(s => s.Value == 0f && !s.Include && !s.Transition), Is.True);
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
