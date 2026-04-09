using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FrentePartido.Core;
using FrentePartido.Networking;

namespace FrentePartido.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Main Buttons")]
        [SerializeField] private Button _createGameButton;
        [SerializeField] private Button _joinGameButton;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private Button _quitButton;

        [Header("Player Name")]
        [SerializeField] private TMP_InputField _playerNameInput;

        [Header("Join Code Panel")]
        [SerializeField] private GameObject _joinCodePanel;
        [SerializeField] private TMP_InputField _joinCodeInput;
        [SerializeField] private Button _confirmJoinButton;
        [SerializeField] private Button _cancelJoinButton;

        [Header("Options Panel")]
        [SerializeField] private GameObject _optionsPanel;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;
        [SerializeField] private Button _closeOptionsButton;

        [Header("Feedback")]
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private GameObject _loadingIndicator;

        private void Start()
        {
            LoadPreferences();
            SetupButtons();

            if (_joinCodePanel != null) _joinCodePanel.SetActive(false);
            if (_optionsPanel != null) _optionsPanel.SetActive(false);
            if (_loadingIndicator != null) _loadingIndicator.SetActive(false);
        }

        private void LoadPreferences()
        {
            var prefs = GameConfig.Preferences;
            if (_playerNameInput != null)
                _playerNameInput.text = prefs.playerName;
            if (_musicVolumeSlider != null)
                _musicVolumeSlider.value = prefs.musicVolume;
            if (_sfxVolumeSlider != null)
                _sfxVolumeSlider.value = prefs.sfxVolume;
        }

        private void SetupButtons()
        {
            _createGameButton?.onClick.AddListener(OnCreateGame);
            _joinGameButton?.onClick.AddListener(OnJoinGameClicked);
            _optionsButton?.onClick.AddListener(() => _optionsPanel?.SetActive(true));
            _quitButton?.onClick.AddListener(() => Application.Quit());
            _confirmJoinButton?.onClick.AddListener(OnConfirmJoin);
            _cancelJoinButton?.onClick.AddListener(() => _joinCodePanel?.SetActive(false));
            _closeOptionsButton?.onClick.AddListener(OnCloseOptions);

            if (_musicVolumeSlider != null)
                _musicVolumeSlider.onValueChanged.AddListener(v => GameConfig.Preferences.musicVolume = v);
            if (_sfxVolumeSlider != null)
                _sfxVolumeSlider.onValueChanged.AddListener(v => GameConfig.Preferences.sfxVolume = v);
        }

        private void SavePlayerName()
        {
            if (_playerNameInput != null && !string.IsNullOrWhiteSpace(_playerNameInput.text))
            {
                GameConfig.Preferences.playerName = _playerNameInput.text.Trim();
            }
            GameConfig.Save();
        }

        private async void OnCreateGame()
        {
            SavePlayerName();
            SetLoading(true, "Creando partida...");

            try
            {
                await NetworkSessionManager.Instance.CreateSession();
                SceneFlowController.LoadScene(SceneFlowController.SCENE_LOBBY);
            }
            catch (System.Exception e)
            {
                SetLoading(false);
                ShowError($"Error al crear: {e.Message}");
            }
        }

        private void OnJoinGameClicked()
        {
            SavePlayerName();
            if (_joinCodePanel != null)
            {
                _joinCodePanel.SetActive(true);
                _joinCodeInput?.Select();
            }
        }

        private async void OnConfirmJoin()
        {
            string code = _joinCodeInput?.text?.Trim().ToUpper();
            if (string.IsNullOrEmpty(code))
            {
                ShowError("Introduce un código válido");
                return;
            }

            _joinCodePanel?.SetActive(false);
            SetLoading(true, "Uniéndose...");

            try
            {
                await NetworkSessionManager.Instance.JoinSession(code);
                SceneFlowController.LoadScene(SceneFlowController.SCENE_LOBBY);
            }
            catch (System.Exception e)
            {
                SetLoading(false);
                ShowError($"Error al unirse: {e.Message}");
            }
        }

        private void OnCloseOptions()
        {
            GameConfig.Save();
            _optionsPanel?.SetActive(false);
        }

        private void SetLoading(bool loading, string message = "")
        {
            if (_loadingIndicator != null) _loadingIndicator.SetActive(loading);
            _createGameButton?.gameObject.SetActive(!loading);
            _joinGameButton?.gameObject.SetActive(!loading);
            if (_statusText != null) _statusText.text = message;
        }

        private void ShowError(string msg)
        {
            if (_statusText != null) _statusText.text = msg;
            Debug.LogError($"[MainMenu] {msg}");
        }
    }
}
