using System.Text.Json;
using CommunityToolkit.Maui;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Server.Cli;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class ScoreTimerBlockingTests
{
    private readonly ILogger<JudgeViewModel> _judgeLogger = Substitute.For<ILogger<JudgeViewModel>>();
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

    private JudgeViewModel CreateJudgeViewModel(string serverId = "server-001")
    {
        var vm = new JudgeViewModel(
            _judgeLogger,
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
        vm.IsConnected = true;
        vm.JudgeGroup = string.Empty;
        vm.JudgePosition = 1;
        return vm;
    }

    #region JudgeViewModel Tests

    [Fact]
    public void JudgeViewModel_InitialState_CannotSendScore()
    {
        var vm = CreateJudgeViewModel();

        Assert.False(vm.IsTimerRunning);
        Assert.False(vm.CanSendScore);
    }

    [Fact]
    public async Task JudgeViewModel_WhenTimerStoppedOrPaused_BlocksAllScoreCommands()
    {
        var vm = CreateJudgeViewModel();
        vm.Score = 5.0;

        // Le chronomètre n'a pas été démarré (IsTimerRunning == false)
        Assert.False(vm.CanSendScore);

        // 1. Essai d'ajout Sanda Rouge
        await vm.AddRedScoreCommand.ExecuteAsync(null);
        Assert.Equal(0, vm.RedScore);

        // 2. Essai d'ajout Sanda Bleu
        await vm.AddBlueScoreCommand.ExecuteAsync(null);
        Assert.Equal(0, vm.BlueScore);

        // 3. Essai de déduction libre
        await vm.DeductPointsCommand.ExecuteAsync(0.5);
        Assert.Equal(5.0, vm.Score);
        Assert.Empty(vm.Codes);

        // 4. Essai d'application d'un code
        var code = new MvtCodeModel { Code = "50", Movement = "Test", Value = 0.2 };
        await vm.ApplyCodeCommand.ExecuteAsync(code);
        Assert.Equal(5.0, vm.Score);
        Assert.Empty(vm.Codes);

        // Aucun message envoyé
        await _bluetoothClient.DidNotReceive().SendMessage(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task JudgeViewModel_TimerLifecycle_ControlsCanSendScoreAndAllowsScoringWhenRunning()
    {
        var vm = CreateJudgeViewModel();
        vm.Score = 5.0;

        // 1. Démarrer le chronomètre
        await vm.HandleMessageAsync(Constants.Message.TIMER_START);
        Assert.True(vm.IsTimerRunning);
        Assert.True(vm.CanSendScore);

        // Noter en mode Sanda
        await vm.AddRedScoreCommand.ExecuteAsync(null);
        Assert.Equal(1, vm.RedScore);
        await _bluetoothClient.Received(1).SendMessage(
            Arg.Is<string>(id => id == "server-001"),
            Arg.Is<string>(msg => msg.StartsWith(Constants.Message.JUDGE_SCORE))
        );

        // 2. Mettre en pause le chronomètre
        await vm.HandleMessageAsync(Constants.Message.TIMER_PAUSE);
        Assert.False(vm.IsTimerRunning);
        Assert.False(vm.CanSendScore);

        // Tenter d'ajouter un point en pause : doit être refusé
        await vm.AddRedScoreCommand.ExecuteAsync(null);
        Assert.Equal(1, vm.RedScore); // inchangé
        await _bluetoothClient.Received(1).SendMessage(Arg.Any<string>(), Arg.Any<string>()); // toujours 1 seul appel

        // 3. Redémarrer
        await vm.HandleMessageAsync(Constants.Message.TIMER_START);
        Assert.True(vm.CanSendScore);

        // Noter une déduction
        await vm.DeductPointsCommand.ExecuteAsync(0.2);
        Assert.Equal(4.8, vm.Score);
        Assert.Single(vm.Codes);

        // 4. Arrêter le chronomètre
        await vm.HandleMessageAsync(Constants.Message.TIMER_STOP);
        Assert.False(vm.IsTimerRunning);
        Assert.False(vm.CanSendScore);
        Assert.Equal("00:00", vm.TimeLeftDisplay);

        // Tenter de noter après arrêt : doit être refusé
        await vm.AddBlueScoreCommand.ExecuteAsync(null);
        Assert.Equal(0, vm.BlueScore);
    }

    [Fact]
    public async Task JudgeViewModel_WhenTimeSetToZero_CanSendScoreIsFalse()
    {
        var vm = CreateJudgeViewModel();

        await vm.HandleMessageAsync(Constants.Message.TIMER_START);
        Assert.True(vm.CanSendScore);

        await vm.HandleMessageAsync("TIMER_SET:00:00");
        Assert.False(vm.CanSendScore);

        await vm.AddRedScoreCommand.ExecuteAsync(null);
        Assert.Equal(0, vm.RedScore);
    }

    #endregion

    #region ServerViewModel Tests

    [Fact]
    public async Task ServerViewModel_RejectsJudgeScoreWhenTimerNotRunning()
    {
        var serverLogger = Substitute.For<ILogger<ServerViewModel>>();
        var popup = Substitute.For<IPopupService>();
        var alert = Substitute.For<IAlertService>();
        var btService = Substitute.For<IBluetoothService>();
        var btServer = Substitute.For<IBluetoothServer>();
        var compRepo = Substitute.For<IRepository<CompetitionModel>>();
        var drawKoRepo = Substitute.For<IRepository<DrawKnockoutModel>>();
        var drawOrderRepo = Substitute.For<IRepository<DrawOrderModel>>();
        var drawPoolsRepo = Substitute.For<IRepository<DrawPoolsModel>>();
        var refDataRepo = Substitute.For<IRepository<RefereeDataModel>>();

        var vm = new ServerViewModel(
            serverLogger,
            _resourceProvider,
            popup,
            alert,
            btService,
            btServer,
            compRepo,
            drawKoRepo,
            drawOrderRepo,
            drawPoolsRepo,
            refDataRepo
        );

        vm.Judges.Add(new JudgeModel { Number = 1, Group = "", Name = "Juge 1", RedPoints = 0, BluePoints = 0 });
        Assert.False(vm.IsTimerRunning);

        var scoreModel = new TransfertScoreModel
        {
            JudgePosition = "1",
            Score = 1.0,
            Side = Constants.Side.Red
        };
        string json = JsonSerializer.Serialize(scoreModel);
        string scoreMsg = $"{Constants.Message.JUDGE_SCORE}{json}";

        // Simuler réception message BLE
        btServer.MessageReceived += Raise.Event<EventHandler<(string ClientId, string Message)>>(
            btServer,
            ("client-1", scoreMsg)
        );

        // Vérifier que le point n'a pas été appliqué
        Assert.Equal(0, vm.Judges[0].RedPoints);
        await refDataRepo.DidNotReceive().AddAsync(Arg.Any<RefereeDataModel>());
    }

    #endregion

    #region Server CLI (ArbitrationServerSession) Tests

    [Fact]
    public void ArbitrationServerSession_RejectsJudgeScoreWhenTimerNotRunning()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        // Le chronomètre est inactif par défaut
        Assert.False(session.IsTimerRunning);

        var judge = session.Judges[0];
        Assert.Equal(0, judge.RedPoints);

        var scoreModel = new TransfertScoreModel
        {
            JudgePosition = "1",
            Score = 1.0,
            Side = Constants.Side.Red
        };
        string json = JsonSerializer.Serialize(scoreModel);
        string scoreMessage = $"{Constants.Message.JUDGE_SCORE}{json}";

        // Déclencher le message reçu via la réflexion sur OnBluetoothMessageReceived
        var method = typeof(ArbitrationServerSession).GetMethod("OnBluetoothMessageReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(session, new object?[] { null, ("client-1", scoreMessage) });

        // Le point doit avoir été refusé
        Assert.Equal(0, judge.RedPoints);
    }

    #endregion
}
