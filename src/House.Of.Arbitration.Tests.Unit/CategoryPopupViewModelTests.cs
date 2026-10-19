using CommunityToolkit.Maui;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class CategoryPopupViewModelTests
{
    private readonly ILogger<CategoryPopupViewModel> _logger;
    private readonly IPopupService _popupService;
    private readonly ResourceProvider _resourceProvider;

    public CategoryPopupViewModelTests()
    {
        _logger = Substitute.For<ILogger<CategoryPopupViewModel>>();
        _popupService = Substitute.For<IPopupService>();
        _resourceProvider = Substitute.For<ResourceProvider>();
    }

    [Theory]
    [InlineData(60, 65, false)]
    [InlineData(52.5, 56.0, false)]
    [InlineData(70, 65, true)]
    [InlineData(65, 65, true)]
    [InlineData(-5, 60, true)]
    [InlineData(60, -5, true)]
    public void HasWeightError_ShouldValidateMinAndMaxWeight(double min, double max, bool expectedError)
    {
        // Arrange
        var viewModel = new CategoryPopupViewModel(_logger, _popupService, _resourceProvider);

        // Act
        viewModel.WeightMin = min;
        viewModel.WeightMax = max;

        // Assert
        Assert.Equal(expectedError, viewModel.HasWeightError);
    }

    [Fact]
    public void HasWeightError_ShouldBeFalse_WhenOnlyOneBoundProvided()
    {
        // Arrange
        var viewModel = new CategoryPopupViewModel(_logger, _popupService, _resourceProvider);

        // Act & Assert (+100kg category)
        viewModel.WeightMin = 100;
        viewModel.WeightMax = null;
        Assert.False(viewModel.HasWeightError);

        // Act & Assert (-65kg category)
        viewModel.WeightMin = null;
        viewModel.WeightMax = 65;
        Assert.False(viewModel.HasWeightError);
    }

    [Fact]
    public void ValidateCommand_CannotExecute_WhenHasWeightErrorIsTrue()
    {
        // Arrange
        var viewModel = new CategoryPopupViewModel(_logger, _popupService, _resourceProvider);
        viewModel.SelectedType = viewModel.CategoryTypes.FirstOrDefault(x => x.Value == CategoryType.Sanda);
        viewModel.SelectedGenre = viewModel.Genres.FirstOrDefault(x => x.Value == Genre.Men);
        viewModel.SelectedRoundType = viewModel.RoundTypes.FirstOrDefault(x => x.Value == RoundType.Order);
        viewModel.SelectedAgeRange = new AgeRangeModel { Id = 1, Label = "Senior" };

        // Act & Assert: Valid weights
        viewModel.WeightMin = 60;
        viewModel.WeightMax = 65;
        Assert.False(viewModel.HasWeightError);
        Assert.True(viewModel.ValidateCommand.CanExecute(null));

        // Act & Assert: Invalid weights (min > max)
        viewModel.WeightMin = 75;
        viewModel.WeightMax = 65;
        Assert.True(viewModel.HasWeightError);
        Assert.False(viewModel.ValidateCommand.CanExecute(null));
    }
}
