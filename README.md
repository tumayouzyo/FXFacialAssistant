# FX Facial Assistant
![Unity](https://img.shields.io/badge/Unity-2022.3-222c37?logo=unity)
![VRChat](https://img.shields.io/badge/VRChat-Avatars_3.0-19b9e7)
![License](https://img.shields.io/badge/License-MIT-green)

**責任者:** [tumayouzyo](https://github.com/tumayouzyo)  
**作成者:** Codex

VRChatアバターの表情を見ながらBlendShapeを調整し、表情Animationと透過アイコンを作成するUnity Editor拡張。

**メニュー:** `Tools > FXFacialAssistant`

## できること

- 元アバターの値を変更しない専用プレビューで、1メッシュのBlendShapeを編集
- 35mm判換算の焦点距離、ズーム、高さ、Y軸回転による画角調整
- 固定表情、または指定時の値から10フレームまでの直線補間をAnimationClipへ記録
- 同じ画角の256×256透過PNGを、Expression Menu用のImport Settingsで保存
- DescriptorのFX Controllerから `Left Hand` / `Right Hand` を検出し、ステートへClipを割り当て
- SubStateMachineの折りたたみ表示と、割り当て変更のUndo
- 編集可能なブラックリストで記録対象を初期化し、対象項目を非表示に切り替え
- 既存Clipから選択メッシュの表情を読み込み

## 対応環境

| 項目 | 確認した環境 |
| --- | --- |
| Unity | 2022.3.22f1 |
| VRChat SDK | Avatars 3.10.6 |
| 描画 | Built-in Render Pipeline |
| 利用場所 | Unity Editorのみ |

先にVCCなどでAvatars SDKを導入する。SDK 3.10.6のImport Workerで確認した既知問題と対処方法は、[トラブルシューティング](Documentation~/troubleshooting.md)に記載。

## インストール

1. [Releases](https://github.com/tumayouzyo/FXFacialAssistant/releases/latest) から最新の `FXFacialAssistant_*.unitypackage` をDL、Import。
2. `Tools > FXFacialAssistant` を開く。

インポート先は `Assets/TY_FXFacialAssistant` 。`git clone`でも。

## 基本操作

1. `Tools > FXFacialAssistant` を開き、Hierarchyのアバターを指定する。
2. BlendShapeを持つ `Body` があれば自動選択する。必要に応じて「編集メッシュ」で指定。
3. プレビューを見ながら、BlendShapeの値と記録チェックを調整する。
4. 保存名を入力し、「表情アニメーションを保存」「透過アイコンを保存」をそれぞれ押す。
5. FXへ割り当てる場合は、左右のステート欄へAnimationClipを指定する。

| プレビュー操作 | 動作 |
| --- | --- |
| 右ボタンドラッグ | アバターのローカルY軸を中心に周回 |
| ホイール | 距離調整 |
| 中ボタンドラッグ | 高さ調整。上へドラッグするとカメラが下がる |
| 高さスライダー | 高さ調整。中ボタンドラッグと同期 |
| 焦点距離 | 初期値85mm、20～200mm |

各行末尾の `10f` チェックは、デフォルト表情から編集値までを0～10フレーム、60fpsで記録する。オフの場合は固定表情。詳細説明はマウスオーバーでも表示する。

## 追加操作

- 画角の「保存」「適用」で、距離・高さ・回転・焦点距離を保存／復元
- 「デフォルト表情を指定」と「読み込み」「上書き」で、基準表情と10f記録の始点を設定
- 「編集項目のみチェック」で、デフォルトと値が異なる表示項目だけを記録対象に
- 「empty自動設定」で、各Handレイヤーと配下のNoneステートへ `proxy_empty` を設定（Undo対応）
- 「クリーンアップ」で、Broken text PPtrの原因となるFX内のローカル参照切れを修復（確認・バックアップ付き）
- 設定で日本語／英語、FA_接頭辞の有無を切り替え

## 保存先

初期値はFX Controllerと同じフォルダ内の `Facial` 。Assets内の任意フォルダへ変更できる。

```text
Facial/
  FA_笑顔.anim
  FA_笑顔.png
```

## ブラックリスト

初期ルールは `vrc.*`、`vrm.*`、`ex_*` と、ARKit・VRCFTの標準名など。**大文字・小文字は区別しない。**

- 上部の「設定」で、1行ごとのルールを追加・削除
- `*` は任意の文字列、`?` は任意の1文字に一致
- 「ブラックリスト適用」で一致項目の記録チェックをオフに
- 「非表示」で一致項目を隠し、記録からも除外

## 使用上の注意

- FXステートへの割り当てはControllerを直接編集する。共有Controllerを使う他のアバターにも反映される。
- レイヤーやステートを自動作成せず、Transition・Parameter・Write Defaultsも変更しない。
- BlendTree、同期レイヤー、AnimatorOverrideControllerの編集には未対応。
- Clipの読み込みは最終時刻のBlendShape値を取得する。未収録のBlendShapeは0にする。元のキー配置・補間をそのまま保持する機能ではない。
- プレビューは描画用の階層を作成する。PhysBone、Constraint、NDMFなどのビルド処理や特殊なシーン依存描画は再現しない。

詳しくは [操作マニュアル](Documentation~/manual.md) と [トラブルシューティング](Documentation~/troubleshooting.md) を参照。

## 開発・不具合報告

不具合は [Issues](https://github.com/tumayouzyo/FXFacialAssistant/issues) へ。開発時のテスト手順は [CONTRIBUTING](CONTRIBUTING.md) に記載。

## ライセンス

[MIT License](LICENSE)

機能・レイアウトの参考元: https://github.com/gatosyocora/VRCAvatarEditor
