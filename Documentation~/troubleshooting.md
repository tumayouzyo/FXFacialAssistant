# トラブルシューティング

## メニューが表示されない

Unity Consoleのコンパイルエラーを確認してください。VRChat Avatars SDKが必要です。Assets版とPackage Manager版を同時に導入するとAssemblyの重複になります。

## アバターやFXが見つからない

Project内のPrefabではなく、Hierarchy上のアバターを指定します。DescriptorのPlayable LayersにカスタムFX Controllerが必要です。`Left Hand` / `Right Hand` は完全一致で検出します。見つからない場合、ツールはレイヤーを自動作成しません。

## PNG保存時にVRCPackageSettingsのpath例外が出る

SDK 3.10.6のImport Workerで、`VRCPackageSettings.EnsurePathExists` がnullのパスを扱い、`ArgumentNullException: path` が発生する場合がある。

この問題の回避を目的に、SDKや外部アセットを変更しない。以前案内していたSDKの局所修正は対処手順から撤回した。

例外が発生した場合は、Consoleの内容とPNGの取り込み結果を確認する。ファイルが作成されていても、Unityへの正常な取り込みまで成功したとは限らない。拡張側だけで対応できない場合は、外部アセットを変更しないことを優先する。SDKのコードを書き換える手順や、例外を隠す処理は採用しない。

## アイコンの画角・色・透過が期待と違う

アイコンは35mm判の縦画角を基準に正方形へクロップした画角です。プレビュー上で画角を調整してください。透過は黒・白背景の描画から復元します。屈折や背景依存のシェーダーは通常の透過と異なる結果になる場合があります。

既存PNGのImport Settingsは、透過アイコンを上書き保存したときに更新します。一括変更は行いません。
