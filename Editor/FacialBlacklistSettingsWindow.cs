using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TY.FXFacialAssistant
{
    internal sealed class FacialBlacklistSettingsWindow : EditorWindow
    {
        [SerializeField] private FacialBlacklistSettings draft;
        [SerializeField] private List<string> shapeNames = new List<string>();
        private Label matchSummary;

        public static void Open(IEnumerable<string> names)
        {
            var window = GetWindow<FacialBlacklistSettingsWindow>(true, "FXFacialAssistant 設定", true);
            if (window.draft == null) window.draft = FacialBlacklistPreferences.Load();
            window.shapeNames = names?.ToList() ?? new List<string>();
            window.RefreshSummary();
        }

        private void OnEnable()
        {
            minSize = new Vector2(580f, 560f);
            if (draft == null) draft = FacialBlacklistPreferences.Load();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var script = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            var path = Path.GetDirectoryName(script).Replace('\\', '/') + "/FacialBlacklistSettingsWindow.uxml";
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            if (!tree) { rootVisualElement.Add(new HelpBox("設定UIが見つかりません: " + path, HelpBoxMessageType.Error)); return; }
            tree.CloneTree(rootVisualElement);
            var rules = rootVisualElement.Q<TextField>("rulesText");
            rules.SetValueWithoutNotify(draft.Rules);
            rules.RegisterValueChangedCallback(evt => { draft.Rules = evt.newValue; RefreshSummary(); });
            matchSummary = rootVisualElement.Q<Label>("matchSummary");
            rootVisualElement.Q<Button>("defaultsButton").clicked += () => rules.value = FacialBlacklistDefaults.Rules;
            rootVisualElement.Q<Button>("cancelButton").clicked += Close;
            rootVisualElement.Q<Button>("applyButton").clicked += () =>
            {
                var settings = FacialBlacklistPreferences.Load();
                settings.Rules = draft.Rules;
                FacialBlacklistPreferences.Save(settings);
                Close();
            };
            RefreshSummary();
        }

        private void RefreshSummary()
        {
            if (matchSummary == null || draft == null) return;
            var blacklist = new BlendShapeBlacklist(draft.Rules);
            matchSummary.text = "有効ルール: " + blacklist.RuleCount + " 件"
                + (shapeNames.Count > 0 ? " ／ 現在のメッシュ: " + shapeNames.Count(blacklist.Matches) + " / " + shapeNames.Count + " 項目が一致" : "");
        }
    }
}
