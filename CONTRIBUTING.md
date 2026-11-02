# 開発について

Unity 2022.3とVRChat Avatars SDKを導入した検証用プロジェクトで作業してください。シーンや共有FX Controllerを編集する前にバックアップを取ります。

## ローカルパッケージとして導入

Package Managerの `Add package from disk...` で、このリポジトリの `package.json` を選択します。Assets版との二重導入は避けてください。

## テスト

`Editor/Tests` にNUnit形式のEditModeテストがあります。Package Managerで導入した場合は、プロジェクトの `Packages/manifest.json` の `testables` に次を追加してください。

```json
"testables": ["io.github.tumayouzyo.fx-facial-assistant"]
```

Unity Test Frameworkを導入し、Test Runnerから `TY.FXFacialAssistant.Editor.Tests` を実行します。テストは専用プレビューシーンと、Assets直下に作る一意の一時フォルダを使用し、終了時に削除します。テストランナーによるシーン保存確認を避けるため、検証用プロジェクトで実行してください。

SDK 3.10.6の画像インポート時の既知問題は、[対処方法](Documentation~/troubleshooting.md)を参照してください。

## 変更の方針

- UI、編集状態、描画、Animation生成、保存、FX参照の責務を分けます。
- Editor専用コードはEditor Assembly内に置きます。
- 元アバターの値と既存アセットのGUIDを維持します。
- 読めば分かる処理のコメントを増やさず、必要な理由を短く残します。
- 修正範囲に合う検証を行い、再現手順・期待結果・確認した環境を記載します。
