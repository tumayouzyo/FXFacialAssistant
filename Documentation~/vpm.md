# VPM配布

パッケージID: `io.github.tumayouzyo.fx-facial-assistant`

共通一覧: `https://tumayouzyo.github.io/vpm-repository/index.json`

## リリース

1. `package.json` の `version` と `url` を同じバージョンへ更新する。
2. CHANGELOGへ変更内容を記載し、検証済みの変更をmainへpushする。
3. GitHub Actionsの「Release package」をmainに対して手動実行する。

VPM用ZIP、Unitypackage、package.jsonがGitHub Releaseに添付される。同じタグの上書きは行わない。通常のpushではパッケージを公開しない。

共通一覧は約30分ごとに更新される。即時反映する場合は、vpm-repositoryの「Publish VPM listing」を手動実行する。

## ローカル確認

`python scripts/build_package.py` で `dist/` に配布ファイルを生成する。生成処理はSDKやUnityプロジェクトを変更しない。

ZIPのルートにpackage.jsonとEditorを配置し、Tests、SDK、作業用スクリプトは収録しない。Unitypackageは従来のAssets配下へ配置する形式で生成する。

## 移行

`legacyFolders` は旧Assets版のEditorだけを対象とする。フォルダGUIDで移動先を探さず、指定パスにある場合だけ移行する。親フォルダのユーザーアセットは削除しない。既存のコード・UIアセットのGUIDを維持する。

## 対応範囲

Unity 2022.3、VRChat Avatars SDK 3.10.x。異なるSDK系列への対応範囲は、その系列で検証してから拡張する。
