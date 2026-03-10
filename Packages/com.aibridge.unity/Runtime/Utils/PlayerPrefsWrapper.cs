
#nullable enable
using UnityEngine;

namespace Extensions.Unity.PlayerPrefsEx
{
    /// <summary>
    /// Simple PlayerPrefs wrapper replacing the PlayerPrefsEx package dependency.
    /// </summary>
    public class PlayerPrefsString
    {
        private readonly string _key;
        private readonly string _defaultValue;

        public PlayerPrefsString(string key, string defaultValue = "")
        {
            _key = key;
            _defaultValue = defaultValue;
        }

        public string Value
        {
            get => PlayerPrefs.GetString(_key, _defaultValue);
            set
            {
                PlayerPrefs.SetString(_key, value);
                PlayerPrefs.Save();
            }
        }
    }

    public class PlayerPrefsBool
    {
        private readonly string _key;
        private readonly bool _defaultValue;

        public PlayerPrefsBool(string key, bool defaultValue = false)
        {
            _key = key;
            _defaultValue = defaultValue;
        }

        public bool Value
        {
            get => PlayerPrefs.GetInt(_key, _defaultValue ? 1 : 0) == 1;
            set
            {
                PlayerPrefs.SetInt(_key, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }

    public class PlayerPrefsInt
    {
        private readonly string _key;
        private readonly int _defaultValue;

        public PlayerPrefsInt(string key, int defaultValue = 0)
        {
            _key = key;
            _defaultValue = defaultValue;
        }

        public int Value
        {
            get => PlayerPrefs.GetInt(_key, _defaultValue);
            set
            {
                PlayerPrefs.SetInt(_key, value);
                PlayerPrefs.Save();
            }
        }
    }
}
