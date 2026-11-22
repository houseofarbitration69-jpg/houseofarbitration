using System.Text.Json;
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

public class JudgeCompetitionDataTests
{
    private readonly ILogger<JudgeViewModel> _logger = Substitute.For<ILogger<JudgeViewModel>>();
    private readonly ResourceProvider _resourceProvider = new();
    private readonly IPopupService _popupService = Substitute.For<IPopupService>();
    private readonly IAlertService _alertService = Substitute.For<IAlertService>();
    private readonly IBluetoothClient _bluetoothClient = Substitute.For<IBluetoothClient>();
    private readonly IBluetoothService _bluetoothService = Substitute.For<IBluetoothService>();
    private readonly IRepository<CompetitionModel> _competitionRepo = Substitute.For<IRepository<CompetitionModel>>();
    private readonly IRepository<DrawKnockoutModel> _drawKnockoutRepo = Substitute.For<IRepository<DrawKnockoutModel>>();
    private readonly IRepository<DrawOrderModel> _drawOrderRepo = Substitute.For<IRepository<DrawOrderModel>>();
    private readonly IRepository<DrawPoolsModel> _drawPoolsRepo = Substitute.For<IRepository<DrawPoolsModel>>();
    private readonly IRepository<MvtCodeModel> _mvtCodesRepo = Substitute.For<IRepository<MvtCodeModel>>();
    private readonly IDatabaseService _databaseService = Substitute.For<IDatabaseService>();

    private JudgeViewModel CreateViewModel(IDatabaseService? databaseService = null)
    {
        return new JudgeViewModel(
            _logger,
            _resourceProvider,
            _popupService,
            _alertService,
            _bluetoothClient,
            _bluetoothService,
            _competitionRepo,
            _drawKnockoutRepo,
            _drawOrderRepo,
            _drawPoolsRepo,
            _mvtCodesRepo,
            databaseService
        );
    }

    [Fact]
    public async Task HandleMessageAsync_CompetitionData_CallsImportCompetitionDataAsync()
    {
        // Arrange
        var vm = CreateViewModel(_databaseService);

        var competition = new CompetitionModel
        {
            Id = 42,
            Name = "Championnat Test",
            Type = CompetitionType.Sanda,
            Categories = new List<CategoryModel>
            {
                new CategoryModel
                {
                    Id = 10,
                    CompetitionId = 42,
                    Type = CategoryType.Sanda,
                    Genre = Genre.Men,
                    Competitors = new List<CompetitorCategoryModel>
                    {
                        new CompetitorCategoryModel
                        {
                            Id = 1,
                            CategoryId = 10,
                            CompetitorId = 101,
                            Competitor = new CompetitorModel
                            {
                                Id = 101,
                                FirstName = "Jean",
                                LastName = "Dupont",
                                CountryIsoCode = "FR"
                            }
                        }
                    },
                    Draw = new DrawModel
                    {
                        Id = 20,
                        CategoryId = 10,
                        DrawKnockouts = new List<DrawKnockoutModel>
                        {
                            new DrawKnockoutModel
                            {
                                Id = 30,
                                DrawId = 20,
                                Order = 1,
                                Competitor1Id = 101
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(competition);
        var message = $"{Constants.Message.COMPETITION_DATA}{json}";

        // Act
        await vm.HandleMessageAsync(message);

        // Assert
        Assert.NotNull(vm.Competition);
        Assert.Equal(42, vm.Competition.Id);
        Assert.Equal("Championnat Test", vm.Competition.Name);

        await _databaseService.Received(1).ImportCompetitionDataAsync(Arg.Is<CompetitionModel>(c =>
            c.Id == 42 &&
            c.Name == "Championnat Test" &&
            c.Categories.Count == 1 &&
            c.Categories[0].Competitors.Count == 1 &&
            c.Categories[0].Draw != null &&
            c.Categories[0].Draw.DrawKnockouts != null &&
            c.Categories[0].Draw.DrawKnockouts.Count == 1
        ));

        await _alertService.Received(1).ShowToast("Compétition reçue : Championnat Test");
    }

    [Fact]
    public async Task HandleMessageAsync_CompetitionData_FallbackWhenDatabaseServiceNull()
    {
        // Arrange
        var vm = CreateViewModel(null);

        var competition = new CompetitionModel
        {
            Id = 99,
            Name = "Fallback Competition",
            Type = CompetitionType.Taolu
        };

        _competitionRepo.GetByIdAsync(99).Returns((CompetitionModel?)null);

        var json = JsonSerializer.Serialize(competition);
        var message = $"{Constants.Message.COMPETITION_DATA}{json}";

        // Act
        await vm.HandleMessageAsync(message);

        // Assert
        Assert.NotNull(vm.Competition);
        Assert.Equal(99, vm.Competition.Id);
        await _competitionRepo.Received(1).AddAsync(Arg.Is<CompetitionModel>(c => c.Id == 99));
    }

    [Fact]
    public async Task HandleMessageAsync_MatchInfo_CallsImportMatchDataAsync_AndUpdatesCurrentDraw()
    {
        // Arrange
        var vm = CreateViewModel(_databaseService);

        var matchData = new MatchInfoData
        {
            Id = 5,
            Type = RoundType.Knockouts,
            Round = 2,
            CategoryName = "Sanda -70kg",
            Competitor1 = new CompetitorModel { Id = 10, FirstName = "Luc", LastName = "Rouge" },
            Competitor2 = new CompetitorModel { Id = 20, FirstName = "Paul", LastName = "Bleu" },
            DrawKnockout = new DrawKnockoutModel
            {
                Id = 5,
                Order = 1,
                CurrentRound = 2,
                Competitor1Id = 10,
                Competitor2Id = 20
            }
        };

        var json = JsonSerializer.Serialize(matchData);
        var message = $"{Constants.Message.MATCH_INFO}{json}";

        // Act
        await vm.HandleMessageAsync(message);

        // Assert
        await _databaseService.Received(1).ImportMatchDataAsync(Arg.Is<MatchInfoData>(m =>
            m.Id == 5 &&
            m.Round == 2 &&
            m.Type == RoundType.Knockouts
        ));

        Assert.Equal(2, vm.CurrentRound);
        Assert.NotNull(vm.CurrentDraw);
        Assert.Equal(5, vm.CurrentDraw.Id);
        Assert.Equal("Sanda -70kg", vm.CategoryName);
    }

    [Fact]
    public void BluetoothTransferManager_ChunksLargeCompetitionData_AndReassemblesCorrectly()
    {
        // Arrange
        var transferManager = new House.Of.Arbitration.Services.BluetoothTransferManager();
        var competition = new CompetitionModel
        {
            Id = 1,
            Name = "Grand Tournoi National de Sanda & Taolu - Edition 2026",
            Type = CompetitionType.Sanda,
            Categories = new List<CategoryModel>()
        };

        for (int i = 1; i <= 10; i++)
        {
            competition.Categories.Add(new CategoryModel
            {
                Id = i,
                Genre = Genre.Men,
                WeightMin = 60 + i,
                WeightMax = 65 + i,
                RoundType = RoundType.Knockouts,
                Competitors = new List<CompetitorCategoryModel>
                {
                    new CompetitorCategoryModel
                    {
                        Id = i * 2,
                        CompetitorId = i * 2,
                        CategoryId = i,
                        Competitor = new CompetitorModel
                        {
                            Id = i * 2,
                            FirstName = $"Jean-{i}",
                            LastName = $"Dupont-{i}",
                            Club = $"Club Dragon Kung Fu {i}"
                        }
                    }
                }
            });
        }

        var json = JsonSerializer.Serialize(competition);
        var originalMessage = $"{Constants.Message.COMPETITION_DATA}{json}";

        // Act: Préparer les paquets
        var chunks = transferManager.PrepareMessagesForSending(originalMessage).ToList();

        // Assert: Doit être découpé en plusieurs chunks de taille <= 250 octets
        Assert.True(chunks.Count > 1, $"Devrait être découpé en plusieurs chunks mais Count={chunks.Count}");
        foreach (var chunk in chunks)
        {
            Assert.StartsWith("CH:", chunk);
            Assert.True(chunk.Length <= 250, $"Chaque chunk doit être <= 250 caractères, mais longueur = {chunk.Length}");
        }

        // Act: Réassembler côté récepteur
        var receiverManager = new House.Of.Arbitration.Services.BluetoothTransferManager();
        string? reassembled = null;
        for (int i = 0; i < chunks.Count; i++)
        {
            reassembled = receiverManager.ProcessReceivedData(chunks[i]);
            if (i < chunks.Count - 1)
            {
                Assert.Null(reassembled);
            }
        }

        // Assert: Message final réassemblé et identique
        Assert.NotNull(reassembled);
        Assert.Equal(originalMessage, reassembled);
        Assert.StartsWith(Constants.Message.COMPETITION_DATA, reassembled);
    }
}
