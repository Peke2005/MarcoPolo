using UnityEngine;

namespace FrentePartido.Data
{
    public enum MatchState : byte
    {
        WaitingForPlayers,
        Lobby,
        Loading,
        InProgress,
        PostMatch
    }

    public enum GameMode : byte
    {
        Rounds1v1,
        Deathmatch
    }

    public enum RoundState : byte
    {
        Intro,
        Active,
        SuddenDeath,
        Ended
    }

    public enum BeaconState : byte
    {
        Inactive,
        Active,
        Contested,
        CapturingP1,
        CapturingP2,
        Captured
    }

    public enum PlayerState : byte
    {
        Idle,
        Moving,
        Shooting,
        Reloading,
        UsingAbility,
        ThrowingGrenade,
        Dead
    }

    public enum PickupType : byte
    {
        Health,
        Ammo,
        Armor
    }

    public enum PlayerFaction : byte
    {
        Blue,
        Red
    }

    public static class RuntimeMatchSettings
    {
        public static GameMode Mode = GameMode.Rounds1v1;
        public static Vector2 BoundsMin = new Vector2(-12f, -7f);
        public static Vector2 BoundsMax = new Vector2(12f, 7f);

        public static void ApplyMode(GameMode mode)
        {
            Mode = mode;
            if (mode == GameMode.Deathmatch)
            {
                BoundsMin = new Vector2(-22f, -13f);
                BoundsMax = new Vector2(22f, 13f);
            }
            else
            {
                BoundsMin = new Vector2(-12f, -7f);
                BoundsMax = new Vector2(12f, 7f);
            }
        }
    }
}
