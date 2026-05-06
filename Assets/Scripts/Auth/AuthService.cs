using System;
using System.Threading.Tasks;
using FrentePartido.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace FrentePartido.Auth
{
    public static class AuthService
    {
        public static string Token { get; private set; }
        public static string Username { get; private set; }
        public static string DisplayName { get; private set; }
        public static int UserId { get; private set; }
        public static bool IsLoggedIn => !string.IsNullOrEmpty(Token);

        public static async Task<AuthResult> Register(string username, string email, string password, string displayName)
        {
            var body = JsonUtility.ToJson(new RegisterRequest
            {
                username = username,
                email = email,
                password = password,
                displayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName
            });

            return await SendAuthRequest("/auth/register", body);
        }

        public static async Task<AuthResult> Login(string username, string password)
        {
            var body = JsonUtility.ToJson(new LoginRequest
            {
                username = username,
                password = password
            });

            return await SendAuthRequest("/auth/login", body);
        }

        public static void Logout()
        {
            Token = null;
            Username = null;
            DisplayName = null;
            UserId = 0;
            ClearSavedAuth();
        }

        public static async Task<bool> TryAutoLogin()
        {
            string savedToken = PlayerPrefs.GetString("auth_token", "");
            if (string.IsNullOrEmpty(savedToken)) return false;

            foreach (string baseUrl in GetBaseUrls())
            {
                try
                {
                    using var request = UnityWebRequest.Get($"{baseUrl}/auth/verify");
                    request.timeout = 5;
                    request.SetRequestHeader("Authorization", $"Bearer {savedToken}");
                    request.SetRequestHeader("Content-Type", "application/json");

                    var op = request.SendWebRequest();
                    while (!op.isDone) await Task.Yield();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        if (IsConnectionFailure(request)) continue;

                        ClearSavedAuth();
                        Debug.LogWarning($"[Auth] Auto-login skipped: {DescribeRequestError(request, baseUrl)}");
                        return false;
                    }

                    var response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                    Token = savedToken;
                    UserId = response.userId;
                    Username = response.username;
                    DisplayName = response.displayName;
                    Debug.Log($"[Auth] Auto-login OK via {baseUrl}");
                    return true;
                }
                catch
                {
                    // Try next configured auth endpoint.
                }
            }

            ClearSavedAuth();
            return false;
        }

        private static void ClearSavedAuth()
        {
            PlayerPrefs.DeleteKey("auth_token");
            PlayerPrefs.DeleteKey("auth_username");
            PlayerPrefs.Save();
        }

        private static async Task<AuthResult> SendAuthRequest(string endpoint, string jsonBody)
        {
            string lastConnectionError = "";
            foreach (string baseUrl in GetBaseUrls())
            {
                try
                {
                    using var request = new UnityWebRequest($"{baseUrl}{endpoint}", "POST");
                    request.timeout = 8;
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");

                    var op = request.SendWebRequest();
                    while (!op.isDone) await Task.Yield();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        string error = DescribeRequestError(request, baseUrl);
                        if (IsConnectionFailure(request))
                        {
                            lastConnectionError = error;
                            Debug.LogWarning($"[Auth] Endpoint unavailable, trying fallback: {error}");
                            continue;
                        }

                        return new AuthResult { success = false, error = error };
                    }

                    var response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                    Token = response.token;
                    UserId = response.userId;
                    Username = response.username;
                    DisplayName = response.displayName;

                    PlayerPrefs.SetString("auth_token", Token);
                    PlayerPrefs.Save();

                    Debug.Log($"[Auth] Request OK via {baseUrl}");
                    return new AuthResult { success = true };
                }
                catch (Exception e)
                {
                    lastConnectionError = $"Sin conexion al servidor ({baseUrl}): {e.Message}";
                }
            }

            return new AuthResult
            {
                success = false,
                error = string.IsNullOrWhiteSpace(lastConnectionError)
                    ? "Sin conexion al servidor de auth"
                    : lastConnectionError
            };
        }

        private static string[] GetBaseUrls()
        {
            string configuredUrl = GameConfig.Preferences?.authBaseUrl;
            if (string.IsNullOrWhiteSpace(configuredUrl))
            {
                configuredUrl = GameConfig.DEFAULT_AUTH_BASE_URL;
            }

            var urls = new System.Collections.Generic.List<string>
            {
                configuredUrl.Trim().TrimEnd('/')
            };

            foreach (string fallback in GameConfig.FALLBACK_AUTH_BASE_URLS)
            {
                string trimmed = fallback.Trim().TrimEnd('/');
                if (!urls.Contains(trimmed))
                {
                    urls.Add(trimmed);
                }
            }

            return urls.ToArray();
        }

        private static bool IsConnectionFailure(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.ConnectionError || request.responseCode == 0;
        }

        private static string DescribeRequestError(UnityWebRequest request, string baseUrl)
        {
            string body = request.downloadHandler?.text;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var errorResp = JsonUtility.FromJson<ErrorResponse>(body);
                    if (!string.IsNullOrWhiteSpace(errorResp?.error))
                    {
                        return errorResp.error;
                    }
                }
                catch
                {
                    // Body is not JSON; fall back to UnityWebRequest error.
                }
            }

            if (!string.IsNullOrWhiteSpace(request.error))
            {
                return $"No se puede contactar con auth ({baseUrl}): {request.error}";
            }

            return $"Error auth HTTP {request.responseCode} ({baseUrl})";
        }

        [Serializable]
        private class LoginRequest
        {
            public string username;
            public string password;
        }

        [Serializable]
        private class RegisterRequest
        {
            public string username;
            public string email;
            public string password;
            public string displayName;
        }

        [Serializable]
        private class AuthResponse
        {
            public string token;
            public int userId;
            public string username;
            public string displayName;
        }

        [Serializable]
        private class ErrorResponse
        {
            public string error;
        }
    }

    public struct AuthResult
    {
        public bool success;
        public string error;
    }
}
