# Basic Auto Save 0.1.0 implementation contract

Implement a NEW original Unity 2022.3+ editor-only UPM package, not a PM_AutoSave fork.
All modifications MUST stay inside this repository. Do not change any real Unity project,
global settings, credentials, or external files. Do not install software, launch Unity,
publish, commit, or create any external messages. Codex will review and run validation.

## Deliverables
- package.json: com.local.basic-autosave, 0.1.0, Unity 2022.3 minimum.
- Editor/BasicAutoSaveSettings.cs, BasicAutoSaveService.cs, BasicAutoSaveWindow.cs
- Editor/BasicAutoSave.Editor.asmdef, editor only.
- Editor/BasicAutoSave.uss (if styles are needed), no external references.
- Tests~/SmokeTests.cs: executable batch verification harness with static Run().
- README.md in Japanese with exact installation and limitations; CHANGELOG.md.
- Unity .meta files for files/folders imported by Unity, stable random GUIDs.
- .gitignore: generated Unity folders, logs, archives. No unrelated features.

## UX
Tools > Basic Auto Save menu. Japanese EditorWindow built with UI Toolkit CreateGUI
(supported in Unity 2022.3, avoid Unity 6-only APIs). Programmatic controls acceptable.
Simple ON/OFF, interval integer 1..30 minutes default 5, last completed save time,
status including paused reasons and first-save-needed scenes, next save countdown,
button 今すぐ保存. Clearly say normal overwrite save, no history or rollback.
Default OFF on first import, user explicitly enables; activation persists per project.
Service must operate without any settings window, including after domain reload/restart.
Use InitializeOnLoad and single static update subscription, not window lifecycle.
No runtime code, scene components, network, telemetry, keypress simulation, Save on Play,
checkpoint, restore, backups, asset history, or dependency packages.

## Settings
ScriptableSingleton with FilePath in UserSettings/BasicAutoSaveSettings.asset,
project-local private settings. Save user preferences immediately when changed.
Validate interval on load. Reset next deadline when enabling/changing interval.
Do not confuse this tool's settings with asset dirty counts.

## Save behavior
At each due interval, save every loaded non-preview dirty Scene with a nonempty path
via EditorSceneManager.SaveScene(scene); check its bool result and catch errors.
Do not prompt for unnamed scenes: leave them dirty, surface names and skipped status.
Do not reopen scenes or clear/collapse Undo. Do not mark objects dirty solely to save.
Always call AssetDatabase.SaveAssets on each save pass, INDEPENDENT of Scene dirtiness;
this saves Material-only edits and other dirty persistent assets via the normal pipeline.
Check dirty persistent assets after pass when practical to detect rejected saves; SaveAssets
returns void and third-party OnWillSaveAssets hooks can block writes. Explain this limit.
Skip persistent settings object itself when counting dirty assets. Only report fully
completed pass as success, distinguish skips/partial failures/no work if detectable.
Record last successful completion, not attempts, using SessionState if reload persistence
is desired. Report errors unconditionally via Debug.LogError and persistent window status.
Prevent reentry with try/finally guard. Delay any due save while EditorApplication.isCompiling,
isUpdating, isPlayingOrWillChangePlaymode, quitting, or saving. Never save Prefab Stage Scene
as normal Scene; pause while PrefabStageUtility.GetCurrentPrefabStage()!=null (explain).
Treat manual save button with identical guards. Due save resumes promptly when safe,
not after another full interval. On failed writes retry with bounded delay (e.g. 30 seconds)
to avoid per-frame retry/log spam. No external-project modifications.
Expose a minimal public editor service API for the harness, e.g. TrySaveNow and settings
Configure(bool enabled,int minutes), plus status and last successful time. Timer can be
tested via internal/private field reflection; no test-mode bypass in production paths.

## Verification harness (Codex will copy into disposable Assets/Editor)
No NUnit/external dependencies. Run using -executeMethod BasicAutoSave.Tests.SmokeTests.Run.
Create ONLY test assets under Assets/BasicAutoSaveSmoke. Throw on failure, print individual
PASS lines and summary to Console, write JSON test report to project root. Tests must cover:
1. New saved Scene; position change using Undo.RecordObject+FlushUndoRecordObjects,
   save using service; assert clean; PerformUndo must restore original position; reopen
   scene separately to verify saved position after a subsequent save (don't wipe Undo early).
2. Material-only edit with scene clean: mark material dirty, use actual timed service update
   (enable via Configure then make deadline due with reflection), assert material clean and
   on-disk material contents differ / persist after reimport. This must NOT test only manual save.
3. BlendShape mesh with a named shape; saved Scene renderer weight, change weight with
   proper Undo/scene dirty marking, save, reopen and verify weight.
4. Multiple dirty loaded saved Scenes saved; unnamed scene skipped without dialog and stays dirty.
5. Settings interval clamp and enabled flag persists to project-local UserSettings file.
6. UI CreateGUI opens successfully with required controls; do not need screenshot in batch.
7. Startup service initialized without window; timer enabled after persisted settings reload
   can be validated by a second static method VerifyRestart (separate Unity process).
Disable autosave after test except enable at end for VerifyRestart; ensure VerifyRestart
disables after verification. No real user projects. Include manual test checklist for Play,
compile/import, read-only files, restart, Prefab Mode and Undo of Material.

Keep implementation small and readable. Avoid inventing validation results. Finish with
changed-file list only. Do not execute tests yourself.
