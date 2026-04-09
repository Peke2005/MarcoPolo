using FrentePartido.Player;

namespace FrentePartido.Match
{
    public static class WinConditionEvaluator
    {
        /// <summary>
        /// Evaluate round winner when time expires.
        /// Returns winner clientId, or 0 if sudden death needed.
        /// </summary>
        public static ulong EvaluateRoundWinner(
            PlayerHealth p1Health, PlayerHealth p2Health,
            BeaconCaptureController beacon,
            ulong p1ClientId, ulong p2ClientId)
        {
            int hp1 = p1Health != null ? p1Health.CurrentHealth.Value : 0;
            int hp2 = p2Health != null ? p2Health.CurrentHealth.Value : 0;

            // Higher health wins
            if (hp1 > hp2) return p1ClientId;
            if (hp2 > hp1) return p2ClientId;

            // Tied health - check beacon presence
            if (beacon != null)
            {
                float t1 = beacon.GetPlayerPresenceTime(p1ClientId);
                float t2 = beacon.GetPlayerPresenceTime(p2ClientId);

                if (t1 > t2) return p1ClientId;
                if (t2 > t1) return p2ClientId;
            }

            // Still tied - sudden death
            return 0;
        }

        /// <summary>
        /// Evaluate winner after sudden death.
        /// Must always return a winner (no 0).
        /// </summary>
        public static ulong EvaluateSuddenDeathWinner(
            PlayerHealth p1Health, PlayerHealth p2Health,
            ulong p1ClientId, ulong p2ClientId)
        {
            bool p1Dead = p1Health == null || p1Health.IsDead;
            bool p2Dead = p2Health == null || p2Health.IsDead;

            if (p1Dead && !p2Dead) return p2ClientId;
            if (p2Dead && !p1Dead) return p1ClientId;

            int hp1 = p1Health != null ? p1Health.CurrentHealth.Value : 0;
            int hp2 = p2Health != null ? p2Health.CurrentHealth.Value : 0;

            if (hp1 >= hp2) return p1ClientId;
            return p2ClientId;
        }
    }
}
