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
            }
            if (_reloadBar != null) _reloadBar.fillAmount = 0f;
            if (_localAbility != null)
                _localAbility.OnCooldownChanged += UpdateAbilityCooldown;

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
            }
            if (_localAbility != null)
                _localAbility.OnCooldownChanged -= UpdateAbilityCooldown;

            UnsubscribeFromMatch();
        }

        private void SubscribeToMatch()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnKillsChanged += UpdateScore;
                MatchManager.Instance.OnMatchWon += HandleMatchWon;
            }

            var beacon = FindAnyObjectByType<BeaconCaptureController>();
            if (beacon != null)
                beacon.OnBeaconStateChanged += UpdateBeaconState;
        }

        private void UnsubscribeFromMatch()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnKillsChanged -= UpdateScore;
                MatchManager.Instance.OnMatchWon -= HandleMatchWon;
            }
        }

        private void HandleMatchWon(ulong winnerClientId)
        {
            string winnerLabel = "EMPATE";
            var gs = FrentePartido.Networking.NetworkGameState.Instance;
            if (gs != null)
            {
                if (winnerClientId == gs.Player1ClientId.Value) winnerLabel = "AZUL";
                else if (winnerClientId == gs.Player2ClientId.Value) winnerLabel = "ROJO";
            }

            int p1 = MatchManager.Instance != null ? MatchManager.Instance.Player1Kills.Value : 0;
            int p2 = MatchManager.Instance != null ? MatchManager.Instance.Player2Kills.Value : 0;
            ShowAnnouncement($"GANA {winnerLabel}  {p1} - {p2}", 6f);

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
            }
            if (MatchManager.Instance != null)
                UpdateScore(MatchManager.Instance.Player1Kills.Value, MatchManager.Instance.Player2Kills.Value);
        }

        private void UpdateHealth(int current, int max)
        {
            if (_healthText != null) _healthText.text = $"{current}";
            if (_healthBar != null) _healthBar.fillAmount = (float)current / max;
        }

        private void UpdateArmor(int armor)
        {
            if (_armorText != null) _armorText.text = armor > 0 ? $"+{armor}" : "";
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
