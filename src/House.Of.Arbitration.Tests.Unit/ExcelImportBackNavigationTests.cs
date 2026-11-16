using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Competition;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class ExcelImportBackNavigationTests
{
    private ExcelImportViewModel CreateViewModel()
    {
        var logger = Substitute.For<ILogger<ExcelImportViewModel>>();
        var resourceProvider = new ResourceProvider();
        var popupService = Substitute.For<IPopupService>();
        var excelService = Substitute.For<IExcelImportExportService>();
        var fileDownloadService = Substitute.For<IFileDownloadService>();

        return new ExcelImportViewModel(
            logger,
            resourceProvider,
            popupService,
            excelService,
            fileDownloadService
        );
    }

    [Fact]
    public async Task BackCommand_WhenInvoked_DoesNotThrowException()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        var exception = await Record.ExceptionAsync(async () =>
        {
            await vm.BackCommand.ExecuteAsync(null);
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task NavigateToCommand_WhenInvokedWithTwoDots_DoesNotThrowException()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        var exception = await Record.ExceptionAsync(async () =>
        {
            await vm.NavigateToCommand.ExecuteAsync("..");
        });

        Assert.Null(exception);
    }
}
