using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace FrentePartido.Auth
{
    public static class AuthService
    {
        // Cambiar a la IP de tu máquina si pruebas en otro dispositivo
        private const string BASE_URL = "http://localhost:3001";

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
                using var request = UnityWebRequest.Get($"{BASE_URL}/auth/verify");
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
                using var request = new UnityWebRequest($"{BASE_URL}{endpoint}", "POST");
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var errorResp = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
                    return new AuthResult { success = false, error = errorResp?.error ?? "Error de conexión" };
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
                return new AuthResult { success = false, error = $"Sin conexión al servidor: {e.Message}" };
            }
        }

        // ── Request/Response DTOs ──
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
