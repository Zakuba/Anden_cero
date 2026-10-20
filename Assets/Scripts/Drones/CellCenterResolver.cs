using System;
using UnityEngine;

/// <summary>
/// Centro de la casilla bajo una posición, calculado igual que lo hace
/// PlayerBombController: raycast hacia abajo y bounds.center del collider de la casilla.
/// No necesita ningún objeto en la escena ni un origen configurado.
///
/// Cada dron tiene su propia instancia (guarda la última casilla encontrada).
/// Solo se usa en el servidor.
/// </summary>
public class CellCenterResolver
{
    private const float RayStartHeight = 0.5f;
    private const float RayLength = 6f;

    // Buffer compartido para no generar basura en cada consulta
    private static readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private static bool warnedNoTile;

    private Vector2 lastCenter; // x = X, y = Z de la última casilla encontrada
    private bool hasLastCenter;

    /// <summary>
    /// Devuelve el centro (X, Z) de la casilla que está debajo de 'position', con la altura 'y' indicada.
    /// </summary>
    public Vector3 Resolve(Vector3 position, float gridSize, float y)
    {
        Vector3 origin = new Vector3(position.x, position.y + RayStartHeight, position.z);

        int count = Physics.RaycastNonAlloc(
            origin, Vector3.down, hitBuffer, RayLength, ~0, QueryTriggerInteraction.Ignore);

        // Casilla más cercana por debajo (los demás colliders del camino se ignoran)
        float bestDistance = float.PositiveInfinity;
        Vector3 bestCenter = default;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            Collider col = hitBuffer[i].collider;
            if (col == null || !IsTile(col)) continue;

            if (hitBuffer[i].distance < bestDistance)
            {
                bestDistance = hitBuffer[i].distance;
                bestCenter = col.bounds.center;
                found = true;
            }
        }

        if (found)
        {
            lastCenter = new Vector2(bestCenter.x, bestCenter.z);
            hasLastCenter = true;
            return new Vector3(bestCenter.x, y, bestCenter.z);
        }

        // Sin casilla debajo (junta entre casillas, borde del mapa o nombre no reconocido):
        // alinea tomando como referencia la última casilla conocida. Si nunca encontró
        // una, redondea a múltiplos de gridSize como se hacía antes.
        if (!hasLastCenter && !warnedNoTile)
        {
            warnedNoTile = true;
            Debug.LogWarning("[CellCenterResolver] No se encontró ninguna casilla bajo (" +
                             position.x + ", " + position.z + "). Revisa que el nombre del objeto " +
                             "de la casilla contenga 'casil', 'piso', 'anden' o 'floor'.");
        }

        float originX = hasLastCenter ? lastCenter.x : 0f;
        float originZ = hasLastCenter ? lastCenter.y : 0f;

        return new Vector3(
            SnapAxis(position.x, originX, gridSize),
            y,
            SnapAxis(position.z, originZ, gridSize));
    }

    public static float SnapAxis(float value, float axisOrigin, float cellSize)
    {
        if (cellSize <= 0f) return value;
        return axisOrigin + Mathf.Round((value - axisOrigin) / cellSize) * cellSize;
    }

    // Mismo criterio de nombres que usa PlayerBombController.GetGridCenter
    private static bool IsTile(Collider col)
    {
        string objName = col.gameObject.name;
        string rootName = col.transform.root.gameObject.name;

        return Has(objName, "casil") || Has(objName, "piso") || Has(objName, "anden") || Has(objName, "floor")
            || Has(rootName, "casil") || Has(rootName, "piso") || Has(rootName, "anden");
    }

    private static bool Has(string text, string token)
    {
        return text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
