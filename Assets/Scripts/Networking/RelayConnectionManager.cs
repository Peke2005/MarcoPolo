using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace FrentePartido.Networking
{
    /// <summary>
    /// Static utility for Unity Relay allocation and transport configuration.
    /// Requires Unity Services to be initialized before use.
    /// </summary>
    public static class RelayConnectionManager
    {
        /// <summary>
        /// Creates a Relay allocation on the host side and returns the join code + allocation ID.
        /// Configures the UnityTransport on NetworkManager to use the relay server.
        /// </summary>
        public static async Task<(string joinCode, string allocationId)> CreateRelayAllocation(int maxPlayers = 2)
        {
            try
            {
                // maxPlayers param is connection count excluding host, so subtract 1
                int maxConnections = Mathf.Max(1, maxPlayers - 1);
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                RelayServerData relayServerData = new RelayServerData(allocation, "dtls");

                UnityTransport transport = GetTransport();
                transport.SetRelayServerData(relayServerData);

                Debug.Log($"[Relay] Allocation created. Region: {allocation.Region}, JoinCode: {joinCode}");

                return (joinCode, allocation.AllocationId.ToString());
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"[Relay] Failed to create allocation: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Joins an existing Relay allocation using a join code.
        /// Configures the UnityTransport on NetworkManager for the client.
        /// Returns the JoinAllocation for inspection if needed.
        /// </summary>
        public static async Task<JoinAllocation> JoinRelayAllocation(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                throw new ArgumentException("[Relay] Join code cannot be null or empty.", nameof(joinCode));
            }

            try
            {
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");

                UnityTransport transport = GetTransport();
                transport.SetRelayServerData(relayServerData);

                Debug.Log($"[Relay] Joined allocation. Region: {joinAllocation.Region}");

                return joinAllocation;
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"[Relay] Failed to join allocation with code '{joinCode}': {e.Message}");
                throw;
            }
        }

        private static UnityTransport GetTransport()
        {
            if (NetworkManager.Singleton == null)
            {
                throw new InvalidOperationException("[Relay] NetworkManager.Singleton is null. Ensure NetworkManager exists in the scene.");
            }

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                throw new InvalidOperationException("[Relay] UnityTransport component not found on NetworkManager.");
            }

            return transport;
        }
    }
}
