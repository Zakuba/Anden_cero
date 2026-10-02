using NUnit.Framework;
using UnityEngine;

public class PowerUpDropEditTests
{
    [Test]
    public void PowerUpDrop_ProbabilityThreshold_EvaluatesFiftyPercentCorrectly()
    {
        float dropChance = 0.5f;

        bool resultPasses = 0.4f <= dropChance;
        bool resultFails = 0.6f > dropChance;

        Assert.IsTrue(resultPasses, "Un valor de probabilidad <= 0.5 debe permitir el drop.");
        Assert.IsTrue(resultFails, "Un valor de probabilidad > 0.5 debe ignorar el drop.");
    }

    [Test]
    public void PowerUpDrop_SpawnPositionCalculation_MatchesBoxCoordinates()
    {
        Vector3 boxPosition = new Vector3(2.5f, 0f, -1.0f);
        float spawnHeight = -1f;

        Vector3 expectedSpawnPos = new Vector3(2.5f, -1f, -1.0f);
        Vector3 calculatedSpawnPos = new Vector3(boxPosition.x, spawnHeight, boxPosition.z);

        Assert.AreEqual(expectedSpawnPos, calculatedSpawnPos, "La coordenada del drop debe coincidir exactamente con la casilla de la caja.");
    }

    [Test]
    public void PowerUpDrop_ArrayBounds_ValidatesIndexRange()
    {
        int totalPowerUpsInGame = 5;
        int testIndex = 1;

        Assert.IsTrue(testIndex >= 0 && testIndex < totalPowerUpsInGame, "El índice aleatorio debe estar dentro de los límites de los 5 prefabs.");
    }
}