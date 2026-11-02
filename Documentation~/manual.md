# FXFacialAssistant

VRChatアバターの表情を専用プレビューで編集し、AnimationClipと256×256の透過PNGをそれぞれ保存するUnity Editor拡張です。

## 使い方

1. `Tools > FXFacialAssistant` を開き、Hierarchyのアバターを「アバター」欄へ指定します。
2. BlendShapeを持つ `Body` があれば自動選択されます。それ以外は「編集メッシュ」へSkinnedMeshRendererを指定するか、Hierarchyで選択して「選択中メッシュを指定」を押します。編集対象は1メッシュです。
3. 焦点距離（35mm判換算）・右側の上下スライダーで画角を調整します。焦点距離の初期値は85mm、調整範囲は20～200mmです。プレビュー上のマウスホイールで距離を調整し、上へ回すと接近、下へ回すと離れます。中ボタン（ホイール）を押して上下にドラッグしても高さを調整でき、上へドラッグするとカメラの基準が下がります。高さスライダーと値は同期します。右ドラッグでアバターローカルY軸を中心に周回し、「正面」で戻します。
4. BlendShapeを調整します。左端のチェックがAnimationへの記録対象です。未チェックでもプレビューの値は変わらず、アイコンには見えている表情が写ります。
5. 各行の右端をチェックすると、その項目を「メッシュ指定時の値 → 編集した値」として0～10フレーム、60fpsで直線補間して記録します。未チェックの項目は最初から固定値です。最小値・最大値は0・100、数値欄では範囲外の値も入力できます。
6. 保存名を入力し、「表情Animationを保存」または「透過アイコンを保存」を押します。それぞれ独立して保存し、押した側と同名のファイルがある場合だけ上書きを確認します。「保存先をハイライト」は、現在の保存先フォルダをProjectウィンドウで示します。フォルダがまだない場合は案内を表示し、保存時に作成します。3つのボタン幅は順に39.2%・39.2%・19.6%で、ボタン間に1%ずつ余白を取ります。
7. 左右のステート欄へAnimationClipを指定すると、DescriptorのFX Controllerを直接編集します。Ctrl+Z / Ctrl+Yで割り当てを元に戻す／やり直すことができます。

## 記録チェックのブラックリスト

上部の「設定」からブラックリストのルールを編集できます。表示方法と再適用は、メッシュ一覧上の「ブラックリスト適用」ボタンと「非表示」チェックで操作します。メッシュ指定時は、一致する項目だけ記録チェックをオフにし、その他の項目をチェックします。元アバターやプレビューのBlendShape値は変えません。

- 「非表示」オフ: 全項目を表示します。一致した項目も、手動でチェックすれば記録できます。Clip読み込みでは、読み込んだカーブのチェックを反映します。
- 「非表示」オン: 一致した項目を隠し、記録からも除外します。Clipに含まれている場合は値だけ読み込み、記録チェックはオフにします。

「ブラックリスト適用」は、保存されているルールを現在のメッシュへ再適用し、一致項目の記録チェックをオフにします。「非表示」をオフへ戻しても、記録チェックは自動でオンには戻りません。設定画面でルールを保存しても、一覧側の表示方法は維持します。

一覧上の `メッシュ名 - n/n` は、記録対象数／全BlendShape数です。`10f` は各行末尾の遷移記録チェックの真上に配置した見出しです。件数、ブラックリスト操作、`10f`、メッシュ指定ボタンなどにマウスを合わせると詳細説明を表示します。

「表示項目をすべて記録」は、現在表示している項目だけに作用します。表示モードでブラックリスト項目も表示している場合は、それらも明示的にチェックできます。非表示モードでは、非表示の項目が一括操作で記録対象に戻ることはありません。

ルールは1行につき1つで、すべて大文字・小文字を区別しません。`eyeBlinkLeft` と `EyeBlinkLeft` は同じ項目として判定します。

```text
vrc.*
vrm.*
VRM_*
ex_*
EyeBlinkLeft
CustomTracking*
```

`*` は任意の文字列、`?` は任意の1文字です。ワイルドカードがなければ名前全体との一致です。空行と `#` で始まる行は無視します。

初期ルールには、上記4つの接頭辞に加え、ARKit / Perfect Sync、VRCFT Unified Expressions、従来のSRanipalの標準名と、一部の左右分割名・表記違いを含みます。任意に改名されたBlendShapeまで自動判定するものではありません。必要な独自名を追加してください。初期名の参照元は [Apple ARKit](https://developer.apple.com/documentation/arkit/arfaceanchor/blendshapelocation/)、[VRCFT Unified Expressions](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/unified-blendshapes)、[VRCFT SRanipal比較表](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/compatibility/vive-sranipal) です。

設定画面では現在のメッシュに一致する項目数を確認できます。「適用して閉じる」で、このプロジェクト用のユーザー設定として保存します。「キャンセル」やウィンドウを閉じた場合は保存しません。「初期ルールに戻す」も、適用するまでは作業中の変更です。

適用時は、現在のメッシュで一致する項目もオフになります。ルールから外した項目のチェックは自動でオンにせず、現在の状態を維持します。全項目を新しいルールで初期化するには、アバターを再読み込みしてください。

## 保存先

初期値はFX Controllerと同じフォルダ内の `Facial` です。初回保存時に作成します。「フォルダ選択」で、このプロジェクトのAssets内の任意フォルダへ変更できます。選択したフォルダへ直接保存します。「FX基準に戻す」で自動指定に戻せます。

```text
FX Controllerのあるフォルダ/
  Facial/
    FA_保存名.anim
    FA_保存名.png
```

アイコンは同じ画角の256×256 RGBA PNGです。Expression Menu用に、Texture TypeはDefault、sRGB、Alpha SourceはInput、Alpha Is Transparency有効、Mip Mapなし、最大256、Wrap ModeはClamp、Filter ModeはBilinear、Read/Write無効にします。圧縮はHigh Quality・品質100・フォーマット自動・Crunchなしです。Standalone / Android / iPhoneの個別オーバーライドは解除し、透過を維持できる自動フォーマットを使用します。SDKのExpression Menuアイコン検査にある最大256px・圧縮有効の条件を満たします。

この設定はアイコンの保存・上書き時に適用します。既に保存済みのアイコンを自動で一括変更することはありません。上書き時は既存アセットのGUIDを維持します。ファイル保存の途中で失敗した場合は既存データの復元を試みます。

SDK 3.10.6のImport Workerで設定パス例外が出る場合は、[トラブルシューティング](troubleshooting.md)を参照してください。

## Animationの読み込み

「Animation読込」欄はClipのドラッグ＆ドロップとオブジェクト選択に対応します。「読み込み」で、選択中メッシュに一致するBlendShapeカーブを最終時刻から読み込みます。一致した項目だけ記録対象にします。読み込まなかった項目の編集値は維持します。

アバターのルートを基準とするメッシュパスで一致判定します。Transform、Material、他メッシュなどのカーブは読み込まず、件数を表示します。時間変化がある項目は遷移チェックを有効にしますが、再保存時は「指定時の値から10fの直線補間」に変換します。元Clipのキー配置や補間をそのまま保持する機能ではありません。

## FXレイヤー

`VRCAvatarDescriptor` のカスタムFX設定を参照します。`Left Hand` / `Right Hand` を完全一致で探し、直下のステートと任意の深さのSubStateMachineを表示します。SubStateMachineは名前付きの折りたたみです。

- レイヤー・ステートの追加、Transition・Parameter・Write Defaultsの変更は行いません。
- BlendTreeなどのMotionは表示のみで、Clipへ置き換えません。
- 同期レイヤーとAnimatorOverrideControllerの編集は未対応です。
- Assets外のControllerは編集できません。
- 複数アバターでFX Controllerを共有している場合、割り当て変更は共有先にも反映されます。

## プレビューと保持する設定

元アバターのTransform階層とMeshRenderer / SkinnedMeshRendererの描画情報を専用プレビューシーンにコピーします。元アバターのスクリプト・Animator・Constraint・PhysBoneなどを複製実行しません。元のBlendShape値・Material・Meshアセットを書き換えません。固定の照明を使用します。Headボーンがない場合はDescriptorのView Positionを代用します。

コピー元のポーズ・Renderer有効状態・子オブジェクトのアクティブ状態を使います。非表示の衣装は非表示です。選択メッシュを切り替えると、それまでの編集をリセットし、元アバターの値を読み直します。アバター再読み込みも同じ動作です。変更がある場合は確認します。

カメラ設定はEditorPrefsに保持し、再度開いた際に復元します。ウィンドウ内の編集値はスクリプト再コンパイル時にも復元しますが、未保存表情の永続保存機能としては扱わないでください。Play Mode中の操作は無効です。

焦点距離は35mm判（36×24mm）の縦画角を基準に計算し、中央を24×24mm相当の正方形へクロップした画角です。プレビューとアイコンは同じ換算を使います。85mm時の縦横の画角は約16.07度です。旧版のFOV設定は引き継がず、85mmから開始し、以後は最後に使った焦点距離を保持します。

特殊な描画、外部ライト・シーン依存のシェーダー、NDMF/Modular Avatarなどのビルド時処理、PhysBoneやConstraintによる動きは再現しません。透明度は黒・白背景の2回の描画から復元します。屈折や背景色に依存する特殊シェーダーは、通常の透過と異なる結果になる場合があります。

## 構成

すべてのC#コードとUIは `Editor/` に置き、Editor専用Assemblyにまとめています。

| ファイル | 責務 |
| --- | --- |
| FXFacialAssistantWindow.cs / .uxml / .uss | ウィンドウと操作の接続、レイアウト |
| FacialMeshSession.cs | 選択メッシュと編集値・記録対象・基準値の保持 |
| BlendShapeBlacklist.cs / FacialBlacklistDefaults.cs | 大文字小文字を区別しないルール判定、設定保存、初期リスト |
| FacialBlacklistSettingsWindow.cs / .uxml / .uss | ブラックリスト設定画面 |
| FacialPreview.cs | 描画専用コピー、カメラ、アイコン撮影、描画リソースの破棄 |
| PreviewOrbitManipulator.cs | プレビュー上の右ドラッグとマウスホイール |
| FacialAnimation.cs | BlendShapeカーブの生成と読み込み |
| FacialAssetWriter.cs | 保存名と保存先の検証、アセット保存と復元 |
| ExpressionMenuIconImportSettings.cs | Expression Menu用アイコンのImport Settings |
| FxControllerAccess.cs | DescriptorからのFX解決、ステート割り当て |
| Tests/ | プレビューシーンと一時アセットを使うEditorテスト |

対象環境: Unity 2022.3.22f1 / VRC SDK Avatars 3.10.6。


