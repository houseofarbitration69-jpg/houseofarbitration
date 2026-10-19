using CommunityToolkit.Maui;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
using NSubstitute;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class CategoriesStepViewModelTests
{
    private readonly IPopupService _popupService;
    private readonly ResourceProvider _resourceProvider;
    private readonly IRepository<CategoryModel> _categoryRepository;
    private readonly IRepository<DrawModel> _drawRepository;
    private readonly IWarningService _warningService;

    public CategoriesStepViewModelTests()
    {
        _popupService = Substitute.For<IPopupService>();
        _resourceProvider = Substitute.For<ResourceProvider>();
        _categoryRepository = Substitute.For<IRepository<CategoryModel>>();
        _drawRepository = Substitute.For<IRepository<DrawModel>>();
        _warningService = Substitute.For<IWarningService>();
    }

    [Fact]
    public void CanExecute_ShouldBeTrueForBothCategories_Initially()
    {
        // Arrange
        var viewModel = new CategoriesStepViewModel(
            _popupService,
            _resourceProvider,
            _categoryRepository,
            _drawRepository,
            _warningService
        );

        var cat1 = new CategoryModel { Id = 1, Type = CategoryType.Sanda };
        var cat2 = new CategoryModel { Id = 2, Type = CategoryType.Taolu };

        // Act & Assert
        Assert.True(viewModel.EditCategoryCommand.CanExecute(cat1));
        Assert.True(viewModel.EditCategoryCommand.CanExecute(cat2));
        Assert.True(viewModel.ShowCompetitorsCommand.CanExecute(cat1));
        Assert.True(viewModel.ShowCompetitorsCommand.CanExecute(cat2));
        Assert.True(viewModel.ShowDrawCommand.CanExecute(cat1));
        Assert.True(viewModel.ShowDrawCommand.CanExecute(cat2));
        Assert.True(viewModel.DeleteCategoryCommand.CanExecute(cat1));
        Assert.True(viewModel.DeleteCategoryCommand.CanExecute(cat2));
    }
}
