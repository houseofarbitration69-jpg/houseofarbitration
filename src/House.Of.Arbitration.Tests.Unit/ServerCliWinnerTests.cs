using House.Of.Arbitration.Models;
using House.Of.Arbitration.Server.Cli;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class ServerCliWinnerTests
{
    [Fact]
    public async Task SetWinnerSideAsync_SetsRedWinnerCorrectly()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var match = session.CurrentMatch;
        Assert.NotNull(match);
        Assert.False(match.IsFinished);

        bool result = await session.SetWinnerSideAsync(Constants.Side.Red, "KO Technique");

        Assert.True(result);
        Assert.True(match.IsFinished);
        Assert.Equal(Constants.Side.Red, match.WinnerSide);
        Assert.Equal(match.RedCompetitor, match.Winner);
        Assert.Equal("KO Technique", match.DecisionReason);
    }

    [Fact]
    public async Task SetWinnerSideAsync_SetsBlueWinnerCorrectly()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var match = session.CurrentMatch;
        Assert.NotNull(match);

        bool result = await session.SetWinnerSideAsync(Constants.Side.Blue);

        Assert.True(result);
        Assert.True(match.IsFinished);
        Assert.Equal(Constants.Side.Blue, match.WinnerSide);
        Assert.Equal(match.BlueCompetitor, match.Winner);
    }

    [Fact]
    public async Task SetAutoWinnerAsync_Sanda_MajorityJudgesWins()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        // 5 juges Sanda par défaut : 3 juges donnent avantage au Rouge, 2 au Bleu
        session.Judges[0].RedPoints = 5;
        session.Judges[0].BluePoints = 2;

        session.Judges[1].RedPoints = 4;
        session.Judges[1].BluePoints = 1;

        session.Judges[2].RedPoints = 3;
        session.Judges[2].BluePoints = 2;

        session.Judges[3].RedPoints = 1;
        session.Judges[3].BluePoints = 4;

        session.Judges[4].RedPoints = 0;
        session.Judges[4].BluePoints = 3;

        bool result = await session.SetAutoWinnerAsync();

        Assert.True(result);
        var match = session.CurrentMatch;
        Assert.NotNull(match);
        Assert.True(match.IsFinished);
        Assert.Equal(Constants.Side.Red, match.WinnerSide);
        Assert.Equal(match.RedCompetitor, match.Winner);
        Assert.Contains("Majorité des juges (3 vs 2)", match.DecisionReason);
    }

    [Fact]
    public async Task SetAutoWinnerAsync_Sanda_TieBreakerByTotalPoints()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        // 2 juges pour Rouge, 2 juges pour Bleu, 1 juge à égalité
        session.Judges[0].RedPoints = 5;
        session.Judges[0].BluePoints = 2; // Rouge +1

        session.Judges[1].RedPoints = 5;
        session.Judges[1].BluePoints = 2; // Rouge +1

        session.Judges[2].RedPoints = 2;
        session.Judges[2].BluePoints = 2; // Nul

        session.Judges[3].RedPoints = 1;
        session.Judges[3].BluePoints = 2; // Bleu +1

        session.Judges[4].RedPoints = 1;
        session.Judges[4].BluePoints = 2; // Bleu +1

        // Total Rouge = 14 pts, Total Bleu = 10 pts
        bool result = await session.SetAutoWinnerAsync();

        Assert.True(result);
        var match = session.CurrentMatch;
        Assert.NotNull(match);
        Assert.Equal(Constants.Side.Red, match.WinnerSide);
        Assert.Contains("départage aux points (14-10)", match.DecisionReason);
    }

    [Fact]
    public async Task SetWinnerAsync_StringParser_SupportsAliases()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        // Test alias "r"
        bool r1 = await session.SetWinnerAsync("r");
        Assert.True(r1);
        Assert.Equal(Constants.Side.Red, session.CurrentMatch?.WinnerSide);

        // Test clear
        await session.ClearWinnerAsync();
        Assert.False(session.CurrentMatch?.IsFinished);
        Assert.Null(session.CurrentMatch?.Winner);

        // Test alias "bleu"
        bool r2 = await session.SetWinnerAsync("bleu");
        Assert.True(r2);
        Assert.Equal(Constants.Side.Blue, session.CurrentMatch?.WinnerSide);

        // Test alias "draw"
        bool r3 = await session.SetWinnerAsync("draw");
        Assert.True(r3);
        Assert.Null(session.CurrentMatch?.WinnerSide);
        Assert.Contains("Égalité", session.CurrentMatch?.Winner);
    }

    [Fact]
    public async Task SetTaoluScoreAsync_CalculatesCorrectGrade()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        await session.SetCompetitionTypeAsync(CompetitionType.Taolu);
        await session.SetMatchIndexAsync(2); // Match Taolu

        // Groupe A (3 juges) : scores = 4.8, 4.9, 5.0 -> moyenne 4.9
        var groupA = session.Judges.Where(j => j.Group == "A").ToList();
        groupA[0].Score = 4.8;
        groupA[1].Score = 4.9;
        groupA[2].Score = 5.0;

        // Groupe B (3 juges) : scores = 2.8, 2.7, 2.9 -> moyenne 2.8
        var groupB = session.Judges.Where(j => j.Group == "B").ToList();
        groupB[0].Score = 2.8;
        groupB[1].Score = 2.7;
        groupB[2].Score = 2.9;

        // Groupe C (3 juges) : scores = 1.9, 2.0, 2.1 -> moyenne 2.0
        var groupC = session.Judges.Where(j => j.Group == "C").ToList();
        groupC[0].Score = 1.9;
        groupC[1].Score = 2.0;
        groupC[2].Score = 2.1;

        bool result = await session.SetTaoluScoreAsync();

        Assert.True(result);
        var match = session.CurrentMatch;
        Assert.NotNull(match);
        Assert.True(match.IsFinished);
        Assert.Equal(9.7, match.FinalScore, precision: 1);
        Assert.Contains("Note finale : 9,70", match.DecisionReason?.Replace('.', ','));
    }
}
