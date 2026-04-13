using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrentePartido.Core
{
    public static class SceneFlowController
    {
        public const string SCENE_BOOT = "00_Boot";
        public const string SCENE_AUTH = "01_Auth";
        public const string SCENE_MAIN_MENU = "02_MainMenu";
        public const string SCENE_LOBBY = "03_Lobby";
        public const string SCENE_GAME = "04_Game";
        public const string SCENE_POST_MATCH = "05_PostMatch";

        public static event Action<string> OnSceneLoadStarted;
        public static event Action<string> OnSceneLoadCompleted;

        public static void LoadScene(string sceneName)
        {
            OnSceneLoadStarted?.Invoke(sceneName);
            SceneManager.LoadScene(sceneName);
            OnSceneLoadCompleted?.Invoke(sceneName);
        }

        public static void LoadSceneNetwork(string sceneName)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[SceneFlow] Only server can load network scenes.");
                return;
            }

            OnSceneLoadStarted?.Invoke(sceneName);
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        public static void ReturnToMainMenu()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            LoadScene(SCENE_MAIN_MENU);
        }
    }
}
