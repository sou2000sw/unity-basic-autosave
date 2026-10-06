using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BasicAutoSave
{
    [InitializeOnLoad]
    public static class BasicAutoSaveService
    {
        private static double nextSaveTime;
        private static double retryDelayUntil;
        private static bool isPendingSaveDue;
        private static bool isSaving;
        private static bool isQuitting;
        private static string currentStatusMessage = "初期化中";
        private static string lastPassOutcomeMessage = "";

        static BasicAutoSaveService()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.quitting -= OnQuitting;
            EditorApplication.quitting += OnQuitting;
            ResetDeadline();
        }

        private static void OnQuitting()
        {
            isQuitting = true;
        }

        public static bool IsEnabled
        {
            get => BasicAutoSaveSettings.instance.AutoSaveEnabled;
            set
            {
                BasicAutoSaveSettings.instance.AutoSaveEnabled = value;
                ResetDeadline();
            }
        }

        public static int IntervalMinutes
        {
            get => BasicAutoSaveSettings.instance.IntervalMinutes;
            set
            {
                BasicAutoSaveSettings.instance.IntervalMinutes = value;
                ResetDeadline();
            }
        }

        public static void Configure(bool enabled, int minutes)
        {
            BasicAutoSaveSettings.instance.AutoSaveEnabled = enabled;
            BasicAutoSaveSettings.instance.IntervalMinutes = minutes;
            BasicAutoSaveSettings.instance.SaveSettings();
            ResetDeadline();
        }

        public static void ResetDeadline()
        {
            nextSaveTime = EditorApplication.timeSinceStartup + IntervalMinutes * 60.0;
            retryDelayUntil = 0;
            isPendingSaveDue = false;
        }

        public static double SecondsUntilNextSave
        {
            get
            {
                if (!IsEnabled) return 0;
                double remaining = nextSaveTime - EditorApplication.timeSinceStartup;
                return remaining > 0 ? remaining : 0;
            }
        }

        public static string StatusMessage
        {
            get
            {
                if (!IsEnabled) return "無効";
                return currentStatusMessage;
            }
        }

        public static string LastPassOutcomeMessage => lastPassOutcomeMessage;

        public static string LastSaveTime
        {
            get
            {
                string stored = SessionState.GetString("BasicAutoSave_LastSaveTime", "");
                return string.IsNullOrEmpty(stored) ? "なし" : stored;
            }
        }

        public static List<string> GetUnnamedDirtyScenes()
        {
            List<string> list = new List<string>();
            int count = SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.IsValid() && !EditorSceneManager.IsPreviewScene(scene) && scene.isDirty && string.IsNullOrEmpty(scene.path))
                {
                    list.Add(string.IsNullOrEmpty(scene.name) ? "Untitled Scene" : scene.name);
                }
            }
            return list;
        }

        public static string GetGuardReason()
        {
            if (isQuitting) return "エディタ終了中";
            if (EditorApplication.isCompiling) return "コンパイル中";
            if (EditorApplication.isUpdating) return "アセット更新中";
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "再生中/準備中";
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return "プレハブ編集モード中";
            if (isSaving) return "保存処理中";
            return null;
        }

        public static bool TrySaveNow()
        {
            if (!IsEnabled)
            {
                Debug.LogWarning("[BasicAutoSave] 自動保存が無効になっています。");
                lastPassOutcomeMessage = "保存スキップ: 自動保存が無効です";
                return false;
            }

            string guard = GetGuardReason();
            if (guard != null)
            {
                Debug.LogWarning($"[BasicAutoSave] 保存できません: {guard}");
                lastPassOutcomeMessage = $"保存スキップ: {guard}";
                return false;
            }

            bool result = ExecuteSavePass();
            if (result)
            {
                ResetDeadline();
            }
            else
            {
                retryDelayUntil = EditorApplication.timeSinceStartup + 30.0;
                nextSaveTime = retryDelayUntil;
            }
            return result;
        }

        private static void OnUpdate()
        {
            if (!IsEnabled)
            {
                currentStatusMessage = "無効";
                return;
            }

            string guard = GetGuardReason();
            if (guard != null)
            {
                currentStatusMessage = $"一時停止中 ({guard})";
                if (EditorApplication.timeSinceStartup >= nextSaveTime)
                {
                    isPendingSaveDue = true;
                }
                return;
            }

            if (EditorApplication.timeSinceStartup < retryDelayUntil)
            {
                int remainingSec = (int)Math.Ceiling(retryDelayUntil - EditorApplication.timeSinceStartup);
                currentStatusMessage = $"再試行待機中 ({remainingSec}秒)";
                return;
            }

            if (EditorApplication.timeSinceStartup >= nextSaveTime || isPendingSaveDue)
            {
                bool success = ExecuteSavePass();
                if (success)
                {
                    isPendingSaveDue = false;
                    retryDelayUntil = 0;
                    nextSaveTime = EditorApplication.timeSinceStartup + IntervalMinutes * 60.0;
                    currentStatusMessage = "正常稼働中";
                }
                else
                {
                    retryDelayUntil = EditorApplication.timeSinceStartup + 30.0;
                    nextSaveTime = EditorApplication.timeSinceStartup + 30.0;
                    currentStatusMessage = "保存エラー (30秒後に再試行)";
                }
            }
            else
            {
                currentStatusMessage = "正常稼働中";
            }
        }

        private static bool ExecuteSavePass()
        {
            if (isSaving) return false;

            string guard = GetGuardReason();
            if (guard != null)
            {
                lastPassOutcomeMessage = $"保存一時停止: {guard}";
                return false;
            }

            isSaving = true;
            bool passSuccess = true;
            int savedSceneCount = 0;
            int skippedUnnamedCount = 0;

            try
            {
                int dirtyAssetCountBefore = GetDirtyPersistentAssets().Count;

                int count = SceneManager.sceneCount;
                for (int i = 0; i < count; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded || !scene.IsValid() || EditorSceneManager.IsPreviewScene(scene)) continue;

                    if (scene.isDirty)
                    {
                        if (string.IsNullOrEmpty(scene.path))
                        {
                            skippedUnnamedCount++;
                            continue;
                        }

                        try
                        {
                            bool saved = EditorSceneManager.SaveScene(scene);
                            if (saved)
                            {
                                savedSceneCount++;
                            }
                            else
                            {
                                passSuccess = false;
                                Debug.LogError($"[BasicAutoSave] シーンの保存に失敗しました: {scene.name} ({scene.path})");
                            }
                        }
                        catch (Exception ex)
                        {
                            passSuccess = false;
                            Debug.LogError($"[BasicAutoSave] シーン保存中に例外が発生しました ({scene.name}): {ex.Message}");
                        }
                    }
                }

                try
                {
                    AssetDatabase.SaveAssets();
                }
                catch (Exception ex)
                {
                    passSuccess = false;
                    Debug.LogError($"[BasicAutoSave] AssetDatabase.SaveAssets() 実行中に例外が発生しました: {ex.Message}");
                }

                List<UnityEngine.Object> remainingDirtyAssets = GetDirtyPersistentAssets();
                if (remainingDirtyAssets.Count > 0)
                {
                    passSuccess = false;
                    foreach (var asset in remainingDirtyAssets)
                    {
                        Debug.LogError($"[BasicAutoSave] 保存失敗: アセットがダーティなまま残っています ({AssetDatabase.GetAssetPath(asset)})");
                    }
                }

                int totalSavedWork = savedSceneCount + (dirtyAssetCountBefore - remainingDirtyAssets.Count);

                if (!passSuccess)
                {
                    lastPassOutcomeMessage = "保存失敗: エラーが発生したかアセットの書き込みがブロックされました";
                    return false;
                }

                if (skippedUnnamedCount > 0)
                {
                    lastPassOutcomeMessage = $"一部スキップ: 未保存の新規シーンが{skippedUnnamedCount}件スキップされました";
                    return true;
                }

                if (totalSavedWork > 0)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    SessionState.SetString("BasicAutoSave_LastSaveTime", timestamp);
                    lastPassOutcomeMessage = $"保存完了 ({timestamp})";
                }
                else
                {
                    lastPassOutcomeMessage = "保存対象なし (変更なし)";
                }

                return true;
            }
            catch (Exception ex)
            {
                lastPassOutcomeMessage = $"保存エラー: {ex.Message}";
                Debug.LogError($"[BasicAutoSave] 自動保存処理中に例外が発生しました: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            finally
            {
                isSaving = false;
            }
        }

        private static List<UnityEngine.Object> GetDirtyPersistentAssets()
        {
            List<UnityEngine.Object> list = new List<UnityEngine.Object>();
            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll<UnityEngine.Object>();
            var settings = BasicAutoSaveSettings.instance;
            foreach (var obj in objects)
            {
                if (obj == null || obj == settings || obj is AssetImporter) continue;
                if (EditorUtility.IsPersistent(obj) && EditorUtility.IsDirty(obj) && AssetDatabase.IsNativeAsset(obj))
                {
                    string path = AssetDatabase.GetAssetPath(obj);
                    if (path.StartsWith("Packages/", StringComparison.Ordinal))
                    {
                        var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                        if (package == null ||
                            (package.source != UnityEditor.PackageManager.PackageSource.Embedded &&
                             package.source != UnityEditor.PackageManager.PackageSource.Local)) continue;
                    }
                    if (!string.IsNullOrEmpty(path) && (path.StartsWith("Assets/") || path.StartsWith("Packages/")))
                    {
                        list.Add(obj);
                    }
                }
            }
            return list;
        }
    }
}
