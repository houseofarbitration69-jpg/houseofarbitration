using House.Of.Arbitration.Models;
using House.Of.Arbitration.Server.Cli;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class ServerCliRoundTests
{
    [Fact]
    public async Task NextRoundAsync_AdvancesToRound2_ResetsJudgeScoresAndTimer()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var match = session.CurrentMatch;
        Assert.NotNull(match);
        Assert.Equal(1, match.CurrentRound);
        Assert.Equal(3, match.MaxRounds);
        Assert.Empty(match.RoundResults);

        // Les juges votent pour le Round 1
        session.Judges[0].RedPoints = 2;
        session.Judges[1].RedPoints = 1;
        session.Judges[2].BluePoints = 1;

        bool roundEventFired = false;
        int eventRoundNumber = 0;
        session.RoundChanged += (_, args) =>
        {
            roundEventFired = true;
            eventRoundNumber = args.RoundNumber;
        };

        // Act - passage au round suivant
        bool result = await session.NextRoundAsync();

        // Assert
        Assert.True(result);
        Assert.True(roundEventFired);
        Assert.Equal(2, eventRoundNumber);
        Assert.Equal(2, match.CurrentRound);
        Assert.False(match.IsFinished);

        // Vérifier l'historique des rounds
        Assert.Single(match.RoundResults);
        var r1 = match.RoundResults[0];
        Assert.Equal(1, r1.RoundNumber);
        Assert.Equal(3, r1.RedVotes);
        Assert.Equal(1, r1.BlueVotes);
        Assert.Equal("Red", r1.WinnerColor);

        // Vérifier la réinitialisation des scores des juges pour le Round 2
        Assert.All(session.Judges, j =>
        {
            Assert.Equal(0, j.RedPoints);
            Assert.Equal(0, j.BluePoints);
        });

        // Vérifier que le timer est réinitialisé à 02:00
        Assert.Equal("02:00", session.TimeLeftDisplay);
        Assert.False(session.IsTimerRunning);
    }

    [Theory]
    [InlineData("red", "Red")]
    [InlineData("blue", "Blue")]
    [InlineData("draw", "Draw")]
    public async Task NextRoundAsync_WithForcedWinner_RecordsSpecifiedColor(string forced, string expectedColor)
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var match = session.CurrentMatch;
        Assert.NotNull(match);

        bool result = await session.NextRoundAsync(forced);

        Assert.True(result);
        Assert.Equal(2, match.CurrentRound);
        Assert.Single(match.RoundResults);
        Assert.Equal(expectedColor, match.RoundResults[0].WinnerColor);
    }

    [Fact]
    public async Task NextRoundAsync_TwoRoundWins_DeclaresWinnerAndEndsMatch()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var match = session.CurrentMatch;
        Assert.NotNull(match);

        // Round 1 : Rouge gagne
        session.Judges[0].RedPoints = 3;
        await session.NextRoundAsync();
        Assert.Equal(2, match.CurrentRound);
        Assert.False(match.IsFinished);

        // Round 2 : Rouge gagne à nouveau (2 victoires)
        session.Judges[0].RedPoints = 2;
        bool result = await session.NextRoundAsync();

        // Le match doit être automatiquement terminé à 2-0
        Assert.True(result);
        Assert.True(match.IsFinished);
        Assert.Equal(Constants.Side.Red, match.WinnerSide);
        Assert.Equal(match.RedCompetitor, match.Winner);
        Assert.Contains("2 rounds à 0", match.DecisionReason);
        Assert.Equal(2, match.RoundResults.Count);
    }

    [Fact]
    public async Task NextRoundAsync_BestOfThree_DecidedInRoundThree()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var match = session.CurrentMatch;
        Assert.NotNull(match);

        // Round 1 : Rouge gagne
        await session.NextRoundAsync("red");
        Assert.Equal(2, match.CurrentRound);
        Assert.False(match.IsFinished);

        // Round 2 : Bleu égalise
        await session.NextRoundAsync("blue");
        Assert.Equal(3, match.CurrentRound);
        Assert.False(match.IsFinished);

        // Round 3 : Rouge gagne
        await session.NextRoundAsync("red");

        // Match terminé 2-1 pour Rouge
        Assert.True(match.IsFinished);
        Assert.Equal(Constants.Side.Red, match.WinnerSide);
        Assert.Equal(match.RedCompetitor, match.Winner);
        Assert.Contains("2 rounds à 1", match.DecisionReason);
        Assert.Equal(3, match.RoundResults.Count);
    }

    [Fact]
    public async Task ResetMatchScores_ResetsRoundToOneAndClearsRoundResults()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var match = session.CurrentMatch;
        Assert.NotNull(match);

        // Passer au round 2
        await session.NextRoundAsync("red");
        Assert.Equal(2, match.CurrentRound);
        Assert.Single(match.RoundResults);

        // Réinitialiser le match
        session.ResetMatchScores();

        Assert.Equal(1, match.CurrentRound);
        Assert.Empty(match.RoundResults);
        Assert.All(session.Judges, j =>
        {
            Assert.Equal(0, j.RedPoints);
            Assert.Equal(0, j.BluePoints);
        });
    }

    [Fact]
    public async Task RecordJudgeScore_CapturesCurrentRoundNumber()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var judge = session.Judges[0];

        // Note au round 1
        var entryR1 = session.RecordJudgeScore(judge, Constants.Side.Red, 1.0, 1.0, null, "Point Rouge", "+1");
        Assert.Equal(1, entryR1.Round);

        // Passer au round 2
        await session.NextRoundAsync();

        // Note au round 2
        var entryR2 = session.RecordJudgeScore(judge, Constants.Side.Blue, 1.0, 1.0, null, "Point Bleu", "+1");
        Assert.Equal(2, entryR2.Round);
    }
}
