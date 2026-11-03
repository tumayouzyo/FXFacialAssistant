using System;
using System.Collections.Generic;
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
        [SerializeField] private bool applyLoadedClip;
        [SerializeField] private float clipPosition = 100f;
        [SerializeField] private AnimationClip defaultClip;
        private bool statusIsError;
        private Action failedAction;
        private Func<bool> errorRecovery;
        [SerializeField] private string blacklistSignature;
        private FacialBlacklistSettings blacklistSettings;
        private BlendShapeBlacklist blacklist;
        private FacialPreview preview;
        private AnimatorController controller;
        private bool previewDirty;
        private bool previewFaulted;
        private bool uiEnglish;
        private Image previewImage;
        private ObjectField avatarField;
        private ObjectField meshField;
        private Toggle allToggle;
        private Toggle hideBlacklistToggle;
        private ListView shapeList;
        private List<BlendShapeValue> visibleShapes = new List<BlendShapeValue>();
        private Label meshSummary;
        private TextField folderField;
        private Label outputSummary;
        private Button saveButton;
        private Button saveIconButton;
        private VisualElement statusArea;
        private double nextRenderTime;

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
            EditorApplication.update += RenderPreview;
            FacialToolPreferences.Changed += OnToolPreferencesChanged;
        }

        private void OnDisable()
        {
            FacialBlacklistPreferences.Changed -= OnBlacklistChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= RenderPreview;
            FacialToolPreferences.Changed -= OnToolPreferencesChanged;
            DisposePreview();
        }

        public void CreateGUI()
        {
            uiEnglish = FacialToolPreferences.English;
            rootVisualElement.Clear();
            var scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            var templatePath = Path.GetDirectoryName(scriptPath).Replace('\\', '/') + "/FXFacialAssistantWindow.uxml";
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(templatePath);
            if (!template)
            {
                rootVisualElement.Add(new HelpBox(FacialLocalization.Format("UIファイルが見つかりません: {0}", templatePath), HelpBoxMessageType.Error));
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
                RequestPreview();
            }, delta =>
            {
                cameraSettings.Distance = Mathf.Clamp(cameraSettings.Distance
                    * Mathf.Exp(Mathf.Clamp(delta, -20f, 20f) * 0.04f), 0.15f, 2f);
                RequestPreview();
            }, delta =>
            {
                var slider = rootVisualElement.Q<Slider>("heightSlider");
                slider.value = (cameraSettings.Height + delta * 0.001f) * HeightSliderScale;
            }));
            // Unity 2022.3 rounds vertical sliders by width; scaling preserves precision in this narrow control.
            BindSlider("heightSlider", cameraSettings.Height * HeightSliderScale,
                value => cameraSettings.Height = value / HeightSliderScale);
            BindSlider("focalLengthSlider", cameraSettings.FocalLength, value => cameraSettings.FocalLength = value);
            Button("frontButton", () => { cameraSettings.Yaw = 0f; RequestPreview(); });
            Button("saveCameraButton", () =>
            {
                EditorPrefs.SetString(CameraPreference, JsonUtility.ToJson(cameraSettings));
                rootVisualElement.Q<Button>("applyCameraButton").SetEnabled(true);
                ShowStatus("画角を保存しました。", HelpBoxMessageType.Info);
            });
            Button("applyCameraButton", () =>
            {
                if (!EditorPrefs.HasKey(CameraPreference)) return;
                var restored = JsonUtility.FromJson<FacialCameraSettings>(EditorPrefs.GetString(CameraPreference));
                cameraSettings.Distance = Mathf.Clamp(restored.Distance, 0.15f, 2f);
                cameraSettings.Height = Mathf.Clamp(restored.Height, -0.4f, 0.5f);
                cameraSettings.FocalLength = Mathf.Clamp(restored.FocalLength, 20f, 200f);
                cameraSettings.Yaw = restored.Yaw;
                rootVisualElement.Q<Slider>("heightSlider").SetValueWithoutNotify(cameraSettings.Height * HeightSliderScale);
                rootVisualElement.Q<Slider>("focalLengthSlider").SetValueWithoutNotify(cameraSettings.FocalLength);
                RequestPreview();
            });
            rootVisualElement.Q<Button>("applyCameraButton").SetEnabled(EditorPrefs.HasKey(CameraPreference));
            Button("editedOnlyButton", () =>
            {
                if (session == null) return;
                foreach (var shape in session.Shapes)
                    shape.Include = IsShapeVisible(shape) && !Mathf.Approximately(shape.Value, shape.DefaultValue);
                BuildShapeList();
            });
            Button("reloadButton", ReloadAvatar);
            Button("settingsButton", () => FacialBlacklistSettingsWindow.Open(session?.Shapes.Select(s => s.Name)));
            Button("selectedMeshButton", () => ChangeMesh(Selection.activeGameObject
                ? Selection.activeGameObject.GetComponent<SkinnedMeshRenderer>() : null));
            var loadField = rootVisualElement.Q<ObjectField>("loadClipField");
            loadField.objectType = typeof(AnimationClip);
            loadField.allowSceneObjects = false;
            loadField.SetValueWithoutNotify(loadClip);
            loadField.RegisterValueChangedCallback(evt => Try(() => ChangeLoadClip(evt.newValue as AnimationClip)));
            Button("clearClipButton", ClearLoadClip);
            var applyToggle = rootVisualElement.Q<Toggle>("applyClipToggle");
            applyToggle.SetValueWithoutNotify(applyLoadedClip);
            applyToggle.RegisterValueChangedCallback(evt =>
            {
                applyLoadedClip = evt.newValue;
                if (applyLoadedClip && loadClip) Try(() => ApplyLoadedAnimation(true));
            });
            var positionSlider = rootVisualElement.Q<Slider>("clipPositionSlider");
            positionSlider.SetValueWithoutNotify(clipPosition);
            positionSlider.RegisterValueChangedCallback(evt =>
            {
                clipPosition = Mathf.Clamp(evt.newValue, 0f, 100f);
                positionSlider.SetValueWithoutNotify(clipPosition);
                if (applyLoadedClip && loadClip) Try(() => ApplyLoadedAnimation(false));
            });
            loadField.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var input = loadField.Q(className: "unity-object-field__input");
                if (input == null) return;
                positionSlider.style.marginLeft = input.worldBound.x - positionSlider.parent.worldBound.x;
                positionSlider.style.width = input.resolvedStyle.width;
            });
            UpdateClipControls();
            var defaultField = rootVisualElement.Q<ObjectField>("defaultClipField");
            defaultField.objectType = typeof(AnimationClip);
            defaultField.allowSceneObjects = false;
            defaultField.SetValueWithoutNotify(defaultClip);
            defaultField.RegisterValueChangedCallback(evt => Try(() =>
            {
                defaultClip = evt.newValue as AnimationClip;
                if (session != null && avatar) FacialAnimation.SetDefault(defaultClip, session, avatar.transform, false);
                BuildShapeList();
            }));
            Button("loadDefaultButton", () =>
            {
                RequireSession();
                FacialAnimation.SetDefault(defaultClip, session, avatar.transform, true);
                BuildShapeList();
                RequestPreview();
            });
            Button("overwriteDefaultButton", OverwriteDefault);
            shapeList = rootVisualElement.Q<ListView>("shapeList");
            shapeList.fixedItemHeight = 24f;
            shapeList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            shapeList.selectionType = SelectionType.None;
            shapeList.makeItem = () => new FacialShapeRow(RequestPreview, UpdateSelectionSummary);
            shapeList.bindItem = (element, index) => ((FacialShapeRow)element).Bind(visibleShapes[index], blacklist.Matches(visibleShapes[index].Name));
            shapeList.unbindItem = (element, index) => ((FacialShapeRow)element).Unbind();
            var shapeScroll = shapeList.Q<ScrollView>();
            shapeScroll.contentViewport.RegisterCallback<GeometryChangedEvent>(evt =>
                rootVisualElement.Q("transitionHeading").EnableInClassList("with-scrollbar",
                    shapeScroll.verticalScroller.resolvedStyle.display != DisplayStyle.None));
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
            nameField.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != nameField) return;
                animationName = evt.newValue;
                UpdateOutputSummary();
            });
            folderField = rootVisualElement.Q<TextField>("saveFolder");
            outputSummary = rootVisualElement.Q<Label>("outputSummary");
            Button("folderButton", ChooseFolder);
            Button("defaultFolderButton", ResetSaveFolder);
            var prefixToggle = rootVisualElement.Q<Toggle>("prefixToggle");
            prefixToggle.SetValueWithoutNotify(FacialToolPreferences.Prefix);
            prefixToggle.RegisterValueChangedCallback(evt => FacialToolPreferences.Prefix = evt.newValue);
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
                        DisposePreview();
                    }
                    if (preview == null) RebuildPreview();
                    else RequestPreview();
                }
                BuildShapeList();
            });
            previewImage.RegisterCallback<GeometryChangedEvent>(evt => previewDirty = true);
            SetPlayModeAvailability();
            FacialLocalization.Translate(rootVisualElement);
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
                RequestPreview();
            });
        }

        private void OnToolPreferencesChanged()
        {
            if (previewImage == null) return;
            if (uiEnglish != FacialToolPreferences.English) CreateGUI();
            else
            {
                rootVisualElement.Q<Toggle>("prefixToggle")?.SetValueWithoutNotify(FacialToolPreferences.Prefix);
                UpdateOutputSummary();
            }
        }

        private void Button(string name, Action action)
            => rootVisualElement.Q<Button>(name).clicked += () => Try(action);

        private bool ConfirmDiscard()
            => session == null || !session.HasEdits || FacialLocalization.Dialog("表情の編集", "現在の編集値と記録対象をリセットして、元アバターから読み直しますか？", "リセット", "キャンセル");

        private void ChangeAvatar(VRCAvatarDescriptor next)
        {
            if (next == avatar && (!next || avatar)) return;
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
                if (!avatar) throw FacialErrors.Operation(FacialProblem.Avatar, "先にアバターを指定してください。");
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
            previewFaulted = false;
            if (session != null && session.IsValid)
            {
                FacialAnimation.SetDefault(defaultClip, session, avatar.transform, false);
                preview.Apply(session);
            }
            RequestPreview();
            if (!string.IsNullOrEmpty(preview.Note)) ShowStatus(preview.Note, HelpBoxMessageType.Info);
        }

        private void DisposePreview()
        {
            if (previewImage != null) previewImage.image = null;
            preview?.Dispose();
            preview = null;
        }

        private void RequestPreview()
        {
            previewDirty = true;
            Repaint();
            RenderPreview();
        }

        private void RenderPreview()
        {
            if (statusIsError && errorRecovery != null && errorRecovery()) ClearError();
            if (previewFaulted || !previewDirty || EditorApplication.timeSinceStartup < nextRenderTime || preview == null || previewImage?.panel == null || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextRenderTime = EditorApplication.timeSinceStartup + 1d / 60d;
            try
            {
                if (!avatar) { DisposePreview(); return; }
                if (session != null) preview.Apply(session);
                previewImage.image = preview.Render(cameraSettings, 512);
                previewImage.MarkDirtyRepaint();
                previewDirty = false;
                Repaint();
            }
            catch (Exception ex)
            {
                previewFaulted = true;
                previewDirty = false;
                ReportError(ex);
            }
        }

        private void RefreshController()
        {
            controller = FxControllerAccess.Resolve(avatar, out var message);
            rootVisualElement.Q<Label>("fxPath").text = controller
                ? FacialLocalization.Text("編集対象FX: ") + AssetDatabase.GetAssetPath(controller) : FacialLocalization.Text(message);
            if (!customFolder) saveFolder = FxControllerAccess.DefaultFolder(controller);
            folderField.SetValueWithoutNotify(saveFolder);
            BuildLayers();
            UpdateOutputSummary();
        }

        private void BuildLayers()
        {
            BuildLayer(rootVisualElement.Q("leftLayer"), "Left Hand");
            BuildLayer(rootVisualElement.Q("rightLayer"), "Right Hand");
            FacialLocalization.Translate(rootVisualElement.Q("leftLayer"));
            FacialLocalization.Translate(rootVisualElement.Q("rightLayer"));
        }

        private void BuildLayer(VisualElement panel, string name)
        {
            panel.Clear();
            var header = new VisualElement();
            header.AddToClassList("row");
            var heading = new Label("FX Layer " + name);
            heading.AddToClassList("layer-heading");
            heading.style.flexGrow = 1;
            header.Add(heading);
            var empty = new Button(() => Try(() =>
            {
                FxControllerAccess.ValidateCurrent(avatar, controller);
                var count = FxControllerMaintenance.FillEmpty(controller, name);
                BuildLayers();
                ShowStatus(FacialLocalization.Format("empty設定: {0}", count), HelpBoxMessageType.Info);
            })) { text = "empty自動設定",
                tooltip = "このレイヤーと配下のSubStateMachineで、MotionがNoneのステートにSDKのproxy_emptyを設定します。既存のMotionは変更しません。Ctrl+Zで元に戻せます。" };
            header.Add(empty);
            panel.Add(header);
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
            if (name == "Right Hand")
            {
                var cleanup = new Button(() => Try(CleanupController)) { text = "クリーンアップ",
                    tooltip = "Broken text PPtrエラーの原因となる、FX Controller内の存在しないローカル参照をクリーンアップします。外部アセットへの参照は対象外です。実行前に確認し、元ファイルとmetaをLibrary/TY_FXFacialAssistantBackupに保存します。" };
                cleanup.style.alignSelf = Align.FlexEnd;
                panel.Add(cleanup);
            }
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
                        FxControllerAccess.ValidateCurrent(avatar, controller);
                        FxControllerAccess.Assign(controller, state, evt.newValue as AnimationClip);
                        ShowStatus(FacialLocalization.Format("{0} のAnimationを変更しました。Ctrl+Zで元に戻せます。", state.name), HelpBoxMessageType.Info);
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
                foldout.Q<Label>()?.AddToClassList("user-text");
                parent.Add(foldout);
                AddStateMachine(foldout, child.stateMachine);
            }
        }

        private void BuildShapeList()
        {
            EnforceHiddenExclusions();
            saveIconButton.SetEnabled(preview != null);
            meshField.SetValueWithoutNotify(session?.Renderer);
            if (session == null || !session.IsValid)
            {
                meshSummary.text = FacialLocalization.Text("編集メッシュ未選択");
                allToggle.SetEnabled(false);
                visibleShapes.Clear();
                shapeList.itemsSource = null;
                saveButton.SetEnabled(false);
                return;
            }
            allToggle.SetEnabled(true);
            saveButton.SetEnabled(true);
            var next = session.Shapes.Where(IsShapeVisible).ToList();
            if (!visibleShapes.SequenceEqual(next)) visibleShapes = next;
            if (!ReferenceEquals(shapeList.itemsSource, visibleShapes)) shapeList.itemsSource = visibleShapes;
            else shapeList.RefreshItems();
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
            meshSummary.tooltip = FacialLocalization.Format("編集メッシュ: {0}\n記録対象: {1} / {2}\n表示: {3} / 非表示: {4}",
                session.Renderer.name, count, session.Shapes.Count, visible.Count, hidden);
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
            ShowStatus(FacialLocalization.Format("ブラックリスト設定を適用しました。記録チェックをオフにした項目: {0}", disabled), HelpBoxMessageType.Info);
        }

        private bool IsShapeVisible(BlendShapeValue shape)
            => blacklistSettings.Display != BlacklistedShapeDisplay.Hide || !blacklist.Matches(shape.Name);

        private int EnforceHiddenExclusions()
            => blacklistSettings.Display == BlacklistedShapeDisplay.Hide && session != null ? session.ApplyBlacklist(blacklist) : 0;

        private void ApplyLoadedAnimation(bool updateName)
        {
            if (!avatar || session == null) throw FacialErrors.Operation(FacialProblem.Mesh, "アバターと編集メッシュを指定してください。");
            var message = FacialAnimation.Load(loadClip, session, avatar.transform, clipPosition / 100f);
            var excluded = EnforceHiddenExclusions();
            if (excluded > 0) message += FacialLocalization.Format(" 非表示のブラックリスト項目 {0} 件は記録対象から外しました。", excluded);
            if (updateName)
            {
                animationName = FacialToolPreferences.Prefix && loadClip.name.StartsWith("FA_", StringComparison.Ordinal) ? loadClip.name.Substring(3) : loadClip.name;
                rootVisualElement.Q<TextField>("animationName").SetValueWithoutNotify(animationName);
            }
            BuildShapeList();
            UpdateOutputSummary();
            RequestPreview();
            ShowStatus(message, HelpBoxMessageType.Info);
        }

        private void ClearLoadClip()
        {
            rootVisualElement.Q<ObjectField>("loadClipField").value = null;
            FacialToolPreferences.Prefix = true;
        }

        private void ChangeLoadClip(AnimationClip clip)
        {
            loadClip = clip;
            UpdateClipControls();
            if (!clip) return;
            var path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(path)) return;
            saveFolder = Path.GetDirectoryName(path).Replace('\\', '/');
            customFolder = true;
            folderField.SetValueWithoutNotify(saveFolder);
            FacialToolPreferences.Prefix = false;
            UpdateOutputSummary();
            if (applyLoadedClip) ApplyLoadedAnimation(true);
        }

        private void UpdateClipControls()
        {
            rootVisualElement.Q<Slider>("clipPositionSlider").style.display = loadClip ? DisplayStyle.Flex : DisplayStyle.None;
            rootVisualElement.Q<Toggle>("applyClipToggle").SetEnabled(loadClip);
        }

        private void ResetSaveFolder()
        {
            customFolder = false;
            FacialToolPreferences.Prefix = true;
            RefreshController();
        }

        private void ChooseFolder()
        {
            var startingFolder = Directory.Exists(saveFolder) ? Path.GetFullPath(saveFolder) : Application.dataPath;
            var chosen = EditorUtility.OpenFolderPanel(FacialLocalization.Text("Animationとアイコンの保存先"), startingFolder, "");
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
                var paths = new FacialSavePaths(saveFolder, animationName, FacialToolPreferences.Prefix);
                outputSummary.text = paths.ClipPath + "  /  " + Path.GetFileName(paths.IconPath) + "（256×256 PNG）";
            }
            catch (ArgumentException) { outputSummary.text = FacialLocalization.Text("FA_保存名.anim と FA_保存名.png を同じフォルダへ保存します。"); }
        }

        private FacialSavePaths GetSavePaths()
        {
            if (!customFolder) RefreshController();
            return new FacialSavePaths(saveFolder, animationName, FacialToolPreferences.Prefix);
        }

        private static bool ConfirmOverwrite(string path)
            => !File.Exists(path) || FacialLocalization.Dialog("同名ファイルの上書き",
                FacialLocalization.Format("{0}\n\n既存のファイルを上書きしますか？", path), "上書き", "キャンセル");

        private void SaveAnimation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!avatar || session == null)
                throw FacialErrors.Operation(FacialProblem.Mesh, "アバターと編集メッシュを指定してください。");
            EnforceHiddenExclusions();
            var paths = GetSavePaths();
            if (!ConfirmOverwrite(paths.ClipPath)) return;
            AnimationClip generated = null;
            try
            {
                generated = FacialAnimation.Create(session, avatar.transform, paths.ClipName);
                var saved = FacialAssetWriter.SaveAnimation(paths, generated);
                EditorGUIUtility.PingObject(saved);
                ShowStatus(FacialLocalization.Format("Animationを保存しました: {0}", paths.ClipPath), HelpBoxMessageType.Info);
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
                throw FacialErrors.Operation(FacialProblem.Avatar, "プレビューするアバターを指定してください。");
            var paths = GetSavePaths();
            if (!ConfirmOverwrite(paths.IconPath)) return;
            Texture2D icon = null;
            try
            {
                if (session != null) preview.Apply(session);
                icon = preview.Capture(cameraSettings);
                FacialAssetWriter.SaveIcon(paths, icon);
                ShowStatus(FacialLocalization.Format("アイコンを保存しました: {0}", paths.IconPath), HelpBoxMessageType.Info);
            }
            finally
            {
                if (icon) Object.DestroyImmediate(icon);
                RequestPreview();
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
            if (state == PlayModeStateChange.EnteredEditMode) Try(RestoreEditMode);
            SetPlayModeAvailability();
        }

        private void RestoreEditMode()
        {
            if (!ReferenceEquals(avatar, null))
                avatar = EditorUtility.InstanceIDToObject(avatar.GetInstanceID()) as VRCAvatarDescriptor;
            session?.RestoreReferences();
            avatarField?.SetValueWithoutNotify(avatar);
            RefreshController();
            BuildShapeList();
            RebuildPreview();
        }

        private void SetPlayModeAvailability()
        {
            rootVisualElement.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
        }

        private void Try(Action action)
        {
            try
            {
                action();
                if (statusIsError && (errorRecovery != null ? errorRecovery() : failedAction == action)) ClearError();
            }
            catch (Exception ex) { ReportError(ex, action); }
        }

        private void ReportError(Exception exception, Action action = null)
        {
            failedAction = action;
            errorRecovery = RecoveryFor(FacialErrors.Problem(exception));
            ShowStatus(exception.Message, HelpBoxMessageType.Error);
            if (!(exception is ArgumentException) && !(exception is InvalidOperationException) && !(exception is IOException))
                Debug.LogException(exception);
        }

        private void ClearError()
        {
            if (!statusIsError) return;
            statusArea?.Clear();
            statusIsError = false;
            failedAction = null;
            errorRecovery = null;
        }

        private Func<bool> RecoveryFor(FacialProblem problem)
        {
            switch (problem)
            {
                case FacialProblem.SaveName:
                case FacialProblem.SaveFolder:
                    return () =>
                    {
                        try { _ = new FacialSavePaths(saveFolder, animationName, FacialToolPreferences.Prefix); return true; }
                        catch (ArgumentException) { return false; }
                    };
                case FacialProblem.LoadClip: return () => loadClip;
                case FacialProblem.Avatar: return () => avatar;
                case FacialProblem.Mesh: return () => avatar && session != null && session.IsValid;
                case FacialProblem.RecordingSelection: return () => session != null && session.Shapes.Any(shape => shape.Include);
                case FacialProblem.DefaultClip:
                    return () => defaultClip && AssetDatabase.GetAssetPath(defaultClip).StartsWith("Assets/", StringComparison.Ordinal)
                        && !AssetDatabase.IsSubAsset(defaultClip) && AssetDatabase.IsOpenForEdit(defaultClip);
                case FacialProblem.Preview: return () => !previewFaulted && avatar && preview != null && session != null && session.IsValid;
                case FacialProblem.Fx: return () => controller && FxControllerAccess.Resolve(avatar, out _) == controller;
                default: return null;
            }
        }

        private void ShowStatus(string text, HelpBoxMessageType type)
        {
            if (statusArea == null) return;
            statusIsError = type == HelpBoxMessageType.Error;
            statusArea.Clear();
            var box = new HelpBox(FacialLocalization.Text(text), type);
            box.AddToClassList("status-message");
            statusArea.Add(box);
        }

        private void RequireSession()
        {
            if (!avatar || session == null || !session.IsValid)
                throw FacialErrors.Operation(FacialProblem.Mesh, "アバターと編集メッシュを指定してください。");
        }

        private void OverwriteDefault()
        {
            RequireSession();
            var path = AssetDatabase.GetAssetPath(defaultClip);
            if (!defaultClip || !path.StartsWith("Assets/", StringComparison.Ordinal) || !AssetDatabase.IsOpenForEdit(defaultClip)
                || AssetDatabase.IsSubAsset(defaultClip))
                throw FacialErrors.Operation(FacialProblem.DefaultClip, "Assetsフォルダ内の編集可能なデフォルト表情を指定してください。");
            if (!FacialLocalization.Dialog("デフォルト表情の上書き",
                "現在のすべてのBlendShape値で上書きします。他のカーブは維持します。別名でコピーしたアニメーションの使用を推奨します。上書きしますか？",
                "上書き", "キャンセル")) return;
            Undo.RegisterCompleteObjectUndo(defaultClip, "Overwrite default expression");
            var bindingPath = session.BindingPath(avatar.transform);
            foreach (var shape in session.Shapes)
                AnimationUtility.SetEditorCurve(defaultClip, EditorCurveBinding.FloatCurve(bindingPath,
                    typeof(SkinnedMeshRenderer), "blendShape." + shape.Name), AnimationCurve.Constant(0f, 1f / 60f, shape.Value));
            EditorUtility.SetDirty(defaultClip);
            AssetDatabase.SaveAssetIfDirty(defaultClip);
            FacialAnimation.SetDefault(defaultClip, session, avatar.transform, false);
            ShowStatus("デフォルト表情を保存しました。", HelpBoxMessageType.Info);
        }

        private void CleanupController()
        {
            FxControllerAccess.ValidateCurrent(avatar, controller);
            if (!FacialLocalization.Dialog("FXクリーンアップ",
                "FX Controller内の存在しないローカル参照のみ解除します。実行前にバックアップをLibraryに保存します。実行しますか？",
                "実行", "キャンセル")) return;
            FxControllerAccess.ValidateCurrent(avatar, controller);
            var count = FxControllerMaintenance.Cleanup(controller);
            BuildLayers();
            ShowStatus(FacialLocalization.Format("クリーンアップ: {0}", count), HelpBoxMessageType.Info);
        }
    }
}
