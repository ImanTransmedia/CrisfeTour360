using System.Collections.Generic;
using UnityEngine;

public class GridNodeGraph : MonoBehaviour
{
    [Header("Grid")]
    public float cellSize = 5f;                 
    public Vector3 gridOrigin = Vector3.zero;   
    public bool useXZPlane = true;              

    // Mapa: coordenada de celda -> nodo
    private Dictionary<Vector2Int, GridNode> nodesByCell = new Dictionary<Vector2Int, GridNode>();

    public IReadOnlyDictionary<Vector2Int, GridNode> NodesByCell => nodesByCell;

    public void Rebuild()
    {
        nodesByCell.Clear();
        var nodes = FindObjectsOfType<GridNode>(true);

        foreach (var node in nodes)
        {
            Vector2Int cell = WorldToCell(node.transform.position);
            if (!nodesByCell.ContainsKey(cell))
                nodesByCell.Add(cell, node);
        }
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        Vector3 local = worldPos - gridOrigin;

        float a = useXZPlane ? local.x : local.x;
        float b = useXZPlane ? local.z : local.y;

        int cx = Mathf.RoundToInt(a / cellSize);
        int cy = Mathf.RoundToInt(b / cellSize);
        return new Vector2Int(cx, cy);
    }

    public Vector3 CellToWorldCenter(Vector2Int cell, float keepY)
    {
        float x = cell.x * cellSize;
        float yOrZ = cell.y * cellSize;

        if (useXZPlane)
            return gridOrigin + new Vector3(x, keepY, yOrZ);
        else
            return gridOrigin + new Vector3(x, yOrZ, 0f);
    }

    public bool HasNode(Vector2Int cell) => nodesByCell.ContainsKey(cell);

    public bool TryGetNode(Vector2Int cell, out GridNode node) => nodesByCell.TryGetValue(cell, out node);

    private void OnEnable()
    {
        Rebuild();
    }
}
