// What killed the player, for the Game Over tip.
public enum DeathCause
{
    Unknown,
    Chaser,
    Dasher,
    Shooter,
    EnemyShot
}

// Counts what happens during one run (start to Restart / Menu; a revive keeps
// counting), for the Game Over screen.
public static class RunStats
{
    public static int Kills { get; private set; }
    // Highest kill-streak multiplier reached (1 = never got a streak going).
    public static int BestMultiplier { get; private set; } = 1;
    public static float Seconds { get; private set; }

    public static void BeginRun()
    {
        Kills = 0;
        BestMultiplier = 1;
        Seconds = 0f;
    }

    public static void RecordKill(int streakMultiplier)
    {
        Kills++;
        if (streakMultiplier > BestMultiplier)
            BestMultiplier = streakMultiplier;
    }

    public static void Tick(float deltaTime)
    {
        Seconds += deltaTime;
    }
}
