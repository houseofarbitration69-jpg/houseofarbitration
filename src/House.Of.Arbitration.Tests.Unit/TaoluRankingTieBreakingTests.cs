using CommunityToolkit.Maui;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Server.Cli;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Competition;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class TaoluRankingTieBreakingTests
{
    [Fact]
    public async Task SetTaoluScoreAsync_CapturesIndividualJudgeScores_InMatchItem()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var comp = new CompetitionModel { Id = 1, Name = "Taolu Comp", Type = CompetitionType.Taolu };
        var matches = new List<MatchItem>
        {
            new MatchItem
            {
                Id = 1,
                Category = "Taolu Changquan",
                Type = RoundType.Order,
                RedCompetitor = "Athlète 1"
            }
        };
        await session.LoadCompetitionAsync(comp, matches);

        session.Judges.Clear();
        session.Judges.Add(new JudgeModel { Name = "Juge 1", Group = "A", Score = 4.8 });
        session.Judges.Add(new JudgeModel { Name = "Juge 2", Group = "B", Score = 2.7 });
        session.Judges.Add(new JudgeModel { Name = "Juge 3", Group = "C", Score = 1.9 });

        bool result = await session.SetTaoluScoreAsync();
        Assert.True(result);

        var match = session.CurrentMatch;
        Assert.NotNull(match);
        Assert.True(match.IsFinished);
        Assert.Equal(3, match.JudgeScores.Count);
        Assert.Contains(4.8, match.JudgeScores);
        Assert.Contains(2.7, match.JudgeScores);
        Assert.Contains(1.9, match.JudgeScores);
        Assert.Equal(4.8, match.JudgeScoresByJudge["Juge 1"]);
        Assert.Equal(2.7, match.JudgeScoresByJudge["Juge 2"]);
        Assert.Equal(1.9, match.JudgeScoresByJudge["Juge 3"]);
    }

    [Fact]
    public async Task TaoluStandings_TieBreaking_HigherIndividualMaxScoreWins()
    {
        // Règle 2 : À note totale égale, celui qui a la note individuelle la plus haute gagne
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var comp = new CompetitionModel { Id = 1, Name = "Taolu Cup", Type = CompetitionType.Taolu };
        var matches = new List<MatchItem>
        {
            new MatchItem
            {
                Id = 1,
                Category = "Taolu NanDao",
                Type = RoundType.Order,
                RedCompetitor = "Athlète Régulier",
                FinalScore = 9.50,
                JudgeScores = new List<double> { 9.50, 9.50, 9.50 }, // Max = 9.50
                IsFinished = true,
                Winner = "Athlète Régulier"
            },
            new MatchItem
            {
                Id = 2,
                Category = "Taolu NanDao",
                Type = RoundType.Order,
                RedCompetitor = "Athlète Pic",
                FinalScore = 9.50,
                JudgeScores = new List<double> { 9.60, 9.50, 9.40 }, // Max = 9.60
                IsFinished = true,
                Winner = "Athlète Pic"
            }
        };

        await session.LoadCompetitionAsync(comp, matches);
        var report = session.GetCategoryHistory("Taolu NanDao");

        Assert.NotNull(report);
        Assert.Equal(2, report.Standings.Count);

        // Athlète Pic a un max de 9.60 > 9.50 -> 1er
        Assert.Equal("Athlète Pic", report.Standings[0].CompetitorName);
        Assert.Equal(1, report.Standings[0].Rank);

        // Athlète Régulier -> 2e
        Assert.Equal("Athlète Régulier", report.Standings[1].CompetitorName);
        Assert.Equal(2, report.Standings[1].Rank);
    }

    [Fact]
    public async Task TaoluStandings_TieBreaking_HigherIndividualMinScoreWins_WhenMaxAreEqual()
    {
        // Règle 3 : À note totale égale ET note max égale, celui qui a la note la moins basse (min le plus élevé) gagne
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var comp = new CompetitionModel { Id = 1, Name = "Taolu Cup", Type = CompetitionType.Taolu };
        var matches = new List<MatchItem>
        {
            new MatchItem
            {
                Id = 1,
                Category = "Taolu Taiji",
                Type = RoundType.Order,
                RedCompetitor = "Athlète Min Plus Bas",
                FinalScore = 9.50,
                JudgeScores = new List<double> { 9.70, 9.60, 9.20 }, // Max = 9.70, Min = 9.20
                IsFinished = true,
                Winner = "Athlète Min Plus Bas"
            },
            new MatchItem
            {
                Id = 2,
                Category = "Taolu Taiji",
                Type = RoundType.Order,
                RedCompetitor = "Athlète Min Moins Bas",
                FinalScore = 9.50,
                JudgeScores = new List<double> { 9.70, 9.50, 9.30 }, // Max = 9.70, Min = 9.30
                IsFinished = true,
                Winner = "Athlète Min Moins Bas"
            }
        };

        await session.LoadCompetitionAsync(comp, matches);
        var report = session.GetCategoryHistory("Taolu Taiji");

        Assert.NotNull(report);
        Assert.Equal(2, report.Standings.Count);

        // Min 9.30 > 9.20 -> Athlète Min Moins Bas est 1er
        Assert.Equal("Athlète Min Moins Bas", report.Standings[0].CompetitorName);
        Assert.Equal(1, report.Standings[0].Rank);

        Assert.Equal("Athlète Min Plus Bas", report.Standings[1].CompetitorName);
        Assert.Equal(2, report.Standings[1].Rank);
    }

    [Fact]
    public async Task TaoluStandings_TieBreaking_ExactTie_SharesSameRank()
    {
        // Règle 4 : Égalité totale (Total, Max et Min identiques) -> même place
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var comp = new CompetitionModel { Id = 1, Name = "Taolu Cup", Type = CompetitionType.Taolu };
        var matches = new List<MatchItem>
        {
            new MatchItem
            {
                Id = 1,
                Category = "Taolu Gunshu",
                Type = RoundType.Order,
                RedCompetitor = "Jumeau 1",
                FinalScore = 9.50,
                JudgeScores = new List<double> { 9.60, 9.50, 9.40 },
                IsFinished = true,
                Winner = "Jumeau 1"
            },
            new MatchItem
            {
                Id = 2,
                Category = "Taolu Gunshu",
                Type = RoundType.Order,
                RedCompetitor = "Jumeau 2",
                FinalScore = 9.50,
                JudgeScores = new List<double> { 9.60, 9.50, 9.40 },
                IsFinished = true,
                Winner = "Jumeau 2"
            },
            new MatchItem
            {
                Id = 3,
                Category = "Taolu Gunshu",
                Type = RoundType.Order,
                RedCompetitor = "Troisième",
                FinalScore = 9.20,
                JudgeScores = new List<double> { 9.30, 9.20, 9.10 },
                IsFinished = true,
                Winner = "Troisième"
            }
        };

        await session.LoadCompetitionAsync(comp, matches);
        var report = session.GetCategoryHistory("Taolu Gunshu");

        Assert.NotNull(report);
        Assert.Equal(3, report.Standings.Count);

        // Les deux premiers partagent la 1ère place
        Assert.Equal(1, report.Standings[0].Rank);
        Assert.Equal(1, report.Standings[1].Rank);

        // Le troisième athlète est 3e (saut du rang 2 classique en compétition)
        Assert.Equal(3, report.Standings[2].Rank);
        Assert.Equal("Troisième", report.Standings[2].CompetitorName);
    }

    [Fact]
    public async Task ResultsViewModel_TaoluRanking_TieBreakingAndSharedRanks()
    {
        var logger = Substitute.For<ILogger<ResultsViewModel>>();
        var resourceProvider = new ResourceProvider();
        var popupService = Substitute.For<IPopupService>();
        var compRepo = Substitute.For<IRepository<CompetitionModel>>();
        var catRepo = Substitute.For<IRepository<CategoryModel>>();
        var drawRepo = Substitute.For<IRepository<DrawModel>>();
        var refRepo = Substitute.For<IRepository<RefereeDataModel>>();
        var excelService = Substitute.For<IExcelImportExportService>();
        var fileDownloadService = Substitute.For<IFileDownloadService>();

        var cat = new CategoryModel
        {
            Id = 1,
            RoundType = RoundType.Order,
            Type = CategoryType.Taolu
        };

        var comp1 = new CompetitorModel { Id = 10, FirstName = "Athlète", LastName = "A" };
        var comp2 = new CompetitorModel { Id = 20, FirstName = "Athlète", LastName = "B" };
        var comp3 = new CompetitorModel { Id = 30, FirstName = "Athlète", LastName = "C" };

        var drawOrders = new List<DrawOrderModel>
        {
            new DrawOrderModel
            {
                Id = 1,
                CompetitorId = 10,
                Competitor = comp1,
                RoundResultsJson = "9.50",
                IsFinished = true
            },
            new DrawOrderModel
            {
                Id = 2,
                CompetitorId = 20,
                Competitor = comp2,
                RoundResultsJson = "9.50",
                IsFinished = true
            },
            new DrawOrderModel
            {
                Id = 3,
                CompetitorId = 30,
                Competitor = comp3,
                RoundResultsJson = "9.10",
                IsFinished = true
            }
        };

        var draw = new DrawModel
        {
            Id = 100,
            CategoryId = 1,
            DrawOrders = drawOrders
        };

        // Arbitres :
        // Athlète A : notes [9.50, 9.50, 9.50] -> max 9.50
        // Athlète B : notes [9.70, 9.50, 9.30] -> max 9.70
        // Athlète C : notes [9.10, 9.10, 9.10]
        var refDatas = new List<RefereeDataModel>
        {
            new RefereeDataModel { Date = DateTime.Now, DrawOrderId = 1, CompetitorId = 10, Score = 9.50, Referee = "J1" },
            new RefereeDataModel { Date = DateTime.Now, DrawOrderId = 1, CompetitorId = 10, Score = 9.50, Referee = "J2" },
            new RefereeDataModel { Date = DateTime.Now, DrawOrderId = 1, CompetitorId = 10, Score = 9.50, Referee = "J3" },

            new RefereeDataModel { Date = DateTime.Now, DrawOrderId = 2, CompetitorId = 20, Score = 9.70, Referee = "J1" },
            new RefereeDataModel { Date = DateTime.Now, DrawOrderId = 2, CompetitorId = 20, Score = 9.50, Referee = "J2" },
            new RefereeDataModel { Date = DateTime.Now, DrawOrderId = 2, CompetitorId = 20, Score = 9.30, Referee = "J3" },

            new RefereeDataModel { Date = DateTime.Now, DrawOrderId = 3, CompetitorId = 30, Score = 9.10, Referee = "J1" }
        };

        refRepo.GetAllAsync().Returns(refDatas);
        drawRepo.GetAllAsync(Arg.Any<string[]>()).Returns(new List<DrawModel> { draw });

        var vm = new ResultsViewModel(
            logger,
            resourceProvider,
            popupService,
            compRepo,
            catRepo,
            drawRepo,
            refRepo,
            excelService,
            fileDownloadService);

        vm.SelectedCategory = cat;
        await vm.LoadResultsForSelectedCategoryAsync();

        Assert.Equal(3, vm.Results.Count);

        // Athlète B est 1er grâce à son max individuel (9.70 > 9.50)
        Assert.Equal("Athlète B", vm.Results[0].CompetitorName);
        Assert.Equal(1, vm.Results[0].Position);
        Assert.Equal("1er", vm.Results[0].RankDisplay);
        Assert.Equal("🥇", vm.Results[0].MedalIcon);

        // Athlète A est 2e
        Assert.Equal("Athlète A", vm.Results[1].CompetitorName);
        Assert.Equal(2, vm.Results[1].Position);
        Assert.Equal("2e", vm.Results[1].RankDisplay);
        Assert.Equal("🥈", vm.Results[1].MedalIcon);

        // Athlète C est 3e
        Assert.Equal("Athlète C", vm.Results[2].CompetitorName);
        Assert.Equal(3, vm.Results[2].Position);
        Assert.Equal("3e", vm.Results[2].RankDisplay);
        Assert.Equal("🥉", vm.Results[2].MedalIcon);
    }

    [Fact]
    public async Task NextMatchAsync_InTaoluMode_AutoFinalizesPreviousMatch_AndSavesScores()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var comp = new CompetitionModel { Id = 1, Name = "Taolu Cup", Type = CompetitionType.Taolu };
        var matches = new List<MatchItem>
        {
            new MatchItem { Id = 1, Category = "Taolu NanDao", Type = RoundType.Order, RedCompetitor = "Passage 1" },
            new MatchItem { Id = 2, Category = "Taolu NanDao", Type = RoundType.Order, RedCompetitor = "Passage 2" }
        };

        var draw = new DrawModel
        {
            Id = 1,
            CategoryId = 1,
            DrawOrders = new List<DrawOrderModel>
            {
                new DrawOrderModel { Id = 1, Order = 1 },
                new DrawOrderModel { Id = 2, Order = 2 }
            }
        };
        var cat = new CategoryModel { Id = 1, RoundType = RoundType.Order, Type = CategoryType.Taolu, Draw = draw };
        comp.Categories = new List<CategoryModel> { cat };

        await session.LoadCompetitionAsync(comp, matches, new[] { cat });

        // Simuler des notes pour le match 1
        session.Judges[0].Score = 4.70; // Groupe A
        session.Judges[1].Score = 4.80; // Groupe A
        session.Judges[3].Score = 2.80; // Groupe B
        session.Judges[6].Score = 1.90; // Groupe C

        var match1 = session.CurrentMatch;
        Assert.NotNull(match1);
        Assert.Equal(1, match1.Id);
        Assert.False(match1.IsFinished);

        // Passer au match suivant
        await session.NextMatchAsync();

        // 1. Le match 1 doit être considéré comme fini
        Assert.True(match1.IsFinished);
        Assert.True(match1.FinalScore > 0);
        Assert.Equal(session.Judges.Count, match1.JudgeScores.Count);
        Assert.Contains(4.70, match1.JudgeScores);
        Assert.True(match1.JudgeScoresByJudge.ContainsKey(session.Judges[0].Name));

        // 2. Le DrawOrder sous-jacent doit être mis à jour
        Assert.True(draw.DrawOrders[0].IsFinished);
        Assert.False(string.IsNullOrEmpty(draw.DrawOrders[0].RoundResultsJson));

        // 3. Le match courant est maintenant le match 2
        Assert.NotNull(session.CurrentMatch);
        Assert.Equal(2, session.CurrentMatch.Id);
        Assert.False(session.CurrentMatch.IsFinished);

        // 4. Passer la fin des matchs : le match 2 doit aussi être finalisé
        session.Judges[0].Score = 4.90;
        var match2 = session.CurrentMatch;
        await session.NextMatchAsync();

        Assert.True(match2.IsFinished);
        Assert.Null(session.CurrentMatch); // Plus de match actif
    }

    [Fact]
    public async Task ExportResults_InTaoluMode_ContainsFinishedMatchesAndJudgeScores()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var comp = new CompetitionModel { Id = 1, Name = "Taolu Cup Export", Type = CompetitionType.Taolu };
        var matches = new List<MatchItem>
        {
            new MatchItem { Id = 1, Category = "Taolu Changquan", Type = RoundType.Order, RedCompetitor = "Athlète A" },
            new MatchItem { Id = 2, Category = "Taolu Changquan", Type = RoundType.Order, RedCompetitor = "Athlète B" }
        };

        await session.LoadCompetitionAsync(comp, matches);

        session.Judges[0].Score = 4.60;
        await session.NextMatchAsync(); // Finalise Athlète A et passe à B

        session.Judges[0].Score = 4.90;
        await session.NextMatchAsync(); // Finalise Athlète B et termine la compétition

        string tempFile = Path.Combine(Path.GetTempPath(), $"TestExportTaolu_{Guid.NewGuid():N}.xlsx");
        try
        {
            string exportedPath = session.ExportResults(tempFile);
            Assert.True(File.Exists(exportedPath));

            // Vérifier via GetCategoryHistory que le rapport contient les deux matchs terminés avec notes
            var report = session.GetCategoryHistory("Taolu Changquan");
            Assert.NotNull(report);
            Assert.Equal(2, report.Matches.Count);
            Assert.All(report.Matches, m => Assert.True(m.IsFinished));
            Assert.All(report.Matches, m => Assert.True(m.FinalScore > 0));
            Assert.All(report.Standings, s => Assert.True(s.HasFinished));
            Assert.All(report.Standings, s => Assert.NotEmpty(s.JudgeScores));
            Assert.Equal("Athlète B", report.CategoryWinner); // Athlète B a eu 4.90 > 4.60
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task TaoluMode_UnconnectedJudges_DoNotAppearInScoresNorExports()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var comp = new CompetitionModel { Id = 1, Name = "Taolu Only Connected", Type = CompetitionType.Taolu };
        var matches = new List<MatchItem>
        {
            new MatchItem { Id = 1, Category = "Taolu Changquan", Type = RoundType.Order, RedCompetitor = "Athlète Seul" }
        };

        await session.LoadCompetitionAsync(comp, matches);

        // Par défaut, SetupJudges(Taolu) crée 9 juges (A1..A3, B1..B3, C1..C3)
        Assert.Equal(9, session.Judges.Count);

        // Seuls 2 juges se connectent réellement : JUGE A1 et JUGE B1
        var judgeA1 = session.Judges.First(j => j.Name == "JUGE A1");
        var judgeB1 = session.Judges.First(j => j.Name == "JUGE B1");
        judgeA1.IsConnected = true;
        judgeA1.Score = 4.85;
        judgeB1.IsConnected = true;
        judgeB1.Score = 2.75;

        // Finaliser le passage
        await session.SetTaoluScoreAsync();

        var match = session.CurrentMatch;
        Assert.NotNull(match);
        Assert.True(match.IsFinished);

        // Les juges non connectés (A2, A3, B2, B3, C1, C2, C3) ne doivent PAS apparaître dans les notes
        Assert.Equal(2, match.JudgeScores.Count);
        Assert.Equal(2, match.JudgeScoresByJudge.Count);
        Assert.True(match.JudgeScoresByJudge.ContainsKey("JUGE A1"));
        Assert.True(match.JudgeScoresByJudge.ContainsKey("JUGE B1"));
        Assert.False(match.JudgeScoresByJudge.ContainsKey("JUGE A2"));
        Assert.False(match.JudgeScoresByJudge.ContainsKey("JUGE C1"));

        // La note finale doit être basée uniquement sur les groupes avec juges connectés (4.85 + 2.75 = 7.60)
        Assert.Equal(7.60, match.FinalScore);

        // Dans le rapport de catégorie
        var report = session.GetCategoryHistory("Taolu Changquan");
        Assert.NotNull(report);
        Assert.Single(report.Standings);
        Assert.Equal(2, report.Standings[0].JudgeScores.Count);
        Assert.Equal(2, report.Standings[0].JudgeScoresByJudge.Count);
    }

    [Fact]
    public async Task SandaMode_UnconnectedJudges_DoNotAppearInVotesNorScores()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var comp = new CompetitionModel { Id = 1, Name = "Sanda Tournament", Type = CompetitionType.Sanda };
        var matches = new List<MatchItem>
        {
            new MatchItem
            {
                Id = 1,
                Category = "Sanda -70kg",
                Type = RoundType.Knockouts,
                RedCompetitor = "Combattant Rouge",
                BlueCompetitor = "Combattant Bleu"
            }
        };

        await session.LoadCompetitionAsync(comp, matches);

        // 5 juges configurés par défaut
        Assert.Equal(5, session.Judges.Count);

        // Seuls 3 juges se connectent (JUGE 1, JUGE 2, JUGE 3)
        session.Judges[0].IsConnected = true; // JUGE 1 vote Rouge (3 pts)
        session.Judges[0].RedPoints = 3;
        session.Judges[1].IsConnected = true; // JUGE 2 vote Rouge (2 pts)
        session.Judges[1].RedPoints = 2;
        session.Judges[2].IsConnected = true; // JUGE 3 vote Bleu (4 pts)
        session.Judges[2].BluePoints = 4;

        // Les juges 4 et 5 ne sont PAS connectés et ont 0-0
        Assert.False(session.Judges[3].IsConnected);
        Assert.False(session.Judges[4].IsConnected);

        var eval = session.EvaluateSandaScores();
        // 2 votes Rouge contre 1 vote Bleu. Les juges 4 et 5 ne doivent PAS ajouter de drawVotes !
        Assert.Equal(2, eval.RedVotes);
        Assert.Equal(1, eval.BlueVotes);
        Assert.Equal(0, eval.Summary.Contains("5 juges") ? 1 : 0); // La décision est prise sur 3 juges

        // Validation automatique du vainqueur
        await session.SetAutoWinnerAsync();
        var match = session.CurrentMatch;
        Assert.NotNull(match);
        Assert.True(match.IsFinished);
        Assert.Equal("Combattant Rouge", match.Winner);
        Assert.Equal(5, match.RedScore); // 3 + 2 = 5
        Assert.Equal(4, match.BlueScore); // 4
    }
}
