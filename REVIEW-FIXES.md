# Correct implementation defects before verification

Do not launch Unity or install software. Modify only this repository. Do not change SPEC.md.

1. Add quitting guard, registered EditorApplication.quitting callback, and preview Scene
   exclusions via EditorSceneManager.IsPreviewScene. Apply guards to EVERY public save
   path, including ExecuteSavePass, or make actual save method private.
2. Remaining dirty project assets must be a FAILURE, Debug.LogError unconditionally,
   preserve last successful timestamp and schedule bounded retry. Count only persistent
   objects with an AssetDatabase path under Assets/ or Packages/, exclude singleton and
   built-in/editor transient resources. Empty pass should not update LastSaveTime.
3. Unnamed dirty Scenes must produce partial/skipped status, not silently report full
   success. Keep an explicit last-pass message separate from pause guard display.
   It may return true for handled skip to avoid repeated error retries, but do not advance
   successful-save timestamp if there are skipped scenes. Warn via window label.
4. Save exceptions must not prevent attempts to save other loaded Scenes and assets.
   Catch per-Scene and separately SaveAssets errors, keep finally for reentry release.
5. Manual save must update persistent status and deadline. Failure status must stay visible
   in the window, not be overwritten as Active on next tick; pause/retry may be additive.
6. UI: root.Clear() in CreateGUI; remove inline element.style styling, apply USS classes.
   Remove unsupported USS font-weight, replace with -unity-font-style. Use clear Japanese
   labels without gratuitous English duplicates. Disable Save Now button when disabled or
   guarded. Use named controls and test them. Prefab Stage pause should be explained.
7. Test UI is batch mode: GetWindow may not invoke CreateGUI synchronously. Explicitly
   call CreateGUI if required and ensure required controls exist without duplication.
8. Material test: saved clean Scene first, assert not dirty before timer, verify file content
   actually changes from before to after. Add Material Undo using RecordObject, flush,
   save, PerformUndo, check original colour; restore/redirty only test assets.
9. Restart test must verify actual timer subscription WITHOUT manually calling Configure
   first: create dirty material in clean saved Scene, force deadline past via reflection,
   register an update observer, let real EditorApplication.update run; save persisted data
   then disable and EditorApplication.Exit(0). Run this process WITHOUT -quit, use watchdog
   time limit in observer and Exit(1) on fail. Report test JSON. This proves reload startup.
10. Test report JSON: use JsonUtility+serializable wrapper instead of manual escaping.
    Close/create fresh Scene before deleting test assets; no test files outside disposable
    project. Test failure still reports results. Add meaningful save-rejected dirty-asset
    case using test-only AssetModificationProcessor.OnWillSaveAssets to exclude ONLY a
    named test Material; check service reports failure and successful timestamp unchanged,
    restore hook afterward. No hooks in production code.
11. Add LICENSE matching MIT in package.json; README install from tarball through Package
    Manager '+' > Add package from tarball, also Add package from disk > package.json.
    Tests~ is ignored by Unity; explain copying harness to disposable Assets/Editor before
    running it. No Git URL claimed because this package has not been published.

Keep corrections focused. Finish with changed file list only.
