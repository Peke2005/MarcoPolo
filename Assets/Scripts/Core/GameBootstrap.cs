using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrentePartido.Core
{
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string _mainMenuScene = "01_MainMenu";

        private async void Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            await ServiceInitializer.InitializeAsync();

            GameConfig.Load();

            SceneManager.LoadScene(_mainMenuScene);
        }
    }
}
