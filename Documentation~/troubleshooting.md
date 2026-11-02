# トラブルシューティング

## メニューが表示されない

Unity Consoleのコンパイルエラーを確認してください。VRChat Avatars SDKが必要です。Assets版とPackage Manager版を同時に導入するとAssemblyの重複になります。

## アバターやFXが見つからない

Project内のPrefabではなく、Hierarchy上のアバターを指定します。DescriptorのPlayable LayersにカスタムFX Controllerが必要です。`Left Hand` / `Right Hand` は完全一致で検出します。見つからない場合、ツールはレイヤーを自動作成しません。

## PNG保存時にVRCPackageSettingsのpath例外が出る

開発環境のSDK 3.10.6では、Import Worker内で `PackageInfo.FindForAssembly` がnullを返し、SDKの `VRCPackageSettings.EnsurePathExists` が `Directory.CreateDirectory(null)` を呼ぶ問題を確認しました。新規保存と上書きのどちらでも発生し得ます。

このパッケージはSDK本体を同梱・自動改変しません。開発環境では、SDKの以下のファイルに局所修正を入れて検証しました。

```text
Packages/com.vrchat.base/Editor/VRCSDK/Dependencies/VRChat/VRCPackageSettings.cs
```

SDKファイルのバックアップを取ったうえで、`GetPathFromType` 内のパッケージ情報がnullならnullを返す処理を、SDKの正規保存先へフォールバックする処理に変更します。

```csharp
var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(t.Assembly);
var packageName = packageInfo != null ? packageInfo.name : "com.vrchat.base";
return Path.Combine(PackageSettingsPath, packageName, "settings.json");
```

これにより既存の `ProjectSettings/Packages/com.vrchat.base/settings.json` を読み、設定値を維持できます。SDK更新で修正が上書きされる可能性があります。異なるSDKバージョンでの適用は、実際のコードと例外箇所を確認して判断してください。

## アイコンの画角・色・透過が期待と違う

アイコンは35mm判の縦画角を基準に正方形へクロップした画角です。プレビュー上で画角を調整してください。透過は黒・白背景の描画から復元します。屈折や背景依存のシェーダーは通常の透過と異なる結果になる場合があります。

既存PNGのImport Settingsは、透過アイコンを上書き保存したときに更新します。一括変更は行いません。
