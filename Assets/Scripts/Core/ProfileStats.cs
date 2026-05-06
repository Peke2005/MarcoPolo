using UnityEngine;

namespace FrentePartido.Core
{
    public static class ProfileStats
    {
        private const string MatchesKey = "FP_Profile_Matches";
        private const string WinsKey = "FP_Profile_Wins";
        private const string LossesKey = "FP_Profile_Losses";

        public readonly struct Snapshot
        {
            public readonly int Matches;
            public readonly int Wins;
            public readonly int Losses;
            public readonly float WinRate;
            public readonly string Rank;

            public Snapshot(int matches, int wins, int losses)
            {
                Matches = Mathf.Max(0, matches);
                Wins = Mathf.Max(0, wins);
                Losses = Mathf.Max(0, losses);
                WinRate = Matches > 0 ? (Wins * 100f) / Matches : 0f;
                Rank = ResolveRank(Matches, Wins, WinRate);
            }
        }

        public static Snapshot Load()
        {
            return new Snapshot(
                PlayerPrefs.GetInt(MatchesKey, 0),
                PlayerPrefs.GetInt(WinsKey, 0),
                PlayerPrefs.GetInt(LossesKey, 0));
        }

        public static void RecordMatch(bool won)
        {
            var current = Load();
            PlayerPrefs.SetInt(MatchesKey, current.Matches + 1);
            PlayerPrefs.SetInt(WinsKey, current.Wins + (won ? 1 : 0));
            PlayerPrefs.SetInt(LossesKey, current.Losses + (won ? 0 : 1));
            PlayerPrefs.Save();
        }

        private static string ResolveRank(int matches, int wins, float winRate)
        {
            if (matches <= 0) return "SIN RANGO";
            if (wins >= 30 && winRate >= 70f) return "ELITE";
            if (wins >= 18 && winRate >= 60f) return "ORO";
            if (wins >= 10 && winRate >= 50f) return "PLATA";
            if (wins >= 4) return "BRONCE";
            return "RECLUTA";
        }
    }
}
