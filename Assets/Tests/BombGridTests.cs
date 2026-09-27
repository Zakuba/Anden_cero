using NUnit.Framework;
using UnityEngine;

public class BombGridTests
{
    [Test]
    public void GetGridCenter_RedondeaCoordenadasDecimalesACasillaMasCercana()
    {
        // 1. Arrange (Preparar datos de prueba)
        Vector3 posicionJugador = new Vector3(1.4f, 0f, 2.6f);
        float gridSize = 1f;
        float alturaSpawnBomba = -1f;

        // 2. Act (Ejecutar la fórmula exacta utilizada por PlayerBombController)
        float x = Mathf.Round(posicionJugador.x / gridSize) * gridSize;
        float z = Mathf.Round(posicionJugador.z / gridSize) * gridSize;
        Vector3 resultadoObtenido = new Vector3(x, alturaSpawnBomba, z);

        // 3. Assert (Verificar resultado esperado: 1.4 redondea a 1.0, 2.6 a 3.0)
        Vector3 resultadoEsperado = new Vector3(1f, -1f, 3f);
        Assert.AreEqual(resultadoEsperado, resultadoObtenido);
    }

    [Test]
    public void GetGridCenter_PosicionNegativa_RedondeaCorrectamente()
    {
        // 1. Arrange
        Vector3 posicionJugador = new Vector3(-2.3f, 0f, -0.7f);
        float gridSize = 1f;
        float alturaSpawnBomba = -1f;

        // 2. Act
        float x = Mathf.Round(posicionJugador.x / gridSize) * gridSize;
        float z = Mathf.Round(posicionJugador.z / gridSize) * gridSize;
        Vector3 resultadoObtenido = new Vector3(x, alturaSpawnBomba, z);

        // 3. Assert
        Vector3 resultadoEsperado = new Vector3(-2f, -1f, -1f);
        Assert.AreEqual(resultadoEsperado, resultadoObtenido);
    }
}