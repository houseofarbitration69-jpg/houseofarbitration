using ClosedXML.Excel;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Server.Cli;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class ExcelCliLoaderTests
{
    [Fact]
    public void ExportTemplateStream_CreatesValidExcelWorkbook()
    {
        using var stream = ExcelCompetitionLoader.ExportTemplateStream();
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        using var workbook = new XLWorkbook(stream);
        Assert.True(workbook.Worksheets.Contains("Compétition"));
        Assert.True(workbook.Worksheets.Contains("Catégories"));
        Assert.True(workbook.Worksheets.Contains("Compétiteurs"));

        var wsComp = workbook.Worksheet("Compétition");
        Assert.Equal("Championnat de France Wushu 2026", wsComp.Cell(2, 1).GetString());
        Assert.Equal("Sanda", wsComp.Cell(2, 3).GetString());
    }

    [Fact]
    public void LoadFromStream_TemplateFile_LoadsCompetitionAndMatchesSuccessfully()
    {
        using var stream = ExcelCompetitionLoader.ExportTemplateStream();
        var result = ExcelCompetitionLoader.LoadFromStream(stream);

        Assert.True(result.Success);
        Assert.NotNull(result.Competition);
        Assert.Equal("Championnat de France Wushu 2026", result.Competition.Name);
        Assert.Equal(CompetitionType.Sanda, result.Competition.Type);

        Assert.NotEmpty(result.Categories);
        Assert.NotEmpty(result.Competitors);
        Assert.NotEmpty(result.Matches);
        Assert.NotEmpty(result.Competition.Categories);
        Assert.All(result.Competition.Categories, c =>
        {
            Assert.NotNull(c.Draw);
        });

        // Check first match
        var firstMatch = result.Matches[0];
        Assert.Equal(1, firstMatch.Id);
        Assert.Contains("DUPONT Jean", firstMatch.RedCompetitor);
        Assert.Contains("MARTIN Marc", firstMatch.BlueCompetitor);
    }

    [Fact]
    public void LoadFromStream_TaoluCompetition_GeneratesOrderMatches()
    {
        using var workbook = new XLWorkbook();
        
        // 1. Feuille Compétition
        var wsComp = workbook.Worksheets.Add("Compétition");
        wsComp.Cell(1, 1).Value = "Nom";
        wsComp.Cell(1, 2).Value = "Date";
        wsComp.Cell(1, 3).Value = "Type";
        wsComp.Cell(2, 1).Value = "Open National Taolu";
        wsComp.Cell(2, 2).Value = "20/04/2026";
        wsComp.Cell(2, 3).Value = "Taolu";

        // 2. Feuille Catégories
        var wsCat = workbook.Worksheets.Add("Catégories");
        wsCat.Cell(1, 1).Value = "TypeCategorie";
        wsCat.Cell(1, 2).Value = "Genre";
        wsCat.Cell(1, 6).Value = "TypeRound";
        wsCat.Cell(1, 7).Value = "NomCategorie";

        wsCat.Cell(2, 1).Value = "Taolu";
        wsCat.Cell(2, 2).Value = "Men";
        wsCat.Cell(2, 6).Value = "Order";
        wsCat.Cell(2, 7).Value = "Taolu Changquan Seniors";

        // 3. Feuille Compétiteurs
        var wsCompetitors = workbook.Worksheets.Add("Compétiteurs");
        wsCompetitors.Cell(1, 1).Value = "Prénom";
        wsCompetitors.Cell(1, 2).Value = "Nom";
        wsCompetitors.Cell(1, 8).Value = "Catégorie";

        wsCompetitors.Cell(2, 1).Value = "Li";
        wsCompetitors.Cell(2, 2).Value = "Wei";
        wsCompetitors.Cell(2, 8).Value = "Taolu Changquan Seniors";

        wsCompetitors.Cell(3, 1).Value = "Chen";
        wsCompetitors.Cell(3, 2).Value = "Tao";
        wsCompetitors.Cell(3, 8).Value = "Taolu Changquan Seniors";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var result = ExcelCompetitionLoader.LoadFromStream(ms);

        Assert.True(result.Success);
        Assert.NotNull(result.Competition);
        Assert.Equal(CompetitionType.Taolu, result.Competition.Type);
        Assert.Equal("Open National Taolu", result.Competition.Name);

        Assert.Equal(2, result.Matches.Count);
        Assert.Equal(RoundType.Order, result.Matches[0].Type);
        Assert.Contains("WEI Li", result.Matches[0].RedCompetitor);
        Assert.Empty(result.Matches[0].BlueCompetitor);

        Assert.Equal(RoundType.Order, result.Matches[1].Type);
        Assert.Contains("TAO Chen", result.Matches[1].RedCompetitor);
        Assert.Empty(result.Matches[1].BlueCompetitor);
    }

    [Fact]
    public void LoadFromFile_NonExistentFile_ReturnsErrorResult()
    {
        var result = ExcelCompetitionLoader.LoadFromFile("un_fichier_completement_inexistant.xlsx");
        Assert.False(result.Success);
        Assert.Contains("introuvable", result.ErrorMessage);
    }

    [Fact]
    public void LoadFromStream_SandaFourCompetitors_GeneratesThreeMatchesIncludingFinal()
    {
        using var workbook = new XLWorkbook();

        var wsComp = workbook.Worksheets.Add("Compétition");
        wsComp.Cell(1, 1).Value = "Nom";
        wsComp.Cell(1, 2).Value = "Date";
        wsComp.Cell(1, 3).Value = "Type";
        wsComp.Cell(2, 1).Value = "Tournoi Sanda 4";
        wsComp.Cell(2, 2).Value = "15/03/2026";
        wsComp.Cell(2, 3).Value = "Sanda";

        var wsCat = workbook.Worksheets.Add("Catégories");
        wsCat.Cell(1, 1).Value = "TypeCategorie";
        wsCat.Cell(1, 2).Value = "Genre";
        wsCat.Cell(1, 6).Value = "TypeRound";
        wsCat.Cell(1, 7).Value = "NomCategorie";

        wsCat.Cell(2, 1).Value = "Sanda";
        wsCat.Cell(2, 2).Value = "Men";
        wsCat.Cell(2, 6).Value = "Knockouts";
        wsCat.Cell(2, 7).Value = "Sanda Seniors Hommes -70kg";

        var wsCompetitors = workbook.Worksheets.Add("Compétiteurs");
        wsCompetitors.Cell(1, 1).Value = "Prénom";
        wsCompetitors.Cell(1, 2).Value = "Nom";
        wsCompetitors.Cell(1, 8).Value = "Catégorie";

        wsCompetitors.Cell(2, 1).Value = "Lucas";
        wsCompetitors.Cell(2, 2).Value = "Dupont";
        wsCompetitors.Cell(2, 8).Value = "Sanda Seniors Hommes -70kg";

        wsCompetitors.Cell(3, 1).Value = "Marco";
        wsCompetitors.Cell(3, 2).Value = "Rossi";
        wsCompetitors.Cell(3, 8).Value = "Sanda Seniors Hommes -70kg";

        wsCompetitors.Cell(4, 1).Value = "Chen";
        wsCompetitors.Cell(4, 2).Value = "Wei";
        wsCompetitors.Cell(4, 8).Value = "Sanda Seniors Hommes -70kg";

        wsCompetitors.Cell(5, 1).Value = "Tarik";
        wsCompetitors.Cell(5, 2).Value = "Benali";
        wsCompetitors.Cell(5, 8).Value = "Sanda Seniors Hommes -70kg";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var result = ExcelCompetitionLoader.LoadFromStream(ms);

        Assert.True(result.Success);
        // Pour 4 compétiteurs en élimination directe : 2 demi-finales + 1 finale = 3 matchs
        Assert.Equal(3, result.Matches.Count);

        var m1 = result.Matches[0];
        var m2 = result.Matches[1];
        var m3 = result.Matches[2];

        Assert.Equal("Demi-finale 1", m1.StageName);
        Assert.Contains("DUPONT", m1.RedCompetitor);
        Assert.Contains("ROSSI", m1.BlueCompetitor);
        Assert.Equal(m3.Id, m1.NextMatchId);
        Assert.Equal(Constants.Side.Red, m1.NextMatchSide);

        Assert.Equal("Demi-finale 2", m2.StageName);
        Assert.Contains("WEI", m2.RedCompetitor);
        Assert.Contains("BENALI", m2.BlueCompetitor);
        Assert.Equal(m3.Id, m2.NextMatchId);
        Assert.Equal(Constants.Side.Blue, m2.NextMatchSide);

        Assert.Equal("Finale", m3.StageName);
        Assert.Contains("Vainqueur", m3.RedCompetitor);
        Assert.Contains("Vainqueur", m3.BlueCompetitor);
        Assert.Null(m3.NextMatchId);
    }

    [Fact]
    public async Task Session_KnockoutFourCompetitors_PropagatesWinnersToFinalAndCalculatesPodium()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        using var workbook = new XLWorkbook();
        var wsComp = workbook.Worksheets.Add("Compétition");
        wsComp.Cell(1, 1).Value = "Nom";
        wsComp.Cell(1, 3).Value = "Type";
        wsComp.Cell(2, 1).Value = "Tournoi Sanda 4";
        wsComp.Cell(2, 3).Value = "Sanda";

        var wsCat = workbook.Worksheets.Add("Catégories");
        wsCat.Cell(1, 1).Value = "TypeCategorie";
        wsCat.Cell(1, 2).Value = "Genre";
        wsCat.Cell(1, 6).Value = "TypeRound";
        wsCat.Cell(1, 7).Value = "NomCategorie";
        wsCat.Cell(2, 1).Value = "Sanda";
        wsCat.Cell(2, 2).Value = "Men";
        wsCat.Cell(2, 6).Value = "Knockouts";
        wsCat.Cell(2, 7).Value = "Sanda -70kg";

        var wsCompetitors = workbook.Worksheets.Add("Compétiteurs");
        wsCompetitors.Cell(1, 1).Value = "Prénom";
        wsCompetitors.Cell(1, 2).Value = "Nom";
        wsCompetitors.Cell(1, 8).Value = "Catégorie";

        wsCompetitors.Cell(2, 1).Value = "Lucas";
        wsCompetitors.Cell(2, 2).Value = "Dupont";
        wsCompetitors.Cell(2, 8).Value = "Sanda -70kg";

        wsCompetitors.Cell(3, 1).Value = "Marco";
        wsCompetitors.Cell(3, 2).Value = "Rossi";
        wsCompetitors.Cell(3, 8).Value = "Sanda -70kg";

        wsCompetitors.Cell(4, 1).Value = "Chen";
        wsCompetitors.Cell(4, 2).Value = "Wei";
        wsCompetitors.Cell(4, 8).Value = "Sanda -70kg";

        wsCompetitors.Cell(5, 1).Value = "Tarik";
        wsCompetitors.Cell(5, 2).Value = "Benali";
        wsCompetitors.Cell(5, 8).Value = "Sanda -70kg";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var loadResult = ExcelCompetitionLoader.LoadFromStream(ms);
        await session.LoadCompetitionAsync(loadResult.Competition!, loadResult.Matches, loadResult.Categories);

        Assert.Equal(3, session.Matches.Count);

        // Match 1 : Demi-finale 1 (Dupont vs Rossi)
        await session.SetMatchIndexAsync(0);
        await session.SetWinnerSideAsync(Constants.Side.Red); // Dupont gagne

        // Vérifier que Dupont est qualifié pour la Finale (Coin Rouge)
        var finale = session.Matches[2];
        Assert.Contains("DUPONT Lucas", finale.RedCompetitor);
        Assert.Contains("Vainqueur", finale.BlueCompetitor); // pas encore joué

        // Match 2 : Demi-finale 2 (Wei vs Benali)
        await session.SetMatchIndexAsync(1);
        await session.SetWinnerSideAsync(Constants.Side.Blue); // Benali gagne

        // Vérifier que Benali est qualifié pour la Finale (Coin Bleu)
        Assert.Contains("BENALI Tarik", finale.BlueCompetitor);

        // Match 3 : Finale (Dupont vs Benali)
        await session.SetMatchIndexAsync(2);
        await session.SetWinnerSideAsync(Constants.Side.Red); // Dupont gagne la finale

        // Vérifier les classements
        var history = session.GetCategoryHistory();
        Assert.NotNull(history);
        Assert.Contains("DUPONT Lucas", history.CategoryWinner);

        // Podium : 1er Champion, 2e Finaliste, 3e ex-aequo les 2 demi-finalistes (sans petite finale)
        Assert.Equal(4, history.Standings.Count);

        var first = history.Standings.First(s => s.Rank == 1);
        Assert.Contains("DUPONT Lucas", first.CompetitorName);
        Assert.Equal("CHAMPION 🥇", first.Status);

        var second = history.Standings.First(s => s.Rank == 2);
        Assert.Contains("BENALI Tarik", second.CompetitorName);
        Assert.Equal("FINALISTE 🥈", second.Status);

        var thirds = history.Standings.Where(s => s.Rank == 3).ToList();
        Assert.Equal(2, thirds.Count);
        Assert.Contains(thirds, t => t.CompetitorName.Contains("ROSSI"));
        Assert.Contains(thirds, t => t.CompetitorName.Contains("WEI"));
        Assert.All(thirds, t => Assert.Equal("Demi-finaliste 🥉", t.Status));
    }

    [Fact]
    public void LoadFromFile_ActualSandaXlsxDocument_GeneratesThreeMatchesWithFinal()
    {
        string sandaPath = @"C:\Works\KungFu\houseofarbitration\Documents\Sanda.xlsx";
        if (!File.Exists(sandaPath)) return;

        var result = ExcelCompetitionLoader.LoadFromFile(sandaPath);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(4, result.Competitors.Count);
        
        // Si Petite Finale activée dans le fichier -> 4 matchs (2 demis + 1 petite finale + 1 finale), sinon 3 matchs
        if (result.Categories.Count > 0 && result.Categories[0].HasBronzeMatch)
        {
            Assert.Equal(4, result.Matches.Count);
            Assert.Equal("Petite finale", result.Matches[2].StageName);
            Assert.Equal("Finale", result.Matches[3].StageName);
        }
        else
        {
            Assert.Equal(3, result.Matches.Count);
            Assert.Equal("Demi-finale 1", result.Matches[0].StageName);
            Assert.Equal("Demi-finale 2", result.Matches[1].StageName);
            Assert.Equal("Finale", result.Matches[2].StageName);
            Assert.Equal(result.Matches[2].Id, result.Matches[0].NextMatchId);
            Assert.Equal(result.Matches[2].Id, result.Matches[1].NextMatchId);
        }
    }

    [Fact]
    public void LoadFromStream_WithBronzeMatchEnabled_GeneratesFourMatchesWithBronzeAndFinal()
    {
        using var workbook = new XLWorkbook();
        var wsComp = workbook.Worksheets.Add("Compétition");
        wsComp.Cell(1, 1).Value = "Nom";
        wsComp.Cell(1, 3).Value = "Type";
        wsComp.Cell(2, 1).Value = "Tournoi Sanda";
        wsComp.Cell(2, 3).Value = "Sanda";

        var wsCat = workbook.Worksheets.Add("Catégories");
        wsCat.Cell(1, 1).Value = "TypeCategorie";
        wsCat.Cell(1, 6).Value = "TypeRound";
        wsCat.Cell(1, 7).Value = "NomCategorie";
        wsCat.Cell(1, 8).Value = "Petite Finale (Oui/Non)";
        wsCat.Cell(2, 1).Value = "Sanda";
        wsCat.Cell(2, 6).Value = "Knockouts";
        wsCat.Cell(2, 7).Value = "Sanda -70kg";
        wsCat.Cell(2, 8).Value = "Oui"; // Petite finale activée

        var wsCompetitors = workbook.Worksheets.Add("Compétiteurs");
        wsCompetitors.Cell(1, 1).Value = "Prénom";
        wsCompetitors.Cell(1, 2).Value = "Nom";
        wsCompetitors.Cell(1, 8).Value = "Catégorie";

        wsCompetitors.Cell(2, 1).Value = "Lucas"; wsCompetitors.Cell(2, 2).Value = "Dupont"; wsCompetitors.Cell(2, 8).Value = "Sanda -70kg";
        wsCompetitors.Cell(3, 1).Value = "Marco"; wsCompetitors.Cell(3, 2).Value = "Rossi"; wsCompetitors.Cell(3, 8).Value = "Sanda -70kg";
        wsCompetitors.Cell(4, 1).Value = "Chen"; wsCompetitors.Cell(4, 2).Value = "Wei"; wsCompetitors.Cell(4, 8).Value = "Sanda -70kg";
        wsCompetitors.Cell(5, 1).Value = "Tarik"; wsCompetitors.Cell(5, 2).Value = "Benali"; wsCompetitors.Cell(5, 8).Value = "Sanda -70kg";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var result = ExcelCompetitionLoader.LoadFromStream(ms);

        Assert.True(result.Success);
        Assert.Single(result.Categories);
        Assert.True(result.Categories[0].HasBronzeMatch);
        // Doit générer 4 matchs : DF1, DF2, Petite finale, Finale
        Assert.Equal(4, result.Matches.Count);

        var df1 = result.Matches[0];
        var df2 = result.Matches[1];
        var bronze = result.Matches[2];
        var finale = result.Matches[3];

        Assert.Equal("Demi-finale 1", df1.StageName);
        Assert.Equal("Demi-finale 2", df2.StageName);
        Assert.Equal("Petite finale", bronze.StageName);
        Assert.True(bronze.IsBronzeMatch);
        Assert.Equal("Finale", finale.StageName);
        Assert.False(finale.IsBronzeMatch);

        // Vainqueurs vers Finale
        Assert.Equal(finale.Id, df1.NextMatchId);
        Assert.Equal(Constants.Side.Red, df1.NextMatchSide);
        Assert.Equal(finale.Id, df2.NextMatchId);
        Assert.Equal(Constants.Side.Blue, df2.NextMatchSide);

        // Perdants vers Petite finale
        Assert.Equal(bronze.Id, df1.NextLoserMatchId);
        Assert.Equal(Constants.Side.Red, df1.NextLoserMatchSide);
        Assert.Equal(bronze.Id, df2.NextLoserMatchId);
        Assert.Equal(Constants.Side.Blue, df2.NextLoserMatchSide);
    }

    [Fact]
    public async Task Session_KnockoutWithBronzeMatch_PropagatesLosersToBronzeMatchAndCalculatesPodium()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        using var workbook = new XLWorkbook();
        var wsComp = workbook.Worksheets.Add("Compétition");
        wsComp.Cell(1, 1).Value = "Nom";
        wsComp.Cell(1, 3).Value = "Type";
        wsComp.Cell(2, 1).Value = "Tournoi Sanda";
        wsComp.Cell(2, 3).Value = "Sanda";

        var wsCat = workbook.Worksheets.Add("Catégories");
        wsCat.Cell(1, 1).Value = "TypeCategorie";
        wsCat.Cell(1, 6).Value = "TypeRound";
        wsCat.Cell(1, 7).Value = "NomCategorie";
        wsCat.Cell(1, 8).Value = "Petite Finale (Oui/Non)";
        wsCat.Cell(2, 1).Value = "Sanda";
        wsCat.Cell(2, 6).Value = "Knockouts";
        wsCat.Cell(2, 7).Value = "Sanda -70kg";
        wsCat.Cell(2, 8).Value = "Oui";

        var wsCompetitors = workbook.Worksheets.Add("Compétiteurs");
        wsCompetitors.Cell(1, 1).Value = "Prénom";
        wsCompetitors.Cell(1, 2).Value = "Nom";
        wsCompetitors.Cell(1, 8).Value = "Catégorie";

        wsCompetitors.Cell(2, 1).Value = "Lucas"; wsCompetitors.Cell(2, 2).Value = "Dupont"; wsCompetitors.Cell(2, 8).Value = "Sanda -70kg";
        wsCompetitors.Cell(3, 1).Value = "Marco"; wsCompetitors.Cell(3, 2).Value = "Rossi"; wsCompetitors.Cell(3, 8).Value = "Sanda -70kg";
        wsCompetitors.Cell(4, 1).Value = "Chen"; wsCompetitors.Cell(4, 2).Value = "Wei"; wsCompetitors.Cell(4, 8).Value = "Sanda -70kg";
        wsCompetitors.Cell(5, 1).Value = "Tarik"; wsCompetitors.Cell(5, 2).Value = "Benali"; wsCompetitors.Cell(5, 8).Value = "Sanda -70kg";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var loadResult = ExcelCompetitionLoader.LoadFromStream(ms);
        await session.LoadCompetitionAsync(loadResult.Competition!, loadResult.Matches, loadResult.Categories);

        Assert.Equal(4, session.Matches.Count);

        var bronze = session.Matches[2];
        var finale = session.Matches[3];

        // Match 1 : Demi-finale 1 (Dupont vs Rossi)
        // Dupont gagne (Red) -> qualifié en Finale ; Rossi perd -> qualifié en Petite finale
        await session.SetMatchIndexAsync(0);
        await session.SetWinnerSideAsync(Constants.Side.Red);

        Assert.Contains("DUPONT Lucas", finale.RedCompetitor);
        Assert.Contains("ROSSI Marco", bronze.RedCompetitor);

        // Match 2 : Demi-finale 2 (Wei vs Benali)
        // Benali gagne (Blue) -> qualifié en Finale ; Wei perd -> qualifié en Petite finale
        await session.SetMatchIndexAsync(1);
        await session.SetWinnerSideAsync(Constants.Side.Blue);

        Assert.Contains("BENALI Tarik", finale.BlueCompetitor);
        Assert.Contains("WEI Chen", bronze.BlueCompetitor);

        // Match 3 : Petite finale (Rossi vs Wei)
        // Rossi gagne (Red) -> 3e place 🥉
        await session.SetMatchIndexAsync(2);
        await session.SetWinnerSideAsync(Constants.Side.Red);

        // Match 4 : Finale (Dupont vs Benali)
        // Dupont gagne (Red) -> Champion 🥇
        await session.SetMatchIndexAsync(3);
        await session.SetWinnerSideAsync(Constants.Side.Red);

        // Vérifier les classements
        var history = session.GetCategoryHistory();
        Assert.NotNull(history);
        Assert.Contains("DUPONT Lucas", history.CategoryWinner);

        Assert.Equal(4, history.Standings.Count);

        var rank1 = history.Standings.First(s => s.Rank == 1);
        Assert.Contains("DUPONT Lucas", rank1.CompetitorName);
        Assert.Equal("CHAMPION 🥇", rank1.Status);

        var rank2 = history.Standings.First(s => s.Rank == 2);
        Assert.Contains("BENALI Tarik", rank2.CompetitorName);
        Assert.Equal("FINALISTE 🥈", rank2.Status);

        var rank3 = history.Standings.First(s => s.Rank == 3);
        Assert.Contains("ROSSI Marco", rank3.CompetitorName);
        Assert.Equal("3e place (Petite finale) 🥉", rank3.Status);

        var rank4 = history.Standings.First(s => s.Rank == 4);
        Assert.Contains("WEI Chen", rank4.CompetitorName);
        Assert.Equal("4e place (Petite finale)", rank4.Status);
    }

    [Fact]
    public async Task Session_KnockoutWithoutBronzeMatch_RanksThirdPlaceByCumulativePoints()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        using var workbook = new XLWorkbook();
        var wsComp = workbook.Worksheets.Add("Compétition");
        wsComp.Cell(1, 1).Value = "Nom";
        wsComp.Cell(1, 3).Value = "Type";
        wsComp.Cell(2, 1).Value = "Tournoi Sanda";
        wsComp.Cell(2, 3).Value = "Sanda";

        var wsCat = workbook.Worksheets.Add("Catégories");
        wsCat.Cell(1, 1).Value = "TypeCategorie";
        wsCat.Cell(1, 6).Value = "TypeRound";
        wsCat.Cell(1, 7).Value = "NomCategorie";
        wsCat.Cell(1, 8).Value = "Petite Finale (Oui/Non)";
        wsCat.Cell(2, 1).Value = "Sanda";
        wsCat.Cell(2, 6).Value = "Knockouts";
        wsCat.Cell(2, 7).Value = "Sanda -70kg";
        wsCat.Cell(2, 8).Value = "Non"; // Pas de petite finale

        var wsCompetitors = workbook.Worksheets.Add("Compétiteurs");
        wsCompetitors.Cell(1, 1).Value = "Prénom";
        wsCompetitors.Cell(1, 2).Value = "Nom";
        wsCompetitors.Cell(1, 8).Value = "Catégorie";

        wsCompetitors.Cell(2, 1).Value = "Lucas"; wsCompetitors.Cell(2, 2).Value = "Dupont"; wsCompetitors.Cell(2, 8).Value = "Sanda -70kg";
        wsCompetitors.Cell(3, 1).Value = "Marco"; wsCompetitors.Cell(3, 2).Value = "Rossi"; wsCompetitors.Cell(3, 8).Value = "Sanda -70kg";
        wsCompetitors.Cell(4, 1).Value = "Chen"; wsCompetitors.Cell(4, 2).Value = "Wei"; wsCompetitors.Cell(4, 8).Value = "Sanda -70kg";
        wsCompetitors.Cell(5, 1).Value = "Tarik"; wsCompetitors.Cell(5, 2).Value = "Benali"; wsCompetitors.Cell(5, 8).Value = "Sanda -70kg";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var loadResult = ExcelCompetitionLoader.LoadFromStream(ms);
        await session.LoadCompetitionAsync(loadResult.Competition!, loadResult.Matches, loadResult.Categories);

        Assert.Equal(3, session.Matches.Count); // Seulement 3 matchs : DF1, DF2, Finale

        // DF 1 : Dupont (Red, 10 pts) vs Rossi (Blue, 6 pts) -> Dupont vainqueur, Rossi marque 6 pts
        session.Matches[0].RedScore = 10;
        session.Matches[0].BlueScore = 6;
        await session.SetMatchIndexAsync(0);
        await session.SetWinnerSideAsync(Constants.Side.Red);

        // DF 2 : Wei (Red, 2 pts) vs Benali (Blue, 8 pts) -> Benali vainqueur, Wei marque 2 pts
        session.Matches[1].RedScore = 2;
        session.Matches[1].BlueScore = 8;
        await session.SetMatchIndexAsync(1);
        await session.SetWinnerSideAsync(Constants.Side.Blue);

        // Finale : Dupont vs Benali -> Dupont gagne
        session.Matches[2].RedScore = 5;
        session.Matches[2].BlueScore = 3;
        await session.SetMatchIndexAsync(2);
        await session.SetWinnerSideAsync(Constants.Side.Red);

        var history = session.GetCategoryHistory();
        Assert.NotNull(history);

        // Rossi a 6 points marqués, Wei a 2 points marqués -> Rossi doit être 3e 🥉, Wei 4e
        var rank3 = history.Standings.First(s => s.Rank == 3);
        Assert.Contains("ROSSI Marco", rank3.CompetitorName);
        Assert.Equal("3e place (Points cumulés) 🥉", rank3.Status);
        Assert.Equal(6, rank3.PointsFor);

        var rank4 = history.Standings.First(s => s.Rank == 4);
        Assert.Contains("WEI Chen", rank4.CompetitorName);
        Assert.Equal("4e place", rank4.Status);
        Assert.Equal(2, rank4.PointsFor);
    }

    [Fact]
    public async Task Session_KnockoutClearWinner_ResetsBothFinalAndBronzeMatch()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        using var workbook = new XLWorkbook();
        var wsComp = workbook.Worksheets.Add("Compétition");
        wsComp.Cell(1, 1).Value = "Nom";
        wsComp.Cell(1, 3).Value = "Type";
        wsComp.Cell(2, 1).Value = "Tournoi Sanda";
        wsComp.Cell(2, 3).Value = "Sanda";

        var wsCat = workbook.Worksheets.Add("Catégories");
        wsCat.Cell(1, 1).Value = "TypeCategorie";
        wsCat.Cell(1, 6).Value = "TypeRound";
        wsCat.Cell(1, 7).Value = "NomCategorie";
        wsCat.Cell(1, 8).Value = "Petite Finale (Oui/Non)";
        wsCat.Cell(2, 1).Value = "Sanda";
        wsCat.Cell(2, 6).Value = "Knockouts";
        wsCat.Cell(2, 7).Value = "Sanda -70kg";
        wsCat.Cell(2, 8).Value = "Oui";

        var wsCompetitors = workbook.Worksheets.Add("Compétiteurs");
        wsCompetitors.Cell(1, 1).Value = "Prénom";
        wsCompetitors.Cell(1, 2).Value = "Nom";
        wsCompetitors.Cell(1, 8).Value = "Catégorie";
        wsCompetitors.Cell(2, 1).Value = "Lucas"; wsCompetitors.Cell(2, 2).Value = "Dupont"; wsCompetitors.Cell(2, 8).Value = "Sanda -70kg";
        wsCompetitors.Cell(3, 1).Value = "Marco"; wsCompetitors.Cell(3, 2).Value = "Rossi"; wsCompetitors.Cell(3, 8).Value = "Sanda -70kg";
        wsCompetitors.Cell(4, 1).Value = "Chen"; wsCompetitors.Cell(4, 2).Value = "Wei"; wsCompetitors.Cell(4, 8).Value = "Sanda -70kg";
        wsCompetitors.Cell(5, 1).Value = "Tarik"; wsCompetitors.Cell(5, 2).Value = "Benali"; wsCompetitors.Cell(5, 8).Value = "Sanda -70kg";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var loadResult = ExcelCompetitionLoader.LoadFromStream(ms);
        await session.LoadCompetitionAsync(loadResult.Competition!, loadResult.Matches, loadResult.Categories);

        // Valider DF 1 (Dupont bat Rossi)
        await session.SetMatchIndexAsync(0);
        await session.SetWinnerSideAsync(Constants.Side.Red);

        Assert.Contains("DUPONT Lucas", session.Matches[3].RedCompetitor); // Finale
        Assert.Contains("ROSSI Marco", session.Matches[2].RedCompetitor);  // Petite finale

        // Annuler la décision sur DF 1
        await session.ClearWinnerAsync();

        // Les deux matchs cibles doivent être réinitialisés avec les placeholders
        Assert.Contains("Vainqueur", session.Matches[3].RedCompetitor);
        Assert.Contains("Perdant", session.Matches[2].RedCompetitor);
    }

    [Fact]
    public async Task ArbitrationServerSession_SendsFullCompetitionWithCategoriesAndDraws_OnGetCompetition()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        // Vérifier l'état par défaut
        Assert.NotEmpty(session.CurrentCompetition.Categories);
        Assert.All(session.CurrentCompetition.Categories, cat =>
        {
            Assert.NotNull(cat.Draw);
            Assert.NotEmpty(cat.Competitors);
        });

        // BroadcastCompetitionDataAsync envoie COMPETITION_DATA:{json}
        await session.BroadcastCompetitionDataAsync();

        // Sérialisation avec options pour vérifier le contenu
        var options = new System.Text.Json.JsonSerializerOptions
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
            PropertyNameCaseInsensitive = true
        };
        string json = System.Text.Json.JsonSerializer.Serialize(session.CurrentCompetition, options);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<CompetitionModel>(json, options);

        Assert.NotNull(deserialized);
        Assert.Equal(session.CurrentCompetition.Name, deserialized.Name);
        Assert.NotEmpty(deserialized.Categories);
        Assert.All(deserialized.Categories, cat =>
        {
            Assert.NotNull(cat.Draw);
            Assert.True(
                (cat.Draw.DrawKnockouts != null && cat.Draw.DrawKnockouts.Count > 0) ||
                (cat.Draw.DrawOrders != null && cat.Draw.DrawOrders.Count > 0) ||
                (cat.Draw.DrawPools != null && cat.Draw.DrawPools.Count > 0));
        });
    }
}
