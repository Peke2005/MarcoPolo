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
            PlayerPrefs.DeleteKey("auth_token");
            PlayerPrefs.Save();
        }

        public static async Task<bool> TryAutoLogin()
        {
            string savedToken = PlayerPrefs.GetString("auth_token", "");
            if (string.IsNullOrEmpty(savedToken)) return false;

            try
            {
                using var request = UnityWebRequest.Get($"{GetBaseUrl()}/auth/verify");
                request.SetRequestHeader("Authorization", $"Bearer {savedToken}");
                request.SetRequestHeader("Content-Type", "application/json");

                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    PlayerPrefs.DeleteKey("auth_token");
                    return false;
                }

                var response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                Token = savedToken;
                UserId = response.userId;
                Username = response.username;
                DisplayName = response.displayName;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<AuthResult> SendAuthRequest(string endpoint, string jsonBody)
        {
            try
            {
                using var request = new UnityWebRequest($"{GetBaseUrl()}{endpoint}", "POST");
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var errorResp = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
                    return new AuthResult { success = false, error = errorResp?.error ?? "Error de conexion" };
                }

                var response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                Token = response.token;
                UserId = response.userId;
                Username = response.username;
                DisplayName = response.displayName;

                PlayerPrefs.SetString("auth_token", Token);
                PlayerPrefs.Save();

                return new AuthResult { success = true };
            }
            catch (Exception e)
            {
                return new AuthResult { success = false, error = $"Sin conexion al servidor: {e.Message}" };
            }
        }

        private static string GetBaseUrl()
        {
            string configuredUrl = GameConfig.Preferences?.authBaseUrl;
            if (string.IsNullOrWhiteSpace(configuredUrl))
            {
                configuredUrl = GameConfig.DEFAULT_AUTH_BASE_URL;
            }

            return configuredUrl.Trim().TrimEnd('/');
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
