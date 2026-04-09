using UnityEngine;
using TMPro;

namespace FrentePartido.UI
{
    public class RoundTimerUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _warningColor = Color.yellow;
        [SerializeField] private Color _dangerColor = Color.red;

        public void UpdateTimer(float secondsRemaining)
        {
            if (_timerText == null) return;

            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            _timerText.text = $"{minutes}:{seconds:D2}";

            if (secondsRemaining <= 5f)
                _timerText.color = _dangerColor;
            else if (secondsRemaining <= 10f)
                _timerText.color = _warningColor;
            else
                _timerText.color = _normalColor;
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
