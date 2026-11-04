using System.Linq;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace TY.FXFacialAssistant
{
    internal static class FacialLocalization
    {
        private static readonly string[,] Entries = {
            { "補間", "Interpolation" },
            { "指定したアニメーションの参照位置を0～100%で指定します。適用がオンの場合、選択した位置のBlendShape値を反映します。", "Select a position from 0 to 100% of the animation. When Apply is enabled, apply the BlendShape values at this position." },
            { "オンにすると、指定アニメーションの選択位置の値を反映します。オフにしても現在の編集値は維持します。", "Enable to apply values at the selected animation position. Disabling keeps the current edited values." },
            { "{0}項目を{1:0.#}%の位置から読み込みました。", "Loaded {0} shapes at {1:0.#}% of the animation." },
            { "接頭辞", "Prefix" },
            { "クリア", "Clear" },
            { "オンにすると、設定で指定した接頭辞をアニメーションとPNGの保存名に付けます。", "Add the prefix from Settings to animation and PNG file names when enabled." },
            { "読み込み用のアニメーション指定をNoneにし、接頭辞をオンにします。編集中の表情と保存先は変更しません。", "Clear the animation selection to None and enable the prefix. Keep the current expression and output folder." },
            { "AnimationClipをドラッグ＆ドロップできます。指定すると保存先をそのアニメーションのフォルダに変更し、接頭辞をオフにします。", "Drag and drop an AnimationClip here. Selecting a clip sets its folder as the output folder and disables the prefix." },
            { "保存先をFX Controllerと同じ場所のFacialフォルダに戻し、接頭辞をオンにします。", "Reset the output folder to the Facial folder beside the FX Controller and enable the prefix." },
            { "このレイヤーと配下のSubStateMachineで、MotionがNoneのステートにSDKのproxy_emptyを設定します。既存のMotionは変更しません。Ctrl+Zで元に戻せます。", "Assign the SDK proxy_empty clip to states whose Motion is None in this layer and its nested SubStateMachines. Existing Motions are preserved. Undo with Ctrl+Z." },
            { "Broken text PPtrエラーの原因となる、FX Controller内の存在しないローカル参照をクリーンアップします。外部アセットへの参照は対象外です。実行前に確認し、元ファイルとmetaをLibrary/TY_FXFacialAssistantBackupに保存します。", "Clean up missing local references within the FX Controller that can cause Broken text PPtr errors. References to external assets are excluded. Confirmation is required, and the original file and its meta are backed up to Library/TY_FXFacialAssistantBackup before cleanup." },
            { "現在の保存先フォルダをProjectウィンドウでハイライトします。フォルダがまだ存在しない場合は案内を表示します。この操作ではフォルダを作成しません。", "Highlight the current output folder in the Project window. If the folder does not exist yet, display a notice. This action does not create the folder." },
            { "同名のBlendShapeがあるため記録できません: {0}", "Duplicate BlendShape name: {0}" },
            { "同じ階層に同名オブジェクトがあるためAnimationの対象を特定できません: {0}", "Duplicate sibling names prevent unambiguous animation paths: {0}" },
            { "保存先フォルダを作成できませんでした: {0}", "Could not create output folder: {0}" },
            { "UIファイルが見つかりません: {0}", "UI file not found: {0}" },
            { "設定UIが見つかりません: {0}", "Settings UI not found: {0}" },
            { "FX設定が変わりました。アバターを再読み込みしてください。 {0}", "FX configuration changed. Reload the avatar. {0}" },
            { "empty設定: {0}", "Empty states filled: {0}" },
            { "{0} のAnimationを変更しました。Ctrl+Zで元に戻せます。", "Animation for {0} changed. Undo with Ctrl+Z." },
            { "ブラックリスト設定を適用しました。記録チェックをオフにした項目: {0}", "Blacklist applied. Recording checks cleared: {0}" },
            { "Animationを保存しました: {0}", "Animation saved: {0}" },
            { "アイコンを保存しました: {0}", "Icon saved: {0}" },
            { "クリーンアップ: {0}", "Cleaned references: {0}" },
            { "{0}\n\n既存のファイルを上書きしますか？", "{0}\n\nOverwrite the existing file?" },
            { "{0} / 指定時: {1}{2}", "{0} / Default: {1}{2}" },
            { "編集メッシュ: {0}\n記録対象: {1} / {2}\n表示: {3} / 非表示: {4}", "Editing mesh: {0}\nRecording: {1} / {2}\nVisible: {3} / Hidden: {4}" },
            { " 対象外のカーブ {0} 件は読み込んでいません。", " {0} unsupported curves ignored." },
            { " 非表示のブラックリスト項目 {0} 件は記録対象から外しました。", " {0} hidden blacklist entries excluded from recording." },
            { "有効ルール: {0} 件", "Active rules: {0}" },
            { " ／ 現在のメッシュ: {0} / {1} 項目が一致", " / Current mesh: {0} / {1} matching items" },
            { "ファイル名", "File names" },
            { "ブラックリスト", "Blacklist" },
            { "アニメーションとPNGに共通で使用する接頭辞です。初期値はFA_です。", "Prefix shared by animation and PNG file names. The default is FA_." },
            { "保存名欄の「接頭辞」をオンにすると、ここで指定した文字を先頭に付けます。", "Enable Prefix next to the save name to add this text." },
            { "接頭辞にはファイル名に使える文字を1～100文字入力してください。末尾にピリオドは使えません。", "Enter a prefix of 1 to 100 valid file name characters. A trailing period is not allowed." },
            { "表示言語", "Display language" },
            { "ライセンス・注意事項", "License and notices" },
            { "MITライセンスです。個人・商用利用、改変、再配布が可能です。再配布時は著作権表示とライセンス文を保持してください。無保証のため、ご自身の責任でご利用ください。作業前のバックアップを推奨します。第三者のアセットは各権利者の規約に従ってください。生成したアニメーション・アイコンへのクレジットは不要です。個別サポートや更新の継続は保証していません。", "MIT License. Personal and commercial use, modification and redistribution are allowed. Retain the copyright and license notice when redistributing. Provided without warranty; use at your own risk. Please back up your work and follow the terms of third-party assets. No credit is required for generated animations or icons. Individual support and continued updates are not guaranteed." },
            { "機能・レイアウトの参考元はVRCAvatarEditor (gatosyocora)です。コードの直接引用は行っていません。共同開発・公認を示すものではありません。", "Features and layout inspired by VRCAvatarEditor (gatosyocora). No source code was copied. No affiliation or endorsement is implied." },
            { "保存した位置・回転・焦点距離を現在のプレビューに適用します。", "Apply the saved position, orbit and focal length to the current preview." },
            { "ブラックリストに一致する項目を一覧から隠し、Animationの記録対象からも外します。オフにすると再表示しますが、記録チェックはオフのままです。BlendShapeの値とプレビューは変わりません。", "Hide matching shapes and exclude them from recording. Showing them again keeps their recording checks off. Values and preview are preserved." },
            { "適用すると、現在のメッシュで一致する項目もオフになります。ルールから外した項目のチェックは維持します。全体を初期化するにはアバターを再読み込みしてください。設定はこのプロジェクト用として保存します。", "Applying clears recording checks for matching shapes. Other checks are preserved. Reload the avatar to reset all choices. Settings are stored per project." },
            { "設定に保存されたブラックリストを適用し、一致する項目の記録チェックをオフにします。他の項目のチェックと、編集中のBlendShape値は維持します。ルールの編集は上部の設定から行えます。", "Uncheck shapes matching the saved blacklist. Other recording choices and all editing values are preserved. Edit rules in Settings." },
            { "Hierarchyで現在選択しているオブジェクトのSkinnedMeshRendererを編集対象にします。指定中のアバター配下で、BlendShapeを持つメッシュを選んでください。", "Use the selected object's SkinnedMeshRenderer. Select a mesh with BlendShapes inside the current avatar." },
            { "各行の末尾チェックをオンにすると、メッシュ指定時の値から編集した表情までを0～10フレーム（60fps、約0.167秒）で直線補間して記録します。オフの場合は固定値を記録します。", "Enable the last checkbox to record a linear transition from the default expression over frames 0-10 (60 fps). Otherwise record a constant value." },
            { "1行に1つずつルールを入力してください。大文字・小文字は区別しません。* は任意の文字列、? は任意の1文字に一致します。空行と # で始まる行は無視します。", "One rule per line, case-insensitive. * matches any text; ? matches one character. Blank lines and # comments are ignored." },
            { "AnimatorOverrideControllerには未対応です。通常のAnimatorControllerを指定してください。", "AnimatorOverrideController is unsupported. Use an AnimatorController." },
            { "FX Controller内の存在しないローカル参照のみ解除します。実行前にバックアップをLibraryに保存します。実行しますか？", "Clear only missing local references in the FX Controller. A backup is saved in Library. Continue?" },
            { "表示の切り替えと再適用は、メインウィンドウのメッシュ一覧上にある「非表示」「ブラックリスト適用」から行えます。", "Use Hide and Apply blacklist above the main window's shape list to change visibility or reapply rules." },
            { "現在表示しているBlendShapeの記録チェックを一括で切り替えます。非表示の項目は記録対象になりません。", "Toggle recording for all visible BlendShapes. Hidden items are excluded." },
            { "Headボーンが取得できないため、DescriptorのView Positionを基準に表示しています。", "Head bone unavailable. Using the Descriptor View Position." },
            { "現在のすべてのBlendShape値で上書きします。他のカーブは維持します。別名でコピーしたアニメーションの使用を推奨します。上書きしますか？", "Overwrite with all current BlendShape values. Other curves are preserved. Using a separately named copy of the animation is recommended." },
            { "Animationとアイコンの保存先は、このプロジェクトのAssetsフォルダ内を選択してください。", "Choose an output folder inside this project's Assets folder." },
            { "Avatar DescriptorのPlayable Layersがカスタマイズされていません。", "Playable Layers are not customized on the Avatar Descriptor." },
            { "Avatar DescriptorにカスタムFX Controllerが設定されていません。", "No custom FX Controller on the Avatar Descriptor." },
            { "右ドラッグで回転し、中ボタンドラッグで高さを調整できます。ホイールを上へ回すと近づき、下へ回すと離れます。", "Right-drag to orbit. Middle-drag to move vertically. Scroll up to zoom in, down to zoom out." },
            { "Project内のPrefabではなく、Hierarchy上のアバターを指定してください。", "Select an avatar in the Hierarchy instead of a Prefab asset." },
            { "保存先フォルダはまだありません。Animationまたはアイコンを保存すると作成されます。", "The output folder does not exist yet. Saving an animation or icon creates it." },
            { "ブラックリストに一致しているため、初期状態ではチェックが外れています。手動でチェックするとアニメーションに記録します。", "Blacklisted (unchecked initially). Check manually to record it." },
            { "同じオブジェクトに複数のSkinnedMeshRendererがあるため記録できません。", "Multiple SkinnedMeshRenderers on the same object prevent unambiguous recording." },
            { "未指定の場合はメッシュ指定時の値を使用します。指定したクリップにない項目は0になります。10f記録の始点にも使用します。", "Without a clip, use the original captured values. Missing shapes in a clip are zero. Also used as the start of 10-frame transitions." },
            { "35mm判（36×24mm）の縦画角を基準に、中央を正方形に切り抜きます。初期値は85mmです。", "Vertical field of view for 36 x 24 mm film, cropped to a square. Default: 85 mm." },
            { "SDKのAV3 Demo Assets内でproxy_emptyを一意に取得できませんでした。", "Could not uniquely locate proxy_empty in the SDK AV3 Demo Assets." },
            { "デフォルト表情から現在の表情までを0～10f（60fps）で直線補間します。チェックを外すと固定値で記録します。", "Linear transition from the default expression to current values over frames 0-10 at 60 fps. Unchecked: constant." },
            { "BlendShapeを持つBodyが見つかりません。編集メッシュを指定してください。", "No Body with BlendShapes found. Select a mesh." },
            { "BlendShapeを持つSkinnedMeshRendererを指定してください。", "Select a SkinnedMeshRenderer with BlendShapes." },
            { "保存名を入力してください。ファイル名に使えない文字や末尾のピリオドは使用できません。", "Enter a valid file name without invalid characters or a trailing period." },
            { "アニメーションとPNGを同じフォルダに保存します。", "Animation and PNG files are saved in the same folder." },
            { "例：vrc.* は前方一致、eyeBlinkLeft は名前全体との一致です。", "Example: vrc.* matches a prefix; eyeBlinkLeft matches the entire name." },
            { "FX Controllerが編集可能なAssetsフォルダ内にありません。", "FX Controller must be editable and inside Assets." },
            { "BlendTreeなどのMotionは、このツールでは置き換えません。", "This tool does not replace BlendTrees or other Motion types." },
            { "オブジェクト名に / があるためAnimationのパスを作れません。", "Object names containing / cannot be used in animation paths." },
            { "保存先は、このプロジェクトのAssetsフォルダ内を指定してください。", "The output folder must be inside this project's Assets folder." },
            { "現在の編集値と記録対象をリセットして、元アバターから読み直しますか？", "Reset current edits and recording choices, then reload from the source avatar?" },
            { "Assetsフォルダ内の編集可能なFX Controllerを指定してください。", "Select an editable FX Controller inside Assets." },
            { "FX Controllerがアセットとして保存されていません。", "FX Controller is not a saved asset." },
            { "FX Controllerとステートを再読み込みしてください。", "Reload the FX Controller and state." },
            { "ステートが指定したFX Controllerに属していません。", "The state does not belong to this FX Controller." },
            { "メッシュが変更されました。アバターを再読み込みしてください。", "The mesh changed. Reload the avatar." },
            { "保存するAnimationまたはアイコンを指定してください。", "Specify an animation or icon to save." },
            { "記録するBlendShapeを1つ以上チェックしてください。", "Check at least one BlendShape to record." },
            { "保存先に別の種類のアセット、または同名フォルダがあります。", "The destination contains a different asset type or a folder with this name." },
            { "デフォルト表情と値が異なる表示項目だけを記録対象にします。", "Record only visible shapes that differ from the default expression." },
            { "現在の位置・回転・焦点距離を保存します。次回起動時に復元します。", "Save the current position, orbit and focal length for the next session." },
            { "責任者: tumayouzyo / 作成者: Codex", "Maintainer: tumayouzyo / Created by: Codex" },
            { "BlendTreeなどのMotionは置き換えません。", "BlendTrees and other Motion types are preserved." },
            { "読み込むAnimationClipを指定してください。", "Select an AnimationClip to load." },
            { "アイコンをTextureとして読み込めませんでした。", "Could not import the icon as a texture." },
            { "Assetsフォルダ内の編集可能なデフォルト表情を指定してください。", "Select an editable default-expression clip inside Assets." },
            { "Hierarchyからアバターを指定してください。", "Select an avatar from the Hierarchy." },
            { "指定したアバター配下のメッシュを選択してください。", "Select a mesh inside the specified avatar." },
            { "連絡先・不具合報告: GitHub Issues", "Contact and bug reports: GitHub Issues" },
            { "保存先フォルダ名に無効な文字が含まれています。", "The output folder name contains invalid characters." },
            { "同期レイヤーのMotion編集には未対応です。", "Editing Motion on synced layers is unsupported." },
            { "テキスト形式のControllerのみ対応しています。", "Only text-serialized controllers are supported." },
            { "既存のAnimationが読み取り専用です。", "The existing animation is read-only." },
            { "アバターと編集メッシュを指定してください。", "Select an avatar and an editing mesh." },
            { "プレビューするアバターを指定してください。", "Select an avatar to preview." },
            { "保存名は100文字以内で入力してください。", "File names must be at most 100 characters." },
            { "右：回転 / 中：高さ / ホイール：距離", "Right: orbit / Middle: height / Wheel: distance" },
            { " 再保存時は固定値／直線補間になります。", " Saving uses constant values or linear interpolation." },
            { "FXFacialAssistant 設定", "FXFacialAssistant Settings" },
            { "編集対象のレイヤーが存在しないか、同期レイヤーに設定されています。", "The target layer is missing or synced." },
            { "Animationとアイコンの保存先", "Animation and icon output folder" },
            { "プレビューを再読み込みしてください。", "Reload the preview." },
            { "有効なメッシュを選択してください。", "Select a valid mesh." },
            { "この名前のレイヤーはありません。", "No layer with this name." },
            { "先にアバターを指定してください。", "Select an avatar first." },
            { "アニメーションに記録します。", "Record in animation" },
            { "PNGの生成に失敗しました。", "PNG generation failed." },
            { " / ブラックリストに一致", " / Blacklisted" },
            { "デフォルト表情を保存しました。", "Default expression saved." },
            { "表情アニメーションを保存", "Save expression animation" },
            { "BlendShapeの記録ルール（ブラックリスト）", "BlendShape recording rules (blacklist)" },
            { "Headボーンを基準にカメラを上下に移動します。", "Vertical offset from the Head bone" },
            { "アニメーション読み込み", "Load animation" },
            { "デフォルト表情の上書き", "Overwrite default expression" },
            { "MITライセンス全文", "Full MIT License" },
            { "デフォルト表情を指定", "Default expression" },
            { "同名ファイルの上書き", "Overwrite existing file" },
            { "編集項目のみチェック", "Check edited only" },
            { "すべてチェック", "Check all" },
            { "選択中メッシュを指定", "Use selected mesh" },
            { "FXクリーンアップ", "Clean up FX" },
            { "empty自動設定", "Fill empty" },
            { "アバター再読み込み", "Reload avatar" },
            { "ブラックリスト適用", "Apply blacklist" },
            { "保存先をハイライト", "Highlight output folder" },
            { "焦点距離 (mm)", "Focal length (mm)" },
            { "編集メッシュ未選択", "No editing mesh selected" },
            { "透過アイコンを保存", "Save transparent icon" },
            { "初期ルールに戻す", "Restore default rules" },
            { "画角を保存しました。", "Camera view saved." },
            { "編集対象FX: ", "FX Controller: " },
            { "クリーンアップ", "Clean up" },
            { "適用して閉じる", "Apply and close" },
            { "フォルダ選択", "Choose folder" },
            { "編集メッシュ", "Editing mesh" },
            { "FX未設定", "FX not configured" },
            { "キャンセル", "Cancel" },
            { "表情の編集", "Edit expression" },
            { "アバター", "Avatar" },
            { "リセット", "Reset" },
            { "読み込み", "Load" },
            { "上書き", "Overwrite" },
            { "保存先", "Output folder" },
            { "保存名", "File name" },
            { "日本語", "日本語" },
            { "最大値", "Max" },
            { "最小値", "Min" },
            { "非表示", "Hide" },
            { "実行", "Run" },
            { "既定", "Default" },
            { "正面", "Front" },
            { "設定", "Settings" },
            { "保存", "Save" },
            { "適用", "Apply" },
        };
        public static string Text(string value)
        {
            return TranslateText(value, FacialToolPreferences.English);
        }
        private static readonly Dictionary<string, string> Translations = BuildTranslations();
        private static Dictionary<string, string> BuildTranslations()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < Entries.GetLength(0); i++) result[Entries[i, 0]] = Entries[i, 1];
            return result;
        }
        internal static string TranslateText(string value, bool english)
            => english && value != null && Translations.TryGetValue(value, out var translated) ? translated : value;
        public static string Format(string template, params object[] arguments) => string.Format(Text(template), arguments);
        public static bool Dialog(string title, string message, string ok, string cancel)
            => EditorUtility.DisplayDialog(Text(title), Text(message), Text(ok), Text(cancel));
        public static void Translate(VisualElement root)
        {
            var english = FacialToolPreferences.English;
            foreach (var element in root.Query<VisualElement>().ToList().Prepend(root))
            {
                if (element.ClassListContains("user-text") || element.ClassListContains("unity-object-field-display")
                    || element.ClassListContains("shape-name") || element.ClassListContains("state-name")
                    || element.name == "meshSummary" || element.name == "rulesText") continue;
                if (element.GetFirstAncestorOfType<TextField>() != null && !(element is Label)) continue;
                element.tooltip = TranslateText(element.tooltip, english);
                if (element is TextElement text)
                    ((INotifyValueChanged<string>)text).SetValueWithoutNotify(TranslateText(text.text, english));
            }
        }
    }
}
