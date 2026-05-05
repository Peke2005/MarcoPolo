using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrentePartido.Core
{
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string _firstScene = "01_Auth";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyPerformanceSettings()
        {
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = 1f / 60f;
            Time.maximumDeltaTime = 0.1f;
        }

        private async void Start()
        {
            ApplyPerformanceSettings();

            await ServiceInitializer.InitializeAsync();

            GameConfig.Load();

            SceneManager.LoadScene(_firstScene);
        }
    }
}
