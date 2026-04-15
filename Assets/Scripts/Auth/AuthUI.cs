using System;
using System.Threading.Tasks;
using FrentePartido.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FrentePartido.Auth
{
    public class AuthUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject _loginPanel;
        [SerializeField] private GameObject _registerPanel;

        [Header("Login")]
        [SerializeField] private TMP_InputField _loginUsername;
        [SerializeField] private TMP_InputField _loginPassword;
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _goToRegisterButton;

        [Header("Register")]
        [SerializeField] private TMP_InputField _regUsername;
        [SerializeField] private TMP_InputField _regEmail;
        [SerializeField] private TMP_InputField _regPassword;
        [SerializeField] private TMP_InputField _regDisplayName;
        [SerializeField] private Button _registerButton;
        [SerializeField] private Button _goToLoginButton;

        [Header("Feedback")]
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private GameObject _loadingIndicator;

        private void Start()
        {
            GameConfig.Load();
            SetupButtons();
            ShowLogin();
            TryAutoLogin();
        }

        private void SetupButtons()
        {
            if (_loginButton != null)
            {
                _loginButton.onClick.RemoveAllListeners();
                _loginButton.onClick.AddListener(OnLogin);
            }

            if (_registerButton != null)
            {
                _registerButton.onClick.RemoveAllListeners();
                _registerButton.onClick.AddListener(OnRegister);
            }

            if (_goToRegisterButton != null)
            {
                _goToRegisterButton.onClick.RemoveAllListeners();
                _goToRegisterButton.onClick.AddListener(ShowRegister);
            }

            if (_goToLoginButton != null)
            {
                _goToLoginButton.onClick.RemoveAllListeners();
                _goToLoginButton.onClick.AddListener(ShowLogin);
            }
        }

        private async void TryAutoLogin()
        {
            try
            {
                SetLoading(true, "Verificando sesion...");
                bool success = await AuthService.TryAutoLogin();

                if (success)
                {
                    await CompleteAuthFlow("Sesion recuperada. Entrando...");
                }
                else
                {
                    SetLoading(false);
                }
            }
            catch (Exception e)
            {
                SetLoading(false);
                ShowError($"No se pudo verificar la sesion: {e.Message}");
            }
        }

        private async void OnLogin()
        {
            string user = _loginUsername?.text?.Trim();
            string pass = _loginPassword?.text;

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                ShowError("Completa todos los campos");
                return;
            }

            try
            {
                SetLoading(true, "Iniciando sesion...");
                var result = await AuthService.Login(user, pass);

                if (result.success)
                {
                    await CompleteAuthFlow("Sesion iniciada. Entrando...");
                }
                else
                {
                    SetLoading(false);
                    ShowError(result.error);
                }
            }
            catch (Exception e)
            {
                SetLoading(false);
                ShowError($"Error al iniciar sesion: {e.Message}");
            }
        }

        private async void OnRegister()
        {
            string user = _regUsername?.text?.Trim();
            string email = _regEmail?.text?.Trim();
            string pass = _regPassword?.text;
            string displayName = _regDisplayName?.text?.Trim();

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass))
            {
                ShowError("Completa todos los campos obligatorios");
                return;
            }

            if (pass.Length < 6)
            {
                ShowError("La contrasena debe tener al menos 6 caracteres");
                return;
            }

            try
            {
                SetLoading(true, $"Registrando en {GameConfig.DEFAULT_AUTH_BASE_URL}...");
                var result = await AuthService.Register(user, email, pass, displayName);

                if (result.success)
                {
                    await CompleteAuthFlow("Registro correcto. Entrando...");
                    return;
                }

                if (LooksLikeExistingAccount(result.error))
                {
                    ShowInfo("La cuenta ya existe. Probando inicio de sesion...");
                    var loginResult = await AuthService.Login(user, pass);
                    if (loginResult.success)
                    {
                        await CompleteAuthFlow("Sesion iniciada. Entrando...");
                        return;
                    }
                }

                SetLoading(false);
                ShowError(result.error);
            }
            catch (Exception e)
            {
                SetLoading(false);
                ShowError($"Error al registrar: {e.Message}");
            }
        }

        private async Task CompleteAuthFlow(string message)
        {
            try
            {
                ShowSuccess(message);

                if (!string.IsNullOrEmpty(AuthService.DisplayName))
                {
                    GameConfig.Preferences.playerName = AuthService.DisplayName;
                }

                GameConfig.Save();
                await Task.Yield();
                SceneFlowController.LoadScene(SceneFlowController.SCENE_MAIN_MENU);
            }
            catch (Exception e)
            {
                SetLoading(false);
                ShowError($"No se pudo abrir el menu principal: {e.Message}");
            }
        }

        private void ShowLogin()
        {
            _loginPanel?.SetActive(true);
            _registerPanel?.SetActive(false);
            ClearStatus();
        }

        private void ShowRegister()
        {
            _loginPanel?.SetActive(false);
            _registerPanel?.SetActive(true);
            ClearStatus();
        }

        private void SetLoading(bool loading, string message = "")
        {
            if (_loadingIndicator != null) _loadingIndicator.SetActive(loading);
            if (_loginButton != null) _loginButton.interactable = !loading;
            if (_registerButton != null) _registerButton.interactable = !loading;

            if (_statusText != null)
            {
                _statusText.text = message;
                _statusText.color = Color.white;
            }
        }

        private void ShowInfo(string msg)
        {
            if (_statusText != null)
            {
                _statusText.text = msg;
                _statusText.color = new Color(1f, 0.9f, 0.35f);
            }

            Debug.Log($"[Auth] {msg}");
        }

        private void ShowSuccess(string msg)
        {
            if (_statusText != null)
            {
                _statusText.text = msg;
                _statusText.color = new Color(0.35f, 1f, 0.55f);
            }

            Debug.Log($"[Auth] {msg}");
        }

        private void ShowError(string msg)
        {
            if (_statusText != null)
            {
                _statusText.text = msg;
                _statusText.color = Color.red;
            }

            Debug.LogWarning($"[Auth] {msg}");
        }

        private void ClearStatus()
        {
            if (_statusText != null)
            {
                _statusText.text = "";
                _statusText.color = Color.white;
            }
        }

        private static bool LooksLikeExistingAccount(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return false;
            }

            string normalized = error.ToLowerInvariant();
            return normalized.Contains("existe") || normalized.Contains("already");
        }
    }
}
