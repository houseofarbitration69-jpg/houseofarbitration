using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class DrawPageViewModelTests
{
    private readonly IPopupService _popupService;
    private readonly ILogger<DrawPageViewModel> _logger;
    private readonly ResourceProvider _resourceProvider;
    private readonly IRepository<DrawModel> _repository;

    public DrawPageViewModelTests()
    {
        _popupService = Substitute.For<IPopupService>();
        _logger = Substitute.For<ILogger<DrawPageViewModel>>();
        _resourceProvider = Substitute.For<ResourceProvider>();
        _repository = Substitute.For<IRepository<DrawModel>>();
    }

    [Fact]
    public void Drop_ShouldSwapCompetitors_WhenSlotsAreDifferent()
    {
        // Arrange
        var viewModel = new DrawPageViewModel(
            _popupService,
            _logger,
            _resourceProvider,
            _repository);

        var competitor1 = new CompetitorModel { LastName = "Smith" };
        var competitor2 = new CompetitorModel { LastName = "Doe" };

        var slot1 = new BracketSlotViewModel { Competitor = competitor1 };
        var slot2 = new BracketSlotViewModel { Competitor = competitor2 };

        // Act
        viewModel.DragStartingCommand.Execute(slot1);
        viewModel.DropCommand.Execute(slot2);

        // Assert
        Assert.Equal(competitor2, slot1.Competitor);
        Assert.Equal(competitor1, slot2.Competitor);
        Assert.Null(viewModel.DraggedSlot);
    }

    [Fact]
    public void Drop_ShouldDoNothing_WhenDraggedSlotIsNull()
    {
        // Arrange
        var viewModel = new DrawPageViewModel(
            _popupService,
            _logger,
            _resourceProvider,
            _repository);

        var competitor1 = new CompetitorModel { LastName = "Smith" };
        var slot1 = new BracketSlotViewModel { Competitor = competitor1 };

        // Act
        viewModel.DropCommand.Execute(slot1);

        // Assert
        Assert.Equal(competitor1, slot1.Competitor);
        Assert.Null(viewModel.DraggedSlot);
    }

    [Fact]
    public void Drop_ShouldDoNothing_WhenTargetSlotIsSameAsDraggedSlot()
    {
        // Arrange
        var viewModel = new DrawPageViewModel(
            _popupService,
            _logger,
            _resourceProvider,
            _repository);

        var competitor1 = new CompetitorModel { LastName = "Smith" };
        var slot1 = new BracketSlotViewModel { Competitor = competitor1 };

        // Act
        viewModel.DragStartingCommand.Execute(slot1);
        viewModel.DropCommand.Execute(slot1);

        // Assert
        Assert.Equal(competitor1, slot1.Competitor);
        Assert.Equal(slot1, viewModel.DraggedSlot);
    }
}
