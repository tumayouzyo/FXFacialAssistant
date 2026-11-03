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
            titleContent = new GUIContent(FacialLocalization.Text("FXFacialAssistant 設定"));
            rootVisualElement.Clear();
            var script = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            var path = Path.GetDirectoryName(script).Replace('\\', '/') + "/FacialBlacklistSettingsWindow.uxml";
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            if (!tree) { rootVisualElement.Add(new HelpBox(FacialLocalization.Format("設定UIが見つかりません: {0}", path), HelpBoxMessageType.Error)); return; }
            tree.CloneTree(rootVisualElement);
            var general = rootVisualElement.Q("generalSettings");
            var languageGroup = new VisualElement();
            languageGroup.AddToClassList("settings-group");
            var languageHeading = new Label("表示言語");
            languageHeading.AddToClassList("settings-heading");
            languageGroup.Add(languageHeading);
            var languages = new VisualElement();
            languages.AddToClassList("settings-language-buttons");
            languages.Add(new Button(() => ChangeLanguage(false)) { text = "日本語" });
            languages.Add(new Button(() => ChangeLanguage(true)) { text = "English" });
            languageGroup.Add(languages);
            general.Add(languageGroup);
            var about = rootVisualElement.Q("aboutSettings");
            about.Add(new Label("FX Facial Assistant") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            var credit = new Label("責任者: tumayouzyo / 作成者: Codex");
            credit.AddToClassList("footer-credit");
            about.Add(credit);
            var links = new VisualElement();
            links.AddToClassList("footer-links");
            links.Add(new Button(() => Application.OpenURL("https://github.com/tumayouzyo/FXFacialAssistant")) { text = "GitHub" });
            links.Add(new Button(() => Application.OpenURL("https://github.com/tumayouzyo/FXFacialAssistant/issues")) { text = "連絡先・不具合報告: GitHub Issues" });
            links.Add(new Button(() => Application.OpenURL("https://github.com/tumayouzyo/FXFacialAssistant/blob/main/LICENSE")) { text = "MITライセンス全文" });
            about.Add(links);
            var details = new Foldout { text = "ライセンス・注意事項", value = false };
            details.AddToClassList("footer-details");
            var detailsScroll = new ScrollView();
            detailsScroll.AddToClassList("footer-details-scroll");
            details.Add(detailsScroll);
            about.Add(details);
            var terms = new Label("MITライセンスです。個人・商用利用、改変、再配布が可能です。再配布時は著作権表示とライセンス文を保持してください。無保証のため、ご自身の責任でご利用ください。作業前のバックアップを推奨します。第三者のアセットは各権利者の規約に従ってください。生成したアニメーション・アイコンへのクレジットは不要です。個別サポートや更新の継続は保証していません。");
            terms.style.whiteSpace = WhiteSpace.Normal;
            terms.AddToClassList("settings-description");
            detailsScroll.Add(terms);
            var reference = new Label("機能・レイアウトの参考元はVRCAvatarEditor (gatosyocora)です。コードの直接引用は行っていません。共同開発・公認を示すものではありません。");
            reference.style.whiteSpace = WhiteSpace.Normal;
            reference.AddToClassList("settings-description");
            detailsScroll.Add(reference);
            detailsScroll.Add(new Button(() => Application.OpenURL("https://github.com/gatosyocora/VRCAvatarEditor")) { text = "VRCAvatarEditor / gatosyocora" });
            var rules = rootVisualElement.Q<TextField>("rulesText");
            rules.SetValueWithoutNotify(draft.Rules);
            rules.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != rules) return;
                draft.Rules = evt.newValue;
                RefreshSummary();
            });
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
            FacialLocalization.Translate(rootVisualElement);
        }

        private void ChangeLanguage(bool english)
        {
            FacialToolPreferences.English = english;
            CreateGUI();
        }

        private void RefreshSummary()
        {
            if (matchSummary == null || draft == null) return;
            var blacklist = new BlendShapeBlacklist(draft.Rules);
            matchSummary.text = FacialLocalization.Format("有効ルール: {0} 件", blacklist.RuleCount)
                + (shapeNames.Count > 0 ? FacialLocalization.Format(" ／ 現在のメッシュ: {0} / {1} 項目が一致",
                    shapeNames.Count(blacklist.Matches), shapeNames.Count) : "");
        }
    }
}
