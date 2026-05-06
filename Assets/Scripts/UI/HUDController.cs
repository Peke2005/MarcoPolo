using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using FrentePartido.Data;
using FrentePartido.Match;

namespace FrentePartido.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private Image _healthBar;
        [SerializeField] private TMP_Text _armorText;
        [SerializeField] private Image _armorBar;

        [Header("Ammo")]
        [SerializeField] private TMP_Text _ammoText;
        [SerializeField] private Image _reloadBar;

        [Header("Ability")]
        [SerializeField] private CooldownWidget _abilityCooldown;

        [Header("Grenade")]
        [SerializeField] private Image _grenadeIcon;
        [SerializeField] private Color _grenadeAvailableColor = Color.white;
        [SerializeField] private Color _grenadeUsedColor = new Color(0.3f, 0.3f, 0.3f);

        [Header("Round")]
        [SerializeField] private RoundTimerUI _roundTimer;
        [SerializeField] private TMP_Text _roundScoreText;
        [SerializeField] private TMP_Text _announcementText;

        [Header("Beacon")]
        [SerializeField] private GameObject _beaconPanel;
        [SerializeField] private Image _beaconCaptureBar;
        [SerializeField] private TMP_Text _beaconStatusText;

        private Player.PlayerHealth _localHealth;
        private Combat.WeaponController _localWeapon;
        private Abilities.AbilityController _localAbility;
        private int _maxHealth;
        private Coroutine _reloadBarCoroutine;
        private GameObject _bigOverlay;
        private TMP_Text _bigOverlayTitle;
        private TMP_Text _bigOverlayScore;
        private TMP_Text _bigOverlayCountdown;
        private Coroutine _bigOverlayCoroutine;
        private CanvasGroup _bigOverlayCg;
        private int _lastAbilityIndex = -999;

        public void Initialize(Player.PlayerHealth health, Combat.WeaponController weapon,
                              Abilities.AbilityController ability, int maxHealth)
        {
            _localHealth = health;
            _localWeapon = weapon;
            _localAbility = ability;
            _maxHealth = maxHealth;

            if (_localHealth != null)
            {
                _localHealth.OnHealthChanged += UpdateHealth;
                _localHealth.OnArmorChanged += UpdateArmor;
            }
            if (_localWeapon != null)
            {
                _localWeapon.OnAmmoChanged += UpdateAmmo;
                _localWeapon.OnReloadStarted += HandleReloadStarted;
                _localWeapon.OnReloadFinished += HandleReloadFinished;
                _localWeapon.OnGrenadeCountChanged += UpdateGrenadeCount;
            }
            if (_reloadBar != null) _reloadBar.fillAmount = 0f;
            if (_localAbility != null)
            {
                _localAbility.OnCooldownChanged += UpdateAbilityCooldown;
                _localAbility.EquippedAbilityIndex.OnValueChanged += HandleEquippedAbilityChanged;
            }

            if (_beaconPanel != null) _beaconPanel.SetActive(false);
            if (_announcementText != null) _announcementText.text = "";

            SubscribeToMatch();
        }

        private void OnDestroy()
        {
            if (_localHealth != null)
            {
                _localHealth.OnHealthChanged -= UpdateHealth;
                _localHealth.OnArmorChanged -= UpdateArmor;
            }
            if (_localWeapon != null)
            {
                _localWeapon.OnAmmoChanged -= UpdateAmmo;
                _localWeapon.OnReloadStarted -= HandleReloadStarted;
                _localWeapon.OnReloadFinished -= HandleReloadFinished;
                _localWeapon.OnGrenadeCountChanged -= UpdateGrenadeCount;
            }
            if (_localAbility != null)
            {
                _localAbility.OnCooldownChanged -= UpdateAbilityCooldown;
                _localAbility.EquippedAbilityIndex.OnValueChanged -= HandleEquippedAbilityChanged;
            }

            UnsubscribeFromMatch();
        }

        private void SubscribeToMatch()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnScoreChanged += UpdateScore;
                MatchManager.Instance.OnMatchWon += HandleMatchWon;
            }
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.OnRoundStateChanged += HandleRoundStateChanged;
                RoundManager.Instance.OnRoundEndedAnnounced += HandleRoundEndedAnnounced;
            }

            var beacon = FindAnyObjectByType<BeaconCaptureController>();
            if (beacon != null)
                beacon.OnBeaconStateChanged += UpdateBeaconState;
        }

        private void UnsubscribeFromMatch()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnScoreChanged -= UpdateScore;
                MatchManager.Instance.OnMatchWon -= HandleMatchWon;
            }
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.OnRoundStateChanged -= HandleRoundStateChanged;
                RoundManager.Instance.OnRoundEndedAnnounced -= HandleRoundEndedAnnounced;
            }
        }

        private void HandleRoundStateChanged(FrentePartido.Data.RoundState state)
        {
            if (state == FrentePartido.Data.RoundState.SuddenDeath)
                ShowAnnouncement("MUERTE SUBITA", 3f);
            else if (state == FrentePartido.Data.RoundState.Active)
                ShowAnnouncement("PELEAD", 1.5f);
        }

        private void HandleRoundEndedAnnounced(ulong winnerClientId)
        {
            string label = ResolveFactionLabel(winnerClientId);
            int p1 = MatchManager.Instance != null ? MatchManager.Instance.Player1Score.Value : 0;
            int p2 = MatchManager.Instance != null ? MatchManager.Instance.Player2Score.Value : 0;
            // Score from MatchManager hasn't incremented yet at this point (server adds the
            // point after the round-end delay). Bump the winner side locally for the banner.
            if (winnerClientId == ResolvePlayerSlotId(1)) p1++;
            else if (winnerClientId == ResolvePlayerSlotId(2)) p2++;

            float countdown = 4f;
            ShowBigOverlay(
                $"GANA {label}",
                $"{p1} - {p2}",
                countdown,
                false,
                FactionColor(winnerClientId));
        }

        private static ulong ResolvePlayerSlotId(int slot)
        {
            var gs = FrentePartido.Networking.NetworkGameState.Instance;
            if (gs == null) return ulong.MaxValue;
            return slot == 1 ? gs.Player1ClientId.Value : gs.Player2ClientId.Value;
        }

        private static string ResolveFactionLabel(ulong clientId)
        {
            var gs = FrentePartido.Networking.NetworkGameState.Instance;
            if (gs == null) return "?";
            if (clientId == gs.Player1ClientId.Value) return "AZUL";
            if (clientId == gs.Player2ClientId.Value) return "ROJO";
            return "?";
        }

        private static Color FactionColor(ulong clientId)
        {
            var gs = FrentePartido.Networking.NetworkGameState.Instance;
            if (gs == null) return new Color(1f, 0.85f, 0.25f, 1f);
            if (clientId == gs.Player1ClientId.Value) return new Color(0.30f, 0.55f, 1.00f, 1f);
            if (clientId == gs.Player2ClientId.Value) return new Color(1.00f, 0.32f, 0.28f, 1f);
            return new Color(1f, 0.85f, 0.25f, 1f);
        }

        private void EnsureBigOverlay()
        {
            if (_bigOverlay != null) return;

            var canvasRT = transform as RectTransform;
            if (canvasRT == null) canvasRT = GetComponent<RectTransform>();
            if (canvasRT == null) return;

            var go = new GameObject("BigOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(canvasRT, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.transform.SetAsLastSibling();
            _bigOverlay = go;
            _bigOverlayCg = go.GetComponent<CanvasGroup>();
            _bigOverlayCg.alpha = 0f;
            _bigOverlayCg.blocksRaycasts = false;
            var dim = go.GetComponent<UnityEngine.UI.Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            dim.raycastTarget = false;

            _bigOverlayTitle = AddOverlayText(rt, "Title", "", 92, FontStyles.Bold, new Vector2(0f, 0.55f), new Vector2(1f, 0.78f));
            _bigOverlayScore = AddOverlayText(rt, "Score", "", 56, FontStyles.Bold, new Vector2(0f, 0.40f), new Vector2(1f, 0.55f));
            _bigOverlayCountdown = AddOverlayText(rt, "Countdown", "", 28, FontStyles.Normal, new Vector2(0f, 0.27f), new Vector2(1f, 0.38f));
            _bigOverlayCountdown.color = new Color(0.92f, 0.94f, 0.92f, 0.85f);
            _bigOverlayTitle.characterSpacing = 8f;
            _bigOverlayScore.characterSpacing = 12f;

            go.SetActive(false);
        }

        private static TMP_Text AddOverlayText(RectTransform parent, string name, string value, float size, FontStyles style, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = value;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        public void ShowBigOverlay(string title, string score, float countdownSeconds, bool isMatchEnd, Color titleColor)
        {
            EnsureBigOverlay();
            if (_bigOverlay == null) return;

            _bigOverlay.SetActive(true);
            if (_bigOverlayTitle != null)
            {
                _bigOverlayTitle.text = title;
                _bigOverlayTitle.color = titleColor;
            }
            if (_bigOverlayScore != null)
                _bigOverlayScore.text = score;

            if (_bigOverlayCoroutine != null) StopCoroutine(_bigOverlayCoroutine);
            _bigOverlayCoroutine = StartCoroutine(BigOverlayRoutine(countdownSeconds, isMatchEnd));
        }

        private IEnumerator BigOverlayRoutine(float duration, bool persistAfter)
        {
            // Fade in
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                if (_bigOverlayCg != null) _bigOverlayCg.alpha = Mathf.Clamp01(t / 0.3f);
                yield return null;
            }
            if (_bigOverlayCg != null) _bigOverlayCg.alpha = 1f;

            // Countdown
            float remaining = duration;
            while (remaining > 0f)
            {
                if (_bigOverlayCountdown != null)
                {
                    int secs = Mathf.CeilToInt(remaining);
                    _bigOverlayCountdown.text = persistAfter
                        ? $"Volviendo al menu en {secs}..."
                        : $"Proxima ronda en {secs}...";
                }
                remaining -= Time.deltaTime;
                yield return null;
            }

            if (persistAfter) yield break;

            // Fade out for round transitions
            t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                if (_bigOverlayCg != null) _bigOverlayCg.alpha = 1f - Mathf.Clamp01(t / 0.3f);
                yield return null;
            }
            if (_bigOverlayCg != null) _bigOverlayCg.alpha = 0f;
            if (_bigOverlay != null) _bigOverlay.SetActive(false);
        }

        private void HandleMatchWon(ulong winnerClientId)
        {
            int p1 = MatchManager.Instance != null ? MatchManager.Instance.Player1Score.Value : 0;
            int p2 = MatchManager.Instance != null ? MatchManager.Instance.Player2Score.Value : 0;
            ShowBigOverlay(
                $"VICTORIA {ResolveFactionLabel(winnerClientId)}",
                $"{p1} - {p2}",
                7f,
                true,
                FactionColor(winnerClientId));
            StartCoroutine(ReturnToMenuAfter(7f));
        }

        private IEnumerator ReturnToMenuAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            FrentePartido.Core.SceneFlowController.ReturnToMainMenu();
        }

        private void Update()
        {
            // Auto-bind local player components once they spawn. Initialize() is not
            // called from anywhere else, so without this the HUD never reflects state.
            if (_localHealth == null)
                TryAutoBindLocalPlayer();

            // Defensive per-frame push of HP/armor/ammo. NetworkVariable.OnValueChanged
            // is the source of truth, but on rare timing edge cases the event handler
            // can miss the very first tick after auto-bind, leaving the bars stale.
            // This makes sure the visual always reflects the authoritative value.
            if (_localHealth != null)
            {
                UpdateHealth(_localHealth.CurrentHealth.Value, _maxHealth);
                UpdateArmor(_localHealth.CurrentArmor.Value);
            }
            if (_localWeapon != null && _localWeapon.GrenadesRemaining != null)
            {
                UpdateGrenadeCount(_localWeapon.GrenadesRemaining.Value);
            }
            if (_localAbility != null && _localAbility.EquippedAbilityIndex.Value != _lastAbilityIndex)
            {
                ConfigureAbilityIcon(_localAbility);
            }

            // Update timer from RoundManager
            if (RoundManager.Instance != null && _roundTimer != null)
                _roundTimer.UpdateTimer(RoundManager.Instance.RoundTimer.Value);

            // Update beacon capture progress
            var beacon = FindAnyObjectByType<BeaconCaptureController>();
            if (beacon != null && _beaconCaptureBar != null)
                _beaconCaptureBar.fillAmount = beacon.CaptureProgress.Value;
        }

        private void TryAutoBindLocalPlayer()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return;

            var localPlayer = nm.SpawnManager.GetLocalPlayerObject();
            if (localPlayer == null) return;

            var health = localPlayer.GetComponent<Player.PlayerHealth>();
            var weapon = localPlayer.GetComponent<Combat.WeaponController>();
            var ability = localPlayer.GetComponent<Abilities.AbilityController>();
            if (health == null) return;

            int maxHp = 100;
            var balance = Resources.Load<BalanceTuningData>("BalanceTuning");
            if (balance != null) maxHp = balance.playerMaxHealth;

            Initialize(health, weapon, ability, maxHp);

            // Initial values: NetworkVariable initial sync does not fire OnValueChanged,
            // so push current state into the UI manually.
            UpdateHealth(health.CurrentHealth.Value, maxHp);
            UpdateArmor(health.CurrentArmor.Value);
            if (weapon != null)
            {
                int magSize = 8;
                UpdateAmmo(weapon.CurrentAmmo.Value, magSize);
                UpdateGrenadeCount(weapon.GrenadesRemaining.Value);
            }
            if (MatchManager.Instance != null)
                UpdateScore(MatchManager.Instance.Player1Score.Value, MatchManager.Instance.Player2Score.Value);

            ConfigureAbilityIcon(ability);
        }

        private void HandleEquippedAbilityChanged(int oldIndex, int newIndex)
        {
            ConfigureAbilityIcon(_localAbility);
        }

        private void ConfigureAbilityIcon(Abilities.AbilityController ability)
        {
            if (_abilityCooldown == null || ability == null) return;
            _lastAbilityIndex = ability.EquippedAbilityIndex.Value;
            var def = ability.CurrentAbility;
            if (def == null)
            {
                _abilityCooldown.ConfigureForAbility("Q", new Color(0.4f, 0.4f, 0.4f, 1f));
                return;
            }
            string label = def.type switch
            {
                FrentePartido.Data.AbilityType.Dash => "D",
                FrentePartido.Data.AbilityType.Shield => "E",
                FrentePartido.Data.AbilityType.Mine => "M",
                _ => "Q"
            };
            Color tint = def.type switch
            {
                FrentePartido.Data.AbilityType.Dash => new Color(0.25f, 0.85f, 1f, 1f),
                FrentePartido.Data.AbilityType.Shield => new Color(0.30f, 0.55f, 1f, 1f),
                FrentePartido.Data.AbilityType.Mine => new Color(1f, 0.45f, 0.18f, 1f),
                _ => new Color(0.5f, 0.5f, 0.5f, 1f)
            };
            _abilityCooldown.ConfigureForAbility(label, tint);
        }

        private void UpdateHealth(int current, int max)
        {
            if (_healthText != null) _healthText.text = $"{current}";
            if (_healthBar != null) _healthBar.fillAmount = (float)current / max;
        }

        private void UpdateArmor(int armor)
        {
            if (_armorText != null) _armorText.text = armor.ToString();
            if (_armorBar != null) _armorBar.fillAmount = (float)armor / 50f;
        }

        private void UpdateAmmo(int current, int max)
        {
            if (_ammoText != null) _ammoText.text = $"{current}/{max}";
        }

        private void UpdateAbilityCooldown(float remaining, float total)
        {
            if (_abilityCooldown != null)
            {
                if (remaining <= 0f)
                    _abilityCooldown.SetReady();
                else
                    _abilityCooldown.UpdateCooldown(remaining, total);
            }
        }

        private void UpdateScore(byte p1, byte p2)
        {
            if (_roundScoreText != null) _roundScoreText.text = $"{p1} - {p2}";
        }

        private void UpdateBeaconState(BeaconState state)
        {
            if (_beaconPanel != null) _beaconPanel.SetActive(state != BeaconState.Inactive);

            if (_beaconStatusText == null) return;

            _beaconStatusText.text = state switch
            {
                BeaconState.Active => "FARO ACTIVO",
                BeaconState.Contested => "DISPUTADO",
                BeaconState.CapturingP1 => "CAPTURANDO (AZUL)",
                BeaconState.CapturingP2 => "CAPTURANDO (ROJO)",
                BeaconState.Captured => "¡CAPTURADO!",
                _ => ""
            };
        }

        private void HandleReloadStarted()
        {
            if (_reloadBar == null || _localWeapon == null) return;
            if (_reloadBarCoroutine != null) StopCoroutine(_reloadBarCoroutine);
            _reloadBarCoroutine = StartCoroutine(ReloadBarCoroutine(_localWeapon.ReloadTime));
        }

        private void HandleReloadFinished()
        {
            if (_reloadBarCoroutine != null)
            {
                StopCoroutine(_reloadBarCoroutine);
                _reloadBarCoroutine = null;
            }
            if (_reloadBar != null) _reloadBar.fillAmount = 0f;
        }

        private IEnumerator ReloadBarCoroutine(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (_reloadBar != null) _reloadBar.fillAmount = elapsed / duration;
                yield return null;
            }
            if (_reloadBar != null) _reloadBar.fillAmount = 0f;
            _reloadBarCoroutine = null;
        }

        public void SetGrenadeAvailable(bool available)
        {
            if (_grenadeIcon != null)
                _grenadeIcon.color = available ? _grenadeAvailableColor : _grenadeUsedColor;
        }

        private TMP_Text _grenadeLabel;
        private void EnsureGrenadeLabel()
        {
            if (_grenadeLabel != null || _grenadeIcon == null) return;
            var go = new GameObject("GrenadeLetter", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(_grenadeIcon.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _grenadeLabel = go.GetComponent<TMP_Text>();
            _grenadeLabel.text = "G";
            _grenadeLabel.alignment = TextAlignmentOptions.Center;
            _grenadeLabel.fontSize = 22;
            _grenadeLabel.fontStyle = FontStyles.Bold;
            _grenadeLabel.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            _grenadeLabel.raycastTarget = false;
        }

        private void UpdateGrenadeCount(int count)
        {
            SetGrenadeAvailable(count > 0);
            EnsureGrenadeLabel();
            if (_grenadeLabel != null)
                _grenadeLabel.color = count > 0 ? new Color(0.15f, 0.15f, 0.15f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.6f);
        }

        public void ShowAnnouncement(string text, float duration = 2f)
        {
            StartCoroutine(AnnouncementCoroutine(text, duration));
        }

        private IEnumerator AnnouncementCoroutine(string text, float duration)
        {
            if (_announcementText == null) yield break;
            _announcementText.text = text;
            _announcementText.gameObject.SetActive(true);
            yield return new WaitForSeconds(duration);
            _announcementText.text = "";
        }
    }
}
