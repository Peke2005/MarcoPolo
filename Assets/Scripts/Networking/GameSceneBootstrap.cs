using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using FrentePartido.Core;

namespace FrentePartido.Networking
{
    /// <summary>
    /// Offline/solo fallback. If 04_Game loads without an active network session
    /// (e.g. pressing Play directly on the Game scene in the editor), auto-starts
    /// a local host so the player can walk around the arena. When the normal
    /// Lobby → Start flow is used, NetworkManager is already listening and this
    /// script does nothing.
    /// </summary>
    public static class GameSceneBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Also handle the case where the Game scene is the very first scene
            // loaded in the editor (Play-in-scene).
            TryRunForActiveScene();
        }

        private static void TryRunForActiveScene()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.name == SceneFlowController.SCENE_GAME)
            {
                StartRunner();
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneFlowController.SCENE_GAME) return;
            StartRunner();
        }

        private static void StartRunner()
        {
            if (LanSmokeBootstrap.HasLanSmokeArgs()) return;

            var runnerGO = new GameObject("~GameSceneBootstrapRunner");
            Object.DontDestroyOnLoad(runnerGO);
            runnerGO.AddComponent<Runner>();
        }

        private class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                // Wait one frame so scene objects finish Awake/OnEnable.
                yield return null;

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    Destroy(gameObject);
                    yield break;
                }

                var spawn = FindFirstObjectByType<PlayerSpawnManager>();
                if (spawn == null || spawn.PlayerPrefab == null)
                {
                    Debug.LogWarning("[OfflineBoot] No PlayerSpawnManager/PlayerPrefab in scene.");
                    Destroy(gameObject);
                    yield break;
                }

                var nm = NetworkManager.Singleton;
                if (nm == null)
                {
                    Debug.LogWarning("[OfflineBoot] NetworkManager.Singleton missing.");
                    Destroy(gameObject);
                    yield break;
                }

                try { nm.AddNetworkPrefab(spawn.PlayerPrefab); }
                catch (System.Exception e) { Debug.LogWarning($"[OfflineBoot] AddNetworkPrefab: {e.Message}"); }

                if (!nm.StartHost())
                {
                    Debug.LogError("[OfflineBoot] StartHost failed.");
                }
                else
                {
                    Debug.Log("[OfflineBoot] Started offline host for solo gameplay.");
                }

                Destroy(gameObject);
            }
        }
    }
}
