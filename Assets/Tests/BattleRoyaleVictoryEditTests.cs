using NUnit.Framework;
using System.Collections.Generic;

public class BattleRoyaleVictoryEditTests
{
    public enum GameResult
    {
        InProgress,
        Victory,
        Draw
    }

    // Método helper para simular la lógica de evaluación del GameManager
    private GameResult EvaluateMatchResult(List<int> playerLives)
    {
        int aliveCount = 0;
        foreach (int lives in playerLives)
        {
            if (lives > 0) aliveCount++;
        }

        if (aliveCount == 1) return GameResult.Victory;
        if (aliveCount == 0) return GameResult.Draw;
        return GameResult.InProgress;
    }

    [Test]
    public void Match_MultiplePlayersAlive_GameContinues()
    {
        // 4 jugadores vivos
        List<int> playerLives = new List<int> { 3, 3, 2, 1 };

        GameResult result = EvaluateMatchResult(playerLives);

        Assert.AreEqual(GameResult.InProgress, result, "El juego debe continuar si hay más de 1 jugador vivo.");
    }

    [Test]
    public void Match_OnePlayerRemaining_TriggersVictory()
    {
        // Queda solo 1 jugador con vida
        List<int> playerLives = new List<int> { 0, 2, 0, 0 };

        GameResult result = EvaluateMatchResult(playerLives);

        Assert.AreEqual(GameResult.Victory, result, "El GameManager debe anunciar la victoria si queda exactamente 1 jugador vivo.");
    }

    [Test]
    public void Match_SimultaneousElimination_TriggersDraw()
    {
        // Muerte simultánea (ej. la misma bomba los elimina a todos)
        List<int> playerLives = new List<int> { 0, 0, 0, 0 };

        GameResult result = EvaluateMatchResult(playerLives);

        Assert.AreEqual(GameResult.Draw, result, "Si los últimos jugadores mueren al mismo tiempo, el sistema debe declarar Empate.");
    }

    [Test]
    public void Match_TwoPlayersRemainingIn4PlayerMode_GameContinues()
    {
        // Modo 4 jugadores, 2 eliminados, 2 con vida
        List<int> playerLives = new List<int> { 3, 0, 0, 1 };

        GameResult result = EvaluateMatchResult(playerLives);

        Assert.AreEqual(GameResult.InProgress, result, "El juego debe continuar mientras queden al menos 2 jugadores en pie.");
    }
}