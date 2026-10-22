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
        Assert.NotNull(vm.CurrentCompetition);
        Assert.Equal(42, vm.CurrentCompetition.Id);
        Assert.Equal("Championnat Test", vm.CurrentCompetition.Name);

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
        Assert.NotNull(vm.CurrentCompetition);
        Assert.Equal(99, vm.CurrentCompetition.Id);
        await _competitionRepo.Received(1).AddAsync(Arg.Is<CompetitionModel>(c => c.Id == 99));
    }
}
