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
        // ── References (auto-built if null) ─────────────────
        [Header("Info")]
        [SerializeField] private TMP_Text _joinCodeText;
        [SerializeField] private Button _copyCodeButton;
        [SerializeField] private TMP_Text _copyFeedbackText;

        [Header("Players")]
        [SerializeField] private TMP_Text _player1NameText;
        [SerializeField] private TMP_Text _player1StatusText;
        [SerializeField] private Image _player1CardBg;
        [SerializeField] private TMP_Text _player2NameText;
        [SerializeField] private TMP_Text _player2StatusText;
        [SerializeField] private Image _player2CardBg;

        [Header("Abilities")]
        [SerializeField] private Button _dashButton;
        [SerializeField] private Button _shieldButton;
        [SerializeField] private Button _mineButton;
        [SerializeField] private TMP_Text _selectedAbilityText;
        [SerializeField] private TMP_Text _selectedAbilityDesc;
        [SerializeField] private Image[] _abilityHighlights;

        [Header("Faction")]
        [SerializeField] private Button _blueButton;
        [SerializeField] private Button _redButton;
        [SerializeField] private Image _blueHighlight;
        [SerializeField] private Image _redHighlight;

        [Header("Actions")]
        [SerializeField] private Button _readyButton;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Button _leaveButton;
        [SerializeField] private TMP_Text _readyButtonText;
        [SerializeField] private Image _readyButtonBg;

        // ── State ───────────────────────────────────────────
        private bool _isReady;
        private int _selectedAbility;
        private int _selectedFaction;
        private float _copyTimer;
        private float _lobbyRefreshTimer;
        private bool _refreshingLobby;

        // ── Theme ───────────────────────────────────────────
        static readonly Color BG        = c(0.06f, 0.07f, 0.10f);
        static readonly Color CARD      = c(0.14f, 0.16f, 0.21f);
        static readonly Color CARD_WAIT = c(0.10f, 0.11f, 0.14f);
        static readonly Color BLUE      = c(0.25f, 0.52f, 0.96f);
        static readonly Color RED       = c(0.92f, 0.30f, 0.30f);
        static readonly Color GREEN     = c(0.22f, 0.80f, 0.42f);
        static readonly Color YELLOW    = c(1.00f, 0.82f, 0.18f);
        static readonly Color TXT       = c(0.95f, 0.96f, 0.98f);
        static readonly Color TXT2      = c(0.62f, 0.65f, 0.72f);
        static readonly Color MUTED     = c(0.42f, 0.45f, 0.52f);
        static readonly Color BTN       = c(0.20f, 0.22f, 0.28f);
        static readonly Color BTN_HOVER = c(0.26f, 0.28f, 0.34f);
        static readonly Color DANGER    = c(0.70f, 0.22f, 0.22f);
        static readonly Color CODE_BG   = c(0.16f, 0.18f, 0.24f);
        static readonly Color ACCENT    = c(1.00f, 0.82f, 0.18f);
        static Color c(float r, float g, float b) => new Color(r, g, b);

        // ── Ability Data ────────────────────────────────────
        static readonly string[] AB_NAME = { "Carrera Tactica", "Escudo Frontal", "Mina de Proximidad" };
        static readonly string[] AB_DESC = { "Sprint rapido para reposicionarte", "Bloquea dano frontal brevemente", "Coloca una trampa explosiva" };
        static readonly string[] AB_ICON = { ">>", "[=]", "(X)" };

        // ════════════════════════════════════════════════════
        //  LIFECYCLE
        // ════════════════════════════════════════════════════

        private void Awake()
        {
            BuildUI();
        }

        private void Start()
        {
            SetupButtons();
            UpdateJoinCode();
            UpdatePlayerList();
            HideCopyFeedback();

            bool isHost = NetworkSessionManager.Instance != null &&
                          NetworkSessionManager.Instance.IsHost;
            if (_startGameButton != null)
            {
                _startGameButton.gameObject.SetActive(isHost);
                _startGameButton.interactable = false;
            }

            SelectAbility(GameConfig.Preferences.abilityIndex);
            SelectFaction(GameConfig.Preferences.colorIndex);

            if (NetworkSessionManager.Instance != null)
            {
                NetworkSessionManager.Instance.OnPlayerConnected += OnPlayerJoined;
                NetworkSessionManager.Instance.OnPlayerDisconnected += OnPlayerLeft;
                NetworkSessionManager.Instance.OnLobbyPlayersChanged += OnLobbyPlayersChanged;
            }

            PublishLobbyState();
        }

        private void Update()
        {
            if (_copyTimer > 0)
            {
                _copyTimer -= Time.deltaTime;
                if (_copyTimer <= 0) HideCopyFeedback();
            }

            _lobbyRefreshTimer -= Time.unscaledDeltaTime;
            if (_lobbyRefreshTimer <= 0f)
            {
                _lobbyRefreshTimer = 0.5f;
                RefreshLobbyView();
            }
        }

        private void OnDestroy()
        {
            if (NetworkSessionManager.Instance != null)
            {
                NetworkSessionManager.Instance.OnPlayerConnected -= OnPlayerJoined;
                NetworkSessionManager.Instance.OnPlayerDisconnected -= OnPlayerLeft;
                NetworkSessionManager.Instance.OnLobbyPlayersChanged -= OnLobbyPlayersChanged;
            }
        }

        // ════════════════════════════════════════════════════
        //  AUTO-BUILD UI
        // ════════════════════════════════════════════════════

        private void BuildUI()
        {
            // Clear existing children
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            // Full-screen dark background
            var bg = Panel("BG", transform, BG);
            Stretch(bg);

            // Main container
            var main = Panel("Main", bg.transform, Color.clear);
            Anchors(main, 0.04f, 0.03f, 0.96f, 0.97f);
            var vl = main.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 16; vl.padding = new RectOffset(12, 12, 8, 8);
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            vl.childControlWidth = true; vl.childControlHeight = false;

            BuildHeader(main.transform);
            BuildPlayers(main.transform);
            BuildLoadout(main.transform);
            BuildActions(main.transform);
        }

        // ── HEADER ──────────────────────────────────────────

        void BuildHeader(Transform p)
        {
            var row = LayoutRow("Header", p, 95);

            // Title block
            var titleTxt = Txt("Title", row.transform, "SALA DE ESPERA", 30, FontStyles.Bold, TXT);
            Anchors(titleTxt, 0, 0.5f, 0.55f, 1f);
            titleTxt.alignment = TextAlignmentOptions.BottomLeft;
            titleTxt.characterSpacing = 3;

            var sub = Txt("Sub", row.transform, "Configura tu loadout", 13, FontStyles.Normal, TXT2);
            Anchors(sub, 0, 0, 0.55f, 0.5f);
            sub.alignment = TextAlignmentOptions.TopLeft;

            // Code box
            var codeBox = Panel("CodeBox", row.transform, CODE_BG);
            Anchors(codeBox, 0.55f, 0.05f, 1f, 0.95f);

            var codeLabel = Txt("CLabel", codeBox.transform, "CÓDIGO DE SALA", 10, FontStyles.Bold, MUTED);
            Anchors(codeLabel, 0.05f, 0.72f, 0.95f, 0.96f);
            codeLabel.alignment = TextAlignmentOptions.Left;
            codeLabel.characterSpacing = 3;

            _joinCodeText = Txt("Code", codeBox.transform, "------", 28, FontStyles.Bold, YELLOW);
            Anchors(_joinCodeText, 0.05f, 0.08f, 0.70f, 0.72f);
            _joinCodeText.alignment = TextAlignmentOptions.Left;
            _joinCodeText.characterSpacing = 8;

            // Copy btn
            var copyBg = Panel("CopyBg", codeBox.transform, BTN);
            Anchors(copyBg, 0.72f, 0.15f, 0.95f, 0.65f);
            _copyCodeButton = copyBg.gameObject.AddComponent<Button>();
            var copyTxt = Txt("CopyTxt", copyBg.transform, "COPIAR", 11, FontStyles.Bold, TXT);
            Stretch(copyTxt); copyTxt.alignment = TextAlignmentOptions.Center;

            _copyFeedbackText = Txt("Feedback", codeBox.transform, "¡Copiado!", 10, FontStyles.Italic, GREEN);
            Anchors(_copyFeedbackText, 0.70f, 0.68f, 0.98f, 0.95f);
            _copyFeedbackText.alignment = TextAlignmentOptions.Right;
        }

        // ── PLAYERS ─────────────────────────────────────────

        void BuildPlayers(Transform p)
        {
            var section = LayoutRow("Players", p, 140);

            var label = Txt("PLabel", section.transform, "JUGADORES", 10, FontStyles.Bold, MUTED);
            Anchors(label, 0, 0.9f, 1, 1); label.alignment = TextAlignmentOptions.BottomLeft;

            var cards = Panel("Cards", section.transform, Color.clear);
            Anchors(cards, 0, 0, 1, 0.87f);
            var hl = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 12; hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
            hl.childControlWidth = true; hl.childControlHeight = true;

            // VS text in middle
            BuildPlayerCard(cards.transform, "P1", true,
                out _player1NameText, out _player1StatusText, out _player1CardBg);

            // VS separator
            var vs = new GameObject("VS", typeof(RectTransform), typeof(LayoutElement));
            vs.transform.SetParent(cards.transform, false);
            vs.GetComponent<LayoutElement>().preferredWidth = 40;
            vs.GetComponent<LayoutElement>().flexibleWidth = 0;
            var vsTxt = Txt("VSTxt", vs.transform, "VS", 20, FontStyles.Bold, MUTED);
            Stretch(vsTxt); vsTxt.alignment = TextAlignmentOptions.Center;

            BuildPlayerCard(cards.transform, "P2", false,
                out _player2NameText, out _player2StatusText, out _player2CardBg);
        }

        void BuildPlayerCard(Transform p, string id, bool isLocal,
            out TMP_Text nameT, out TMP_Text statusT, out Image cardBg)
        {
            var card = Panel($"{id}Card", p, isLocal ? CARD : CARD_WAIT);
            cardBg = card.GetComponent<Image>();

            // Colored side stripe
            var stripe = Panel($"{id}Stripe", card.transform, isLocal ? BLUE : RED);
            Anchors(stripe, 0f, 0f, 0.04f, 1f);

            // Badge
            var badge = Panel($"{id}Badge", card.transform, isLocal ? BLUE : RED);
            Anchors(badge, 0.08f, 0.70f, 0.28f, 0.92f);
            var badgeTxt = Txt($"{id}BT", badge.transform, isLocal ? "P1" : "P2",
                13, FontStyles.Bold, Color.white);
            Stretch(badgeTxt); badgeTxt.alignment = TextAlignmentOptions.Center;

            // Status dot
            var dot = Panel($"{id}Dot", card.transform, isLocal ? GREEN : MUTED);
            Anchors(dot, 0.88f, 0.78f, 0.95f, 0.90f);

            // Name
            nameT = Txt($"{id}Name", card.transform,
                isLocal ? "Jugador 1" : "Esperando rival...",
                19, FontStyles.Bold, isLocal ? TXT : TXT2);
            Anchors(nameT, 0.08f, 0.36f, 0.95f, 0.66f);
            nameT.alignment = TextAlignmentOptions.Left;

            // Status
            statusT = Txt($"{id}Status", card.transform,
                isLocal ? "● Conectado" : "— sin conexión",
                12, FontStyles.Normal, isLocal ? GREEN : MUTED);
            Anchors(statusT, 0.08f, 0.08f, 0.95f, 0.34f);
            statusT.alignment = TextAlignmentOptions.Left;
        }

        // ── LOADOUT ─────────────────────────────────────────

        void BuildLoadout(Transform p)
        {
            var section = LayoutRow("Loadout", p, 210);
            var cols = Panel("Cols", section.transform, Color.clear);
            Stretch(cols);
            var hl = cols.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 12; hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
            hl.childControlWidth = true; hl.childControlHeight = true;

            BuildAbilities(cols.transform);
            BuildFaction(cols.transform);
        }

        void BuildAbilities(Transform p)
        {
            var panel = Panel("AbPanel", p, CARD);

            var title = Txt("AbTitle", panel.transform, "HABILIDAD", 11, FontStyles.Bold, MUTED);
            Anchors(title, 0.06f, 0.89f, 0.95f, 0.99f);
            title.alignment = TextAlignmentOptions.Left;
            title.characterSpacing = 3;

            // Buttons row
            var row = Panel("AbRow", panel.transform, Color.clear);
            Anchors(row, 0.05f, 0.48f, 0.95f, 0.87f);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10; hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
            hl.childControlWidth = true; hl.childControlHeight = true;

            _abilityHighlights = new Image[3];
            _dashButton   = AbilityBtn(row.transform, "Dash",   AB_ICON[0], 0);
            _shieldButton = AbilityBtn(row.transform, "Shield", AB_ICON[1], 1);
            _mineButton   = AbilityBtn(row.transform, "Mine",   AB_ICON[2], 2);

            // Info strip
            var infoBg = Panel("AbInfo", panel.transform, c(0.10f, 0.11f, 0.14f));
            Anchors(infoBg, 0.05f, 0.06f, 0.95f, 0.44f);

            _selectedAbilityText = Txt("AbName", infoBg.transform, AB_ICON[0] + "  " + AB_NAME[0],
                16, FontStyles.Bold, YELLOW);
            Anchors(_selectedAbilityText, 0.04f, 0.52f, 0.96f, 0.95f);
            _selectedAbilityText.alignment = TextAlignmentOptions.Left;

            _selectedAbilityDesc = Txt("AbDesc", infoBg.transform, AB_DESC[0],
                12, FontStyles.Normal, TXT2);
            Anchors(_selectedAbilityDesc, 0.04f, 0.08f, 0.96f, 0.52f);
            _selectedAbilityDesc.alignment = TextAlignmentOptions.TopLeft;
        }

        Button AbilityBtn(Transform p, string name, string icon, int idx)
        {
            var bg = Panel(name, p, BTN);
            _abilityHighlights[idx] = bg.GetComponent<Image>();
            var btn = bg.gameObject.AddComponent<Button>();

            var label = Txt($"{name}Lbl", bg.transform, icon, 24, FontStyles.Bold, TXT);
            Anchors(label, 0, 0.42f, 1, 0.96f); label.alignment = TextAlignmentOptions.Center;

            var sub = Txt($"{name}Sub", bg.transform, AB_NAME[idx], 10, FontStyles.Bold, TXT2);
            Anchors(sub, 0.05f, 0.05f, 0.95f, 0.42f); sub.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        void BuildFaction(Transform p)
        {
            var panel = Panel("FacPanel", p, CARD);
            panel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0.45f;

            var title = Txt("FTitle", panel.transform, "EQUIPO", 11, FontStyles.Bold, MUTED);
            Anchors(title, 0.08f, 0.89f, 0.92f, 0.99f);
            title.alignment = TextAlignmentOptions.Left;
            title.characterSpacing = 3;

            // Blue
            var blBg = Panel("BlBtn", panel.transform, BLUE);
            Anchors(blBg, 0.08f, 0.50f, 0.92f, 0.85f);
            _blueButton = blBg.gameObject.AddComponent<Button>();
            var blTxt = Txt("BlTxt", blBg.transform, "AZUL", 18, FontStyles.Bold, Color.white);
            Stretch(blTxt); blTxt.alignment = TextAlignmentOptions.Center;
            blTxt.characterSpacing = 4;
            _blueHighlight = Panel("BlHL", blBg.transform, new Color(1, 1, 1, 0f)).GetComponent<Image>();
            Anchors(_blueHighlight, 0f, 0f, 1f, 0.08f);
            _blueHighlight.color = YELLOW;

            // Red
            var rdBg = Panel("RdBtn", panel.transform, RED);
            Anchors(rdBg, 0.08f, 0.10f, 0.92f, 0.45f);
            _redButton = rdBg.gameObject.AddComponent<Button>();
            var rdTxt = Txt("RdTxt", rdBg.transform, "ROJO", 18, FontStyles.Bold, Color.white);
            Stretch(rdTxt); rdTxt.alignment = TextAlignmentOptions.Center;
            rdTxt.characterSpacing = 4;
            _redHighlight = Panel("RdHL", rdBg.transform, new Color(1, 1, 1, 0f)).GetComponent<Image>();
            Anchors(_redHighlight, 0f, 0f, 1f, 0.08f);
            _redHighlight.color = YELLOW;
        }

        // ── ACTIONS ─────────────────────────────────────────

        void BuildActions(Transform p)
        {
            var section = LayoutRow("Actions", p, 62);
            var row = Panel("ARow", section.transform, Color.clear);
            Stretch(row);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            hl.childControlWidth = false; hl.childControlHeight = true;

            // Leave
            _leaveButton = ActionBtn(row.transform, "Leave", "SALIR", DANGER, 110);

            // Spacer
            var sp = new GameObject("Sp", typeof(RectTransform), typeof(LayoutElement));
            sp.transform.SetParent(row.transform, false);
            sp.GetComponent<LayoutElement>().flexibleWidth = 1;

            // Ready
            var readyBg = Panel("ReadyBg", row.transform, GREEN);
            readyBg.gameObject.AddComponent<LayoutElement>().preferredWidth = 160;
            _readyButton = readyBg.gameObject.AddComponent<Button>();
            _readyButtonBg = readyBg.GetComponent<Image>();
            _readyButtonText = Txt("ReadyTxt", readyBg.transform, "LISTO", 17, FontStyles.Bold, Color.white);
            Stretch(_readyButtonText); _readyButtonText.alignment = TextAlignmentOptions.Center;

            // Start (host only)
            var startBg = Panel("StartBg", row.transform, BLUE);
            startBg.gameObject.AddComponent<LayoutElement>().preferredWidth = 160;
            _startGameButton = startBg.gameObject.AddComponent<Button>();
            var startTxt = Txt("StartTxt", startBg.transform, "INICIAR", 17, FontStyles.Bold, Color.white);
            Stretch(startTxt); startTxt.alignment = TextAlignmentOptions.Center;
        }

        Button ActionBtn(Transform p, string name, string label, Color bg, float w)
        {
            var panel = Panel(name, p, bg);
            panel.gameObject.AddComponent<LayoutElement>().preferredWidth = w;
            var btn = panel.gameObject.AddComponent<Button>();
            var txt = Txt($"{name}T", panel.transform, label, 14, FontStyles.Bold, Color.white);
            Stretch(txt); txt.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        // ════════════════════════════════════════════════════
        //  LOGIC
        // ════════════════════════════════════════════════════

        void SetupButtons()
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

        void UpdateJoinCode()
        {
            if (_joinCodeText == null || NetworkSessionManager.Instance == null) return;
            _joinCodeText.text = NetworkSessionManager.Instance.JoinCode ?? "------";
        }

        void CopyCodeToClipboard()
        {
            if (NetworkSessionManager.Instance == null) return;
            GUIUtility.systemCopyBuffer = NetworkSessionManager.Instance.JoinCode;
            ShowCopyFeedback();
        }

        void ShowCopyFeedback()
        {
            if (_copyFeedbackText != null)
            {
                _copyFeedbackText.gameObject.SetActive(true);
                _copyFeedbackText.text = "Copiado!";
            }
            _copyTimer = 2f;
        }

        void HideCopyFeedback()
        {
            if (_copyFeedbackText != null)
                _copyFeedbackText.gameObject.SetActive(false);
        }

        void SelectAbility(int index)
        {
            _selectedAbility = Mathf.Clamp(index, 0, 2);
            GameConfig.Preferences.abilityIndex = _selectedAbility;

            if (_selectedAbilityText != null)
                _selectedAbilityText.text = AB_ICON[_selectedAbility] + " " + AB_NAME[_selectedAbility];
            if (_selectedAbilityDesc != null)
                _selectedAbilityDesc.text = AB_DESC[_selectedAbility];

            if (_abilityHighlights != null)
                for (int i = 0; i < _abilityHighlights.Length; i++)
                    if (_abilityHighlights[i] != null)
                        _abilityHighlights[i].color = i == _selectedAbility ? YELLOW : BTN;

            PublishLobbyState();
        }

        void SelectFaction(int faction)
        {
            _selectedFaction = faction;
            GameConfig.Preferences.colorIndex = faction;

            if (_blueHighlight != null) _blueHighlight.enabled = faction == 0;
            if (_redHighlight != null)  _redHighlight.enabled  = faction == 1;

            PublishLobbyState();
        }

        void ToggleReady()
        {
            _isReady = !_isReady;
            if (_readyButtonText != null)
                _readyButtonText.text = _isReady ? "LISTO  !" : "LISTO";
            if (_readyButtonBg != null)
                _readyButtonBg.color = _isReady ? GREEN : BTN;

            PublishLobbyState();
            UpdatePlayerList();
            CheckStartCondition();
        }

        void OnStartGame()
        {
            GameConfig.Save();
            SceneFlowController.LoadSceneNetwork(SceneFlowController.SCENE_GAME);
        }

        void OnLeave()
        {
            if (NetworkSessionManager.Instance != null)
                NetworkSessionManager.Instance.LeaveSession();
            else
                SceneFlowController.ReturnToMainMenu();
        }

        void UpdatePlayerList()
        {
            var session = NetworkSessionManager.Instance;
            var players = session != null
                ? session.GetLobbyPlayers()
                : System.Array.Empty<NetworkSessionManager.LobbyPlayerInfo>();
            bool hasP2 = players.Count >= 2;

            string p1Name = players.Count > 0 ? players[0].PlayerName : (GameConfig.Preferences.playerName ?? "Jugador 1");
            string p2Name = hasP2 ? players[1].PlayerName : "Esperando rival...";
            bool p1Ready = players.Count > 0 && players[0].IsReady;
            bool p2Ready = hasP2 && players[1].IsReady;

            if (_player1NameText != null)
                _player1NameText.text = p1Name;
            if (_player1StatusText != null)
                _player1StatusText.text = p1Ready ? "Listo" : "Conectado";
            if (_player1CardBg != null)
                _player1CardBg.color = p1Ready ? new Color(0.12f, 0.22f, 0.14f, 1f) : CARD;

            if (_player2NameText != null)
                _player2NameText.text = hasP2 ? p2Name : "Esperando rival...";
            if (_player2StatusText != null)
                _player2StatusText.text = hasP2 ? (p2Ready ? "Listo" : "Conectado") : "";
            if (_player2CardBg != null)
                _player2CardBg.color = hasP2 ? (p2Ready ? new Color(0.12f, 0.22f, 0.14f, 1f) : CARD) : CARD_WAIT;
        }

        void OnPlayerJoined(ulong id) { UpdatePlayerList(); CheckStartCondition(); }
        void OnPlayerLeft(ulong id)   { UpdatePlayerList(); CheckStartCondition(); }
        void OnLobbyPlayersChanged()  { UpdatePlayerList(); CheckStartCondition(); }

        void CheckStartCondition()
        {
            if (_startGameButton == null) return;
            bool hasNet = Unity.Netcode.NetworkManager.Singleton != null &&
                          Unity.Netcode.NetworkManager.Singleton.IsListening;
            var session = NetworkSessionManager.Instance;
            bool canStart = hasNet && session != null && session.IsHost && session.AreLobbyPlayersReady();
            _startGameButton.interactable = canStart;
        }

        void RefreshLobbyView()
        {
            UpdatePlayerList();
            CheckStartCondition();
        }

        void PublishLobbyState()
        {
            NetworkSessionManager.Instance?.SubmitLobbyPlayerState(
                GameConfig.Preferences.playerName,
                _selectedAbility,
                _selectedFaction,
                _isReady);
        }

        // ════════════════════════════════════════════════════
        //  UI HELPERS
        // ════════════════════════════════════════════════════

        static RectTransform Panel(string n, Transform p, Color col)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(p, false);
            go.GetComponent<Image>().color = col;
            return go.GetComponent<RectTransform>();
        }

        static RectTransform LayoutRow(string n, Transform p, float h)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(p, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = h; le.flexibleWidth = 1;
            return go.GetComponent<RectTransform>();
        }

        static TMP_Text Txt(string n, Transform p, string text, float size, FontStyles style, Color col)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(p, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = col;
            t.raycastTarget = false; t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        static void Stretch(RectTransform rt)
        { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        static void Stretch(Component c) => Stretch(c.GetComponent<RectTransform>());

        static void Anchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        { rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax); rt.offsetMin = rt.offsetMax = Vector2.zero; }

        static void Anchors(Component c, float xMin, float yMin, float xMax, float yMax)
            => Anchors(c.GetComponent<RectTransform>(), xMin, yMin, xMax, yMax);
    }
}
