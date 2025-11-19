namespace Grid
{
    // Bitmask flags describing static and dynamic states of a node.
    [System.Flags]
    public enum NodeState
    {
        Walkable = 1 << 0,    // Traversable by enemies.
        Buildable = 1 << 1,    // Can host a tower.
        HasTower = 1 << 2,    // Tower currently placed.
        HasEnemy = 1 << 3,    // At least one enemy occupying this node.
        IsEnemyGoal = 1 << 4     // Node represents an enemy goal cell.
    }
}

public enum TurretFirePattern
{
    Consecutive,
    Cone
}
