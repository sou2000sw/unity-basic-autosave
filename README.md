# Basic Auto Save 0.1.0

Basic Auto Save は、Unity 2022.3 以降の Editor 向けの通常保存自動化パッケージです。初期版 0.1.0 の実機検証は Unity 2022.3.22f1 で行っています。

## 主な機能
- **定期的な自動保存**: 1〜30分（デフォルト5分）の間隔で変更のあったシーンおよびアセットを自動保存します。
- **アセット保存対応**: シーンに変更がなくても、マテリアル等の変更済みアセットを `AssetDatabase.SaveAssets()` で保存します。保存は Editor のメインスレッドで行うため、大きなプロジェクトでは一時的に操作が止まる場合があります。
- **一時停止ガード**: プレイモード中、スクリプトコンパイル中、アセット更新中、プレハブ編集モード中（Prefab Stage）、エディタ終了中は自動保存を安全に一時停止します。
- **Undo 履歴の保護**: 保存実行時に Undo 履歴を破壊・クリアしません。
- **プロジェクトローカル設定**: 設定は `UserSettings/BasicAutoSaveSettings.asset` に保存され、リポジトリ共有を汚しません。
- **バックグラウンド稼働**: 設定ウィンドウを開いていなくても `[InitializeOnLoad]` により常時稼働します。

## インストール方法
Package Manager (`Window > Package Manager`) を開き、左上の `+` ボタンからインストールします。

- **ローカルフォルダから導入**: `Add package from disk...` を選択し、本パッケージの `package.json` を指定します。
- **tarball (.tgz) から導入**: `Add package from tarball...` を選択し、ビルドされた `.tgz` ファイルを指定します。

※インストールの完了後、メニューバーに `Tools > Basic Auto Save` が追加されます。

## 使用方法
1. メニューから `Tools > Basic Auto Save` を開き、「自動保存を有効化」にチェックを入れます（初回インストール時はデフォルト OFF です）。
2. 必要に応じて「保存間隔 (分)」を設定します（1〜30分）。
3. 「今すぐ保存」ボタンをクリックすると、即座に保存テストを実行できます。

## 制限事項・仕様上の注意点
- **名前なしシーンの扱い**: まだディスクに保存されていない新規シーン（`Untitled Scene` 等）は、保存場所ダイアログを出さずにスキップされ、ステータス画面に未保存シーンとして警告表示されます。
- **履歴・ロールバック非対応**: 本ツールは標準の上書き保存を行います。過去のバックアップやロールバック機能はありません。
- **シェイプキー**: Scene上のSkinnedMeshRendererに記録された重みはScene保存の対象です。Play中の変更や、外部モデリングソフトで編集中の形状は対象外です。
- **Prefab編集**: Prefab Mode中はツール全体を延期します。Prefab編集中の保存はUnity側で行ってください。
- **最終保存日時**: 変更の保存が完了した時刻です。変更なし・新規Sceneのスキップ・保存失敗では更新しません。Editor再起動時には表示がリセットされます。
- **Undo**: Sceneの位置変更とMaterialの色変更で保存後のUndoを確認済みです。Undo非対応の外部ツールによる変更までUndo可能にする機能ではありません。
- **`AssetDatabase.SaveAssets()` の制約**: サードパーティ製の `OnWillSaveAssets` フックやファイル権限（読み取り専用）によって書き込みが拒否された場合、アセットがダーティなまま残り、保存失敗としてエラーログが出力されます。

## 検証方法（Verification Harness）
以下のテストは使い捨ての検証用プロジェクトだけで実行してください。テスト用Sceneへ切り替え、`Assets/BasicAutoSaveSmoke`を作成・削除します。制作中のプロジェクトでは実行しないでください。

Unity の仕様上 `Tests~` フォルダ内の C# スクリプトはアセットデータベースから除外されます。バッチモード検証を行う際は、あらかじめ `Tests~/SmokeTests.cs` をプロジェクトの `Assets/Editor/SmokeTests.cs` へコピーしてください。

テスト実行コマンド：

```bash
Unity.exe -batchmode -quit -projectPath <path> -executeMethod BasicAutoSave.Tests.SmokeTests.Run
```

再起動・設定復元検証コマンド（※本プロセスは内部で判定後に自動終了します）：

```bash
Unity.exe -batchmode -projectPath <path> -executeMethod BasicAutoSave.Tests.SmokeTests.VerifyRestart
```

## 手動テストチェックリスト
- [ ] **Play Mode**: プレイモード開始時および実行中に自動保存が「一時停止中」になり、プレイ終了後に再開されること。
- [ ] **Compile / Import**: スクリプト変更時（コンパイル中）やアセットインポート中に自動保存が一時停止すること。
- [ ] **Read-Only / Blocked Assets**: 読み取り専用ファイルやブロックされた保存に対してエラーログが正しく出力され、最終保存日時が更新されないこと。
- [ ] **Restart / Domain Reload**: エディタ再起動およびドメインリロード後に、設定（有効フラグ・間隔）が保持されバックグラウンドでタイマーが稼働すること。
- [ ] **Prefab Mode**: プレハブアセットをダブルクリックして Prefab Stage に入った際、自動保存が一時停止すること。
- [ ] **Material Undo**: マテリアルのプロパティを変更して自動保存された後も、Undo (Ctrl+Z) で変更を元に戻せること。
