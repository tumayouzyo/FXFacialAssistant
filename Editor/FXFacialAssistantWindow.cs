using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace TY.FXFacialAssistant
{
    public sealed class FXFacialAssistantWindow : EditorWindow
    {
        private const string CameraPreference = "TY.FXFacialAssistant.Camera.v1";
        private const float HeightSliderScale = 1000f;
        [SerializeField] private VRCAvatarDescriptor avatar;
        [SerializeField] private FacialMeshSession session;
        [SerializeField] private FacialCameraSettings cameraSettings = new FacialCameraSettings();
        [SerializeField] private string animationName = "";
        [SerializeField] private string saveFolder = "";
        [SerializeField] private bool customFolder;
        [SerializeField] private AnimationClip loadClip;
        [SerializeField] private string blacklistSignature;
        private FacialBlacklistSettings blacklistSettings;
        private BlendShapeBlacklist blacklist;
        private FacialPreview preview;
        private AnimatorController controller;
        private bool previewDirty;
        private Image previewImage;
        private ObjectField avatarField;
        private ObjectField meshField;
        private Toggle allToggle;
        private Toggle hideBlacklistToggle;
        private ScrollView shapeList;
        private Label meshSummary;
        private TextField folderField;
        private Label outputSummary;
        private Button saveButton;
        private Button saveIconButton;
        private VisualElement statusArea;
        private IVisualElementScheduledItem renderSchedule;

        [MenuItem("Tools/FXFacialAssistant")]
        public static void Open() => GetWindow<FXFacialAssistantWindow>("FXFacialAssistant");

        private void OnEnable()
        {
            minSize = new Vector2(980f, 760f);
            if (EditorPrefs.HasKey(CameraPreference))
            {
                try { JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(CameraPreference), cameraSettings); }
                catch (ArgumentException) { cameraSettings = new FacialCameraSettings(); }
            }
            cameraSettings.Distance = Mathf.Clamp(cameraSettings.Distance, 0.15f, 2f);
            cameraSettings.Height = Mathf.Clamp(cameraSettings.Height, -0.4f, 0.5f);
            cameraSettings.FocalLength = Mathf.Clamp(cameraSettings.FocalLength, 20f, 200f);
            ReloadBlacklist();
            if (blacklistSignature != blacklistSettings.Signature)
            {
                session?.ApplyBlacklist(blacklist);
                blacklistSignature = blacklistSettings.Signature;
            }
            FacialBlacklistPreferences.Changed += OnBlacklistChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(CameraPreference, JsonUtility.ToJson(cameraSettings));
            FacialBlacklistPreferences.Changed -= OnBlacklistChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            renderSchedule?.Pause();
            DisposePreview();
        }

        public void CreateGUI()
        {
            renderSchedule?.Pause();
            rootVisualElement.Clear();
            var scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            var templatePath = Path.GetDirectoryName(scriptPath).Replace('\\', '/') + "/FXFacialAssistantWindow.uxml";
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(templatePath);
            if (!template)
            {
                rootVisualElement.Add(new HelpBox("UIファイルが見つかりません: " + templatePath, HelpBoxMessageType.Error));
                return;
            }
            template.CloneTree(rootVisualElement);
            avatarField = rootVisualElement.Q<ObjectField>("avatarField");
            avatarField.objectType = typeof(VRCAvatarDescriptor);
            avatarField.allowSceneObjects = true;
            avatarField.SetValueWithoutNotify(avatar);
            avatarField.RegisterValueChangedCallback(evt => Try(() => ChangeAvatar(evt.newValue as VRCAvatarDescriptor)));
            meshField = rootVisualElement.Q<ObjectField>("meshField");
            meshField.objectType = typeof(SkinnedMeshRenderer);
            meshField.allowSceneObjects = true;
            meshField.SetValueWithoutNotify(session?.Renderer);
            meshField.RegisterValueChangedCallback(evt => Try(() => ChangeMesh(evt.newValue as SkinnedMeshRenderer)));
            previewImage = rootVisualElement.Q<Image>("previewImage");
            previewImage.scaleMode = ScaleMode.ScaleToFit;
            previewImage.AddManipulator(new PreviewOrbitManipulator(delta =>
            {
                cameraSettings.Yaw = Mathf.Repeat(cameraSettings.Yaw + delta + 180f, 360f) - 180f;
                previewDirty = true;
            }, delta =>
            {
                cameraSettings.Distance = Mathf.Clamp(cameraSettings.Distance
                    * Mathf.Exp(Mathf.Clamp(delta, -20f, 20f) * 0.04f), 0.15f, 2f);
                previewDirty = true;
            }, delta =>
            {
                var slider = rootVisualElement.Q<Slider>("heightSlider");
                slider.value = (cameraSettings.Height + delta * 0.001f) * HeightSliderScale;
            }));
            // Unity 2022.3 rounds vertical sliders by width; scaling preserves precision in this narrow control.
            BindSlider("heightSlider", cameraSettings.Height * HeightSliderScale,
                value => cameraSettings.Height = value / HeightSliderScale);
            BindSlider("focalLengthSlider", cameraSettings.FocalLength, value => cameraSettings.FocalLength = value);
            Button("frontButton", () => { cameraSettings.Yaw = 0f; previewDirty = true; });
            Button("reloadButton", ReloadAvatar);
            Button("settingsButton", () => FacialBlacklistSettingsWindow.Open(session?.Shapes.Select(s => s.Name)));
            Button("selectedMeshButton", () => ChangeMesh(Selection.activeGameObject
                ? Selection.activeGameObject.GetComponent<SkinnedMeshRenderer>() : null));
            var loadField = rootVisualElement.Q<ObjectField>("loadClipField");
            loadField.objectType = typeof(AnimationClip);
            loadField.allowSceneObjects = false;
            loadField.SetValueWithoutNotify(loadClip);
            loadField.RegisterValueChangedCallback(evt => loadClip = evt.newValue as AnimationClip);
            Button("loadClipButton", LoadAnimation);
            shapeList = rootVisualElement.Q<ScrollView>("shapeList");
            shapeList.contentViewport.RegisterCallback<GeometryChangedEvent>(evt =>
                rootVisualElement.Q("transitionHeading").EnableInClassList("with-scrollbar",
                    shapeList.verticalScroller.resolvedStyle.display != DisplayStyle.None));
            meshSummary = rootVisualElement.Q<Label>("meshSummary");
            allToggle = rootVisualElement.Q<Toggle>("allShapesToggle");
            allToggle.RegisterValueChangedCallback(evt =>
            {
                if (session == null) return;
                foreach (var shape in session.Shapes.Where(IsShapeVisible)) shape.Include = evt.newValue;
                BuildShapeList();
            });
            Button("applyBlacklistButton", OnBlacklistChanged);
            hideBlacklistToggle = rootVisualElement.Q<Toggle>("hideBlacklistToggle");
            hideBlacklistToggle.SetValueWithoutNotify(blacklistSettings.Display == BlacklistedShapeDisplay.Hide);
            hideBlacklistToggle.RegisterValueChangedCallback(evt =>
            {
                var settings = FacialBlacklistPreferences.Load();
                settings.Display = evt.newValue ? BlacklistedShapeDisplay.Hide : BlacklistedShapeDisplay.ShowUnchecked;
                FacialBlacklistPreferences.Save(settings);
            });
            var nameField = rootVisualElement.Q<TextField>("animationName");
            nameField.SetValueWithoutNotify(animationName);
            nameField.RegisterValueChangedCallback(evt => { animationName = evt.newValue; UpdateOutputSummary(); });
            folderField = rootVisualElement.Q<TextField>("saveFolder");
            outputSummary = rootVisualElement.Q<Label>("outputSummary");
            Button("folderButton", ChooseFolder);
            Button("defaultFolderButton", () => { customFolder = false; RefreshController(); });
            saveButton = rootVisualElement.Q<Button>("saveButton");
            saveIconButton = rootVisualElement.Q<Button>("saveIconButton");
            Button("saveButton", SaveAnimation);
            Button("saveIconButton", SaveIcon);
            Button("highlightFolderButton", HighlightSaveFolder);
            statusArea = rootVisualElement.Q("statusArea");
            Try(() =>
            {
                RefreshController();
                if (avatar)
                {
                    if (session == null || !session.IsValid)
                    {
                        var body = FacialMeshSession.FindBody(avatar.gameObject);
                        session = body ? FacialMeshSession.Capture(body, avatar.transform, blacklist) : null;
                    }
                    RebuildPreview();
                }
                BuildShapeList();
            });
            renderSchedule = rootVisualElement.schedule.Execute(RenderPreview).Every(50);
            SetPlayModeAvailability();
        }

        private void BindSlider(string name, float value, Action<float> changed)
        {
            var slider = rootVisualElement.Q<Slider>(name);
            slider.SetValueWithoutNotify(value);
            slider.RegisterValueChangedCallback(evt =>
            {
                var clamped = Mathf.Clamp(evt.newValue, slider.lowValue, slider.highValue);
                slider.SetValueWithoutNotify(clamped);
                changed(clamped);
                previewDirty = true;
            });
        }

        private void Button(string name, Action action)
            => rootVisualElement.Q<Button>(name).clicked += () => Try(action);

        private bool ConfirmDiscard()
            => session == null || !session.HasEdits || EditorUtility.DisplayDialog("表情の編集", "現在の編集値と記録対象をリセットして、元アバターから読み直しますか？", "リセット", "キャンセル");

        private void ChangeAvatar(VRCAvatarDescriptor next)
        {
            if (next == avatar) return;
            if (next && (EditorUtility.IsPersistent(next) || !next.gameObject.scene.IsValid()
                || UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(next.gameObject.scene)))
            {
                avatarField.SetValueWithoutNotify(avatar);
                throw new ArgumentException("Project内のPrefabではなく、Hierarchy上のアバターを指定してください。");
            }
            if (!ConfirmDiscard()) { avatarField.SetValueWithoutNotify(avatar); return; }
            FacialMeshSession captured = null;
            if (next)
            {
                var body = FacialMeshSession.FindBody(next.gameObject);
                try { if (body) captured = FacialMeshSession.Capture(body, next.transform, blacklist); }
                catch { avatarField.SetValueWithoutNotify(avatar); throw; }
            }
            avatar = next;
            session = captured;
            customFolder = false;
            saveFolder = "";
            RefreshController();
            RebuildPreview();
            BuildShapeList();
            if (avatar && session == null) ShowStatus("BlendShapeを持つBodyが見つかりません。編集メッシュを指定してください。", HelpBoxMessageType.Info);
        }

        private void ChangeMesh(SkinnedMeshRenderer next)
        {
            try
            {
                if (!avatar) throw new InvalidOperationException("先にアバターを指定してください。");
                if (session != null && next == session.Renderer) return;
                var captured = next ? FacialMeshSession.Capture(next, avatar.transform, blacklist) : null;
                if (!ConfirmDiscard()) return;
                session = captured;
                RebuildPreview();
                BuildShapeList();
            }
            finally { meshField.SetValueWithoutNotify(session?.Renderer); }
        }

        private void ReloadAvatar()
        {
            if (!ConfirmDiscard()) return;
            var mesh = session?.Renderer;
            if (avatar)
            {
                if (!mesh) mesh = FacialMeshSession.FindBody(avatar.gameObject);
                session = mesh ? FacialMeshSession.Capture(mesh, avatar.transform, blacklist) : null;
            }
            else session = null;
            RefreshController();
            RebuildPreview();
            BuildShapeList();
        }

        private void RebuildPreview()
        {
            DisposePreview();
            if (!avatar || EditorApplication.isPlayingOrWillChangePlaymode) return;
            preview = new FacialPreview(avatar);
            if (session != null && session.IsValid) preview.Apply(session);
            previewDirty = true;
            if (!string.IsNullOrEmpty(preview.Note)) ShowStatus(preview.Note, HelpBoxMessageType.Info);
        }

        private void DisposePreview()
        {
            if (previewImage != null) previewImage.image = null;
            preview?.Dispose();
            preview = null;
        }

        private void RenderPreview()
        {
            if (!previewDirty || preview == null || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            previewDirty = false;
            Try(() =>
            {
                if (!avatar) { DisposePreview(); return; }
                if (session != null) preview.Apply(session);
                previewImage.image = preview.Render(cameraSettings, 512);
                previewImage.MarkDirtyRepaint();
            });
        }

        private void RefreshController()
        {
            controller = FxControllerAccess.Resolve(avatar, out var message);
            rootVisualElement.Q<Label>("fxPath").text = controller
                ? "編集対象FX: " + AssetDatabase.GetAssetPath(controller) : message;
            if (!customFolder) saveFolder = FxControllerAccess.DefaultFolder(controller);
            folderField.SetValueWithoutNotify(saveFolder);
            BuildLayers();
            UpdateOutputSummary();
        }

        private void BuildLayers()
        {
            BuildLayer(rootVisualElement.Q("leftLayer"), "Left Hand");
            BuildLayer(rootVisualElement.Q("rightLayer"), "Right Hand");
        }

        private void BuildLayer(VisualElement panel, string name)
        {
            panel.Clear();
            var heading = new Label("FX Layer " + name);
            heading.AddToClassList("layer-heading");
            panel.Add(heading);
            var layer = FxControllerAccess.FindLayer(controller, name);
            if (layer == null)
            {
                panel.Add(new HelpBox(controller ? "この名前のレイヤーはありません。" : "FX未設定", HelpBoxMessageType.Info));
                return;
            }
            if (layer.syncedLayerIndex >= 0)
            {
                panel.Add(new HelpBox("同期レイヤーのMotion編集には未対応です。", HelpBoxMessageType.Info));
                return;
            }
            var scroll = new ScrollView();
            scroll.AddToClassList("layer-scroll");
            panel.Add(scroll);
            AddStateMachine(scroll, layer.stateMachine);
        }

        private void AddStateMachine(VisualElement parent, AnimatorStateMachine machine)
        {
            if (!machine) return;
            foreach (var child in machine.states)
            {
                var state = child.state;
                if (!state) continue;
                var row = new VisualElement();
                row.AddToClassList("row");
                row.AddToClassList("state-row");
                var label = new Label(state.name) { tooltip = state.name };
                label.AddToClassList("state-name");
                row.Add(label);
                var unsupported = state.motion && !(state.motion is AnimationClip);
                var field = new ObjectField { objectType = unsupported ? typeof(Motion) : typeof(AnimationClip), allowSceneObjects = false };
                field.AddToClassList("state-clip");
                field.SetValueWithoutNotify(state.motion);
                field.SetEnabled(!unsupported && AssetDatabase.GetAssetPath(controller).StartsWith("Assets/", StringComparison.Ordinal));
                if (unsupported) field.tooltip = "BlendTreeなどのMotionは置き換えません。";
                field.RegisterValueChangedCallback(evt =>
                {
                    Try(() =>
                    {
                        var current = FxControllerAccess.Resolve(avatar, out var message);
                        if (current != controller) throw new InvalidOperationException("FX設定が変わりました。アバターを再読み込みしてください。 " + message);
                        FxControllerAccess.Assign(controller, state, evt.newValue as AnimationClip);
                        ShowStatus(state.name + " のAnimationを変更しました。Ctrl+Zで元に戻せます。", HelpBoxMessageType.Info);
                    });
                    field.SetValueWithoutNotify(state.motion);
                });
                row.Add(field);
                parent.Add(row);
            }
            foreach (var child in machine.stateMachines)
            {
                if (!child.stateMachine) continue;
                var foldout = new Foldout { text = child.stateMachine.name, value = false,
                    viewDataKey = "machine-" + child.stateMachine.GetInstanceID() };
                parent.Add(foldout);
                AddStateMachine(foldout, child.stateMachine);
            }
        }

        private void BuildShapeList()
        {
            shapeList.Clear();
            EnforceHiddenExclusions();
            saveIconButton.SetEnabled(preview != null);
            meshField.SetValueWithoutNotify(session?.Renderer);
            if (session == null || !session.IsValid)
            {
                meshSummary.text = "編集メッシュ未選択";
                allToggle.SetEnabled(false);
                saveButton.SetEnabled(false);
                return;
            }
            allToggle.SetEnabled(true);
            saveButton.SetEnabled(true);
            foreach (var shape in session.Shapes.Where(IsShapeVisible))
            {
                var row = new VisualElement();
                row.AddToClassList("row");
                row.AddToClassList("shape-row");
                var blacklisted = blacklist.Matches(shape.Name);
                var include = new Toggle { value = shape.Include, tooltip = blacklisted
                    ? "ブラックリストに一致（初期値オフ）。手動でチェックするとAnimationに記録します。" : "Animationに記録する" };
                include.AddToClassList("shape-include");
                include.RegisterValueChangedCallback(evt => { shape.Include = evt.newValue; UpdateSelectionSummary(); });
                row.Add(include);
                var label = new Label(shape.Name) { tooltip = shape.Name + " / 指定時: " + shape.DefaultValue
                    + (blacklisted ? " / ブラックリストに一致" : "") };
                label.AddToClassList("shape-name");
                row.Add(label);
                var slider = new Slider(0f, 100f) { value = Mathf.Clamp(shape.Value, 0f, 100f) };
                slider.AddToClassList("shape-slider");
                var number = new FloatField { value = shape.Value };
                number.AddToClassList("shape-value");
                void SetValue(float value)
                {
                    if (float.IsNaN(value) || float.IsInfinity(value)) { number.SetValueWithoutNotify(shape.Value); return; }
                    shape.Value = value;
                    slider.SetValueWithoutNotify(Mathf.Clamp(value, 0f, 100f));
                    number.SetValueWithoutNotify(value);
                    previewDirty = true;
                }
                slider.RegisterValueChangedCallback(evt => SetValue(evt.newValue));
                number.RegisterValueChangedCallback(evt => SetValue(evt.newValue));
                row.Add(slider);
                row.Add(number);
                var minimum = new Button(() => SetValue(0f)) { text = "最小値" };
                minimum.AddToClassList("shape-button");
                row.Add(minimum);
                var maximum = new Button(() => SetValue(100f)) { text = "最大値" };
                maximum.AddToClassList("shape-button");
                row.Add(maximum);
                var transition = new Toggle { value = shape.Transition,
                    tooltip = "指定時の値から現在の表情までを0～10f（60fps）で直線補間。未チェックは固定値。" };
                transition.AddToClassList("shape-transition");
                transition.RegisterValueChangedCallback(evt => shape.Transition = evt.newValue);
                row.Add(transition);
                shapeList.Add(row);
            }
            UpdateSelectionSummary();
        }

        private void UpdateSelectionSummary()
        {
            if (session == null) return;
            var count = session.Shapes.Count(s => s.Include);
            var visible = session.Shapes.Where(IsShapeVisible).ToList();
            var hidden = session.Shapes.Count - visible.Count;
            allToggle.SetEnabled(visible.Count > 0);
            allToggle.SetValueWithoutNotify(visible.Count > 0 && visible.All(s => s.Include));
            meshSummary.text = session.Renderer.name + " - " + count + "/" + session.Shapes.Count;
            meshSummary.tooltip = "編集メッシュ: " + session.Renderer.name + "\n記録対象: " + count + " / " + session.Shapes.Count
                + "\n表示: " + visible.Count + " / 非表示: " + hidden;
        }

        private void ReloadBlacklist()
        {
            blacklistSettings = FacialBlacklistPreferences.Load();
            blacklist = new BlendShapeBlacklist(blacklistSettings.Rules);
        }

        private void OnBlacklistChanged()
        {
            ReloadBlacklist();
            hideBlacklistToggle?.SetValueWithoutNotify(blacklistSettings.Display == BlacklistedShapeDisplay.Hide);
            var disabled = session?.ApplyBlacklist(blacklist) ?? 0;
            blacklistSignature = blacklistSettings.Signature;
            if (shapeList != null) BuildShapeList();
            ShowStatus("ブラックリスト設定を適用しました。記録チェックをオフにした項目: " + disabled, HelpBoxMessageType.Info);
        }

        private bool IsShapeVisible(BlendShapeValue shape)
            => blacklistSettings.Display != BlacklistedShapeDisplay.Hide || !blacklist.Matches(shape.Name);

        private int EnforceHiddenExclusions()
            => blacklistSettings.Display == BlacklistedShapeDisplay.Hide && session != null ? session.ApplyBlacklist(blacklist) : 0;

        private void LoadAnimation()
        {
            if (!avatar || session == null) throw new InvalidOperationException("アバターと編集メッシュを指定してください。");
            var message = FacialAnimation.Load(loadClip, session, avatar.transform);
            var excluded = EnforceHiddenExclusions();
            if (excluded > 0) message += " 非表示のブラックリスト項目 " + excluded + " 件は記録対象から外しました。";
            animationName = loadClip.name.StartsWith("FA_", StringComparison.Ordinal) ? loadClip.name.Substring(3) : loadClip.name;
            rootVisualElement.Q<TextField>("animationName").SetValueWithoutNotify(animationName);
            BuildShapeList();
            UpdateOutputSummary();
            previewDirty = true;
            ShowStatus(message, HelpBoxMessageType.Info);
        }

        private void ChooseFolder()
        {
            var startingFolder = Directory.Exists(saveFolder) ? Path.GetFullPath(saveFolder) : Application.dataPath;
            var chosen = EditorUtility.OpenFolderPanel("Animationとアイコンの保存先", startingFolder, "");
            if (string.IsNullOrEmpty(chosen)) return;
            saveFolder = FacialAssetWriter.FromAbsoluteFolder(chosen);
            customFolder = true;
            folderField.SetValueWithoutNotify(saveFolder);
            UpdateOutputSummary();
        }

        private void UpdateOutputSummary()
        {
            if (outputSummary == null) return;
            try
            {
                var paths = new FacialSavePaths(saveFolder, animationName);
                outputSummary.text = paths.ClipPath + "  /  " + Path.GetFileName(paths.IconPath) + "（256×256 PNG）";
            }
            catch (ArgumentException) { outputSummary.text = "FA_保存名.anim と FA_保存名.png を同じフォルダへ保存します。"; }
        }

        private FacialSavePaths GetSavePaths()
        {
            if (!customFolder) RefreshController();
            return new FacialSavePaths(saveFolder, animationName);
        }

        private static bool ConfirmOverwrite(string path)
            => !File.Exists(path) || EditorUtility.DisplayDialog("同名ファイルの上書き",
                path + "\n\n既存のファイルを上書きしますか？", "上書き", "キャンセル");

        private void SaveAnimation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!avatar || session == null)
                throw new InvalidOperationException("アバターと編集メッシュを指定してください。");
            EnforceHiddenExclusions();
            var paths = GetSavePaths();
            if (!ConfirmOverwrite(paths.ClipPath)) return;
            AnimationClip generated = null;
            try
            {
                generated = FacialAnimation.Create(session, avatar.transform, paths.ClipName);
                var saved = FacialAssetWriter.SaveAnimation(paths, generated);
                EditorGUIUtility.PingObject(saved);
                ShowStatus("Animationを保存しました: " + paths.ClipPath, HelpBoxMessageType.Info);
            }
            finally
            {
                if (generated) Object.DestroyImmediate(generated);
            }
        }

        private void SaveIcon()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!avatar || preview == null)
                throw new InvalidOperationException("プレビューするアバターを指定してください。");
            var paths = GetSavePaths();
            if (!ConfirmOverwrite(paths.IconPath)) return;
            Texture2D icon = null;
            try
            {
                if (session != null) preview.Apply(session);
                icon = preview.Capture(cameraSettings);
                FacialAssetWriter.SaveIcon(paths, icon);
                ShowStatus("アイコンを保存しました: " + paths.IconPath, HelpBoxMessageType.Info);
            }
            finally
            {
                if (icon) Object.DestroyImmediate(icon);
                previewDirty = true;
            }
        }

        private void HighlightSaveFolder()
        {
            if (!customFolder) RefreshController();
            FacialAssetWriter.ValidateFolder(saveFolder);
            if (!AssetDatabase.IsValidFolder(saveFolder))
            {
                ShowStatus("保存先フォルダはまだありません。Animationまたはアイコンを保存すると作成されます。", HelpBoxMessageType.Info);
                return;
            }
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<DefaultAsset>(saveFolder));
        }

        private void OnUndoRedo()
        {
            if (rootVisualElement.Q("leftLayer") != null) BuildLayers();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) DisposePreview();
            if (state == PlayModeStateChange.EnteredEditMode) Try(RebuildPreview);
            SetPlayModeAvailability();
        }

        private void SetPlayModeAvailability()
        {
            rootVisualElement.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
        }

        private void Try(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                ShowStatus(ex.Message, HelpBoxMessageType.Error);
                if (!(ex is ArgumentException) && !(ex is InvalidOperationException) && !(ex is IOException)) Debug.LogException(ex);
            }
        }

        private void ShowStatus(string text, HelpBoxMessageType type)
        {
            if (statusArea == null) return;
            statusArea.Clear();
            var box = new HelpBox(text, type);
            box.AddToClassList("status-message");
            statusArea.Add(box);
        }
    }
}
