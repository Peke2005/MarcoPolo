using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FrentePartido.Core;
using FrentePartido.Networking;
using FrentePartido.Data;

namespace FrentePartido.UI
{
    public class LobbyUI : MonoBehaviour
    {
        [Header("Info")]
        [SerializeField] private TMP_Text _joinCodeText;
        [SerializeField] private Button _copyCodeButton;

        [Header("Players")]
        [SerializeField] private TMP_Text _player1NameText;
        [SerializeField] private TMP_Text _player2NameText;
        [SerializeField] private TMP_Text _player1StatusText;
        [SerializeField] private TMP_Text _player2StatusText;

        [Header("Abilities")]
        [SerializeField] private Button _dashButton;
        [SerializeField] private Button _shieldButton;
        [SerializeField] private Button _mineButton;
        [SerializeField] private TMP_Text _selectedAbilityText;
        [SerializeField] private Image[] _abilityHighlights;

        [Header("Faction")]
        [SerializeField] private Button _blueButton;
        [SerializeField] private Button _redButton;

        [Header("Actions")]
        [SerializeField] private Button _readyButton;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Button _leaveButton;
        [SerializeField] private TMP_Text _readyButtonText;

        private bool _isReady;
        private int _selectedAbility;

        private void Start()
        {
            SetupButtons();
            UpdateJoinCode();
            UpdatePlayerList();

            // Only host sees start button
            bool isHost = NetworkSessionManager.Instance != null && NetworkSessionManager.Instance.IsHost;
            if (_startGameButton != null)
            {
                _startGameButton.gameObject.SetActive(isHost);
                _startGameButton.interactable = false;
            }

            // Default ability selection
            SelectAbility(GameConfig.Preferences.abilityIndex);

            if (NetworkSessionManager.Instance != null)
            {
                NetworkSessionManager.Instance.OnPlayerConnected += OnPlayerJoined;
                NetworkSessionManager.Instance.OnPlayerDisconnected += OnPlayerLeft;
            }
        }

        private void OnDestroy()
        {
            if (NetworkSessionManager.Instance != null)
            {
                NetworkSessionManager.Instance.OnPlayerConnected -= OnPlayerJoined;
                NetworkSessionManager.Instance.OnPlayerDisconnected -= OnPlayerLeft;
            }
        }

        private void OnPlayerJoined(ulong clientId)
        {
            UpdatePlayerList();
            CheckStartCondition();
        }

        private void OnPlayerLeft(ulong clientId)
        {
            UpdatePlayerList();
            CheckStartCondition();
        }

        private void SetupButtons()
        {
            _dashButton?.onClick.AddListener(() => SelectAbility(0));
            _shieldButton?.onClick.AddListener(() => SelectAbility(1));
            _mineButton?.onClick.AddListener(() => SelectAbility(2));
            _blueButton?.onClick.AddListener(() => SelectFaction(0));
            _redButton?.onClick.AddListener(() => SelectFaction(1));
            _readyButton?.onClick.AddListener(ToggleReady);
            _startGameButton?.onClick.AddListener(OnStartGame);
            _leaveButton?.onClick.AddListener(OnLeave);
            _copyCodeButton?.onClick.AddListener(CopyCodeToClipboard);
        }

        private void UpdateJoinCode()
        {
            if (_joinCodeText != null && NetworkSessionManager.Instance != null)
            {
                string code = NetworkSessionManager.Instance.JoinCode;
                _joinCodeText.text = $"CÓDIGO: {code}";
            }
        }

        private void CopyCodeToClipboard()
        {
            if (NetworkSessionManager.Instance != null)
            {
                GUIUtility.systemCopyBuffer = NetworkSessionManager.Instance.JoinCode;
                Debug.Log("[Lobby] Code copied to clipboard");
            }
        }

        private void SelectAbility(int index)
        {
            _selectedAbility = index;
            GameConfig.Preferences.abilityIndex = index;

            string[] names = { "Carrera Táctica", "Escudo Frontal", "Mina de Proximidad" };
            if (_selectedAbilityText != null && index < names.Length)
                _selectedAbilityText.text = names[index];

            // Update highlights
            if (_abilityHighlights != null)
            {
                for (int i = 0; i < _abilityHighlights.Length; i++)
                {
                    if (_abilityHighlights[i] != null)
                        _abilityHighlights[i].enabled = (i == index);
                }
            }

            LobbyManager.UpdatePlayerData("AbilityId", index.ToString());
        }

        private async void SelectFaction(int faction)
        {
            GameConfig.Preferences.colorIndex = faction;
            await LobbyManager.UpdatePlayerData("Faction", faction.ToString());
        }

        private async void ToggleReady()
        {
            _isReady = !_isReady;
            if (_readyButtonText != null)
                _readyButtonText.text = _isReady ? "LISTO ✓" : "LISTO";

            await LobbyManager.UpdatePlayerData("IsReady", _isReady.ToString());
            CheckStartCondition();
        }

        private void OnStartGame()
        {
            GameConfig.Save();
            SceneFlowController.LoadSceneNetwork(SceneFlowController.SCENE_GAME);
        }

        private void OnLeave()
        {
            if (NetworkSessionManager.Instance != null)
                NetworkSessionManager.Instance.LeaveSession();
            else
                SceneFlowController.ReturnToMainMenu();
        }

        private void UpdatePlayerList()
        {
            // Update from NetworkGameState
            var state = NetworkGameState.Instance;
            if (state == null) return;

            if (_player1NameText != null)
                _player1NameText.text = "Jugador 1";
            if (_player2NameText != null)
                _player2NameText.text = "Esperando...";

            if (_player1StatusText != null)
                _player1StatusText.text = "Conectado";
            if (_player2StatusText != null)
                _player2StatusText.text = "---";
        }

        private void CheckStartCondition()
        {
            if (_startGameButton == null) return;
            // Enable start when 2 players connected (simplified)
            bool canStart = Unity.Netcode.NetworkManager.Singleton != null &&
                           Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds.Count >= 2;
            _startGameButton.interactable = canStart;
        }
    }
}
