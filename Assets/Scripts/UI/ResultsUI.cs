using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using FrentePartido.Core;
using FrentePartido.Match;

namespace FrentePartido.UI
{
    public class ResultsUI : MonoBehaviour
    {
        [Header("Result")]
        [SerializeField] private TMP_Text _winnerText;
        [SerializeField] private TMP_Text _player1NameText;
        [SerializeField] private TMP_Text _player2NameText;
        [SerializeField] private TMP_Text _player1ScoreText;
        [SerializeField] private TMP_Text _player2ScoreText;

        [Header("Actions")]
        [SerializeField] private Button _rematchButton;
        [SerializeField] private Button _mainMenuButton;

        [Header("Animation")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeInDuration = 0.5f;

        private void Start()
        {
            _rematchButton?.onClick.AddListener(OnRematch);
            _mainMenuButton?.onClick.AddListener(OnMainMenu);

            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        public void ShowResults(string winnerName, int p1Score, int p2Score,
                                string p1Name = "Jugador 1", string p2Name = "Jugador 2")
        {
            if (_winnerText != null) _winnerText.text = $"¡{winnerName} GANA!";
            if (_player1NameText != null) _player1NameText.text = p1Name;
            if (_player2NameText != null) _player2NameText.text = p2Name;
            if (_player1ScoreText != null) _player1ScoreText.text = p1Score.ToString();
            if (_player2ScoreText != null) _player2ScoreText.text = p2Score.ToString();

            gameObject.SetActive(true);
            StartCoroutine(FadeIn());
        }

        private IEnumerator FadeIn()
        {
            if (_canvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeInDuration);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        private void OnRematch()
        {
            if (MatchManager.Instance != null)
                MatchManager.Instance.RequestRematchServerRpc();
        }

        private void OnMainMenu()
        {
            SceneFlowController.ReturnToMainMenu();
        }
    }
}
