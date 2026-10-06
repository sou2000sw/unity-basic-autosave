using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BasicAutoSave.Tests
{
    public static class SmokeTests
    {
        [Serializable]
        private class TestReportItem
        {
            public string name;
            public bool passed;
            public string message;
        }

        [Serializable]
        private class TestReportData
        {
            public int total;
            public int passed;
            public int failed;
            public List<TestReportItem> results = new List<TestReportItem>();
        }

        private class TestAssetSaveBlocker : UnityEditor.AssetModificationProcessor
        {
            public static bool BlockSave = false;

            private static string[] OnWillSaveAssets(string[] paths)
            {
                if (!BlockSave) return paths;
                List<string> allowed = new List<string>();
                foreach (var path in paths)
                {
                    if (path.Contains("BlockedMat.mat"))
                    {
                        continue;
                    }
                    allowed.Add(path);
                }
                return allowed.ToArray();
            }
        }

        private static List<TestReportItem> results = new List<TestReportItem>();

        public static void Run()
        {
            results.Clear();
            Debug.Log("=== Starting BasicAutoSave Smoke Tests ===");

            EnsureTestFolderExists();

            try
            {
                RunTest("1. Scene save and Undo preservation", Test1_SceneSaveAndUndo);
                RunTest("2. Material edit, Undo, and timed service save", Test2_MaterialEditUndoAndTimedSave);
                RunTest("3. BlendShape mesh weight save", Test3_BlendShapeMeshSave);
                RunTest("4. Multiple dirty scenes & unnamed scene skipped", Test4_MultipleScenesAndUnnamedSkipped);
                RunTest("5. Settings interval clamp and persistence", Test5_SettingsIntervalClampAndPersist);
                RunTest("6. UI CreateGUI opens successfully", Test6_UICreateGUI);
                RunTest("7. Save-rejected dirty asset failure handling", Test7_SaveRejectedDirtyAssetHandling);
                RunTest("9. Imported shader and importer excluded from save failure", Test9_ImportedObjects);
                RunTest("8. Service init setup for restart test", Test8_ServiceInitSetupForRestart);

                WriteTestReport();
                PrintSummary();

                int failures = results.FindAll(r => !r.passed).Count;
                if (failures > 0)
                {
                    throw new Exception($"[SmokeTests] FAILED: {failures} test(s) failed.");
                }
            }
            finally
            {
                CloseScenesAndCleanupTestFolder();
            }
        }

        public static void VerifyRestart()
        {
            results.Clear();
            Debug.Log("=== Running BasicAutoSave VerifyRestart ===");

            EnsureTestFolderExists();

            int watchdogFrameCount = 0;
            const int maxWatchdogFrames = 600;

            string scenePath = "Assets/BasicAutoSaveSmoke/RestartScene.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);

            string matPath = "Assets/BasicAutoSaveSmoke/RestartMat.mat";
            Material mat = new Material(Shader.Find("Standard")) { color = Color.green };
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            mat.color = Color.magenta;
            EditorUtility.SetDirty(mat);

            FieldInfo nextSaveTimeField = typeof(BasicAutoSaveService).GetField("nextSaveTime", BindingFlags.Static | BindingFlags.NonPublic);
            if (nextSaveTimeField != null)
            {
                nextSaveTimeField.SetValue(null, EditorApplication.timeSinceStartup - 10.0);
            }

            EditorApplication.CallbackFunction updateHandler = null;
            updateHandler = () =>
            {
                watchdogFrameCount++;

                if (!EditorUtility.IsDirty(mat))
                {
                    EditorApplication.update -= updateHandler;
                    Debug.Log("[PASS] VerifyRestart: Automatic timed save verified via real EditorApplication.update loop.");
                    results.Add(new TestReportItem { name = "VerifyRestart", passed = true, message = "Passed" });
                    WriteTestReport();

                    BasicAutoSaveService.Configure(false, 5);
                    CloseScenesAndCleanupTestFolder();
                    EditorApplication.Exit(0);
                    return;
                }

                if (watchdogFrameCount >= maxWatchdogFrames)
                {
                    EditorApplication.update -= updateHandler;
                    Debug.LogError("[FAIL] VerifyRestart: Watchdog timeout expired without automatic save.");
                    results.Add(new TestReportItem { name = "VerifyRestart", passed = false, message = "Watchdog timeout" });
                    WriteTestReport();

                    BasicAutoSaveService.Configure(false, 5);
                    CloseScenesAndCleanupTestFolder();
                    EditorApplication.Exit(1);
                    return;
                }
            };

            EditorApplication.update += updateHandler;
        }

        private static void RunTest(string name, Action testAction)
        {
            try
            {
                testAction();
                results.Add(new TestReportItem { name = name, passed = true, message = "Passed" });
                Debug.Log($"[PASS] {name}");
            }
            catch (Exception ex)
            {
                results.Add(new TestReportItem { name = name, passed = false, message = ex.Message });
                Debug.LogError($"[FAIL] {name}: {ex.Message}");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"Assertion failed: {message}");
            }
        }

        private static void EnsureTestFolderExists()
        {
            if (!AssetDatabase.IsValidFolder("Assets/BasicAutoSaveSmoke"))
            {
                AssetDatabase.CreateFolder("Assets", "BasicAutoSaveSmoke");
            }
        }

        private static void CloseScenesAndCleanupTestFolder()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            catch { }

            if (AssetDatabase.IsValidFolder("Assets/BasicAutoSaveSmoke"))
            {
                AssetDatabase.DeleteAsset("Assets/BasicAutoSaveSmoke");
            }
        }

        private static void Test1_SceneSaveAndUndo()
        {
            string scenePath = "Assets/BasicAutoSaveSmoke/TestScene1.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Cube";
            cube.transform.position = Vector3.zero;
            bool saved = EditorSceneManager.SaveScene(scene, scenePath);
            Assert(saved, "Failed to save initial scene 1");

            Undo.RecordObject(cube.transform, "Move Cube");
            cube.transform.position = new Vector3(1, 2, 3);
            Undo.FlushUndoRecordObjects();
            Assert(scene.isDirty, "Scene should be dirty after cube move");

            BasicAutoSaveService.Configure(true, 5);
            bool autosaved = BasicAutoSaveService.TrySaveNow();
            Assert(autosaved, "TrySaveNow failed");
            Assert(!scene.isDirty, "Scene should be clean after save");

            Undo.PerformUndo();
            Assert(cube.transform.position == Vector3.zero, "PerformUndo should restore cube position to Vector3.zero");

            Undo.PerformRedo();
            BasicAutoSaveService.TrySaveNow();

            EditorSceneManager.OpenScene(scenePath);
            GameObject reloadedCube = GameObject.Find("Cube");
            Assert(reloadedCube != null, "Reloaded cube missing");
            Assert(Vector3.Distance(reloadedCube.transform.position, new Vector3(1, 2, 3)) < 0.001f, "Reopened scene cube position should be (1, 2, 3)");
        }

        private static void Test2_MaterialEditUndoAndTimedSave()
        {
            string scenePath = "Assets/BasicAutoSaveSmoke/TestScene2.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);

            string matPath = "Assets/BasicAutoSaveSmoke/TestMat.mat";
            Material mat = new Material(Shader.Find("Standard")) { color = Color.blue };
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            string beforeContent = File.ReadAllText(matPath);
            Assert(!scene.isDirty, "Scene should be clean before edit");
            Assert(!EditorUtility.IsDirty(mat), "Material should be clean before edit");

            Undo.RecordObject(mat, "Change Material Color");
            mat.color = Color.red;
            Undo.FlushUndoRecordObjects();
            EditorUtility.SetDirty(mat);

            Assert(EditorUtility.IsDirty(mat), "Material should be dirty before timed save");

            BasicAutoSaveService.Configure(true, 5);
            FieldInfo nextSaveTimeField = typeof(BasicAutoSaveService).GetField("nextSaveTime", BindingFlags.Static | BindingFlags.NonPublic);
            Assert(nextSaveTimeField != null, "Reflection failed: nextSaveTime field not found");
            nextSaveTimeField.SetValue(null, EditorApplication.timeSinceStartup - 10.0);

            MethodInfo onUpdateMethod = typeof(BasicAutoSaveService).GetMethod("OnUpdate", BindingFlags.Static | BindingFlags.NonPublic);
            Assert(onUpdateMethod != null, "Reflection failed: OnUpdate method not found");
            onUpdateMethod.Invoke(null, null);

            Assert(!EditorUtility.IsDirty(mat), "Material should be clean after timed service save pass");

            string afterContent = File.ReadAllText(matPath);
            Assert(beforeContent != afterContent, "On-disk material content must change after save");

            Undo.PerformUndo();
            Assert(mat.color == Color.blue, "PerformUndo must restore material color to Color.blue");
        }

        private static void Test3_BlendShapeMeshSave()
        {
            string meshPath = "Assets/BasicAutoSaveSmoke/TestMesh.asset";
            Mesh mesh = new Mesh { name = "TestMesh" };
            mesh.vertices = new Vector3[] { Vector3.zero, Vector3.up, Vector3.right };
            mesh.triangles = new int[] { 0, 1, 2 };
            mesh.AddBlendShapeFrame("Shape1", 100f, new Vector3[] { Vector3.forward, Vector3.forward, Vector3.forward }, null, null);
            AssetDatabase.CreateAsset(mesh, meshPath);

            string scenePath = "Assets/BasicAutoSaveSmoke/TestScene3.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject go = new GameObject("BlendShapeGO");
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.SetBlendShapeWeight(0, 20f);
            EditorSceneManager.SaveScene(scene, scenePath);

            Undo.RecordObject(smr, "Change BlendShape Weight");
            smr.SetBlendShapeWeight(0, 80f);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.FlushUndoRecordObjects();
            Assert(scene.isDirty, "Scene 3 should be dirty");

            bool saved = BasicAutoSaveService.TrySaveNow();
            Assert(saved, "TrySaveNow failed in Test 3");

            EditorSceneManager.OpenScene(scenePath);
            GameObject reloadedGO = GameObject.Find("BlendShapeGO");
            Assert(reloadedGO != null, "BlendShapeGO missing");
            var reloadedSMR = reloadedGO.GetComponent<SkinnedMeshRenderer>();
            Assert(reloadedSMR != null, "SkinnedMeshRenderer missing");
            Assert(Mathf.Approximately(reloadedSMR.GetBlendShapeWeight(0), 80f), "Reopened scene blend shape weight should be 80f");
        }

        private static void Test4_MultipleScenesAndUnnamedSkipped()
        {
            string scenePathA = "Assets/BasicAutoSaveSmoke/SceneA.unity";
            string scenePathB = "Assets/BasicAutoSaveSmoke/SceneB.unity";

            Scene sceneA = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(sceneA, scenePathA);

            Scene sceneB = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(sceneB, scenePathB);

            Scene unnamedScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            GameObject goA = new GameObject("ObjA");
            SceneManager.MoveGameObjectToScene(goA, sceneA);
            EditorSceneManager.MarkSceneDirty(sceneA);

            GameObject goB = new GameObject("ObjB");
            SceneManager.MoveGameObjectToScene(goB, sceneB);
            EditorSceneManager.MarkSceneDirty(sceneB);

            GameObject goU = new GameObject("ObjU");
            SceneManager.MoveGameObjectToScene(goU, unnamedScene);
            EditorSceneManager.MarkSceneDirty(unnamedScene);

            Assert(sceneA.isDirty, "Scene A should be dirty");
            Assert(sceneB.isDirty, "Scene B should be dirty");
            Assert(unnamedScene.isDirty, "Unnamed scene should be dirty");

            BasicAutoSaveService.TrySaveNow();

            Assert(!sceneA.isDirty, "Scene A should be clean after save");
            Assert(!sceneB.isDirty, "Scene B should be clean after save");
            Assert(unnamedScene.isDirty, "Unnamed scene should remain dirty");
        }

        private static void Test5_SettingsIntervalClampAndPersist()
        {
            BasicAutoSaveService.Configure(true, 100);
            Assert(BasicAutoSaveService.IntervalMinutes == 30, "Interval 100 should clamp to 30");

            BasicAutoSaveService.Configure(true, -5);
            Assert(BasicAutoSaveService.IntervalMinutes == 1, "Interval -5 should clamp to 1");

            BasicAutoSaveService.Configure(true, 15);
            Assert(BasicAutoSaveService.IntervalMinutes == 15, "Interval should be set to 15");

            string settingsFile = "UserSettings/BasicAutoSaveSettings.asset";
            Assert(File.Exists(settingsFile), "Settings asset file should exist at UserSettings/BasicAutoSaveSettings.asset");
        }

        private static void Test6_UICreateGUI()
        {
            var window = ScriptableObject.CreateInstance<BasicAutoSaveWindow>();
            window.CreateGUI();
            Assert(window.rootVisualElement != null, "rootVisualElement should not be null");

            var button = window.rootVisualElement.Q<Button>("SaveNowButton");
            Assert(button != null, "SaveNowButton control should exist in UI");
            Assert(button.text.Contains("今すぐ保存"), "Save Now button should have correct Japanese text");
        }

        private static void Test7_SaveRejectedDirtyAssetHandling()
        {
            string matPath = "Assets/BasicAutoSaveSmoke/BlockedMat.mat";
            Material mat = new Material(Shader.Find("Standard")) { color = Color.yellow };
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            mat.color = Color.magenta;
            EditorUtility.SetDirty(mat);

            string initialSaveTime = BasicAutoSaveService.LastSaveTime;

            TestAssetSaveBlocker.BlockSave = true;
            try
            {
                bool success = BasicAutoSaveService.TrySaveNow();
                Assert(!success, "TrySaveNow should report failure when dirty asset save is blocked");
                Assert(BasicAutoSaveService.LastSaveTime == initialSaveTime, "LastSaveTime must not update on rejected asset save");
            }
            finally
            {
                TestAssetSaveBlocker.BlockSave = false;
            }
        }

        private static void Test8_ServiceInitSetupForRestart()
        {
            BasicAutoSaveService.Configure(true, 5);
            Assert(BasicAutoSaveService.IsEnabled, "Service must be enabled at end of Run for VerifyRestart test");
        }

        private static void Test9_ImportedObjects()
        {
            const string shaderPath = "Assets/BasicAutoSaveTests/Imported.shader";
            Directory.CreateDirectory("Assets/BasicAutoSaveTests");
            File.WriteAllText(shaderPath, "Shader \"Hidden/BasicAutoSaveRegression\" { SubShader { Pass {} } }");
            AssetDatabase.ImportAsset(shaderPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            var importer = AssetImporter.GetAtPath(shaderPath);
            Assert(shader != null && importer != null, "Imported test objects missing");
            EditorUtility.SetDirty(shader);
            EditorUtility.SetDirty(importer);
            var method = typeof(BasicAutoSaveService).GetMethod("GetDirtyPersistentAssets", BindingFlags.NonPublic | BindingFlags.Static);
            var assets = (List<UnityEngine.Object>)method.Invoke(null, null);
            Assert(!assets.Contains(shader) && !assets.Contains(importer), "Imported objects must not report SaveAssets failure");
            AssetDatabase.DeleteAsset(shaderPath);
        }

        private static void WriteTestReport()
        {
            var reportData = new TestReportData
            {
                total = results.Count,
                passed = results.FindAll(r => r.passed).Count,
                failed = results.FindAll(r => !r.passed).Count,
                results = results
            };

            string json = JsonUtility.ToJson(reportData, true);
            File.WriteAllText("test-report.json", json);
            Debug.Log("[SmokeTests] Wrote test report to test-report.json");
        }

        private static void PrintSummary()
        {
            int total = results.Count;
            int passed = results.FindAll(r => r.passed).Count;
            int failed = total - passed;

            Debug.Log($"=== Smoke Tests Summary: {passed}/{total} Passed, {failed} Failed ===");
        }
    }
}
