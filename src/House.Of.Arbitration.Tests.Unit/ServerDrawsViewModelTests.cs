using CommunityToolkit.Maui;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Competition;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace House.Of.Arbitration.Tests.Unit;

public class ServerDrawsViewModelTests
{
    private static ServerDrawsViewModel CreateVm(
        out IRepository<CompetitionModel> compRepo,
        out IRepository<CategoryModel> catRepo,
        out IRepository<DrawModel> drawRepo,
        out IRepository<DrawKnockoutModel> koRepo,
        out IRepository<DrawPoolsModel> poolRepo,
        out IRepository<DrawOrderModel> orderRepo,
        out IRepository<RefereeDataModel> refereeDataRepo)
    {
        compRepo = Substitute.For<IRepository<CompetitionModel>>();
        catRepo = Substitute.For<IRepository<CategoryModel>>();
        drawRepo = Substitute.For<IRepository<DrawModel>>();
        koRepo = Substitute.For<IRepository<DrawKnockoutModel>>();
        poolRepo = Substitute.For<IRepository<DrawPoolsModel>>();
        orderRepo = Substitute.For<IRepository<DrawOrderModel>>();
        refereeDataRepo = Substitute.For<IRepository<RefereeDataModel>>();
        var alert = Substitute.For<IAlertService>();
        var resourceProvider = Substitute.For<ResourceProvider>();
        var popupService = Substitute.For<IPopupService>();

        return new ServerDrawsViewModel(
            NullLogger<ServerDrawsViewModel>.Instance,
            resourceProvider,
            popupService,
            compRepo,
            catRepo,
            drawRepo,
            koRepo,
            poolRepo,
            orderRepo,
            refereeDataRepo,
            alert);
    }

    [Fact]
    public async Task LoadCategoriesAsync_SelectsCurrentCategoryByDefault()
    {
        var vm = CreateVm(out var compRepo, out var catRepo, out var drawRepo, out _, out _, out _, out _);
        compRepo.GetByIdAsync(1).Returns(new CompetitionModel { Id = 1, Name = "Open" });
        catRepo.GetAllAsync(Arg.Any<string[]>()).Returns(new List<CategoryModel>
        {
            new() { Id = 10, CompetitionId = 1, RoundType = RoundType.Knockouts },
            new() { Id = 20, CompetitionId = 1, RoundType = RoundType.Pools }
        });
        drawRepo.GetAllAsync(Arg.Any<string[]>()).Returns(new List<DrawModel>());

        vm.CompetitionId = 1;
        vm.CurrentCategoryId = 20;
        await vm.LoadCategoriesAsync();

        Assert.NotNull(vm.SelectedCategory);
        Assert.Equal(20, vm.SelectedCategory!.Id);
        Assert.True(vm.IsPools);
    }

    [Fact]
    public async Task Knockouts_SetWinner_ResetAndDragDrop_CascadeToNextMatches()
    {
        var vm = CreateVm(out var compRepo, out var catRepo, out var drawRepo, out var koRepo, out _, out _, out var refRepo);

        var c1 = new CompetitorModel { Id = 1, FirstName = "A", LastName = "1" };
        var c2 = new CompetitorModel { Id = 2, FirstName = "B", LastName = "2" };
        var c3 = new CompetitorModel { Id = 3, FirstName = "C", LastName = "3" };
        var c4 = new CompetitorModel { Id = 4, FirstName = "D", LastName = "4" };

        var refDataSemi1 = new RefereeDataModel { Id = 501, DrawKnockoutId = 101, Score = 8.5, Date = DateTime.UtcNow };
        refRepo.GetAllAsync().Returns(new List<RefereeDataModel> { refDataSemi1 });

        var semi1 = new DrawKnockoutModel { Id = 101, DrawId = 5, Order = 1, Competitor1Id = 1, Competitor1 = c1, Competitor2Id = 2, Competitor2 = c2, CurrentRound = 2, RoundResultsJson = "[{\"Round\":1}]", RefereeDatas = new List<RefereeDataModel> { refDataSemi1 } };
        var semi2 = new DrawKnockoutModel { Id = 102, DrawId = 5, Order = 2, Competitor1Id = 3, Competitor1 = c3, Competitor2Id = 4, Competitor2 = c4 };
        var bronze = new DrawKnockoutModel { Id = 103, DrawId = 5, Order = 3, IsBronzeMatch = true };
        var final = new DrawKnockoutModel { Id = 104, DrawId = 5, Order = 4, IsBronzeMatch = false };

        var draw = new DrawModel
        {
            Id = 5,
            CategoryId = 10,
            DrawKnockouts = new List<DrawKnockoutModel> { semi1, semi2, bronze, final }
        };

        compRepo.GetByIdAsync(1).Returns(new CompetitionModel { Id = 1, Name = "Open" });
        catRepo.GetAllAsync(Arg.Any<string[]>()).Returns(new List<CategoryModel>
        {
            new() { Id = 10, CompetitionId = 1, RoundType = RoundType.Knockouts }
        });
        drawRepo.GetAllAsync(Arg.Any<string[]>()).Returns(new List<DrawModel> { draw });

        vm.CompetitionId = 1;
        vm.CurrentCategoryId = 10;
        await vm.LoadCategoriesAsync();

        // Vérifier le centrage géométrique de l'arbre (n+1 centré entre les deux matchs n) et la petite finale
        Assert.Equal(2, vm.KnockoutRounds.Count);
        Assert.Equal(180.0, vm.KnockoutRounds[0].Matches[0].BracketSlotHeight);
        Assert.Equal(180.0, vm.KnockoutRounds[0].Matches[1].BracketSlotHeight);
        Assert.Equal(360.0, vm.KnockoutRounds[1].Matches[0].BracketSlotHeight);
        Assert.False(vm.KnockoutRounds[0].Matches[0].ShowLeftConnector);
        Assert.True(vm.KnockoutRounds[0].Matches[0].ShowRightConnector);
        Assert.True(vm.KnockoutRounds[0].Matches[0].IsTopFeederConnector);
        Assert.True(vm.KnockoutRounds[0].Matches[1].IsBottomFeederConnector);
        Assert.True(vm.KnockoutRounds[1].Matches[0].ShowLeftConnector);
        Assert.False(vm.KnockoutRounds[1].Matches[0].ShowRightConnector);
        Assert.True(vm.HasBronzeMatch);
        Assert.Single(vm.BronzeMatches);

        // 1. Déclarer c1 vainqueur de semi1 → c1 en Finale (Slot1) et c2 en Petite finale (Slot1)
        var semi1Item = vm.KnockoutRounds[0].Matches[0];
        await vm.SetKnockoutWinner(semi1Item.Slot1);

        Assert.True(semi1.IsFinished);
        Assert.Equal(1, semi1.WinnerId);
        Assert.Equal(1, final.Competitor1Id);
        Assert.Equal(2, bronze.Competitor1Id);

        // 2. Déclarer c1 vainqueur de la Finale
        var finalItem = vm.KnockoutRounds.Last().Matches[0];
        await vm.SetKnockoutWinner(finalItem.Slot1);
        Assert.True(final.IsFinished);
        Assert.Equal(1, final.WinnerId);

        // 3. Réinitialiser semi1 → doit effacer c1 de la Finale ET réinitialiser la Finale en cascade !
        semi1Item = vm.KnockoutRounds[0].Matches[0];
        await vm.ResetKnockoutMatch(semi1Item);

        Assert.False(semi1.IsFinished);
        Assert.Null(semi1.WinnerId);
        Assert.Equal(1, semi1.CurrentRound);
        Assert.Equal(string.Empty, semi1.RoundResultsJson);
        Assert.Empty(semi1.RefereeDatas!);
        await refRepo.Received().DeleteAsync(refDataSemi1);
        Assert.Null(final.Competitor1Id);
        Assert.False(final.IsFinished);
        Assert.Null(final.WinnerId);
        Assert.Null(bronze.Competitor1Id);

        // 4. Drag'n'drop entre semi1.Slot1 (c1) et semi2.Slot1 (c3)
        semi1Item = vm.KnockoutRounds[0].Matches[0];
        var semi2Item = vm.KnockoutRounds[0].Matches[1];
        await vm.SwapKnockoutSlotsAsync(semi1Item.Slot1, semi2Item.Slot1);

        Assert.Equal(3, semi1.Competitor1Id);
        Assert.Equal(1, semi2.Competitor1Id);
    }

    [Fact]
    public async Task Pools_SetWinner_UpdatesStandings()
    {
        var vm = CreateVm(out var compRepo, out var catRepo, out var drawRepo, out _, out var poolRepo, out _, out var refRepo);

        var c1 = new CompetitorModel { Id = 1, FirstName = "Alice", LastName = "A" };
        var c2 = new CompetitorModel { Id = 2, FirstName = "Bob", LastName = "B" };
        var refDataPool = new RefereeDataModel { Id = 601, DrawPoolsId = 201, Score = 9.0, Date = DateTime.UtcNow };
        refRepo.GetAllAsync().Returns(new List<RefereeDataModel> { refDataPool });
        var poolMatch = new DrawPoolsModel { Id = 201, DrawId = 6, Order = 1, Competitor1Id = 1, Competitor1 = c1, Competitor2Id = 2, Competitor2 = c2, CurrentRound = 2, RoundResultsJson = "[{\"Round\":1}]", RefereeDatas = new List<RefereeDataModel> { refDataPool } };

        compRepo.GetByIdAsync(1).Returns(new CompetitionModel { Id = 1, Name = "Open" });
        catRepo.GetAllAsync(Arg.Any<string[]>()).Returns(new List<CategoryModel>
        {
            new() { Id = 20, CompetitionId = 1, RoundType = RoundType.Pools }
        });
        drawRepo.GetAllAsync(Arg.Any<string[]>()).Returns(new List<DrawModel>
        {
            new() { Id = 6, CategoryId = 20, DrawPools = new List<DrawPoolsModel> { poolMatch } }
        });

        vm.CompetitionId = 1;
        vm.CurrentCategoryId = 20;
        await vm.LoadCategoriesAsync();

        await vm.SetPoolWinner2(vm.PoolMatches[0]);

        Assert.True(poolMatch.IsFinished);
        Assert.Equal(2, poolMatch.WinnerId);
        Assert.Equal("Bob B", vm.PoolStandings[0].CompetitorName);
        Assert.Equal(1, vm.PoolStandings[0].Wins);

        await vm.ResetPoolMatch(vm.PoolMatches[0]);
        Assert.False(poolMatch.IsFinished);
        Assert.Null(poolMatch.WinnerId);
        Assert.Null(poolMatch.LooserId);
        Assert.Equal(1, poolMatch.CurrentRound);
        Assert.Equal(string.Empty, poolMatch.RoundResultsJson);
        Assert.Empty(poolMatch.RefereeDatas!);
        await refRepo.Received(1).DeleteAsync(refDataPool);
    }

    [Fact]
    public async Task Taolu_SaveOrderScore_RecalculatesWinnerAndRankings()
    {
        var vm = CreateVm(out var compRepo, out var catRepo, out var drawRepo, out _, out _, out var orderRepo, out _);

        var c1 = new CompetitorModel { Id = 1, FirstName = "Li", LastName = "Wei" };
        var c2 = new CompetitorModel { Id = 2, FirstName = "Chen", LastName = "Yu" };
        var o1 = new DrawOrderModel { Id = 301, DrawId = 7, Order = 1, CompetitorId = 1, Competitor = c1, RoundResultsJson = "8.50", IsFinished = true };
        var o2 = new DrawOrderModel { Id = 302, DrawId = 7, Order = 2, CompetitorId = 2, Competitor = c2, RoundResultsJson = "8.80", IsFinished = true };

        compRepo.GetByIdAsync(1).Returns(new CompetitionModel { Id = 1, Name = "Open" });
        catRepo.GetAllAsync(Arg.Any<string[]>()).Returns(new List<CategoryModel>
        {
            new() { Id = 30, CompetitionId = 1, RoundType = RoundType.Order }
        });
        drawRepo.GetAllAsync(Arg.Any<string[]>()).Returns(new List<DrawModel>
        {
            new() { Id = 7, CategoryId = 30, DrawOrders = new List<DrawOrderModel> { o1, o2 } }
        });

        vm.CompetitionId = 1;
        vm.CurrentCategoryId = 30;
        await vm.LoadCategoriesAsync();

        // Au départ Chen Yu (8.80) est 1er
        Assert.Contains("Chen Yu", vm.TaoluWinnerName);

        // On modifie la note de Li Wei à 9.10
        vm.OrderItems[0].ScoreText = "9.10";
        await vm.SaveOrderScore(vm.OrderItems[0]);

        Assert.Equal("9.10", o1.RoundResultsJson);
        Assert.True(vm.OrderItems[0].IsWinner);
        Assert.Equal(1, vm.OrderItems[0].Rank);
        Assert.Equal(2, vm.OrderItems[1].Rank);
        Assert.Contains("Li Wei", vm.TaoluWinnerName);
    }
}
