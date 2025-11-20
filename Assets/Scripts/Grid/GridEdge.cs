using Grid;
using UnityEngine;

public class GridEdge
{
    public readonly GridNode a;
    public readonly GridNode b;
    public int weight;

    public GridEdge(in GridNode a, in GridNode b, int weight = 1)
    {
        if (a == null || b == null || a == b)
        {
            Debug.LogError(a == null ? "GridNode a cannot be null." : b == null ? "GridNode b  cannot be null." : "You can't create an edge that links a node to itself.");
            return;
        }

        this.a = a;
        this.b = b;
        this.weight = weight;
    }
    public GridNode GetOppositeNode(in GridNode node)
    {
        if (node == null)
        {
            Debug.LogError("Given node  cannot be null.");
            return null;
        }

        if (node == a)
            return b;
        else if (node == b)
            return a;

        Debug.LogError("This edge does not include the specified node.");
        return null;
    }
}