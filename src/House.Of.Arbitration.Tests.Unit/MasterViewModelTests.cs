using CommunityToolkit.Maui;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class MasterViewModelTests
{
    private readonly ILogger<MasterViewModel> _logger;
    private readonly ResourceProvider _resourceProvider;
    private readonly IRepository<CompetitionModel> _repository;
    private readonly IPopupService _popupService;

    public MasterViewModelTests()
    {
        _logger = Substitute.For<ILogger<MasterViewModel>>();
        _resourceProvider = Substitute.For<ResourceProvider>();
        _repository = Substitute.For<IRepository<CompetitionModel>>();
        _popupService = Substitute.For<IPopupService>();
    }

    [Fact]
    public void CanExecute_ShouldBeTrueForBothCompetitions_Initially()
    {
        // Arrange
        var viewModel = new MasterViewModel(_logger, _resourceProvider, _repository, _popupService);
        var comp1 = new CompetitionModel { Id = 1, Name = "Comp 1" };
        var comp2 = new CompetitionModel { Id = 2, Name = "Comp 2" };

        // Act & Assert
        Assert.True(viewModel.EditCompetitionCommand.CanExecute(comp1));
        Assert.True(viewModel.EditCompetitionCommand.CanExecute(comp2));
        Assert.True(viewModel.DrawsCommand.CanExecute(comp1));
        Assert.True(viewModel.DrawsCommand.CanExecute(comp2));
        Assert.True(viewModel.DeleteCompetitionCommand.CanExecute(comp1));
        Assert.True(viewModel.DeleteCompetitionCommand.CanExecute(comp2));
        Assert.True(viewModel.StartCompetitionCommand.CanExecute(comp1));
        Assert.True(viewModel.StartCompetitionCommand.CanExecute(comp2));
    }
}
