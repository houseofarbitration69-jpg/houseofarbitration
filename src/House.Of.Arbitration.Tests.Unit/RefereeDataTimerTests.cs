using ClosedXML.Excel;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services;
using House.Of.Arbitration.Services.Abstractions;
using NSubstitute;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class RefereeDataTimerTests
{
    [Fact]
    public void RefereeDataModel_MatchTimer_FormatsCorrectlyAsMinutesAndSeconds()
    {
        var data = new RefereeDataModel
        {
            Date = DateTime.Now,
            MatchTimer = TimeSpan.FromSeconds(30),
            Referee = "JUGE 1",
            Score = 1,
            Round = 1
        };

        Assert.Equal(TimeSpan.FromSeconds(30), data.MatchTimer);
        Assert.Equal("00:30", data.MatchTimerDisplay);

        var data2 = new RefereeDataModel
        {
            Date = DateTime.Now,
            MatchTimer = TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(45)),
            Referee = "JUGE 2",
            Score = 2,
            Round = 2
        };

        Assert.Equal("01:45", data2.MatchTimerDisplay);

        var dataNull = new RefereeDataModel
        {
            Date = DateTime.Now,
            MatchTimer = null,
            Referee = "JUGE 3",
            Score = -1,
            Round = 1
        };

        Assert.Equal("00:00", dataNull.MatchTimerDisplay);
    }

    [Fact]
    public async Task ExportRefereeDataAsync_IncludesMatchTimerColumnWithFormattedElapsedValue()
    {
        var compRepo = Substitute.For<IRepository<CompetitionModel>>();
        var catRepo = Substitute.For<IRepository<CategoryModel>>();
        var comporRepo = Substitute.For<IRepository<CompetitorModel>>();
        var compCatRepo = Substitute.For<IRepository<CompetitorCategoryModel>>();
        var drawRepo = Substitute.For<IRepository<DrawModel>>();

        var service = new ExcelImportExportService(compRepo, catRepo, comporRepo, compCatRepo, drawRepo);

        var categories = new List<CategoryRefereeExportData>
        {
            new()
            {
                CategoryName = "Sanda Seniors Hommes -70kg",
                Rows = new List<RefereeScoreExportRow>
                {
                    new()
                    {
                        Date = new DateTime(2026, 11, 14, 14, 30, 0),
                        MatchTimer = TimeSpan.FromSeconds(30), // Point après 30s du début du match
                        Referee = "JUGE 1",
                        CompetitorLastName = "Dupont",
                        CompetitorFirstName = "Jean",
                        Score = 1,
                        Round = 1,
                        IsCorrection = false,
                        MatchType = "Knockout",
                        MatchId = 1,
                        GlobalOrder = 1
                    }
                }
            }
        };

        using var stream = await service.ExportRefereeDataAsync("Championnat de France", DateTime.Today, categories);
        Assert.NotNull(stream);

        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet("Sanda Seniors Hommes -70kg");
        Assert.NotNull(ws);

        // En-tête ligne 5 : colonne 3 = Chrono
        Assert.Equal("Chrono", ws.Cell(5, 3).GetString());

        // Ligne 6 : données
        Assert.Equal("00:30", ws.Cell(6, 3).GetString());
        Assert.Equal("JUGE 1", ws.Cell(6, 4).GetString());
        Assert.Equal("Dupont", ws.Cell(6, 5).GetString());
        Assert.Equal(1.0, ws.Cell(6, 7).GetDouble());
    }
}
