using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class ServerDrawsOrderingTests
{
    private (ServerViewModel vm, IRepository<DrawKnockoutModel> koRepo, IRepository<CompetitionModel> compRepo) CreateViewModel()
    {
        var logger = Substitute.For<ILogger<ServerViewModel>>();
        var resourceProvider = new ResourceProvider();
        var popupService = Substitute.For<IPopupService>();
        var alertService = Substitute.For<IAlertService>();
        var bluetoothService = Substitute.For<IBluetoothService>();
        var bluetoothServer = Substitute.For<IBluetoothServer>();
        bluetoothServer.InstanceId.Returns(Guid.NewGuid());

        var competitionRepo = Substitute.For<IRepository<CompetitionModel>>();
        var drawKnockoutRepo = Substitute.For<IRepository<DrawKnockoutModel>>();
        var drawOrderRepo = Substitute.For<IRepository<DrawOrderModel>>();
        var drawPoolsRepo = Substitute.For<IRepository<DrawPoolsModel>>();
        var refereeDataRepo = Substitute.For<IRepository<RefereeDataModel>>();

        drawOrderRepo.GetAllAsync(Arg.Any<string[]>()).Returns((IReadOnlyList<DrawOrderModel>)new List<DrawOrderModel>());
        drawPoolsRepo.GetAllAsync(Arg.Any<string[]>()).Returns((IReadOnlyList<DrawPoolsModel>)new List<DrawPoolsModel>());

        var vm = new ServerViewModel(
            logger,
            resourceProvider,
            popupService,
            alertService,
            bluetoothService,
            bluetoothServer,
            competitionRepo,
            drawKnockoutRepo,
            drawOrderRepo,
            drawPoolsRepo,
            refereeDataRepo
        );

        return (vm, drawKnockoutRepo, competitionRepo);
    }

    [Fact]
    public async Task Init_KnockoutDraws_PetiteFinaleAppearsBeforeFinaleInDrawsList()
    {
        // Arrange
        var (vm, koRepo, compRepo) = CreateViewModel();

        var competition = new CompetitionModel { Id = 1, Name = "Championnat de France", Type = CompetitionType.Sanda };
        compRepo.GetAllAsync(Arg.Any<string[]>()).Returns((IReadOnlyList<CompetitionModel>)new List<CompetitionModel> { competition });

        var category = new CategoryModel
        {
            Id = 10,
            Type = CategoryType.Sanda,
            Genre = Genre.Men,
            WeightMin = 65,
            WeightMax = 70,
            CompetitionId = 1,
            RoundType = RoundType.Knockouts,
            HasBronzeMatch = true
        };

        var draw = new DrawModel
        {
            Id = 100,
            CategoryId = category.Id,
            Category = category
        };

        var comp1 = new CompetitorModel { Id = 1, LastName = "Dupont", FirstName = "Jean" };
        var comp2 = new CompetitorModel { Id = 2, LastName = "Martin", FirstName = "Paul" };
        var comp3 = new CompetitorModel { Id = 3, LastName = "Durand", FirstName = "Luc" };
        var comp4 = new CompetitorModel { Id = 4, LastName = "Lefebvre", FirstName = "Marc" };

        var demi1 = new DrawKnockoutModel
        {
            Id = 1,
            Order = 1,
            GlobalOrder = 1,
            DrawId = draw.Id,
            Draw = draw,
            Competitor1Id = comp1.Id,
            Competitor1 = comp1,
            Competitor2Id = comp2.Id,
            Competitor2 = comp2,
            IsBronzeMatch = false
        };

        var demi2 = new DrawKnockoutModel
        {
            Id = 2,
            Order = 2,
            GlobalOrder = 2,
            DrawId = draw.Id,
            Draw = draw,
            Competitor1Id = comp3.Id,
            Competitor1 = comp3,
            Competitor2Id = comp4.Id,
            Competitor2 = comp4,
            IsBronzeMatch = false
        };

        // Supposons même un cas où le GlobalOrder de la finale était inférieur à celui de la petite finale
        var finale = new DrawKnockoutModel
        {
            Id = 3,
            Order = 4,
            GlobalOrder = 3,
            DrawId = draw.Id,
            Draw = draw,
            IsBronzeMatch = false
        };

        var petiteFinale = new DrawKnockoutModel
        {
            Id = 4,
            Order = 3,
            GlobalOrder = 4,
            DrawId = draw.Id,
            Draw = draw,
            IsBronzeMatch = true
        };

        // Fournis dans un ordre quelconque par le repository
        var knockouts = new List<DrawKnockoutModel> { finale, demi2, petiteFinale, demi1 };
        koRepo.GetAllAsync(Arg.Any<string[]>()).Returns((IReadOnlyList<DrawKnockoutModel>)knockouts);

        // Act
        vm.CompetitionId = 1;
        await vm.Init();

        // Assert
        Assert.NotNull(vm.Draws);
        var drawsList = vm.Draws.OfType<DrawKnockoutModel>().ToList();
        Assert.Equal(4, drawsList.Count);

        int petiteFinaleIndex = drawsList.IndexOf(petiteFinale);
        int finaleIndex = drawsList.IndexOf(finale);

        Assert.True(petiteFinaleIndex >= 0, "La petite finale doit être présente dans Draws");
        Assert.True(finaleIndex >= 0, "La finale doit être présente dans Draws");
        Assert.True(petiteFinaleIndex < finaleIndex,
            $"Dans la liste Draws de ServerSandaPage, la petite finale (index {petiteFinaleIndex}) doit apparaître avant la finale (index {finaleIndex})");

        // Vérifier les libellés de chaque étape
        Assert.Equal("Demi-finale", demi1.StageName);
        Assert.Equal("Demi-finale", demi2.StageName);
        Assert.Equal("Petite finale", petiteFinale.StageName);
        Assert.Equal("Finale", finale.StageName);
    }
}
