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
        public const string AUTH_URL_PREFS_KEY = "FP_AuthBaseUrl";
        public const string DEFAULT_AUTH_BASE_URL = "https://kufkgjyeptuzptmegsmf.supabase.co/functions/v1/frentepartido-auth";
        public static readonly string[] FALLBACK_AUTH_BASE_URLS = {};

        public static PlayerPreferences Preferences { get; private set; } = new PlayerPreferences();

        public static void Load()
        {
            string json = PlayerPrefs.GetString(PREFS_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                Preferences = JsonUtility.FromJson<PlayerPreferences>(json);
            }

            if (Preferences == null)
            {
                Preferences = new PlayerPreferences();
            }

            string envUrl = System.Environment.GetEnvironmentVariable("FP_AUTH_BASE_URL");
            string savedOverride = PlayerPrefs.GetString(AUTH_URL_PREFS_KEY, "");

            if (!string.IsNullOrWhiteSpace(envUrl))
            {
                Preferences.authBaseUrl = envUrl.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(savedOverride))
            {
                Preferences.authBaseUrl = savedOverride.Trim();
            }
            else
            {
                Preferences.authBaseUrl = DEFAULT_AUTH_BASE_URL;
            }

            if (IsLegacyRadminUrl(Preferences.authBaseUrl))
            {
                Preferences.authBaseUrl = DEFAULT_AUTH_BASE_URL;
                PlayerPrefs.DeleteKey(AUTH_URL_PREFS_KEY);
            }

            Debug.Log($"[GameConfig] Loaded. Name: {Preferences.playerName} | Auth: {Preferences.authBaseUrl}");
        }

        private static bool IsLegacyRadminUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            return url.Contains("26.234.30.190") || url.Contains("26.17.117.206");
        }

        public static void Save()
        {
            string json = JsonUtility.ToJson(Preferences);
            PlayerPrefs.SetString(PREFS_KEY, json);
            PlayerPrefs.Save();
        }
    }
}
