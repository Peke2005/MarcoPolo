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
}
