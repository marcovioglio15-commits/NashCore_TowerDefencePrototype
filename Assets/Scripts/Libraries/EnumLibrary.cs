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
    Cone
}
