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

/// <summary>
/// Tests pour le scénario de déconnexion/reconnexion du juge avec scores hors-ligne.
/// </summary>
public class JudgeReconnectTests
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

    private JudgeViewModel CreateViewModel(string serverId = "server-001")
    {
        var vm = new JudgeViewModel(
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
            _mvtCodesRepo
        );
        vm.ServerId = serverId;
        return vm;
    }

    /// <summary>
    /// Après reconnexion, ServerId doit être mis à jour avec le deviceId du serveur reconnecté.
    /// </summary>
    [Fact]
    public async Task HandleDeviceConnectedAsync_UpdatesServerId()
    {
        // Arrange
        var vm = CreateViewModel("old-server-id");

        // Act - simule la reconnexion avec un nouveau deviceId
        await vm.HandleDeviceConnectedAsync("new-server-id");

        // Assert
        Assert.Equal("new-server-id", vm.ServerId);
    }

    /// <summary>
    /// Après reconnexion, IsConnected doit repasser à true.
    /// </summary>
    [Fact]
    public async Task HandleDeviceConnectedAsync_SetsIsConnectedTrue()
    {
        // Arrange
        var vm = CreateViewModel("server-001");
        await vm.HandleDeviceDisconnectedAsync();
        Assert.False(vm.IsConnected);

        // Act
        await vm.HandleDeviceConnectedAsync("server-001");

        // Assert
        Assert.True(vm.IsConnected);
    }

    /// <summary>
    /// Après reconnexion, JUDGE_POSITION doit être envoyé avec le format attendu (ex: "A_1" pour Taolu ou "2" pour Sanda).
    /// </summary>
    [Fact]
    public async Task HandleDeviceConnectedAsync_ReregistersJudgePosition_Taolu()
    {
        // Arrange
        var vm = CreateViewModel("server-001");
        vm.JudgeGroup = "C";
        vm.JudgePosition = 1;
        await vm.HandleDeviceDisconnectedAsync();

        // Act
        await vm.HandleDeviceConnectedAsync("server-001");

        // Assert
        await _bluetoothClient.Received().SendMessage(
            Arg.Is<string>(id => id == "server-001"),
            Arg.Is<string>(msg => msg == $"{Constants.Message.JUDGE_POSITION}C_1")
        );
    }

    /// <summary>
    /// Après reconnexion en Sanda (sans groupe), JUDGE_POSITION doit être envoyé sous forme de numéro simple (ex: "2").
    /// </summary>
    [Fact]
    public async Task HandleDeviceConnectedAsync_ReregistersJudgePosition_Sanda()
    {
        // Arrange
        var vm = CreateViewModel("server-001");
        vm.JudgeGroup = string.Empty;
        vm.JudgePosition = 2;
        await vm.HandleDeviceDisconnectedAsync();

        // Act
        await vm.HandleDeviceConnectedAsync("server-001");

        // Assert
        await _bluetoothClient.Received().SendMessage(
            Arg.Is<string>(id => id == "server-001"),
            Arg.Is<string>(msg => msg == $"{Constants.Message.JUDGE_POSITION}2")
        );
    }

    /// <summary>
    /// Après reconnexion, GET_MATCH doit être envoyé pour récupérer l'état courant du match.
    /// </summary>
    [Fact]
    public async Task HandleDeviceConnectedAsync_RequestsCurrentMatch()
    {
        // Arrange
        var vm = CreateViewModel("server-001");
        vm.JudgeGroup = "B";
        vm.JudgePosition = 1;
        await vm.HandleDeviceDisconnectedAsync();

        // Act
        await vm.HandleDeviceConnectedAsync("server-001");

        // Assert
        await _bluetoothClient.Received().SendMessage(
            Arg.Is<string>(id => id == "server-001"),
            Arg.Is<string>(msg => msg == Constants.Message.GET_MATCH)
        );
    }

    /// <summary>
    /// Après reconnexion avec HasUnsyncedChanges=true, les scores doivent être synchronisés.
    /// </summary>
    [Fact]
    public async Task HandleDeviceConnectedAsync_SyncsPendingScores_WhenHasUnsyncedChanges()
    {
        // Arrange
        var vm = CreateViewModel("server-001");
        vm.JudgeGroup = "A";
        vm.JudgePosition = 1;
        await vm.HandleDeviceDisconnectedAsync();

        // Marquer des changements non synchronisés
        vm.HasUnsyncedChanges = true;

        // Act - reconnexion
        await vm.HandleDeviceConnectedAsync("server-001");

        // Assert - JUDGE_SYNC_STATE doit avoir été envoyé
        await _bluetoothClient.Received().SendMessage(
            Arg.Is<string>(id => id == "server-001"),
            Arg.Is<string>(msg => msg.StartsWith(Constants.Message.JUDGE_SYNC_STATE))
        );
        Assert.False(vm.HasUnsyncedChanges);
    }

    /// <summary>
    /// En Sanda, lorsque le juge note hors-ligne (AddRedScore/AddBlueScore),
    /// les points s'accumulent dans _unsyncedScores et HasUnsyncedChanges passe à true.
    /// À la reconnexion, tous les scores sont envoyés avec le PositionCode du juge.
    /// </summary>
    [Fact]
    public async Task Sanda_OfflineScoring_SyncsAllScoresOnReconnect()
    {
        // Arrange
        var vm = CreateViewModel("server-001");
        vm.JudgeGroup = string.Empty;
        vm.JudgePosition = 1;

        // Passer hors-ligne
        await vm.HandleDeviceDisconnectedAsync();
        Assert.False(vm.IsConnected);

        // Noter hors-ligne : 2 rouges, 1 bleu
        await vm.AddRedScoreCommand.ExecuteAsync(null);
        await vm.AddRedScoreCommand.ExecuteAsync(null);
        await vm.AddBlueScoreCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.RedScore);
        Assert.Equal(1, vm.BlueScore);
        Assert.True(vm.HasUnsyncedChanges);
        Assert.Equal(3, vm.UnsyncedScores.Count);

        // Act - reconnexion
        await vm.HandleDeviceConnectedAsync("server-001");

        // Assert
        Assert.True(vm.IsConnected);
        Assert.False(vm.HasUnsyncedChanges);
        Assert.Empty(vm.UnsyncedScores);

        // 3 scores individuels + 1 sync state
        await _bluetoothClient.Received(3).SendMessage(
            Arg.Is<string>(id => id == "server-001"),
            Arg.Is<string>(msg => msg.StartsWith(Constants.Message.JUDGE_SCORE))
        );
        await _bluetoothClient.Received(1).SendMessage(
            Arg.Is<string>(id => id == "server-001"),
            Arg.Is<string>(msg => msg.StartsWith(Constants.Message.JUDGE_SYNC_STATE))
        );
    }

    /// <summary>
    /// _lastServerDeviceId doit être mémorisé dès la déconnexion pour permettre
    /// la reconnexion automatique via ConnectToDevice.
    /// </summary>
    [Fact]
    public async Task HandleDeviceDisconnectedAsync_SavesLastServerDeviceId_ForAutoReconnect()
    {
        // Arrange
        var vm = CreateViewModel("my-server-123");

        // Act
        await vm.HandleDeviceDisconnectedAsync();

        // La commande Reconnect doit appeler ConnectToDevice avec l'ID sauvegardé
        await vm.ReconnectCommand.ExecuteAsync(null);

        // Assert
        await _bluetoothClient.Received().ConnectToDevice("my-server-123");
    }

    /// <summary>
    /// La commande Reconnect ne doit pas appeler ConnectToDevice plusieurs fois en parallèle.
    /// </summary>
    [Fact]
    public async Task Reconnect_IsReentrantSafe()
    {
        // Arrange
        var vm = CreateViewModel("server-001");
        await vm.HandleDeviceDisconnectedAsync();

        var callCount = 0;
        _bluetoothClient
            .ConnectToDevice(Arg.Any<string>())
            .Returns(async _ =>
            {
                callCount++;
                await Task.Delay(200);
            });

        // Act - deux appels quasi-simultanés
        var t1 = vm.ReconnectCommand.ExecuteAsync(null);
        await Task.Delay(10);
        var t2 = vm.ReconnectCommand.ExecuteAsync(null);

        await Task.WhenAll(t1, t2);

        // Assert - ConnectToDevice ne doit être appelé qu'une seule fois
        Assert.Equal(1, callCount);
    }
}
