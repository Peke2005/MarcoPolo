using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FrentePartido.Core;

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
            SetupButtons();
            ShowLogin();
            TryAutoLogin();
        }

        private void SetupButtons()
        {
            _loginButton?.onClick.AddListener(OnLogin);
            _registerButton?.onClick.AddListener(OnRegister);
            _goToRegisterButton?.onClick.AddListener(ShowRegister);
            _goToLoginButton?.onClick.AddListener(ShowLogin);
        }

        private async void TryAutoLogin()
        {
            SetLoading(true, "Verificando sesión...");
            bool success = await AuthService.TryAutoLogin();

            if (success)
            {
                GoToMainMenu();
            }
            else
            {
                SetLoading(false);
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

            SetLoading(true, "Iniciando sesión...");
            var result = await AuthService.Login(user, pass);

            if (result.success)
            {
                GoToMainMenu();
            }
            else
            {
                SetLoading(false);
                ShowError(result.error);
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
                ShowError("La contraseña debe tener al menos 6 caracteres");
                return;
            }

            SetLoading(true, "Registrando...");
            var result = await AuthService.Register(user, email, pass, displayName);

            if (result.success)
            {
                GoToMainMenu();
            }
            else
            {
                SetLoading(false);
                ShowError(result.error);
            }
        }

        private void GoToMainMenu()
        {
            // Set display name in game config
            if (!string.IsNullOrEmpty(AuthService.DisplayName))
            {
                GameConfig.Preferences.playerName = AuthService.DisplayName;
                GameConfig.Save();
            }

            SceneFlowController.LoadScene(SceneFlowController.SCENE_MAIN_MENU);
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
            if (_statusText != null) _statusText.text = message;
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
    }
}
