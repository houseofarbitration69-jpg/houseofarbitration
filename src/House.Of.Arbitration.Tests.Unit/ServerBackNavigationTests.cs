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

public class ServerBackNavigationTests
{
    private (ServerViewModel vm, IAlertService alertService, IBluetoothServer bluetoothServer) CreateViewModel()
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

        return (vm, alertService, bluetoothServer);
    }

    [Fact]
    public async Task RequestBackNavigation_WhenServerNotStarted_NavigatesWithoutAlertOrStoppingServer()
    {
        // Arrange
        var (vm, alertService, bluetoothServer) = CreateViewModel();
        vm.IsServerStarted = false;

        // Act
        var result = await vm.RequestBackNavigation();

        // Assert
        await alertService.DidNotReceiveWithAnyArgs().ShowConfirmation(default!, default!);
        await bluetoothServer.DidNotReceiveWithAnyArgs().StopAdvertising();
        Assert.True(result);
    }

    [Fact]
    public async Task RequestBackNavigation_WhenServerStartedAndUserCancels_ShowsConfirmationAndDoesNotStopServer()
    {
        // Arrange
        var (vm, alertService, bluetoothServer) = CreateViewModel();
        vm.IsServerStarted = true;
        alertService.ShowConfirmation(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        // Act
        var result = await vm.RequestBackNavigation();

        // Assert
        await alertService.Received(1).ShowConfirmation(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("arrêter le serveur")),
            Arg.Any<string>(),
            Arg.Any<string>());
        await bluetoothServer.DidNotReceiveWithAnyArgs().StopAdvertising();
        Assert.True(vm.IsServerStarted);
        Assert.False(result);
    }

    [Fact]
    public async Task RequestBackNavigation_WhenServerStartedAndUserConfirms_ShowsConfirmationStopsServerAndReturnsTrue()
    {
        // Arrange
        var (vm, alertService, bluetoothServer) = CreateViewModel();
        vm.IsServerStarted = true;
        alertService.ShowConfirmation(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        // Act
        var result = await vm.RequestBackNavigation();

        // Assert
        await alertService.Received(1).ShowConfirmation(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("arrêter le serveur")),
            Arg.Any<string>(),
            Arg.Any<string>());
        await bluetoothServer.Received(1).StopAdvertising();
        Assert.False(vm.IsServerStarted);
        Assert.True(result);
    }

    [Fact]
    public async Task GoToDraws_WhenServerStartedAndUserCancels_DoesNotStopServer()
    {
        // Arrange
        var (vm, alertService, bluetoothServer) = CreateViewModel();
        vm.IsServerStarted = true;
        alertService.ShowConfirmation(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        // Act
        await vm.GoToDrawsCommand.ExecuteAsync(null);

        // Assert
        await alertService.Received(1).ShowConfirmation(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("arrêter le serveur")),
            Arg.Any<string>(),
            Arg.Any<string>());
        await bluetoothServer.DidNotReceiveWithAnyArgs().StopAdvertising();
        Assert.True(vm.IsServerStarted);
    }

    [Fact]
    public async Task GoToDraws_WhenServerStartedAndUserConfirms_StopsServer()
    {
        // Arrange
        var (vm, alertService, bluetoothServer) = CreateViewModel();
        vm.IsServerStarted = true;
        alertService.ShowConfirmation(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        // Act
        await vm.GoToDrawsCommand.ExecuteAsync(null);

        // Assert
        await alertService.Received(1).ShowConfirmation(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("arrêter le serveur")),
            Arg.Any<string>(),
            Arg.Any<string>());
        await bluetoothServer.Received(1).StopAdvertising();
        Assert.False(vm.IsServerStarted);
    }
}
