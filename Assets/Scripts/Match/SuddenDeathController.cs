using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;

namespace FrentePartido.Match
{
    /// <summary>
    /// Sudden death marker. Cover must stay stable between rounds; removing it
    /// makes the map desync visually and breaks collision readability.
    /// </summary>
    public class SuddenDeathController : NetworkBehaviour
    {
        [SerializeField] private BalanceTuningData _balance;

        public void StartSuddenDeath()
        {
            Debug.Log("[SuddenDeath] Started. Cover remains intact.");
        }

        public void StopSuddenDeath()
        {
            Debug.Log("[SuddenDeath] Stopped.");
        }
    }
}
