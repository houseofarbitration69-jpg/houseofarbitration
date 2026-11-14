using House.Of.Arbitration.Models;
using House.Of.Arbitration.Server.Cli;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class ServerCliCategoryHistoryTests
{
    [Fact]
    public void GetCategoriesSummary_DefaultMatches_ReturnsGroupedCategoriesWithProgress()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var summaries = session.GetCategoriesSummary();

        Assert.NotNull(summaries);
        Assert.Equal(4, summaries.Count);

        var first = summaries[0];
        Assert.Equal("Sanda -65kg", first.Name);
        Assert.Equal(RoundType.Knockouts, first.RoundType);
        Assert.Equal(1, first.TotalMatches);
        Assert.Equal(0, first.CompletedMatches);
        Assert.Equal("À venir", first.StatusText);
        Assert.False(first.IsFinished);
    }

    [Fact]
    public async Task GetCategoryHistory_SandaKnockouts_CalculatesChampionAndStandings()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var comp = new CompetitionModel { Id = 1, Name = "Championnat", Type = CompetitionType.Sanda };
        var matches = new List<MatchItem>
        {
            new MatchItem
            {
                Id = 1,
                Category = "Sanda Senior -75kg",
                Type = RoundType.Knockouts,
                RedCompetitor = "Dupont",
                BlueCompetitor = "Martin",
                RedScore = 2,
                BlueScore = 0,
                IsFinished = true,
                Winner = "Dupont",
                WinnerSide = Constants.Side.Red,
                DecisionReason = "2 rounds à 0"
            },
            new MatchItem
            {
                Id = 2,
                Category = "Sanda Senior -75kg",
                Type = RoundType.Knockouts,
                RedCompetitor = "Zhang",
                BlueCompetitor = "Bernard",
                RedScore = 0,
                BlueScore = 2,
                IsFinished = true,
                Winner = "Bernard",
                WinnerSide = Constants.Side.Blue,
                DecisionReason = "KO Round 2"
            },
            new MatchItem
            {
                Id = 3,
                Category = "Sanda Senior -75kg",
                Type = RoundType.Knockouts,
                RedCompetitor = "Dupont",
                BlueCompetitor = "Bernard",
                RedScore = 2,
                BlueScore = 1,
                IsFinished = true,
                Winner = "Dupont",
                WinnerSide = Constants.Side.Red,
                DecisionReason = "Victoire Finale"
            }
        };

        await session.LoadCompetitionAsync(comp, matches);

        var report = session.GetCategoryHistory("Sanda Senior -75kg");

        Assert.NotNull(report);
        Assert.True(report.Summary.IsFinished);
        Assert.Equal("Dupont", report.CategoryWinner);
        Assert.Equal(3, report.Matches.Count);

        // Vérification du classement
        Assert.Equal(4, report.Standings.Count);
        var champion = report.Standings[0];
        Assert.Equal("Dupont", champion.CompetitorName);
        Assert.Equal(2, champion.Wins);
        Assert.Contains("CHAMPION", champion.Status);

        var finaliste = report.Standings[1];
        Assert.Equal("Bernard", finaliste.CompetitorName);
        Assert.Contains("FINALISTE", finaliste.Status);
    }

    [Fact]
    public async Task GetCategoryHistory_SandaPools_CalculatesWinsAndPointsDifference()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var comp = new CompetitionModel { Id = 1, Name = "Coupe de Poules", Type = CompetitionType.Sanda };
        var matches = new List<MatchItem>
        {
            // Match 1 : Combattant A bat Combattant B (5 - 2)
            new MatchItem
            {
                Id = 1,
                Category = "Poule A -60kg",
                Type = RoundType.Pools,
                RedCompetitor = "Combattant A",
                BlueCompetitor = "Combattant B",
                RedScore = 5,
                BlueScore = 2,
                IsFinished = true,
                Winner = "Combattant A",
                WinnerSide = Constants.Side.Red
            },
            // Match 2 : Combattant B bat Combattant C (4 - 1)
            new MatchItem
            {
                Id = 2,
                Category = "Poule A -60kg",
                Type = RoundType.Pools,
                RedCompetitor = "Combattant B",
                BlueCompetitor = "Combattant C",
                RedScore = 4,
                BlueScore = 1,
                IsFinished = true,
                Winner = "Combattant B",
                WinnerSide = Constants.Side.Red
            },
            // Match 3 : Combattant A bat Combattant C (6 - 0)
            new MatchItem
            {
                Id = 3,
                Category = "Poule A -60kg",
                Type = RoundType.Pools,
                RedCompetitor = "Combattant A",
                BlueCompetitor = "Combattant C",
                RedScore = 6,
                BlueScore = 0,
                IsFinished = true,
                Winner = "Combattant A",
                WinnerSide = Constants.Side.Red
            }
        };

        await session.LoadCompetitionAsync(comp, matches);

        var report = session.GetCategoryHistory("Poule A -60kg");

        Assert.NotNull(report);
        Assert.True(report.Summary.IsFinished);
        Assert.Equal("Combattant A", report.CategoryWinner);

        // Standings
        Assert.Equal(3, report.Standings.Count);
        Assert.Equal("Combattant A", report.Standings[0].CompetitorName);
        Assert.Equal(2, report.Standings[0].Wins);
        Assert.Equal(11, report.Standings[0].PointsFor);
        Assert.Equal(2, report.Standings[0].PointsAgainst);
        Assert.Equal(9, report.Standings[0].PointDifference);

        Assert.Equal("Combattant B", report.Standings[1].CompetitorName);
        Assert.Equal(1, report.Standings[1].Wins);
        Assert.Equal(1, report.Standings[1].Losses);

        Assert.Equal("Combattant C", report.Standings[2].CompetitorName);
        Assert.Equal(0, report.Standings[2].Wins);
        Assert.Equal(2, report.Standings[2].Losses);
    }

    [Fact]
    public async Task GetCategoryHistory_TaoluOrder_RanksByFinalScoreDescending()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var comp = new CompetitionModel { Id = 1, Name = "Taolu Cup", Type = CompetitionType.Taolu };
        var matches = new List<MatchItem>
        {
            new MatchItem
            {
                Id = 1,
                Category = "Taolu Changquan Senior",
                Type = RoundType.Order,
                RedCompetitor = "Athlète A",
                FinalScore = 9.40,
                IsFinished = true,
                Winner = "Athlète A"
            },
            new MatchItem
            {
                Id = 2,
                Category = "Taolu Changquan Senior",
                Type = RoundType.Order,
                RedCompetitor = "Athlète B",
                FinalScore = 9.80,
                IsFinished = true,
                Winner = "Athlète B"
            },
            new MatchItem
            {
                Id = 3,
                Category = "Taolu Changquan Senior",
                Type = RoundType.Order,
                RedCompetitor = "Athlète C",
                FinalScore = 9.65,
                IsFinished = true,
                Winner = "Athlète C"
            },
            new MatchItem
            {
                Id = 4,
                Category = "Taolu Changquan Senior",
                Type = RoundType.Order,
                RedCompetitor = "Athlète D",
                FinalScore = 0.0,
                IsFinished = false
            }
        };

        await session.LoadCompetitionAsync(comp, matches);

        var report = session.GetCategoryHistory("Changquan");

        Assert.NotNull(report);
        Assert.Equal(4, report.Matches.Count);
        Assert.Equal(3, report.Summary.CompletedMatches);
        Assert.Equal("Athlète B", report.CategoryWinner);

        // Podium ordonné : B (9.80), C (9.65), A (9.40), D (en attente)
        Assert.Equal(4, report.Standings.Count);
        Assert.Equal("Athlète B", report.Standings[0].CompetitorName);
        Assert.Equal(9.80, report.Standings[0].Score);
        Assert.Equal(1, report.Standings[0].Rank);

        Assert.Equal("Athlète C", report.Standings[1].CompetitorName);
        Assert.Equal(9.65, report.Standings[1].Score);
        Assert.Equal(2, report.Standings[1].Rank);

        Assert.Equal("Athlète A", report.Standings[2].CompetitorName);
        Assert.Equal(9.40, report.Standings[2].Score);
        Assert.Equal(3, report.Standings[2].Rank);

        Assert.Equal("Athlète D", report.Standings[3].CompetitorName);
        Assert.False(report.Standings[3].HasFinished);
    }

    [Fact]
    public void GetCategoryHistory_FindsByIndexOrPartialNameOrCurrentMatch()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        // 1. Sans argument -> prend la catégorie du match courant ("Sanda -65kg")
        var byDefault = session.GetCategoryHistory();
        Assert.NotNull(byDefault);
        Assert.Equal("Sanda -65kg", byDefault.Summary.Name);

        // 2. Par index numérique 1-based (ex: 2 -> "Sanda -70kg")
        var byIndex = session.GetCategoryHistory("2");
        Assert.NotNull(byIndex);
        Assert.Equal("Sanda -70kg", byIndex.Summary.Name);

        // 3. Par nom partiel insensible à la casse
        var byPartial = session.GetCategoryHistory("nanquan");
        Assert.NotNull(byPartial);
        Assert.Equal("Taolu Nanquan", byPartial.Summary.Name);

        // 4. Catégorie inexistante
        var notFound = session.GetCategoryHistory("Inexistante 999");
        Assert.Null(notFound);
    }
}
