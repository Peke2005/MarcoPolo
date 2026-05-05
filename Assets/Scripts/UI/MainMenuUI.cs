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
        [SerializeField] private TMP_Text _profileText;

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

        static readonly Color BG = C(0.035f, 0.055f, 0.050f);
        static readonly Color PANEL = C(0.095f, 0.125f, 0.115f);
        static readonly Color PANEL2 = C(0.130f, 0.165f, 0.145f);
        static readonly Color GREEN = C(0.34f, 0.78f, 0.42f);
        static readonly Color GOLD = C(1.00f, 0.76f, 0.22f);
        static readonly Color BLUE = C(0.25f, 0.52f, 0.96f);
        static readonly Color RED = C(0.78f, 0.22f, 0.20f);
        static readonly Color TXT = C(0.94f, 0.96f, 0.92f);
        static readonly Color MUTED = C(0.62f, 0.68f, 0.62f);
        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        private void Awake()
        {
            BuildRuntimeUI();
        }

        private void Start()
        {
            ApplyAuthenticatedName();
            LoadPreferences();
            SetupButtons();

            if (_joinCodePanel != null) _joinCodePanel.SetActive(false);
            if (_optionsPanel != null) _optionsPanel.SetActive(false);
            if (_loadingIndicator != null) _loadingIndicator.SetActive(false);
        }

        private void BuildRuntimeUI()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            var bg = Panel("BG", transform, BG);
            Stretch(bg);

            AddSoftGlow(bg.transform, "GlowLeft", new Vector2(-360f, 220f), new Vector2(520f, 520f), new Color(0.25f, 0.65f, 0.30f, 0.18f));
            AddSoftGlow(bg.transform, "GlowRight", new Vector2(390f, -210f), new Vector2(620f, 620f), new Color(1.0f, 0.72f, 0.18f, 0.12f));

            var shell = Panel("Shell", bg.transform, new Color(0f, 0f, 0f, 0f));
            Anchors(shell, 0.11f, 0.08f, 0.89f, 0.92f);

            var title = Text("Title", shell.transform, "MARCO POLO", 64, FontStyles.Bold, TXT);
            Anchors(title, 0f, 0.82f, 1f, 1f);
            title.alignment = TextAlignmentOptions.Left;
            title.characterSpacing = 8f;

            // Accent bar under the title — adds visual weight without extra text.
            var accent = Panel("TitleAccent", shell.transform, GOLD);
            Anchors(accent, 0f, 0.80f, 0.10f, 0.815f);

            var subtitle = Text("Subtitle", shell.transform, "DUELO TACTICO ONLINE", 22, FontStyles.Bold, GOLD);
            Anchors(subtitle, 0.115f, 0.795f, 1f, 0.84f);
            subtitle.alignment = TextAlignmentOptions.Left;
            subtitle.characterSpacing = 8f;

            var card = Panel("MenuCard", shell.transform, PANEL);
            Anchors(card, 0f, 0.03f, 0.58f, 0.68f);

            _profileText = Text("Profile", card.transform, "Sesion: Jugador", 16, FontStyles.Bold, TXT);
            Anchors(_profileText, 0.08f, 0.82f, 0.92f, 0.93f);
            _profileText.alignment = TextAlignmentOptions.Left;

            var hint = Text("Hint", card.transform, "Crea sala, copia codigo y tu amigo entra desde Unity.", 12, FontStyles.Normal, MUTED);
            Anchors(hint, 0.08f, 0.73f, 0.92f, 0.82f);
            hint.alignment = TextAlignmentOptions.Left;

            _createGameButton = MenuButton(card.transform, "Create", "CREAR SALA", GREEN, 0.08f, 0.56f, 0.92f, 0.69f);
            _joinGameButton = MenuButton(card.transform, "Join", "UNIRSE CON CODIGO", BLUE, 0.08f, 0.40f, 0.92f, 0.53f);
            _optionsButton = MenuButton(card.transform, "Options", "AJUSTES", PANEL2, 0.08f, 0.24f, 0.92f, 0.37f);
            _quitButton = MenuButton(card.transform, "Quit", "SALIR", RED, 0.08f, 0.08f, 0.92f, 0.21f);

            BuildJoinPanel(shell.transform);
            BuildOptionsPanel(shell.transform);

            BuildLoadingOverlay(card.transform);
        }

        private void BuildLoadingOverlay(Transform cardTransform)
        {
            // Full-card modal that shows during async ops. Sits over the buttons so the
            // loading message is centered and never overlaps SALIR or floats off-card.
            var overlay = Panel("LoadingOverlay", cardTransform, new Color(0.06f, 0.085f, 0.075f, 0.94f));
            Stretch(overlay);

            var pulse = Panel("LoadingPulse", overlay.transform, new Color(GOLD.r, GOLD.g, GOLD.b, 0.10f));
            Anchors(pulse, 0.18f, 0.42f, 0.82f, 0.62f);

            var label = Text("LoadingLabel", overlay.transform, "CARGANDO", 26, FontStyles.Bold, GOLD);
            Anchors(label, 0.05f, 0.50f, 0.95f, 0.66f);
            label.alignment = TextAlignmentOptions.Center;
            label.characterSpacing = 10f;

            _statusText = Text("LoadingStatus", overlay.transform, "", 14, FontStyles.Normal, TXT);
            Anchors(_statusText, 0.05f, 0.36f, 0.95f, 0.48f);
            _statusText.alignment = TextAlignmentOptions.Center;
            _statusText.characterSpacing = 2f;

            var dots = Text("LoadingDots", overlay.transform, "•   •   •", 22, FontStyles.Bold, GOLD);
            Anchors(dots, 0.05f, 0.24f, 0.95f, 0.36f);
            dots.alignment = TextAlignmentOptions.Center;
            dots.characterSpacing = 8f;
            dots.gameObject.AddComponent<LoadingDotsAnimator>();

            _loadingIndicator = overlay.gameObject;
        }

        private void BuildJoinPanel(Transform parent)
        {
            var panel = Panel("JoinCodePanel", parent, PANEL);
            Anchors(panel, 0.62f, 0.30f, 1f, 0.68f);
            _joinCodePanel = panel.gameObject;

            var title = Text("JoinTitle", panel.transform, "ENTRAR A SALA", 22, FontStyles.Bold, TXT);
            Anchors(title, 0.08f, 0.76f, 0.92f, 0.92f);
            title.alignment = TextAlignmentOptions.Left;

            _joinCodeInput = Input(panel.transform, "JoinInput", "CODIGO RELAY", 0.08f, 0.50f, 0.92f, 0.68f);
            _confirmJoinButton = MenuButton(panel.transform, "ConfirmJoin", "CONECTAR", GREEN, 0.08f, 0.25f, 0.92f, 0.43f);
            _cancelJoinButton = MenuButton(panel.transform, "CancelJoin", "CANCELAR", RED, 0.08f, 0.06f, 0.92f, 0.20f);
        }

        private void BuildOptionsPanel(Transform parent)
        {
            var panel = Panel("OptionsPanel", parent, PANEL);
            Anchors(panel, 0.62f, 0.05f, 1f, 0.27f);
            _optionsPanel = panel.gameObject;

            var title = Text("OptTitle", panel.transform, "AUDIO", 18, FontStyles.Bold, TXT);
            Anchors(title, 0.08f, 0.70f, 0.92f, 0.92f);
            title.alignment = TextAlignmentOptions.Left;

            _musicVolumeSlider = Slider(panel.transform, "Music", 0.08f, 0.45f, 0.92f, 0.58f);
            _sfxVolumeSlider = Slider(panel.transform, "Sfx", 0.08f, 0.25f, 0.92f, 0.38f);
            _closeOptionsButton = MenuButton(panel.transform, "CloseOptions", "OK", BLUE, 0.65f, 0.05f, 0.92f, 0.20f);
        }

        private void ApplyAuthenticatedName()
        {
            if (_playerNameInput != null)
                _playerNameInput.gameObject.SetActive(false);
            if (_profileText != null)
                _profileText.text = "Sesion: " + (GameConfig.Preferences.playerName ?? "Jugador");
        }

        private void LoadPreferences()
        {
            var prefs = GameConfig.Preferences;
            if (_musicVolumeSlider != null) _musicVolumeSlider.value = prefs.musicVolume;
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.value = prefs.sfxVolume;
        }

        private void SetupButtons()
        {
            _createGameButton?.onClick.RemoveAllListeners();
            _joinGameButton?.onClick.RemoveAllListeners();
            _optionsButton?.onClick.RemoveAllListeners();
            _quitButton?.onClick.RemoveAllListeners();
            _confirmJoinButton?.onClick.RemoveAllListeners();
            _cancelJoinButton?.onClick.RemoveAllListeners();
            _closeOptionsButton?.onClick.RemoveAllListeners();

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
            ApplyAuthenticatedName();
            GameConfig.Save();
        }

        private async void OnCreateGame()
        {
            SavePlayerName();
            SetLoading(true, "Creando sala Relay...");

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
                _joinCodeInput.text = "";
                _joinCodeInput.Select();
            }
        }

        private async void OnConfirmJoin()
        {
            string code = _joinCodeInput?.text?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
            {
                ShowError("Introduce un codigo valido");
                return;
            }

            _joinCodePanel?.SetActive(false);
            SetLoading(true, "Conectando a Relay...");

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
            if (_statusText != null)
            {
                _statusText.text = message;
                _statusText.color = loading ? TXT : new Color(1f, 0.34f, 0.28f, 1f);
            }
        }

        private void ShowError(string msg)
        {
            // Briefly flash the loading overlay with an error styling so users see why
            // the action failed instead of the message landing off-card in gold text.
            if (_loadingIndicator != null) _loadingIndicator.SetActive(true);
            if (_statusText != null)
            {
                _statusText.text = msg;
                _statusText.color = new Color(1f, 0.34f, 0.28f, 1f);
            }
            CancelInvoke(nameof(HideLoading));
            Invoke(nameof(HideLoading), 3f);
            Debug.LogError($"[MainMenu] {msg}");
        }

        private void HideLoading()
        {
            if (_loadingIndicator != null) _loadingIndicator.SetActive(false);
        }

        private static Button MenuButton(Transform parent, string name, string label, Color color, float x1, float y1, float x2, float y2)
        {
            var rt = Panel(name, parent, color);
            Anchors(rt, x1, y1, x2, y2);
            var button = rt.gameObject.AddComponent<Button>();
            var t = Text(name + "Text", rt.transform, label, 15, FontStyles.Bold, Color.white);
            Stretch(t.GetComponent<RectTransform>());
            t.alignment = TextAlignmentOptions.Center;
            t.characterSpacing = 2f;
            return button;
        }

        private static TMP_InputField Input(Transform parent, string name, string placeholder, float x1, float y1, float x2, float y2)
        {
            var bg = Panel(name, parent, PANEL2);
            Anchors(bg, x1, y1, x2, y2);
            var input = bg.gameObject.AddComponent<TMP_InputField>();

            var text = Text("Text", bg.transform, "", 22, FontStyles.Bold, TXT);
            Anchors(text, 0.05f, 0f, 0.95f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.characterSpacing = 5f;

            var ph = Text("Placeholder", bg.transform, placeholder, 15, FontStyles.Bold, MUTED);
            Stretch(ph.GetComponent<RectTransform>());
            ph.alignment = TextAlignmentOptions.Center;

            input.textComponent = text;
            input.placeholder = ph;
            input.characterLimit = 8;
            input.contentType = TMP_InputField.ContentType.Alphanumeric;
            return input;
        }

        private static Slider Slider(Transform parent, string name, float x1, float y1, float x2, float y2)
        {
            var bg = Panel(name, parent, PANEL2);
            Anchors(bg, x1, y1, x2, y2);
            var slider = bg.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            var fill = Panel("Fill", bg.transform, GOLD);
            Stretch(fill);
            slider.fillRect = fill;
            slider.targetGraphic = fill.GetComponent<Image>();
            return slider;
        }

        private static void AddSoftGlow(Transform parent, string name, Vector2 anchored, Vector2 size, Color color)
        {
            var rt = Panel(name, parent, color);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = size;
        }

        private static RectTransform Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }

        private static TMP_Text Text(string name, Transform parent, string value, float size, FontStyles style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = value;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Stretch(Component component)
        {
            Stretch(component.GetComponent<RectTransform>());
        }

        private static void Anchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Anchors(Component component, float xMin, float yMin, float xMax, float yMax)
        {
            Anchors(component.GetComponent<RectTransform>(), xMin, yMin, xMax, yMax);
        }
    }

    /// <summary>Cycles "•   •   •" dot brightness for a simple pulsing indicator.</summary>
    public class LoadingDotsAnimator : MonoBehaviour
    {
        private TMP_Text _text;
        private float _t;

        private void Awake() { _text = GetComponent<TMP_Text>(); }

        private void Update()
        {
            if (_text == null) return;
            _t += Time.deltaTime * 2.4f;
            int phase = ((int)_t) % 4;
            _text.text = phase switch
            {
                0 => "<alpha=#FF>•   <alpha=#55>•   <alpha=#55>•",
                1 => "<alpha=#55>•   <alpha=#FF>•   <alpha=#55>•",
                2 => "<alpha=#55>•   <alpha=#55>•   <alpha=#FF>•",
                _ => "<alpha=#22>•   <alpha=#22>•   <alpha=#22>•",
            };
        }
    }
}
