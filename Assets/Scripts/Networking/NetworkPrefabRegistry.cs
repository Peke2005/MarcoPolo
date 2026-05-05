using Unity.Netcode;
using UnityEngine;

namespace FrentePartido.Networking
{
    /// <summary>
    /// Registers every runtime-spawned NetworkObject prefab before host/client start.
    /// Prefabs live under Resources/NetworkPrefabs so builds and LAN smoke can load
    /// the exact same assets the scenes reference by GUID.
    /// </summary>
    public static class NetworkPrefabRegistry
    {
        private const string ResourcePath = "NetworkPrefabs";

        public static void RegisterDefaults(NetworkManager networkManager)
        {
            if (networkManager == null) return;
            if (networkManager.NetworkConfig == null)
            {
                networkManager.NetworkConfig = new NetworkConfig();
            }

            GameObject[] prefabs = Resources.LoadAll<GameObject>(ResourcePath);
            int registered = 0;

            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null) continue;
                if (prefab.GetComponent<NetworkObject>() == null) continue;

                try
                {
                    networkManager.AddNetworkPrefab(prefab);
                    registered++;
                }
                catch (System.Exception e)
                {
                    // NGO throws if a prefab is already present; that is safe here.
                    if (e.Message == null || e.Message.IndexOf("already", System.StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        Debug.LogWarning($"[NetworkPrefabs] Could not register {prefab.name}: {e.Message}");
                    }
                }
            }

            Debug.Log($"[NetworkPrefabs] Registered runtime prefabs from Resources/{ResourcePath}: {registered}/{prefabs.Length}");
        }
    }
}
