using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FrentePartido.Core
{
    public static class ProfileStats
    {
        private static Snapshot _cached;
        private static bool _hasCache;

        public readonly struct Snapshot
        {
            public readonly int Matches;
            public readonly int Wins;
            public readonly int Losses;
            public readonly float WinRate;
            public readonly string Rank;

            public Snapshot(int matches, int wins, int losses)
            {
                Matches = Mathf.Max(0, matches);
                Wins = Mathf.Max(0, wins);
                Losses = Mathf.Max(0, losses);
                WinRate = Matches > 0 ? (Wins * 100f) / Matches : 0f;
                Rank = ResolveRank(Matches, Wins, WinRate);
            }
        }

        public static Snapshot Load()
        {
            return _hasCache ? _cached : new Snapshot(0, 0, 0);
        }

        public static Task<Snapshot> FetchAsync()
        {
            return SendStatsRequest("GET", "/profile/stats", null);
        }

        public static Task<Snapshot> RecordMatchAsync(bool won)
        {
            string body = JsonUtility.ToJson(new MatchResultRequest { won = won });
            return SendStatsRequest("POST", "/profile/match", body);
        }

        private static async Task<Snapshot> SendStatsRequest(string method, string endpoint, string jsonBody)
        {
            string token = PlayerPrefs.GetString("auth_token", "");
            if (string.IsNullOrWhiteSpace(token))
            {
                Debug.LogWarning("[ProfileStats] No auth_token. Stats require login.");
                return Load();
            }

            string lastError = "";
            foreach (string baseUrl in GetBaseUrls())
            {
                try
                {
                    using var request = new UnityWebRequest($"{baseUrl}{endpoint}", method);
                    request.timeout = 8;
                    request.downloadHandler = new DownloadHandlerBuffer();
                    if (!string.IsNullOrEmpty(jsonBody))
                    {
                        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    }
                    request.SetRequestHeader("Authorization", $"Bearer {token}");
                    request.SetRequestHeader("Content-Type", "application/json");

                    var op = request.SendWebRequest();
                    while (!op.isDone) await Task.Yield();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        lastError = DescribeRequestError(request, baseUrl);
                        if (IsConnectionFailure(request)) continue;
                        Debug.LogWarning($"[ProfileStats] {lastError}");
                        return Load();
                    }

                    var response = JsonUtility.FromJson<StatsResponse>(request.downloadHandler.text);
                    _cached = new Snapshot(response.matchesPlayed, response.wins, response.losses);
                    _hasCache = true;
                    return _cached;
                }
                catch (Exception e)
                {
                    lastError = $"Sin conexion a stats ({baseUrl}): {e.Message}";
                }
            }

            if (!string.IsNullOrWhiteSpace(lastError))
                Debug.LogWarning($"[ProfileStats] {lastError}");
            return Load();
        }

        private static IEnumerable<string> GetBaseUrls()
        {
            string configuredUrl = GameConfig.Preferences?.authBaseUrl;
            if (string.IsNullOrWhiteSpace(configuredUrl))
                configuredUrl = GameConfig.DEFAULT_AUTH_BASE_URL;

            var seen = new HashSet<string>();
            string primary = configuredUrl.Trim().TrimEnd('/');
            if (seen.Add(primary)) yield return primary;

            foreach (string fallback in GameConfig.FALLBACK_AUTH_BASE_URLS)
            {
                string trimmed = fallback.Trim().TrimEnd('/');
                if (seen.Add(trimmed)) yield return trimmed;
            }
        }

        private static bool IsConnectionFailure(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.ConnectionError || request.responseCode == 0;
        }

        private static string DescribeRequestError(UnityWebRequest request, string baseUrl)
        {
            if (!string.IsNullOrWhiteSpace(request.error))
                return $"No se puede contactar con stats ({baseUrl}): {request.error}";
            return $"Stats HTTP {request.responseCode} ({baseUrl})";
        }

        private static string ResolveRank(int matches, int wins, float winRate)
        {
            if (matches <= 0) return "SIN RANGO";
            if (wins >= 30 && winRate >= 70f) return "ELITE";
            if (wins >= 18 && winRate >= 60f) return "ORO";
            if (wins >= 10 && winRate >= 50f) return "PLATA";
            if (wins >= 4) return "BRONCE";
            return "RECLUTA";
        }

        [Serializable]
        private class MatchResultRequest
        {
            public bool won;
        }

        [Serializable]
        private class StatsResponse
        {
            public int matchesPlayed;
            public int wins;
            public int losses;
            public float winRate;
            public string rank;
        }
    }
}
