using System.Text.Json;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Monitor.Cli;

namespace House.Of.Arbitration.Tests.Unit;

public class MonitorSessionTests
{
    [Fact]
    public async Task MonitorDatabase_SavesAndQueriesMatchAndScores()
    {
        await using var db = new MonitorDatabase(":memory:");
        await db.InitializeAsync();

        var comp = new CompetitionModel
        {
            Id = 10,
            Name = "Open Wushu 2026",
            Type = CompetitionType.Sanda
        };
        await db.SaveCompetitionAsync(comp);

        var match = new MatchInfoData
        {
            Id = 42,
            Type = RoundType.Knockouts,
            Round = 1,
            CategoryId = 3,
            CategoryName = "Senior -70kg",
            StageName = "1/2 finale",
            Competitor1 = new CompetitorModel { Id = 101, FirstName = "Alice", LastName = "Dupont" },
            Competitor2 = new CompetitorModel { Id = 102, FirstName = "Bob", LastName = "Martin" }
        };
        await db.SaveMatchAsync(match);

        var score = new TransfertScoreModel
        {
            JudgePosition = "1",
            CompetitorId = 101,
            Side = Constants.Side.Red,
            Score = 2.0,
            Round = 1
        };
        await db.SaveJudgeScoreAsync(match.Id, score);

        await db.MarkMatchFinishedAsync(match.Id, 101);

        var history = await db.GetMatchHistoryAsync();
        Assert.Single(history);
        Assert.Equal(42, history[0].Id);
        Assert.Equal("Senior -70kg", history[0].CategoryName);
        Assert.Equal(101, history[0].WinnerId);
        Assert.True(history[0].IsFinished);

        var scores = await db.GetScoresForMatchAsync(42);
        Assert.Single(scores);
        Assert.Equal("1", scores[0].JudgePosition);
        Assert.Equal("Red", scores[0].Side);
        Assert.Equal(2.0, scores[0].Score);
    }

    [Fact]
    public async Task MonitorDatabase_LogsAndAcksCorrections()
    {
        await using var db = new MonitorDatabase(":memory:");
        await db.InitializeAsync();

        long id = await db.LogCorrectionSentAsync("CORRECTION_WINNER", "{\"MatchId\":5,\"WinningSide\":1}");
        Assert.True(id > 0);
        await db.MarkCorrectionAckedAsync(id);
    }

    [Fact]
    public void CorrectionModels_SerializeAndDeserializeCorrectly()
    {
        var winnerCorr = new WinnerCorrectionModel
        {
            MatchId = 12,
            WinningSide = Constants.Side.Blue,
            WinnerCompetitorId = 205,
            Reason = "Erreur de saisie coin"
        };
        string wMsg = $"{Constants.Message.CORRECTION_WINNER}{JsonSerializer.Serialize(winnerCorr)}";
        Assert.StartsWith("CORRECTION_WINNER:", wMsg);

        var wParsed = JsonSerializer.Deserialize<WinnerCorrectionModel>(wMsg[Constants.Message.CORRECTION_WINNER.Length..]);
        Assert.NotNull(wParsed);
        Assert.Equal(12, wParsed.MatchId);
        Assert.Equal(Constants.Side.Blue, wParsed.WinningSide);
        Assert.Equal(205, wParsed.WinnerCompetitorId);

        var scoreCorr = new ScoreCorrectionModel
        {
            MatchId = 12,
            JudgePosition = "A_1",
            Side = Constants.Side.Red,
            NewScore = 4.5,
            Round = 2,
            Reason = "Recalcul jury"
        };
        string sMsg = $"{Constants.Message.CORRECTION_SCORE}{JsonSerializer.Serialize(scoreCorr)}";
        Assert.StartsWith("CORRECTION_SCORE:", sMsg);

        var sParsed = JsonSerializer.Deserialize<ScoreCorrectionModel>(sMsg[Constants.Message.CORRECTION_SCORE.Length..]);
        Assert.NotNull(sParsed);
        Assert.Equal("A_1", sParsed.JudgePosition);
        Assert.Equal(4.5, sParsed.NewScore);
        Assert.Equal(2, sParsed.Round);
    }

    [Fact]
    public async Task MonitorSession_ProcessMessageAsync_SavesCompetitionMatchScoresAndTimer()
    {
        await using var db = new MonitorDatabase(":memory:");
        await db.InitializeAsync();
        using var client = new WindowsBluetoothClient();
        using var session = new MonitorSession(client, db);

        var jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        var comp = new CompetitionModel { Id = 7, Name = "Test Comp", Type = CompetitionType.Sanda };
        var cat = new CategoryModel { Id = 1, Competition = comp };
        comp.Categories.Add(cat);

        var match = new MatchInfoData
        {
            Id = 99,
            Type = RoundType.Knockouts,
            Round = 1,
            CategoryId = 1,
            CategoryName = "Senior -70kg",
            StageName = "Finale",
            Competitor1 = new CompetitorModel { Id = 1, FirstName = "A", LastName = "B" },
            Competitor2 = new CompetitorModel { Id = 2, FirstName = "C", LastName = "D" }
        };

        var score = new TransfertScoreModel
        {
            JudgePosition = "1",
            Side = Constants.Side.Red,
            CompetitorId = 1,
            Score = 1.0,
            Round = 1
        };

        await session.ProcessMessageAsync($"{Constants.Message.SERVER_NAME}TestServer");
        await session.ProcessMessageAsync($"{Constants.Message.COMPETITION_DATA}{JsonSerializer.Serialize(comp, jsonOpts)}");
        await session.ProcessMessageAsync($"{Constants.Message.MATCH_INFO}{JsonSerializer.Serialize(match, jsonOpts)}");
        await session.ProcessMessageAsync($"{Constants.Message.JUDGE_SCORE}{JsonSerializer.Serialize(score, jsonOpts)}");
        await session.ProcessMessageAsync(Constants.Message.TIMER_START);

        var result = new MatchResultModel
        {
            MatchId = 99,
            WinnerId = 1,
            WinnerName = "A B",
            WinnerSide = Constants.Side.Red,
            IsFinished = true,
            Reason = "Victoire Coin Rouge"
        };
        await session.ProcessMessageAsync($"{Constants.Message.MATCH_RESULT}{JsonSerializer.Serialize(result, jsonOpts)}");

        Assert.Equal("TestServer", session.ServerName);
        Assert.NotNull(session.CurrentCompetition);
        Assert.NotNull(session.CurrentMatch);
        Assert.Equal(1, session.TotalScoresReceived);
        Assert.True(session.IsTimerRunning);

        var history = await db.GetMatchHistoryAsync();
        Assert.Single(history);
        Assert.Equal(99, history[0].Id);
        Assert.Equal(1, history[0].WinnerId);
        Assert.True(history[0].IsFinished);

        var scores = await db.GetScoresForMatchAsync(99);
        Assert.Single(scores);
        Assert.Equal("1", scores[0].JudgePosition);
    }
}
