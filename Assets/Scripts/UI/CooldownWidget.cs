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

        public void SetIcon(Sprite icon)
        {
            if (_iconImage != null)
                _iconImage.sprite = icon;
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
