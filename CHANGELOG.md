# Changelog

## 0.1.3 - 2026-11-01
- スクリプトのコンパイルやPlay開始のたびに保存までのカウントダウンが最初からやり直しになり、こまめにPlayやコンパイルをすると自動保存されないことがある問題を修正。コンパイル中に保存時刻を迎えた場合も、終了後すぐに保存する。
- 名前のない新規Sceneを開いていると、他のSceneやアセットを保存しても最終保存日時が更新されなかった問題を修正。
- 保存間隔の入力欄が入力途中の値で確定され、数字を打ち直しにくかった問題を修正（Enterまたはフォーカスを外したときに反映）。

## 0.1.2 - 2026-10-08
- VCC User Packages向けローカルVPM配布を追加。保存処理は0.1.1と同じ。
- 公開にあわせてパッケージ名を `com.sou2000sw.basic-autosave` に変更（旧 `com.local.basic-autosave`）。VPMリポジトリ（GitHub Pages）から配布。

# Changelog

All notable changes to the Basic Auto Save package will be documented in this file.

## [0.1.1] - 2026-10-07

### Fixed
- 読み取り専用UPMパッケージ、インポート済みシェーダー・プラグイン、AssetImporterを保存失敗の判定対象から除外。通常のScene/Asset保存は維持。

## [0.1.0] - 2026-10-05

### Added
- 初版リリース (Basic Auto Save 0.1.0)。
- `ScriptableSingleton` によるプロジェクトローカル設定 (`UserSettings/BasicAutoSaveSettings.asset`)。
- `InitializeOnLoad` によるバックグラウンドタイマー更新 (`BasicAutoSaveService`)。
- UI Toolkit による設定・ステータスウィンドウ (`BasicAutoSaveWindow`)。
- コンパイル中、アセット更新中、プレイモード中、プレハブステージ中の一時停止ガード機能。
- 名前なし（未保存）シーンのダイアログなしスキップとステータス警告機能。
- `AssetDatabase.SaveAssets()` によるアセット単体変更の保存対応。
- バッチ検証用スモークテスト (`SmokeTests.Run` および `SmokeTests.VerifyRestart`)。
