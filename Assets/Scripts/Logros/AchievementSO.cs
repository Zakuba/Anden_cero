using UnityEngine;

[CreateAssetMenu(fileName = "New Achievement", menuName = "Logros/Nuevo Logro")]
public class AchievementSO : ScriptableObject
{
    public string id; // Ej: "bateador_experto"
    public string title;
    public string description;
    public int targetGoal; // Ej: 50
    // public Sprite icon; // Descomentar cuando agregues iconos
}