using Grid;
using System.Collections;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public DijkstraInfo path;
    Grid3D grid;
    void Start()
    {
        grid = Grid3D.Instance;
        StartCoroutine(Move());
    }
    IEnumerator Move()
    {
        for (int i = 0; i < path.pathIndexes.Length; i++)
        {
            for (int j = 0; j < grid.graph.Length; j++)
            {
                if (grid.graph[j].Index == path.pathIndexes[i])
                {
                    transform.position = grid.graph[j].WorldPosition;
                    break;
                }
            }
            Debug.Log("here");
            yield return new WaitForSeconds(.1f);
        }
    }
}
