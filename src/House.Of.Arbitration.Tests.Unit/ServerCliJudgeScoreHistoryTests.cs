using House.Of.Arbitration.Models;
using House.Of.Arbitration.Server.Cli;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class ServerCliJudgeScoreHistoryTests
{
    [Fact]
    public void RecordJudgeScore_SandaPoints_AddsEntryToHistoryAndInvokesEvent()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        JudgeScoreEntry? recordedEntry = null;
        session.JudgeScoreRecorded += (_, entry) => recordedEntry = entry;

        var judge = session.Judges[0]; // Juge 1
        judge.RedPoints = 1;

        var entry = session.RecordJudgeScore(
            judge,
            Constants.Side.Red,
            deltaPoints: 1.0,
            resultingScore: judge.RedPoints,
            deductionCode: null,
            actionType: "Point Rouge",
            description: "+1 Rouge (Total: 1)"
        );

        Assert.NotNull(entry);
        Assert.NotNull(recordedEntry);
        Assert.Equal(entry.Id, recordedEntry.Id);
        Assert.Equal("JUGE 1", entry.JudgeName);
        Assert.Equal(Constants.Side.Red, entry.Side);
        Assert.Equal(1.0, entry.DeltaPoints);
        Assert.Equal(1.0, entry.ResultingScore);
        Assert.Equal("Point Rouge", entry.ActionType);
        Assert.Single(session.JudgeScoreHistory);
    }

    [Fact]
    public void RecordJudgeScore_TaoluDeduction_AddsEntryWithCodeAndDetails()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var judge = new JudgeModel { Name = "JUGE A1", Number = 1, Group = "A", Score = 4.80 };
        var code = new MvtTimeCodeModel
        {
            Code = new MvtCodeModel { Code = "11", Value = 0.20, Description = "Perte d'équilibre" }
        };

        var entry = session.RecordJudgeScore(
            judge,
            side: null,
            deltaPoints: -0.20,
            resultingScore: 4.80,
            deductionCode: code,
            actionType: "Déduction",
            description: "Déduction 11 (-0.20) => Nouveau score: 4.80"
        );

        Assert.NotNull(entry);
        Assert.Equal("JUGE A1", entry.JudgeName);
        Assert.Equal("A", entry.JudgeGroup);
        Assert.Equal("Déduction", entry.ActionType);
        Assert.Equal(-0.20, entry.DeltaPoints);
        Assert.Equal(4.80, entry.ResultingScore);
        Assert.NotNull(entry.DeductionCode);
        Assert.Equal("11", entry.DeductionCode.Code?.Code);
    }

    [Fact]
    public async Task GetJudgeScoresHistory_FilterByMatchId_ReturnsOnlyMatchingMatch()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var judge1 = session.Judges[0];
        var judge2 = session.Judges[1];

        // Match 1 actif
        session.RecordJudgeScore(judge1, Constants.Side.Red, 1.0, 1.0, null, "Point Rouge", "+1 Rouge");
        session.RecordJudgeScore(judge2, Constants.Side.Blue, 2.0, 2.0, null, "Point Bleu", "+2 Bleu");

        // Passage au match 2
        await session.NextMatchAsync();
        session.RecordJudgeScore(judge1, Constants.Side.Blue, 1.0, 1.0, null, "Point Bleu", "+1 Bleu");

        var allEntries = session.GetJudgeScoresHistory();
        Assert.Equal(3, allEntries.Count);

        var match1Entries = session.GetJudgeScoresHistory(matchId: 1);
        Assert.Equal(2, match1Entries.Count);
        Assert.All(match1Entries, e => Assert.Equal(1, e.MatchId));

        var match2Entries = session.GetJudgeScoresHistory(matchId: 2);
        Assert.Single(match2Entries);
        Assert.Equal(2, match2Entries[0].MatchId);
    }

    [Fact]
    public void GetJudgeScoresHistory_FilterByJudge_ReturnsOnlyMatchingJudge()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var judge1 = session.Judges[0]; // JUGE 1
        var judge2 = session.Judges[1]; // JUGE 2

        session.RecordJudgeScore(judge1, Constants.Side.Red, 1.0, 1.0, null, "Point Rouge", "+1 Rouge");
        session.RecordJudgeScore(judge2, Constants.Side.Blue, 1.0, 1.0, null, "Point Bleu", "+1 Bleu");
        session.RecordJudgeScore(judge1, Constants.Side.Red, 2.0, 3.0, null, "Point Rouge", "+2 Rouge");

        var j1Entries = session.GetJudgeScoresHistory(judgeNameOrNum: "1");
        Assert.Equal(2, j1Entries.Count);
        Assert.All(j1Entries, e => Assert.Equal("JUGE 1", e.JudgeName));

        var j2Entries = session.GetJudgeScoresHistory(judgeNameOrNum: "JUGE 2");
        Assert.Single(j2Entries);
        Assert.Equal("JUGE 2", j2Entries[0].JudgeName);
    }

    [Fact]
    public void GetJudgeScoreSummary_CalculatesRedBlueAndDeductionTotals()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var judge = session.Judges[0];

        session.RecordJudgeScore(judge, Constants.Side.Red, 1.0, 1.0, null, "Point Rouge", "+1 Rouge");
        session.RecordJudgeScore(judge, Constants.Side.Red, 1.0, 2.0, null, "Point Rouge", "+1 Rouge");
        session.RecordJudgeScore(judge, Constants.Side.Blue, 2.0, 2.0, null, "Point Bleu", "+2 Bleu");
        session.RecordJudgeScore(judge, null, -0.2, 9.8, new MvtTimeCodeModel(), "Déduction", "Faute");

        var summary = session.GetJudgeScoreSummary();

        Assert.Equal(4, summary.TotalEntries);
        Assert.Equal(2, summary.RedEntries);
        Assert.Equal(1, summary.BlueEntries);
        Assert.Equal(1, summary.DeductionEntries);
    }

    [Fact]
    public void ClearJudgeScoresHistory_EmptiesList()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var judge = session.Judges[0];
        session.RecordJudgeScore(judge, Constants.Side.Red, 1.0, 1.0, null, "Point Rouge", "+1 Rouge");
        Assert.NotEmpty(session.JudgeScoreHistory);

        session.ClearJudgeScoresHistory();
        Assert.Empty(session.JudgeScoreHistory);
    }
}
