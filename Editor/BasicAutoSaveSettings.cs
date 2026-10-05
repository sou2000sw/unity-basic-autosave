using UnityEditor;
using UnityEngine;

namespace BasicAutoSave
{
    [FilePath("UserSettings/BasicAutoSaveSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class BasicAutoSaveSettings : ScriptableSingleton<BasicAutoSaveSettings>
    {
        [SerializeField] private bool autoSaveEnabled = false;
        [SerializeField] private int intervalMinutes = 5;

        public bool AutoSaveEnabled
        {
            get => autoSaveEnabled;
            set
            {
                if (autoSaveEnabled != value)
                {
                    autoSaveEnabled = value;
                    SaveSettings();
                }
            }
        }

        public int IntervalMinutes
        {
            get => intervalMinutes;
            set
            {
                int clamped = Mathf.Clamp(value, 1, 30);
                if (intervalMinutes != clamped)
                {
                    intervalMinutes = clamped;
                    SaveSettings();
                }
            }
        }

        private void OnEnable()
        {
            intervalMinutes = Mathf.Clamp(intervalMinutes, 1, 30);
        }

        public void SaveSettings()
        {
            intervalMinutes = Mathf.Clamp(intervalMinutes, 1, 30);
            Save(true);
        }
    }
}
