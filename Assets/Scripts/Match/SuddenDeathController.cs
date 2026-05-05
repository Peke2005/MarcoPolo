using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;

namespace FrentePartido.Match
{
    /// <summary>
    /// Sudden death: when a round timer expires without a kill, all environment
    /// cover and decor are removed so the two players have nowhere to hide and
    /// must finish the round.
    /// </summary>
    public class SuddenDeathController : NetworkBehaviour
    {
        [SerializeField] private BalanceTuningData _balance;

        public void StartSuddenDeath()
        {
            ClearCoverEverywhere();
            ClearCoverClientRpc();
        }

        public void StopSuddenDeath()
        {
            // No-op: cover is rebuilt by GameplayVisualNormalizer when the next
            // round starts (it runs at scene/round refresh time).
        }

        private static void ClearCoverEverywhere()
        {
            // Decor (crates, barrels, grass) lives under ~ArenaDecor. Wipe the root
            // outright so colliders, sprites, and shadows all vanish in one go.
            var decorRoot = GameObject.Find("~ArenaDecor");
            if (decorRoot != null) Destroy(decorRoot);

            // Defensive: anything matching the cover/decor naming convention that
            // ended up outside the root (legacy scenes, etc.).
            foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr == null) continue;
                string n = sr.gameObject.name;
                if (n.StartsWith("Cover_") || n.StartsWith("Decor_"))
                    Destroy(sr.gameObject);
            }
        }

        [ClientRpc]
        private void ClearCoverClientRpc()
        {
            ClearCoverEverywhere();
        }
    }
}
