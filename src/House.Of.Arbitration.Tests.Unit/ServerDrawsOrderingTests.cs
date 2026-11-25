using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using House.Of.Arbitration.Data;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
            refereeDataRepo,
            Substitute.For<IDrawPdfExportService>(),
            Substitute.For<IFileDownloadService>()
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

    [Fact]
    public async Task SetWinner_SemiFinalRedWins_PropagatesWinnerToFinale_AndLoserToPetiteFinale()
    {
        // Arrange : SQLite en mémoire avec de vrais repositories
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var compRepo = new Repository<CompetitionModel>(context);
        var koRepo = new Repository<DrawKnockoutModel>(context);
        var orderRepo = new Repository<DrawOrderModel>(context);
        var poolRepo = new Repository<DrawPoolsModel>(context);
        var refereeRepo = new Repository<RefereeDataModel>(context);

        var logger = Substitute.For<ILogger<ServerViewModel>>();
        var resourceProvider = new ResourceProvider();
        var popupService = Substitute.For<IPopupService>();
        var alertService = Substitute.For<IAlertService>();
        var bluetoothService = Substitute.For<IBluetoothService>();
        var bluetoothServer = Substitute.For<IBluetoothServer>();
        bluetoothServer.InstanceId.Returns(Guid.NewGuid());

        var vm = new ServerViewModel(
            logger,
            resourceProvider,
            popupService,
            alertService,
            bluetoothService,
            bluetoothServer,
            compRepo,
            koRepo,
            orderRepo,
            poolRepo,
            refereeRepo,
            Substitute.For<IDrawPdfExportService>(),
            Substitute.For<IFileDownloadService>()
        );

        // Données du tournoi
        var france = new CountryModel { IsoCode = "FR", Name = "France" };
        var comp1 = new CompetitorModel { FirstName = "Jean", LastName = "Dupont", CountryIsoCode = "FR" };
        var comp2 = new CompetitorModel { FirstName = "Paul", LastName = "Martin", CountryIsoCode = "FR" };
        var comp3 = new CompetitorModel { FirstName = "Luc", LastName = "Durand", CountryIsoCode = "FR" };
        var comp4 = new CompetitorModel { FirstName = "Marc", LastName = "Lefebvre", CountryIsoCode = "FR" };

        var competition = new CompetitionModel
        {
            Name = "Championnat Test",
            Type = CompetitionType.Sanda
        };
        await compRepo.AddAsync(competition);

        var category = new CategoryModel
        {
            CompetitionId = competition.Id,
            Type = CategoryType.Sanda,
            Genre = Genre.Men,
            WeightMin = 65,
            WeightMax = 70,
            RoundType = RoundType.Knockouts,
            HasBronzeMatch = true
        };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var draw = new DrawModel
        {
            CategoryId = category.Id
        };
        context.Draws.Add(draw);
        await context.SaveChangesAsync();

        context.Competitors.AddRange(comp1, comp2, comp3, comp4);
        await context.SaveChangesAsync();

        var demi1 = new DrawKnockoutModel
        {
            Order = 1,
            GlobalOrder = 1,
            DrawId = draw.Id,
            Competitor1Id = comp1.Id,
            Competitor2Id = comp2.Id,
            IsBronzeMatch = false
        };
        var demi2 = new DrawKnockoutModel
        {
            Order = 2,
            GlobalOrder = 2,
            DrawId = draw.Id,
            Competitor1Id = comp3.Id,
            Competitor2Id = comp4.Id,
            IsBronzeMatch = false
        };
        var petiteFinale = new DrawKnockoutModel
        {
            Order = 3,
            GlobalOrder = 3,
            DrawId = draw.Id,
            IsBronzeMatch = true
        };
        var finale = new DrawKnockoutModel
        {
            Order = 4,
            GlobalOrder = 4,
            DrawId = draw.Id,
            IsBronzeMatch = false
        };

        context.DrawsKnockouts.AddRange(demi1, demi2, petiteFinale, finale);
        await context.SaveChangesAsync();

        // Initialiser le ViewModel
        vm.CompetitionId = competition.Id;
        await vm.Init();

        // Vérifier l'état initial
        Assert.NotNull(vm.CurrentDraw);
        Assert.Equal(demi1.Id, ((DrawKnockoutModel)vm.CurrentDraw).Id);

        // Act : Premier round gagné par Rouge
        await vm.SetWinnerCommand.ExecuteAsync("Red");
        // Deuxième round gagné par Rouge -> Match terminé !
        await vm.SetWinnerCommand.ExecuteAsync("Red");

        // Assert : Vérifier que Finale a reçu comp1 (vainqueur DF1) et Petite Finale a reçu comp2 (perdant DF1)
        var updatedFinale = await koRepo.GetByIdAsync(finale.Id, "Competitor1", "Competitor2");
        var updatedPetiteFinale = await koRepo.GetByIdAsync(petiteFinale.Id, "Competitor1", "Competitor2");

        Assert.NotNull(updatedFinale);
        Assert.Equal(comp1.Id, updatedFinale.Competitor1Id);

        Assert.NotNull(updatedPetiteFinale);
        Assert.Equal(comp2.Id, updatedPetiteFinale.Competitor1Id);

        // Vérifier que la liste des tirages (Draws) dans le ViewModel contient bien les nouveaux compétiteurs
        Assert.NotNull(vm.Draws);
        var unfinishedMatches = vm.Draws.OfType<DrawKnockoutModel>().ToList();

        var finaleInDraws = unfinishedMatches.FirstOrDefault(m => m.Id == finale.Id);
        Assert.NotNull(finaleInDraws);
        Assert.NotNull(finaleInDraws.Competitor1);
        Assert.Equal(comp1.Id, finaleInDraws.Competitor1.Id);
        Assert.Equal("Dupont", finaleInDraws.Competitor1.LastName);

        var petiteFinaleInDraws = unfinishedMatches.FirstOrDefault(m => m.Id == petiteFinale.Id);
        Assert.NotNull(petiteFinaleInDraws);
        Assert.NotNull(petiteFinaleInDraws.Competitor1);
        Assert.Equal(comp2.Id, petiteFinaleInDraws.Competitor1.Id);
        Assert.Equal("Martin", petiteFinaleInDraws.Competitor1.LastName);

        // Act 2 : Jouer la Demi-finale 2 (CurrentDraw doit maintenant être Demi 2)
        Assert.Equal(demi2.Id, ((DrawKnockoutModel)vm.CurrentDraw).Id);

        // Victoire de Bleu (comp4) au tour 1 et tour 2
        await vm.SetWinnerCommand.ExecuteAsync("Blue");
        await vm.SetWinnerCommand.ExecuteAsync("Blue");

        // Assert 2 : Finale doit avoir comp1 en Rouge et comp4 en Bleu
        // Petite finale doit avoir comp2 en Rouge et comp3 en Bleu
        updatedFinale = await koRepo.GetByIdAsync(finale.Id, "Competitor1", "Competitor2");
        updatedPetiteFinale = await koRepo.GetByIdAsync(petiteFinale.Id, "Competitor1", "Competitor2");

        Assert.NotNull(updatedFinale);
        Assert.Equal(comp1.Id, updatedFinale.Competitor1Id);
        Assert.Equal(comp4.Id, updatedFinale.Competitor2Id);

        Assert.NotNull(updatedPetiteFinale);
        Assert.Equal(comp2.Id, updatedPetiteFinale.Competitor1Id);
        Assert.Equal(comp3.Id, updatedPetiteFinale.Competitor2Id);

        // Vérifier que vm.Draws contient bien la finale et petite finale complètes avec les deux compétiteurs
        unfinishedMatches = vm.Draws.OfType<DrawKnockoutModel>().ToList();

        finaleInDraws = unfinishedMatches.FirstOrDefault(m => m.Id == finale.Id);
        Assert.NotNull(finaleInDraws);
        Assert.NotNull(finaleInDraws.Competitor1);
        Assert.NotNull(finaleInDraws.Competitor2);
        Assert.Equal("Dupont", finaleInDraws.Competitor1.LastName);
        Assert.Equal("Lefebvre", finaleInDraws.Competitor2.LastName);

        petiteFinaleInDraws = unfinishedMatches.FirstOrDefault(m => m.Id == petiteFinale.Id);
        Assert.NotNull(petiteFinaleInDraws);
        Assert.NotNull(petiteFinaleInDraws.Competitor1);
        Assert.NotNull(petiteFinaleInDraws.Competitor2);
        Assert.Equal("Martin", petiteFinaleInDraws.Competitor1.LastName);
        Assert.Equal("Durand", petiteFinaleInDraws.Competitor2.LastName);
    }

    [Fact]
    public async Task NextMatch_SemiFinalFinished_PropagatesWinnerToFinale_AndLoserToPetiteFinale_WithMultipleCategories()
    {
        // Arrange : SQLite en mémoire avec de vrais repositories
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var compRepo = new Repository<CompetitionModel>(context);
        var koRepo = new Repository<DrawKnockoutModel>(context);
        var orderRepo = new Repository<DrawOrderModel>(context);
        var poolRepo = new Repository<DrawPoolsModel>(context);
        var refereeRepo = new Repository<RefereeDataModel>(context);

        var logger = Substitute.For<ILogger<ServerViewModel>>();
        var resourceProvider = new ResourceProvider();
        var popupService = Substitute.For<IPopupService>();
        var alertService = Substitute.For<IAlertService>();
        var bluetoothService = Substitute.For<IBluetoothService>();
        var bluetoothServer = Substitute.For<IBluetoothServer>();
        bluetoothServer.InstanceId.Returns(Guid.NewGuid());

        var vm = new ServerViewModel(
            logger,
            resourceProvider,
            popupService,
            alertService,
            bluetoothService,
            bluetoothServer,
            compRepo,
            koRepo,
            orderRepo,
            poolRepo,
            refereeRepo,
            Substitute.For<IDrawPdfExportService>(),
            Substitute.For<IFileDownloadService>()
        );

        var competition = new CompetitionModel
        {
            Name = "Championnat Multi-Catégories",
            Type = CompetitionType.Sanda
        };
        await compRepo.AddAsync(competition);

        // Catégorie 1 : 4 combattants avec petite finale
        var cat1 = new CategoryModel
        {
            CompetitionId = competition.Id,
            Type = CategoryType.Sanda,
            Genre = Genre.Men,
            WeightMin = 65,
            WeightMax = 70,
            RoundType = RoundType.Knockouts,
            HasBronzeMatch = true
        };
        // Catégorie 2 : autre catégorie pour s'assurer que targetDrawId ne mélange pas les tirages
        var cat2 = new CategoryModel
        {
            CompetitionId = competition.Id,
            Type = CategoryType.Sanda,
            Genre = Genre.Men,
            WeightMin = 70,
            WeightMax = 75,
            RoundType = RoundType.Knockouts,
            HasBronzeMatch = true
        };
        context.Categories.AddRange(cat1, cat2);
        await context.SaveChangesAsync();

        var draw1 = new DrawModel { CategoryId = cat1.Id };
        var draw2 = new DrawModel { CategoryId = cat2.Id };
        context.Draws.AddRange(draw1, draw2);
        await context.SaveChangesAsync();

        var c1 = new CompetitorModel { FirstName = "Jean", LastName = "Dupont", CountryIsoCode = "FR" };
        var c2 = new CompetitorModel { FirstName = "Paul", LastName = "Martin", CountryIsoCode = "FR" };
        var c3 = new CompetitorModel { FirstName = "Luc", LastName = "Durand", CountryIsoCode = "FR" };
        var c4 = new CompetitorModel { FirstName = "Marc", LastName = "Lefebvre", CountryIsoCode = "FR" };
        var c5 = new CompetitorModel { FirstName = "Pierre", LastName = "Autre", CountryIsoCode = "FR" };
        var c6 = new CompetitorModel { FirstName = "Jacques", LastName = "Autre2", CountryIsoCode = "FR" };
        context.Competitors.AddRange(c1, c2, c3, c4, c5, c6);
        await context.SaveChangesAsync();

        // Catégorie 1 : Demi 1, Demi 2, Petite finale, Finale
        var cat1Demi1 = new DrawKnockoutModel { Order = 1, GlobalOrder = 1, Draw = draw1, Competitor1Id = c1.Id, Competitor2Id = c2.Id };
        var cat1Demi2 = new DrawKnockoutModel { Order = 2, GlobalOrder = 2, Draw = draw1, Competitor1Id = c3.Id, Competitor2Id = c4.Id };
        var cat1PetiteFinale = new DrawKnockoutModel { Order = 3, GlobalOrder = 3, Draw = draw1, IsBronzeMatch = true };
        var cat1Finale = new DrawKnockoutModel { Order = 4, GlobalOrder = 4, Draw = draw1 };

        // Catégorie 2 : match pour vérifier qu'il n'est pas pollué
        var cat2Demi1 = new DrawKnockoutModel { Order = 1, GlobalOrder = 5, Draw = draw2, Competitor1Id = c5.Id, Competitor2Id = c6.Id };
        var cat2Finale = new DrawKnockoutModel { Order = 2, GlobalOrder = 6, Draw = draw2 };

        context.DrawsKnockouts.AddRange(cat1Demi1, cat1Demi2, cat1PetiteFinale, cat1Finale, cat2Demi1, cat2Finale);
        await context.SaveChangesAsync();

        // Initialiser
        vm.CompetitionId = competition.Id;
        await vm.Init();

        // Act : L'utilisateur utilise NextMatchCommand sur la Demi-finale 1 (Rouge par défaut gagne)
        Assert.NotNull(vm.CurrentDraw);
        Assert.Equal(cat1Demi1.Id, ((DrawKnockoutModel)vm.CurrentDraw).Id);

        await vm.NextMatchCommand.ExecuteAsync(null);

        // Assert : Finale de cat1 doit avoir c1 (vainqueur Demi 1) et Petite finale doit avoir c2 (perdant Demi 1)
        var updatedFinaleCat1 = await koRepo.GetByIdAsync(cat1Finale.Id, "Competitor1", "Competitor2");
        var updatedPetiteFinaleCat1 = await koRepo.GetByIdAsync(cat1PetiteFinale.Id, "Competitor1", "Competitor2");

        Assert.NotNull(updatedFinaleCat1);
        Assert.Equal(c1.Id, updatedFinaleCat1.Competitor1Id);

        Assert.NotNull(updatedPetiteFinaleCat1);
        Assert.Equal(c2.Id, updatedPetiteFinaleCat1.Competitor1Id);

        // S'assurer que cat2 n'a PAS été touché
        var updatedFinaleCat2 = await koRepo.GetByIdAsync(cat2Finale.Id, "Competitor1", "Competitor2");
        Assert.NotNull(updatedFinaleCat2);
        Assert.Null(updatedFinaleCat2.Competitor1Id);

        // Vérifier que vm.Draws contient bien Finale et Petite Finale avec les bons compétiteurs
        var drawsKnockouts = vm.Draws.OfType<DrawKnockoutModel>().ToList();
        var finaleInVm = drawsKnockouts.FirstOrDefault(k => k.Id == cat1Finale.Id);
        var petiteFinaleInVm = drawsKnockouts.FirstOrDefault(k => k.Id == cat1PetiteFinale.Id);

        Assert.NotNull(finaleInVm);
        Assert.NotNull(finaleInVm.Competitor1);
        Assert.Equal("Dupont", finaleInVm.Competitor1.LastName);

        Assert.NotNull(petiteFinaleInVm);
        Assert.NotNull(petiteFinaleInVm.Competitor1);
        Assert.Equal("Martin", petiteFinaleInVm.Competitor1.LastName);
    }

    [Fact]
    public async Task Init_UpdatesRemainingMatchesCount_WhenMatchFinishes()
    {
        var (vm, koRepo, compRepo) = CreateViewModel();

        var competition = new CompetitionModel { Id = 1, Name = "Sanda", Type = CompetitionType.Sanda };
        compRepo.GetAllAsync(Arg.Any<string[]>()).Returns((IReadOnlyList<CompetitionModel>)new List<CompetitionModel> { competition });

        var category = new CategoryModel
        {
            Id = 10,
            Type = CategoryType.Sanda,
            Genre = Genre.Men,
            CompetitionId = 1,
            RoundType = RoundType.Knockouts
        };
        var draw = new DrawModel { Id = 100, CategoryId = category.Id, Category = category };

        var c1 = new CompetitorModel { Id = 1, LastName = "A", FirstName = "1" };
        var c2 = new CompetitorModel { Id = 2, LastName = "B", FirstName = "2" };
        var c3 = new CompetitorModel { Id = 3, LastName = "C", FirstName = "3" };

        // 1 bye (finished) + 1 demi-finale + 1 finale = 2 vrais combats restants
        var byeMatch = new DrawKnockoutModel
        {
            Id = 1, Order = 1, GlobalOrder = 0, DrawId = draw.Id, Draw = draw,
            Competitor1Id = c1.Id, Competitor1 = c1, IsFinished = true, WinnerId = c1.Id
        };
        var semiMatch = new DrawKnockoutModel
        {
            Id = 2, Order = 2, GlobalOrder = 1, DrawId = draw.Id, Draw = draw,
            Competitor1Id = c2.Id, Competitor1 = c2, Competitor2Id = c3.Id, Competitor2 = c3, IsFinished = false
        };
        var finalMatch = new DrawKnockoutModel
        {
            Id = 3, Order = 3, GlobalOrder = 2, DrawId = draw.Id, Draw = draw,
            Competitor1Id = c1.Id, Competitor1 = c1, IsFinished = false
        };
        draw.DrawKnockouts = new List<DrawKnockoutModel> { byeMatch, semiMatch, finalMatch };

        koRepo.GetAllAsync(Arg.Any<string[]>()).Returns((IReadOnlyList<DrawKnockoutModel>)draw.DrawKnockouts);

        await vm.Init();
        Assert.Equal(2, vm.TotalMatches);
        Assert.Equal(2, vm.RemainingMatchesCount);

        // Simuler la fin de la demi-finale et rappel de Init()
        semiMatch.IsFinished = true;
        await vm.Init();
        Assert.Equal(2, vm.TotalMatches);
        Assert.Equal(1, vm.RemainingMatchesCount);
    }
}
