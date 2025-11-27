namespace Grid
{
    // Bitmask flags describing static and dynamic states of a node.
    [System.Flags]
    public enum NodeState
    {
        Default = 1 << 0,
        Walkable = 1 << 1,    // Traversable by enemies.
        Buildable = 1 << 2,    // Can host a tower.
        HasTower = 1 << 3,    // Tower currently placed.
        HasEnemy = 1 << 4,    // At least one enemy occupying this node.
        IsEnemyGoal = 1 << 5     // Node represents an enemy goal cell.
    }
}

public enum TurretFirePattern
{
    None,
    Consecutive,
    Bazooka
}

/// <summary>
/// Describes high-level gameplay phases used to gate build and defence systems.
/// </summary>
public enum GamePhase
{
    Building,
    Defence
}

/// <summary>
/// Describes when the next wave should start after the current one finishes spawning.
/// </summary>
public enum WaveAdvanceMode
{
    FixedInterval,
    AfterClear
}

/// <summary>
/// Defines when a sub-wave begins relative to its macro wave.
/// </summary>
public enum SubWaveStartMode
{
    DelayFromWaveStart,
    AfterPreviousClear
}
