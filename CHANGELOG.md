# Changelog

All notable changes to the Basic Auto Save package will be documented in this file.

## 0.1.5 - 2026-11-01
- 文書のみ更新。READMEの効果の説明を言い切らない表現に直し、配布していない `.tgz` の導入手順を削除し、テストがZIPに含まれないことを明記。CHANGELOGの見出しの重複と0.1.2の配布方法の記述を修正。保存処理は0.1.3と同じ。
- 配布用ワークフローの `actions/checkout` で認証情報を残さないようにした（`persist-credentials: false`）。配布物の中身には影響しない。

## 0.1.4 - 2026-11-01
- READMEのみ更新（見出しから古いバージョン番号を外し、動作確認した環境と仕組みの説明を追加）。保存処理は0.1.3と同じ。

## 0.1.3 - 2026-11-01
- スクリプトのコンパイルやPlay開始のたびに保存までのカウントダウンが最初からやり直しになり、こまめにPlayやコンパイルをすると自動保存されないことがある問題を修正。コンパイル中に保存時刻を迎えた場合も、終了後すぐに保存する。
- 名前のない新規Sceneを開いていると、他のSceneやアセットを保存しても最終保存日時が更新されなかった問題を修正。
- 保存間隔の入力欄が入力途中の値で確定され、数字を打ち直しにくかった問題を修正（Enterまたはフォーカスを外したときに反映）。
- Playを終了する途中に保存処理が走り、「This cannot be used during play mode」のエラーで保存に失敗する（30秒後の再試行で保存される）ことがある問題を修正。Play終了が完了するまで保存を待つ。
- 名前なしSceneの警告に使っていた絵文字がEditorのフォントで表示できず、四角になり警告ログが出ていた問題を修正。

## 0.1.2 - 2026-10-08
- VCC User Packages向けローカルVPM配布を追加。保存処理は0.1.1と同じ。
- 公開にあわせてパッケージ名を `com.sou2000sw.basic-autosave` に変更（旧 `com.local.basic-autosave`）。VPMリポジトリ（GitHub Pages）からの配布は0.1.3から。

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
