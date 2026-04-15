using UnityEngine;

namespace FrentePartido.Core
{
    [System.Serializable]
    public class PlayerPreferences
    {
        public string playerName = "Soldado";
        public string authBaseUrl = GameConfig.DEFAULT_AUTH_BASE_URL;
        public int colorIndex;
        public int abilityIndex;
        public float musicVolume = 0.7f;
        public float sfxVolume = 1f;
        public float mouseSensitivity = 1f;
    }

    public static class GameConfig
    {
        private const string PREFS_KEY = "FP_PlayerPrefs";
        public const string DEFAULT_AUTH_BASE_URL = "http://26.234.30.190:3001";

        public static PlayerPreferences Preferences { get; private set; } = new PlayerPreferences();

        public static void Load()
        {
            string json = PlayerPrefs.GetString(PREFS_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                Preferences = JsonUtility.FromJson<PlayerPreferences>(json);
            }

            Preferences.authBaseUrl = DEFAULT_AUTH_BASE_URL;

            Debug.Log($"[GameConfig] Loaded. Name: {Preferences.playerName} | Auth: {Preferences.authBaseUrl}");
        }

        public static void Save()
        {
            string json = JsonUtility.ToJson(Preferences);
            PlayerPrefs.SetString(PREFS_KEY, json);
            PlayerPrefs.Save();
        }
    }
}
