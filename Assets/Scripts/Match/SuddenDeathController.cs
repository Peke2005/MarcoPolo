using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;
using FrentePartido.Core;

namespace FrentePartido.Match
{
    /// <summary>
    /// Sudden death clears breakable cover on all peers. Round reset rebuilds it.
    /// </summary>
    public class SuddenDeathController : NetworkBehaviour
    {
        [SerializeField] private BalanceTuningData _balance;

        public void StartSuddenDeath()
        {
            BreakCoverClientRpc();
        }

        public void StopSuddenDeath()
        {
            Debug.Log("[SuddenDeath] Stopped.");
        }

        [ClientRpc]
        private void BreakCoverClientRpc()
        {
            int removed = GameplayVisualNormalizer.BreakSuddenDeathCover();
            Debug.Log($"[SuddenDeath] Started. Breakable cover removed: {removed}");
        }
    }
}
