using ClosedXML.Excel;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Server.Cli;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class ServerCliExcelExportTests
{
    [Fact]
    public async Task ExportResults_GeneratesValidExcelFileWithSummaryAndCategorySheets()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var comp = new CompetitionModel
        {
            Id = 1,
            Name = "Championnat National 2026",
            Date = new DateTime(2026, 4, 15),
            Type = CompetitionType.Sanda
        };

        var matches = new List<MatchItem>
        {
            new MatchItem
            {
                Id = 1,
                Category = "Sanda Senior -70kg",
                Type = RoundType.Knockouts,
                RedCompetitor = "Dupont",
                BlueCompetitor = "Martin",
                RedScore = 2,
                BlueScore = 0,
                IsFinished = true,
                Winner = "Dupont",
                WinnerSide = Constants.Side.Red,
                DecisionReason = "2-0"
            },
            new MatchItem
            {
                Id = 2,
                Category = "Sanda Senior -70kg",
                Type = RoundType.Knockouts,
                RedCompetitor = "Zhang",
                BlueCompetitor = "Bernard",
                RedScore = 0,
                BlueScore = 2,
                IsFinished = true,
                Winner = "Bernard",
                WinnerSide = Constants.Side.Blue,
                DecisionReason = "0-2"
            },
            new MatchItem
            {
                Id = 3,
                Category = "Taolu Changquan",
                Type = RoundType.Order,
                RedCompetitor = "Leclerc",
                BlueCompetitor = "",
                FinalScore = 9.45,
                IsFinished = true,
                Winner = "Leclerc"
            }
        };

        await session.LoadCompetitionAsync(comp, matches, new List<CategoryModel>());

        string tempFile = Path.Combine(Path.GetTempPath(), $"test_results_{Guid.NewGuid():N}.xlsx");

        try
        {
            string generatedPath = session.ExportResults(tempFile);

            Assert.Equal(Path.GetFullPath(tempFile), generatedPath);
            Assert.True(File.Exists(generatedPath));

            using var workbook = new XLWorkbook(generatedPath);

            // Vérifier l'onglet Résumé
            Assert.True(workbook.Worksheets.Contains("Résumé"));
            var wsSummary = workbook.Worksheet("Résumé");
            Assert.Equal("Championnat National 2026", wsSummary.Cell(1, 1).GetString());
            Assert.Contains("Sanda", wsSummary.Cell(2, 1).GetString());

            // En-têtes du résumé
            Assert.Equal("Catégorie", wsSummary.Cell(4, 1).GetString());
            Assert.Equal("Type de tournoi", wsSummary.Cell(4, 2).GetString());
            Assert.Contains("1er", wsSummary.Cell(4, 3).GetString());

            // Les 2 catégories doivent être listées dans le résumé
            Assert.True(wsSummary.RowsUsed().Count() >= 5);
            Assert.Equal("Sanda Senior -70kg", wsSummary.Cell(5, 1).GetString());
            Assert.Equal("Taolu Changquan", wsSummary.Cell(6, 1).GetString());

            // Vérifier que les onglets de catégories existent
            Assert.True(workbook.Worksheets.Count >= 3);
            var wsSanda = workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("Sanda"));
            Assert.NotNull(wsSanda);

            // Vérifier les classements dans l'onglet Sanda
            Assert.True(wsSanda.RowsUsed().Count() >= 5);
            Assert.Equal("Rang", wsSanda.Cell(6, 1).GetString());

            var wsTaolu = workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("Taolu"));
            Assert.NotNull(wsTaolu);
            Assert.Equal("Rang", wsTaolu.Cell(6, 1).GetString());
            Assert.Equal("Leclerc", wsTaolu.Cell(7, 3).GetString());
            Assert.Equal(9.45, wsTaolu.Cell(7, 4).GetDouble(), 2);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    [Fact]
    public async Task ExportJudgeScores_GeneratesValidExcelFileWithScoresAndDetails()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        var comp = new CompetitionModel
        {
            Id = 1,
            Name = "Open International Wushu",
            Date = new DateTime(2026, 5, 20),
            Type = CompetitionType.Sanda
        };

        var matches = new List<MatchItem>
        {
            new MatchItem
            {
                Id = 10,
                Category = "Sanda Senior -75kg",
                Type = RoundType.Knockouts,
                RedCompetitor = "Dupont",
                BlueCompetitor = "Martin"
            }
        };

        await session.LoadCompetitionAsync(comp, matches, new List<CategoryModel>());

        // Enregistrer des notes de juges
        var judge1 = new JudgeModel { Name = "Juge 1", Number = 1, RedPoints = 2, BluePoints = 1 };
        var judge2 = new JudgeModel { Name = "Juge 2", Number = 2, RedPoints = 1, BluePoints = 0 };

        session.RecordJudgeScore(judge1, Constants.Side.Red, 2.0, 2.0, null, "Point Rouge", "Coup de pied corps");
        session.RecordJudgeScore(judge1, Constants.Side.Blue, 1.0, 1.0, null, "Point Bleu", "Coup de poing");
        session.RecordJudgeScore(judge2, Constants.Side.Red, 1.0, 1.0, null, "Point Rouge", "Frappe");

        string tempFile = Path.Combine(Path.GetTempPath(), $"test_scores_{Guid.NewGuid():N}.xlsx");

        try
        {
            string generatedPath = session.ExportJudgeScores(tempFile);

            Assert.Equal(Path.GetFullPath(tempFile), generatedPath);
            Assert.True(File.Exists(generatedPath));

            using var workbook = new XLWorkbook(generatedPath);

            // Vérifier l'onglet Résumé
            Assert.True(workbook.Worksheets.Contains("Résumé"));
            var wsSummary = workbook.Worksheet("Résumé");
            Assert.Contains("Notes des juges", wsSummary.Cell(1, 1).GetString());
            Assert.Contains("Open International Wushu", wsSummary.Cell(1, 1).GetString());

            // Vérifier la ligne de données dans le résumé
            Assert.Equal("Sanda Senior -75kg", wsSummary.Cell(5, 1).GetString());
            Assert.Equal(3, wsSummary.Cell(5, 2).GetDouble()); // 3 notes
            Assert.Equal(2, wsSummary.Cell(5, 3).GetDouble()); // 2 points rouge
            Assert.Equal(1, wsSummary.Cell(5, 4).GetDouble()); // 1 point bleu
            Assert.Equal(2, wsSummary.Cell(5, 6).GetDouble()); // 2 juges distincts

            // Vérifier l'onglet de catégorie
            var wsCat = workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("Sanda"));
            Assert.NotNull(wsCat);

            // En-têtes : Date, Heure, Chrono, Round, Juge, Côté, Action, Valeur, Score cumulé, Match, Détails
            Assert.Equal("Date", wsCat.Cell(5, 1).GetString());
            Assert.Equal("Heure", wsCat.Cell(5, 2).GetString());
            Assert.Equal("Chrono", wsCat.Cell(5, 3).GetString());
            Assert.Equal("Round", wsCat.Cell(5, 4).GetString());
            Assert.Equal("Juge / Arbitre", wsCat.Cell(5, 5).GetString());
            Assert.Equal("Côté / Athlète", wsCat.Cell(5, 6).GetString());
            Assert.Equal("Action", wsCat.Cell(5, 7).GetString());
            Assert.Equal("Valeur", wsCat.Cell(5, 8).GetString());
            Assert.Equal("Match", wsCat.Cell(5, 10).GetString());

            // Lignes de notes
            Assert.Equal(1, wsCat.Cell(6, 4).GetDouble()); // Round 1
            Assert.Equal("Juge 1", wsCat.Cell(6, 5).GetString());
            Assert.Equal("Rouge", wsCat.Cell(6, 6).GetString());
            Assert.Equal(2.0, wsCat.Cell(6, 8).GetDouble(), 2);
            Assert.Equal("Match #10", wsCat.Cell(6, 10).GetString());

            Assert.Equal("Juge 1", wsCat.Cell(7, 5).GetString());
            Assert.Equal("Bleu", wsCat.Cell(7, 6).GetString());
            Assert.Equal(1.0, wsCat.Cell(7, 8).GetDouble(), 2);

            Assert.Equal("Juge 2", wsCat.Cell(8, 5).GetString());
            Assert.Equal("Rouge", wsCat.Cell(8, 6).GetString());
            Assert.Equal(1.0, wsCat.Cell(8, 8).GetDouble(), 2);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    [Fact]
    public void ExportResults_NullFilePath_GeneratesFileWithAutoName()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        string? createdPath = null;
        try
        {
            createdPath = session.ExportResults();
            Assert.NotNull(createdPath);
            Assert.True(File.Exists(createdPath));
            Assert.EndsWith(".xlsx", createdPath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (!string.IsNullOrEmpty(createdPath) && File.Exists(createdPath))
            {
                try { File.Delete(createdPath); } catch { }
            }
        }
    }
}
