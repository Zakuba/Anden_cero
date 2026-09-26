using UnityEngine;

public class GridVisualizer : MonoBehaviour
{
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private float gridExtent = 25f;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        float offset = gridSize / 2f;

        // Líneas verticales
        for (float x = -gridExtent + offset; x <= gridExtent; x += gridSize)
        {
            Gizmos.DrawLine(
                new Vector3(x, 0f, -gridExtent),
                new Vector3(x, 0f, gridExtent)
            );
        }

        // Líneas horizontales
        for (float z = -gridExtent + offset; z <= gridExtent; z += gridSize)
        {
            Gizmos.DrawLine(
                new Vector3(-gridExtent, 0f, z),
                new Vector3(gridExtent, 0f, z)
            );
        }
    }
}