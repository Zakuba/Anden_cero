using NUnit.Framework;
using UnityEngine;

public class PlayerMovementAndUIEditTests
{
    [Test]
    public void PlayerMovimiento_CalculoDeDireccionNormalizada_ProcesaVectorCorrectamente()
    {
        // Arrange
        float horizontal = 1f;
        float vertical = 1f;

        // Act
        Vector3 inputVector = new Vector3(horizontal, 0f, vertical).normalized;

        // Assert
        Assert.AreEqual(1f, inputVector.magnitude, 0.001f, "El vector de entrada diagonal debe normalizarse a magnitud 1.");
    }

    [Test]
    public void PlayerMovimiento_SinTeclasPresionadas_ProduceVectorCero()
    {
        // Arrange
        float horizontal = 0f;
        float vertical = 0f;

        // Act
        Vector3 inputVector = new Vector3(horizontal, 0f, vertical).normalized;

        // Assert
        Assert.AreEqual(Vector3.zero, inputVector, "Si no se presionan teclas, el vector de movimiento debe ser nulo.");
    }
}