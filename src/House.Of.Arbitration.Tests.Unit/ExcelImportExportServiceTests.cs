using ClosedXML.Excel;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services;
using NSubstitute;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class ExcelImportExportServiceTests
{
    [Theory]
    [InlineData(CategoryType.Sanda, Genre.Men, 60, 65, "Seniors", "Sanda Seniors Hommes -65kg")]
    [InlineData(CategoryType.Sanda, Genre.Women, 0, 70, "Juniors", "Sanda Juniors Femmes -70kg")]
    [InlineData(CategoryType.Sanda, Genre.Men, 80, 0, "Seniors", "Sanda Seniors Hommes +80kg")]
    [InlineData(CategoryType.Taolu, Genre.Women, 0, 0, "Cadets", "Taolu Cadets Femmes")]
    [InlineData(CategoryType.TaoluModerneNord, Genre.Mixte, 0, 0, null, "Taolu Moderne Nord Mixte")]
    [InlineData(CategoryType.TaoluModerneSud, Genre.Men, 0, 0, "Seniors", "Taolu Moderne Sud Seniors Hommes")]
    [InlineData(CategoryType.SandaLight, Genre.Men, 55, 60, "Seniors", "Sanda Light Seniors Hommes -60kg")]
    [InlineData(CategoryType.SandaWushu, Genre.Women, 50, 55, "Juniors", "Sanda Wushu Juniors Femmes -55kg")]
    [InlineData(CategoryType.SandaTradi, Genre.Men, 70, 75, "Seniors", "Sanda Tradi Seniors Hommes -75kg")]
    public void BuildCategoryDisplayName_FormatsCorrectly(
        CategoryType type, Genre genre, double minWeight, double maxWeight, string? ageLabel, string expected)
    {
        var result = ExcelImportExportService.BuildCategoryDisplayName(type, genre, minWeight, maxWeight, ageLabel);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Taolu Moderne Nord", CategoryType.TaoluModerneNord)]
    [InlineData("TaoluModerneNord", CategoryType.TaoluModerneNord)]
    [InlineData("Taolu Moderne Sud", CategoryType.TaoluModerneSud)]
    [InlineData("Sanda Light", CategoryType.SandaLight)]
    [InlineData("SandaLight", CategoryType.SandaLight)]
    [InlineData("Sanda Wushu", CategoryType.SandaWushu)]
    [InlineData("Sanda Tradi", CategoryType.SandaTradi)]
    [InlineData("Taolu", CategoryType.Taolu)]
    [InlineData("Sanda", CategoryType.Sanda)]
    public void TryParseCategoryType_ParsesBothSpacedAndCamelCase(string input, CategoryType expected)
    {
        bool success = ExcelImportExportService.TryParseCategoryType(input, out var result);
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ExportTemplateAsync_GeneratesWorkbookWithValidDropdownAndFormula()
    {
        var compRepo = Substitute.For<IRepository<CompetitionModel>>();
        var catRepo = Substitute.For<IRepository<CategoryModel>>();
        var comporRepo = Substitute.For<IRepository<CompetitorModel>>();
        var compCatRepo = Substitute.For<IRepository<CompetitorCategoryModel>>();
        var drawRepo = Substitute.For<IRepository<DrawModel>>();

        var service = new ExcelImportExportService(compRepo, catRepo, comporRepo, compCatRepo, drawRepo);

        using var stream = await service.ExportTemplateAsync();
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        using var workbook = new XLWorkbook(stream);
        var wsCat = workbook.Worksheet("Catégories");
        var wsComp = workbook.Worksheet("Compétiteurs");

        // Verify category column G has auto header
        Assert.Equal("NomCategorie (auto)", wsCat.Cell(1, 7).GetString());
        Assert.True(wsCat.Cell(2, 7).HasFormula);

        // Verify competitors column H has "Catégorie" header and dropdown refers to ListeCategories
        Assert.Equal("Catégorie", wsComp.Cell(1, 8).GetString());
        Assert.Equal("Sanda Seniors Hommes -65kg", wsComp.Cell(2, 8).GetString());

        var validations = wsComp.DataValidations;
        Assert.NotEmpty(validations);
    }

    [Fact]
    public void TestDateCellParsing()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Test");

        // String "15/03/2000"
        ws.Cell(1, 1).Value = "15/03/2000";
        // DateTime object
        ws.Cell(2, 1).Value = new DateTime(2000, 3, 15);
        // String with timestamp like ClosedXML might render
        ws.Cell(3, 1).Value = "15/03/2000 00:00:00";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        using var wb2 = new XLWorkbook(ms);
        var ws2 = wb2.Worksheet("Test");

        bool ok1 = ExcelImportExportService.TryParseCellDate(ws2.Cell(1, 1), out var dt1);
        bool ok2 = ExcelImportExportService.TryParseCellDate(ws2.Cell(2, 1), out var dt2);
        bool ok3 = ExcelImportExportService.TryParseCellDate(ws2.Cell(3, 1), out var dt3);

        Assert.True(ok1);
        Assert.Equal(new DateTime(2000, 3, 15), dt1);

        Assert.True(ok2);
        Assert.Equal(new DateTime(2000, 3, 15), dt2);

        Assert.True(ok3);
        Assert.Equal(new DateTime(2000, 3, 15), dt3);
    }

    [Fact]
    public async Task ImportAsync_GeneratesDrawsForImportedCategories()
    {
        var compRepo = Substitute.For<IRepository<CompetitionModel>>();
        var catRepo = Substitute.For<IRepository<CategoryModel>>();
        var comporRepo = Substitute.For<IRepository<CompetitorModel>>();
        var compCatRepo = Substitute.For<IRepository<CompetitorCategoryModel>>();
        var drawRepo = Substitute.For<IRepository<DrawModel>>();

        compRepo.AddAsync(Arg.Any<CompetitionModel>()).Returns(call =>
        {
            var c = call.Arg<CompetitionModel>();
            c.Id = 1;
            return Task.FromResult<CompetitionModel?>(c);
        });

        catRepo.AddAsync(Arg.Any<CategoryModel>()).Returns(call =>
        {
            var cat = call.Arg<CategoryModel>();
            cat.Id = 10;
            return Task.FromResult<CategoryModel?>(cat);
        });

        int compIdCounter = 100;
        comporRepo.AddAsync(Arg.Any<CompetitorModel>()).Returns(call =>
        {
            var comp = call.Arg<CompetitorModel>();
            comp.Id = compIdCounter++;
            return Task.FromResult<CompetitorModel?>(comp);
        });

        compCatRepo.AddAsync(Arg.Any<CompetitorCategoryModel>()).Returns(call =>
            Task.FromResult<CompetitorCategoryModel?>(call.Arg<CompetitorCategoryModel>()));

        DrawModel? capturedDraw = null;
        drawRepo.AddAsync(Arg.Any<DrawModel>()).Returns(call =>
        {
            capturedDraw = call.Arg<DrawModel>();
            capturedDraw.Id = 50;
            return Task.FromResult<DrawModel?>(capturedDraw);
        });

        var service = new ExcelImportExportService(compRepo, catRepo, comporRepo, compCatRepo, drawRepo);

        // Build Excel in memory
        using var workbook = new XLWorkbook();
        var wsComp = workbook.Worksheets.Add("Compétition");
        wsComp.Cell(1, 1).Value = "Nom";
        wsComp.Cell(1, 2).Value = "Date (JJ/MM/AAAA)";
        wsComp.Cell(1, 3).Value = "Type";
        wsComp.Cell(2, 1).Value = "Compétition Test";
        wsComp.Cell(2, 2).Value = "15/03/2026";
        wsComp.Cell(2, 3).Value = "Sanda";

        var wsCat = workbook.Worksheets.Add("Catégories");
        wsCat.Cell(1, 1).Value = "TypeCategorie";
        wsCat.Cell(1, 2).Value = "Genre";
        wsCat.Cell(1, 3).Value = "PoidsMin";
        wsCat.Cell(1, 4).Value = "PoidsMax";
        wsCat.Cell(1, 5).Value = "TrancheAge";
        wsCat.Cell(1, 6).Value = "TypeRound";
        wsCat.Cell(1, 7).Value = "NomCategorie (auto)";

        wsCat.Cell(2, 1).Value = "Sanda";
        wsCat.Cell(2, 2).Value = "Men";
        wsCat.Cell(2, 3).Value = "60";
        wsCat.Cell(2, 4).Value = "65";
        wsCat.Cell(2, 5).Value = "Seniors";
        wsCat.Cell(2, 6).Value = "Knockouts";
        wsCat.Cell(2, 7).Value = "Sanda Seniors Hommes -65kg";

        var wsCompetitors = workbook.Worksheets.Add("Compétiteurs");
        wsCompetitors.Cell(1, 1).Value = "Prénom";
        wsCompetitors.Cell(1, 2).Value = "Nom";
        wsCompetitors.Cell(1, 3).Value = "Genre";
        wsCompetitors.Cell(1, 4).Value = "DateNaissance (JJ/MM/AAAA)";
        wsCompetitors.Cell(1, 5).Value = "Club";
        wsCompetitors.Cell(1, 6).Value = "PaysISO";
        wsCompetitors.Cell(1, 7).Value = "Poids (kg)";
        wsCompetitors.Cell(1, 8).Value = "Catégorie";

        wsCompetitors.Cell(2, 1).Value = "Jean";
        wsCompetitors.Cell(2, 2).Value = "Dupont";
        wsCompetitors.Cell(2, 3).Value = "Men";
        wsCompetitors.Cell(2, 4).Value = "15/03/2000";
        wsCompetitors.Cell(2, 5).Value = "Club Paris";
        wsCompetitors.Cell(2, 6).Value = "FRA";
        wsCompetitors.Cell(2, 7).Value = "63";
        wsCompetitors.Cell(2, 8).Value = "Sanda Seniors Hommes -65kg";

        wsCompetitors.Cell(3, 1).Value = "Pierre";
        wsCompetitors.Cell(3, 2).Value = "Martin";
        wsCompetitors.Cell(3, 3).Value = "Men";
        wsCompetitors.Cell(3, 4).Value = "10/05/1998";
        wsCompetitors.Cell(3, 5).Value = "Club Lyon";
        wsCompetitors.Cell(3, 6).Value = "FRA";
        wsCompetitors.Cell(3, 7).Value = "64";
        wsCompetitors.Cell(3, 8).Value = "Sanda Seniors Hommes -65kg";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var result = await service.ImportAsync(ms);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Equal(1, result.ImportedCategories);
        Assert.Equal(2, result.ImportedCompetitors);
        Assert.Equal(1, result.ImportedDraws);
        Assert.NotNull(capturedDraw);
        Assert.Equal(10, capturedDraw.CategoryId);
        Assert.NotNull(capturedDraw.DrawKnockouts);
        Assert.NotEmpty(capturedDraw.DrawKnockouts);
    }

    [Fact]
    public async Task ImportAsync_TaoluCompetition_AcceptsEmptyWeight()
    {
        var compRepo = Substitute.For<IRepository<CompetitionModel>>();
        var catRepo = Substitute.For<IRepository<CategoryModel>>();
        var comporRepo = Substitute.For<IRepository<CompetitorModel>>();
        var compCatRepo = Substitute.For<IRepository<CompetitorCategoryModel>>();
        var drawRepo = Substitute.For<IRepository<DrawModel>>();

        compRepo.AddAsync(Arg.Any<CompetitionModel>()).Returns(call =>
        {
            var c = call.Arg<CompetitionModel>();
            c.Id = 1;
            return Task.FromResult<CompetitionModel?>(c);
        });

        catRepo.AddAsync(Arg.Any<CategoryModel>()).Returns(call =>
        {
            var cat = call.Arg<CategoryModel>();
            cat.Id = 10;
            return Task.FromResult<CategoryModel?>(cat);
        });

        CompetitorModel? capturedComp = null;
        comporRepo.AddAsync(Arg.Any<CompetitorModel>()).Returns(call =>
        {
            capturedComp = call.Arg<CompetitorModel>();
            capturedComp.Id = 101;
            return Task.FromResult<CompetitorModel?>(capturedComp);
        });

        compCatRepo.AddAsync(Arg.Any<CompetitorCategoryModel>()).Returns(call =>
            Task.FromResult<CompetitorCategoryModel?>(call.Arg<CompetitorCategoryModel>()));

        DrawModel? capturedDraw = null;
        drawRepo.AddAsync(Arg.Any<DrawModel>()).Returns(call =>
        {
            capturedDraw = call.Arg<DrawModel>();
            capturedDraw.Id = 50;
            return Task.FromResult<DrawModel?>(capturedDraw);
        });

        var service = new ExcelImportExportService(compRepo, catRepo, comporRepo, compCatRepo, drawRepo);

        // Build Excel in memory with Taolu competition and empty weights
        using var workbook = new XLWorkbook();
        var wsComp = workbook.Worksheets.Add("Compétition");
        wsComp.Cell(1, 1).Value = "Nom";
        wsComp.Cell(1, 2).Value = "Date (JJ/MM/AAAA)";
        wsComp.Cell(1, 3).Value = "Type";
        wsComp.Cell(2, 1).Value = "Compétition Taolu Test";
        wsComp.Cell(2, 2).Value = "15/03/2026";
        wsComp.Cell(2, 3).Value = "Taolu";

        var wsCat = workbook.Worksheets.Add("Catégories");
        wsCat.Cell(1, 1).Value = "TypeCategorie";
        wsCat.Cell(1, 2).Value = "Genre";
        wsCat.Cell(1, 3).Value = "PoidsMin";
        wsCat.Cell(1, 4).Value = "PoidsMax";
        wsCat.Cell(1, 5).Value = "TrancheAge";
        wsCat.Cell(1, 6).Value = "TypeRound";
        wsCat.Cell(1, 7).Value = "NomCategorie (auto)";

        wsCat.Cell(2, 1).Value = "Taolu Moderne Nord";
        wsCat.Cell(2, 2).Value = "Men";
        wsCat.Cell(2, 3).Value = "";
        wsCat.Cell(2, 4).Value = "";
        wsCat.Cell(2, 5).Value = "Seniors";
        wsCat.Cell(2, 6).Value = "Order";
        wsCat.Cell(2, 7).Value = "Taolu Moderne Nord Seniors Hommes";

        var wsCompetitors = workbook.Worksheets.Add("Compétiteurs");
        wsCompetitors.Cell(1, 1).Value = "Prénom";
        wsCompetitors.Cell(1, 2).Value = "Nom";
        wsCompetitors.Cell(1, 3).Value = "Genre";
        wsCompetitors.Cell(1, 4).Value = "DateNaissance (JJ/MM/AAAA)";
        wsCompetitors.Cell(1, 5).Value = "Club";
        wsCompetitors.Cell(1, 6).Value = "PaysISO";
        wsCompetitors.Cell(1, 7).Value = "Poids (kg)";
        wsCompetitors.Cell(1, 8).Value = "Catégorie";

        wsCompetitors.Cell(2, 1).Value = "Jean";
        wsCompetitors.Cell(2, 2).Value = "Dupont";
        wsCompetitors.Cell(2, 3).Value = "Men";
        wsCompetitors.Cell(2, 4).Value = "15/03/2000";
        wsCompetitors.Cell(2, 5).Value = "Club Paris";
        wsCompetitors.Cell(2, 6).Value = "FRA";
        wsCompetitors.Cell(2, 7).Value = ""; // POIDS VIDE POUR TAOLU
        wsCompetitors.Cell(2, 8).Value = "Taolu Moderne Nord Seniors Hommes";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var result = await service.ImportAsync(ms);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Equal(1, result.ImportedCategories);
        Assert.Equal(1, result.ImportedCompetitors);
        Assert.Equal(1, result.ImportedDraws);
        Assert.NotNull(capturedComp);
        Assert.Equal(0, capturedComp.Weight);
        Assert.NotNull(capturedDraw);
        Assert.NotNull(capturedDraw.DrawOrders);
        Assert.Single(capturedDraw.DrawOrders);
    }
}
