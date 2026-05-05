using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FrentePartido.UI
{
    public class CooldownWidget : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _cooldownOverlay;
        [SerializeField] private TMP_Text _cooldownText;
        private TMP_Text _labelText;

        public void SetIcon(Sprite icon)
        {
            if (_iconImage != null)
                _iconImage.sprite = icon;
        }

        /// <summary>Style the icon for an ability — color tint + 1-letter label so it
        /// reads at a glance even without a sprite asset assigned.</summary>
        public void ConfigureForAbility(string label, Color tint)
        {
            if (_iconImage != null) _iconImage.color = tint;

            if (_labelText == null)
            {
                var go = new GameObject("AbilityLetter", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                _labelText = go.GetComponent<TMP_Text>();
                _labelText.alignment = TextAlignmentOptions.Center;
                _labelText.fontSize = 22;
                _labelText.fontStyle = FontStyles.Bold;
                _labelText.color = Color.white;
                _labelText.raycastTarget = false;
                if (_cooldownText != null)
                    _labelText.transform.SetSiblingIndex(_cooldownText.transform.GetSiblingIndex());
            }
            _labelText.text = label;
        }

        public void UpdateCooldown(float remaining, float total)
        {
            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.gameObject.SetActive(true);
                _cooldownOverlay.fillAmount = remaining / total;
            }

            if (_cooldownText != null)
            {
                _cooldownText.gameObject.SetActive(true);
                _cooldownText.text = Mathf.CeilToInt(remaining).ToString();
            }
        }

        public void SetReady()
        {
            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.fillAmount = 0f;
                _cooldownOverlay.gameObject.SetActive(false);
            }

            if (_cooldownText != null)
                _cooldownText.gameObject.SetActive(false);
        }
    }
}
