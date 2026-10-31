using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BasicAutoSave
{
    public class BasicAutoSaveWindow : EditorWindow
    {
        private Toggle enableToggle;
        private IntegerField intervalField;
        private Label statusLabel;
        private Label countdownLabel;
        private Label lastSaveLabel;
        private Label lastPassOutcomeLabel;
        private Label unnamedScenesLabel;
        private Button saveNowButton;
        private IVisualElementScheduledItem statusUpdater;

        [MenuItem("Tools/Basic Auto Save")]
        public static void ShowWindow()
        {
            var window = GetWindow<BasicAutoSaveWindow>("Basic Auto Save");
            window.minSize = new Vector2(320, 380);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            statusUpdater?.Pause();
            root.Clear();
            root.AddToClassList("window-container");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.sou2000sw.basic-autosave/Editor/BasicAutoSave.uss");
            if (styleSheet == null)
            {
                styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/BasicAutoSave.uss");
            }
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }

            Label titleLabel = new Label("自動保存 (Basic Auto Save)");
            titleLabel.AddToClassList("title-label");
            root.Add(titleLabel);

            HelpBox helpBox = new HelpBox(
                "プロジェクト内の変更されたシーンおよびアセットを定期的に自動保存します。\n" +
                "※通常の更新保存を行います。過去のバックアップやロールバック機能はありません。\n" +
                "※プレハブ編集モード中、コンパイル中、再生中は自動保存が一時停止します。",
                HelpBoxMessageType.Info);
            helpBox.AddToClassList("help-box");
            root.Add(helpBox);

            VisualElement controlsGroup = new VisualElement();
            controlsGroup.AddToClassList("control-group");

            enableToggle = new Toggle("自動保存を有効化");
            enableToggle.name = "EnableToggle";
            enableToggle.value = BasicAutoSaveService.IsEnabled;
            enableToggle.RegisterValueChangedCallback(evt =>
            {
                BasicAutoSaveService.Configure(evt.newValue, BasicAutoSaveService.IntervalMinutes);
                UpdateStatusUI();
            });
            controlsGroup.Add(enableToggle);

            intervalField = new IntegerField("保存間隔 (分) [1-30]");
            intervalField.name = "IntervalField";
            // Apply only on Enter or focus loss, so a half-typed value is not clamped mid-edit
            intervalField.isDelayed = true;
            intervalField.value = BasicAutoSaveService.IntervalMinutes;
            intervalField.RegisterValueChangedCallback(evt =>
            {
                int clamped = Mathf.Clamp(evt.newValue, 1, 30);
                if (clamped != evt.newValue)
                {
                    intervalField.SetValueWithoutNotify(clamped);
                }
                BasicAutoSaveService.Configure(BasicAutoSaveService.IsEnabled, clamped);
                UpdateStatusUI();
            });
            controlsGroup.Add(intervalField);

            root.Add(controlsGroup);

            saveNowButton = new Button(() =>
            {
                BasicAutoSaveService.TrySaveNow();
                UpdateStatusUI();
            })
            {
                text = "今すぐ保存"
            };
            saveNowButton.name = "SaveNowButton";
            saveNowButton.AddToClassList("action-button");
            root.Add(saveNowButton);

            VisualElement statusGroup = new VisualElement();
            statusGroup.AddToClassList("status-group");

            Label statusHeader = new Label("ステータス情報:");
            statusHeader.AddToClassList("status-header");
            statusGroup.Add(statusHeader);

            statusLabel = new Label();
            statusLabel.name = "StatusLabel";
            statusGroup.Add(statusLabel);

            countdownLabel = new Label();
            countdownLabel.name = "CountdownLabel";
            statusGroup.Add(countdownLabel);

            lastSaveLabel = new Label();
            lastSaveLabel.name = "LastSaveLabel";
            statusGroup.Add(lastSaveLabel);

            lastPassOutcomeLabel = new Label();
            lastPassOutcomeLabel.name = "LastPassOutcomeLabel";
            lastPassOutcomeLabel.AddToClassList("last-pass-label");
            statusGroup.Add(lastPassOutcomeLabel);

            unnamedScenesLabel = new Label();
            unnamedScenesLabel.name = "UnnamedScenesLabel";
            unnamedScenesLabel.AddToClassList("warning-label");
            statusGroup.Add(unnamedScenesLabel);

            root.Add(statusGroup);

            UpdateStatusUI();

            statusUpdater = root.schedule.Execute(UpdateStatusUI).Every(500);
        }

        private static bool IsEditing(VisualElement field)
        {
            // Focus lands on the field's inner text input, so check descendants too
            var focused = field.focusController?.focusedElement as VisualElement;
            return focused != null && (focused == field || field.Contains(focused));
        }

        private void UpdateStatusUI()
        {
            bool isEnabled = BasicAutoSaveService.IsEnabled;
            string guardReason = BasicAutoSaveService.GetGuardReason();

            if (enableToggle != null)
            {
                enableToggle.SetValueWithoutNotify(isEnabled);
            }
            // The 500 ms refresh must not overwrite text the user is still typing
            if (intervalField != null && !IsEditing(intervalField))
            {
                intervalField.SetValueWithoutNotify(BasicAutoSaveService.IntervalMinutes);
            }
            if (saveNowButton != null)
            {
                saveNowButton.SetEnabled(isEnabled && guardReason == null);
            }

            if (statusLabel != null)
            {
                statusLabel.text = $"状態: {BasicAutoSaveService.StatusMessage}";
            }

            if (countdownLabel != null)
            {
                if (isEnabled)
                {
                    double sec = BasicAutoSaveService.SecondsUntilNextSave;
                    int minutes = (int)(sec / 60);
                    int seconds = (int)(sec % 60);
                    countdownLabel.text = $"次回保存まで: {minutes:D2}:{seconds:D2}";
                }
                else
                {
                    countdownLabel.text = "次回保存まで: --:--";
                }
            }

            if (lastSaveLabel != null)
            {
                lastSaveLabel.text = $"最終保存日時: {BasicAutoSaveService.LastSaveTime}";
            }

            if (lastPassOutcomeLabel != null)
            {
                string outcome = BasicAutoSaveService.LastPassOutcomeMessage;
                lastPassOutcomeLabel.text = string.IsNullOrEmpty(outcome) ? "" : $"直近の処理結果: {outcome}";
            }

            if (unnamedScenesLabel != null)
            {
                List<string> unnamed = BasicAutoSaveService.GetUnnamedDirtyScenes();
                if (unnamed.Count > 0)
                {
                    unnamedScenesLabel.text = $"⚠️ 未保存(名前なし)シーン: {string.Join(", ", unnamed)}\n※保存先が指定されていないため自動保存はスキップされます。";
                    unnamedScenesLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    unnamedScenesLabel.style.display = DisplayStyle.None;
                }
            }
        }
    }
}
