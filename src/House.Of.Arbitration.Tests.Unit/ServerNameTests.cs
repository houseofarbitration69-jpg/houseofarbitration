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

public class ServerNameTests
{
    [Fact]
    public void ServerSetupPopupViewModel_ApplyQueryAttributes_PreservesConfiguredServerName()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ServerSetupPopupViewModel>>();
        var resourceProvider = new ResourceProvider();
        var popupService = Substitute.For<IPopupService>();

        var vm = new ServerSetupPopupViewModel(logger, resourceProvider, popupService)
        {
            ServerName = "MonServeurCustom"
        };

        var competition = new CompetitionModel
        {
            Id = 1,
            Name = "Championnat de France",
            Type = CompetitionType.Sanda
        };

        var query = new Dictionary<string, object?>
        {
            { Constants.Navigation.Parameter.COMPETITION, competition }
        };

        // Act
        vm.ApplyQueryAttributes(query);

        // Assert: ServerName should NOT be overwritten by the competition name!
        Assert.Equal("MonServeurCustom", vm.ServerName);
        Assert.Equal(competition, vm.Competition);
    }

    [Fact]
    public async Task JudgeViewModel_HandleMessageAsync_ServerName_UpdatesServerNameAndTitle()
    {
        // Arrange
        var logger = Substitute.For<ILogger<JudgeViewModel>>();
        var resourceProvider = new ResourceProvider();
        var popupService = Substitute.For<IPopupService>();
        var alertService = Substitute.For<IAlertService>();
        var bluetoothClient = Substitute.For<IBluetoothClient>();
        var bluetoothService = Substitute.For<IBluetoothService>();
        var competitionRepo = Substitute.For<IRepository<CompetitionModel>>();
        var drawKnockoutRepo = Substitute.For<IRepository<DrawKnockoutModel>>();
        var drawOrderRepo = Substitute.For<IRepository<DrawOrderModel>>();
        var drawPoolsRepo = Substitute.For<IRepository<DrawPoolsModel>>();
        var mvtCodesRepo = Substitute.For<IRepository<MvtCodeModel>>();

        var vm = new JudgeViewModel(
            logger,
            resourceProvider,
            popupService,
            alertService,
            bluetoothClient,
            bluetoothService,
            competitionRepo,
            drawKnockoutRepo,
            drawOrderRepo,
            drawPoolsRepo,
            mvtCodesRepo,
            null
        );

        // Act
        await vm.HandleMessageAsync($"{Constants.Message.SERVER_NAME}Tapis 1");

        // Assert
        Assert.Equal("Tapis 1", vm.ServerName);
        Assert.Equal("JUDGE - Tapis 1", vm.Title);
    }

    [Fact]
    public void JudgeViewModel_ApplyQueryAttributes_ServerName_SetsServerNameAndTitle()
    {
        // Arrange
        var logger = Substitute.For<ILogger<JudgeViewModel>>();
        var resourceProvider = new ResourceProvider();
        var popupService = Substitute.For<IPopupService>();
        var alertService = Substitute.For<IAlertService>();
        var bluetoothClient = Substitute.For<IBluetoothClient>();
        var bluetoothService = Substitute.For<IBluetoothService>();
        var competitionRepo = Substitute.For<IRepository<CompetitionModel>>();
        var drawKnockoutRepo = Substitute.For<IRepository<DrawKnockoutModel>>();
        var drawOrderRepo = Substitute.For<IRepository<DrawOrderModel>>();
        var drawPoolsRepo = Substitute.For<IRepository<DrawPoolsModel>>();
        var mvtCodesRepo = Substitute.For<IRepository<MvtCodeModel>>();

        var vm = new JudgeViewModel(
            logger,
            resourceProvider,
            popupService,
            alertService,
            bluetoothClient,
            bluetoothService,
            competitionRepo,
            drawKnockoutRepo,
            drawOrderRepo,
            drawPoolsRepo,
            mvtCodesRepo,
            null
        );

        var query = new Dictionary<string, object>
        {
            { Constants.Navigation.Parameter.SERVER_NAME, "Ring Principal" }
        };

        // Act
        vm.ApplyQueryAttributes(query);

        // Assert
        Assert.Equal("Ring Principal", vm.ServerName);
        Assert.Equal("JUDGE - Ring Principal", vm.Title);
    }

    [Fact]
    public void JudgePositionViewModel_ApplyQueryAttributes_ServerName_SetsServerNameAndTitle()
    {
        // Arrange
        var logger = Substitute.For<ILogger<JudgePositionViewModel>>();
        var resourceProvider = new ResourceProvider();
        var popupService = Substitute.For<IPopupService>();
        var bluetoothClient = Substitute.For<IBluetoothClient>();
        var competitionRepo = Substitute.For<IRepository<CompetitionModel>>();

        var vm = new JudgePositionViewModel(
            logger,
            resourceProvider,
            popupService,
            bluetoothClient,
            competitionRepo
        );

        var query = new Dictionary<string, object>
        {
            { Constants.Navigation.Parameter.SERVER_NAME, "Arene A" }
        };

        // Act
        vm.ApplyQueryAttributes(query);

        // Assert
        Assert.Equal("Arene A", vm.ServerName);
        Assert.Equal("JUDGE - Arene A", vm.Title);
    }

    [Fact]
    public void DiscoveredDeviceModel_PropertyChanged_FiresForDisplayNameAndSignal()
    {
        // Arrange
        var model = new DiscoveredDeviceModel
        {
            DeviceId = "DEV_01"
        };

        string? changedProp = null;
        model.PropertyChanged += (s, e) => changedProp = e.PropertyName;

        // Act 1: setting Name
        model.Name = "MonServeur";

        // Assert 1: DisplayName should be notified
        Assert.Equal(nameof(DiscoveredDeviceModel.DisplayName), changedProp);
        Assert.Equal("MonServeur", model.DisplayName);

        // Act 2: setting Rssi
        model.Rssi = -55;

        // Assert 2: SignalIcon should be notified
        Assert.Equal(nameof(DiscoveredDeviceModel.SignalIcon), changedProp);
        Assert.Equal("󰤨", model.SignalIcon);
    }

    [Theory]
    [InlineData("SERVEUR_KUNGFU", "SERVEUR_KUNGFU")]
    [InlineData("", "DEV_MAC_123")]
    [InlineData(null, "DEV_MAC_123")]
    public void DiscoveredDeviceModel_DisplayName_ReturnsNameOrDeviceId(string? name, string expectedDisplay)
    {
        var model = new DiscoveredDeviceModel
        {
            DeviceId = "DEV_MAC_123",
            Name = name ?? string.Empty
        };

        Assert.Equal(expectedDisplay, model.DisplayName);
    }

    [Fact]
    public void BleManufacturerId_Constant_IsDefined()
    {
        Assert.Equal((ushort)0x4B46, Constants.BLE_MANUFACTURER_ID);
    }
}
