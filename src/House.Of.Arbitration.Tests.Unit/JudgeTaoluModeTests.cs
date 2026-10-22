using CommunityToolkit.Maui;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class JudgeTaoluModeTests
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

    private JudgeViewModel CreateViewModel()
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
            _mvtCodesRepo
        );
    }

    [Fact]
    public async Task TypeNumber_ShouldWaitWhileMultipleCodesMatch_AndAutoValidateWhenOnlyOneCodeMatches()
    {
        // Arrange
        var vm = CreateViewModel();
        _mvtCodesRepo.GetAllAsync().Returns(MvtCodeModel.DefaultCodes);
        await vm.LoadRecognizedCodesAsync();
        vm.Score = 5.0;

        // Act 1: Type '5' (matches 50, 51, 52, 53, 54 -> 5 codes possible)
        vm.TypeNumberCommand.Execute("5");

        // Assert 1: Multiple codes possible, no auto-validation yet
        Assert.Equal("5", vm.Code);
        Assert.Equal(5, vm.MatchingCodes.Count);
        Assert.Empty(vm.Codes);
        Assert.Equal(5.0, vm.Score);

        // Act 2: Type '0' -> only '50' matches (1 code remaining)
        vm.TypeNumberCommand.Execute("0");

        // Assert 2: Auto-validated! Code reset, Score deducted, Code added to history
        Assert.Empty(vm.Code);
        Assert.Empty(vm.MatchingCodes);
        Assert.Single(vm.Codes);
        Assert.Equal("50", vm.Codes[0].Code?.Code);
        Assert.Equal("Gong Bu (Position arc)", vm.Codes[0].Code?.Movement);
        Assert.Equal(4.9, vm.Score);
    }

    [Fact]
    public void BackspaceCode_ShouldRemoveLastDigit()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.TypeNumberCommand.Execute("1");
        vm.TypeNumberCommand.Execute("2");
        vm.TypeNumberCommand.Execute("3");

        // Act
        vm.BackspaceCodeCommand.Execute(null);

        // Assert
        Assert.Equal("12", vm.Code);
    }

    [Fact]
    public void ClearCode_ShouldResetCodeAndMatchingList()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.TypeNumberCommand.Execute("5");

        // Act
        vm.ClearCodeCommand.Execute(null);

        // Assert
        Assert.Empty(vm.Code);
        Assert.Empty(vm.MatchingCodes);
    }

    [Fact]
    public void SwitchMode_ShouldSwitchBetweenCodePointsAndGroupCodes()
    {
        // Arrange
        var vm = CreateViewModel();

        // Initial mode is Code
        Assert.True(vm.IsCodeMode);
        Assert.False(vm.IsPointMode);
        Assert.False(vm.IsGroupCodesMode);

        // Switch to Points
        vm.SwitchModeCommand.Execute("Points");
        Assert.False(vm.IsCodeMode);
        Assert.True(vm.IsPointMode);
        Assert.False(vm.IsGroupCodesMode);

        // Switch to GroupCodes
        vm.SwitchModeCommand.Execute("GroupCodes");
        Assert.False(vm.IsCodeMode);
        Assert.False(vm.IsPointMode);
        Assert.True(vm.IsGroupCodesMode);

        // Switch back to Code
        vm.SwitchModeCommand.Execute("Code");
        Assert.True(vm.IsCodeMode);
        Assert.False(vm.IsPointMode);
        Assert.False(vm.IsGroupCodesMode);
    }

    [Fact]
    public void TypePoint_And_DeductPoints_ShouldDeductDirectPointsFromScore()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CurrentCompetition?.Type = CompetitionType.Taolu;
        vm.Score = 5.0;

        // Act: Type 0.3
        vm.TypePointCommand.Execute("0");
        vm.TypePointCommand.Execute(".");
        vm.TypePointCommand.Execute("3");
        Assert.Equal("0.3", vm.PointsInput);

        // Deduct
        vm.DeductPointsCommand.Execute(null);

        // Assert
        Assert.Equal(4.7, vm.Score);
        Assert.Empty(vm.PointsInput);
        Assert.Single(vm.Codes);
        Assert.Equal(0.3, vm.Codes[0].Code?.Value);
    }

    [Fact]
    public void DeductPoints_WithParameter_ShouldDeductImmediately()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CurrentCompetition?.Type = CompetitionType.Taolu;
        vm.Score = 5.0;

        // Act: Quick deduction chip 0.2
        vm.DeductPointsCommand.Execute(0.2);

        // Assert
        Assert.Equal(4.8, vm.Score);
        Assert.Single(vm.Codes);
    }

    [Fact]
    public async Task SwitchMode_GroupCodes_ShouldFilterForSelectedJudgeGroup()
    {
        // Arrange
        var vm = CreateViewModel();
        _mvtCodesRepo.GetAllAsync().Returns(MvtCodeModel.DefaultCodes);
        await vm.LoadRecognizedCodesAsync();

        // Assign Judge to Group B
        vm.SelectedJudge = new JudgeModel { Name = "JUGE 1", Number = 1, Group = "B" };

        // Act
        vm.SwitchModeCommand.Execute("GroupCodes");

        // Assert
        Assert.True(vm.IsGroupCodesMode);
        Assert.NotEmpty(vm.FilteredRecognizedCodes);
        // All filtered codes should belong to Group B ("Autres erreurs" / "Performance globale")
        Assert.All(vm.FilteredRecognizedCodes, c =>
        {
            Assert.Contains(c.GroupLabel, new[] { "Autres erreurs", "Performance globale" });
        });
    }

    [Fact]
    public async Task SwitchMode_GroupCodes_ShouldFilterForSelectedJudgeGroupA()
    {
        // Arrange
        var vm = CreateViewModel();
        _mvtCodesRepo.GetAllAsync().Returns(MvtCodeModel.DefaultCodes);
        await vm.LoadRecognizedCodesAsync();

        // Assign Judge to Group A
        vm.SelectedJudge = new JudgeModel { Name = "JUGE 1", Number = 1, Group = "A" };

        // Act
        vm.SwitchModeCommand.Execute("GroupCodes");

        // Assert
        Assert.True(vm.IsGroupCodesMode);
        Assert.NotEmpty(vm.FilteredRecognizedCodes);
        // Group A includes Positions, Techniques de main, jambe, équilibres, armes
        Assert.Contains(vm.FilteredRecognizedCodes, c => c.GroupLabel == "Positions");
        Assert.Contains(vm.FilteredRecognizedCodes, c => c.GroupLabel == "Techniques de main");
        // Must NOT contain Group B or Group C codes
        Assert.DoesNotContain(vm.FilteredRecognizedCodes, c => c.GroupLabel == "Autres erreurs");
        Assert.DoesNotContain(vm.FilteredRecognizedCodes, c => c.GroupLabel == "Techniques de saut");
    }

    [Fact]
    public async Task ReceiveJudgeState_ShouldRestoreScoreAndCodesWithCanDeleteOnTopItem()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SelectedJudge = new JudgeModel { Name = "JUGE 1", Number = 1, Group = "A", Score = 5.0 };

        var code1 = new MvtTimeCodeModel
        {
            Date = DateTime.Now.AddMinutes(-2),
            Code = new MvtCodeModel { Code = "50", Movement = "Gong Bu", GroupName = "Positions", Value = 0.1 },
            Score = 4.9
        };
        var code2 = new MvtTimeCodeModel
        {
            Date = DateTime.Now.AddMinutes(-1),
            Code = new MvtCodeModel { Code = "51", Movement = "Ma Bu", GroupName = "Positions", Value = 0.1 },
            Score = 4.8
        };

        var state = new JudgeStateModel
        {
            Group = "A",
            Number = 1,
            Name = "JUGE 1",
            Score = 4.8,
            Codes = new List<MvtTimeCodeModel> { code2, code1 }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(state);

        // Act: simulate receiving JUDGE_STATE message from server
        await vm.HandleMessageAsync($"{Constants.Message.JUDGE_STATE}{json}");

        // Assert
        Assert.Equal(4.8, vm.Score);
        Assert.Equal(2, vm.Codes.Count);
        Assert.Equal("51", vm.Codes[0].Code?.Code);
        Assert.True(vm.Codes[0].CanDelete);
        Assert.Equal("50", vm.Codes[1].Code?.Code);
        Assert.False(vm.Codes[1].CanDelete);
        Assert.Equal(4.8, vm.SelectedJudge.Score);
    }

    [Fact]
    public async Task Disconnect_WhenNoJudgeSelected_ShouldClearCodes()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Codes.Add(new MvtTimeCodeModel());
        Assert.NotEmpty(vm.Codes);

        // Act: simulate device disconnection when no judge is selected
        await vm.HandleDeviceDisconnectedAsync("device-123");

        // Assert
        Assert.Empty(vm.Codes);
        Assert.False(vm.IsConnected);
        Assert.Null(vm.SelectedJudge);
    }

    [Fact]
    public async Task Disconnect_WhenJudgeSelected_ShouldPreserveDataAndActivateOfflineMode()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SelectedJudge = new JudgeModel { Name = "JUGE 1", Number = 1, Group = "A", Score = 4.8 };
        vm.Score = 4.8;
        vm.Codes.Add(new MvtTimeCodeModel { Score = 4.8 });
        vm.IsConnected = true;

        // Act: server disconnects/crashes
        await vm.HandleDeviceDisconnectedAsync("device-server");

        // Assert
        Assert.False(vm.IsConnected);
        Assert.True(vm.IsServerOffline);
        Assert.True(vm.IsJudging);
        Assert.False(vm.ShowConnectionScreen);
        Assert.NotNull(vm.SelectedJudge);
        Assert.Equal(4.8, vm.Score);
        Assert.Single(vm.Codes);
        Assert.Equal("DÉCONNECTÉ (HORS-LIGNE)", vm.ConnectionStatusText);
    }

    [Fact]
    public async Task OfflineScoring_ShouldUpdateScoreAndSetHasUnsyncedChanges()
    {
        // Arrange
        var vm = CreateViewModel();
        _mvtCodesRepo.GetAllAsync().Returns(MvtCodeModel.DefaultCodes);
        await vm.LoadRecognizedCodesAsync();
        vm.SelectedJudge = new JudgeModel { Name = "JUGE 1", Number = 1, Group = "A", Score = 5.0 };
        vm.Score = 5.0;
        vm.IsConnected = false; // Disconnected / Offline mode

        // Act: Judge continues scoring while disconnected
        vm.TypeNumberCommand.Execute("5");
        vm.TypeNumberCommand.Execute("0"); // Gong Bu (Value = 0.1)

        // Assert
        Assert.Equal(4.9, vm.Score);
        Assert.Single(vm.Codes);
        Assert.Equal("50", vm.Codes[0].Code?.Code);
        Assert.True(vm.HasUnsyncedChanges);
    }

    [Fact]
    public async Task Reconnect_WithOfflineChanges_ShouldSendPositionAndSyncState_AndAckClearsFlag()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SelectedJudge = new JudgeModel { Name = "JUGE 1", Number = 1, Group = "A", Score = 4.8 };
        vm.Score = 4.8;
        vm.Codes.Add(new MvtTimeCodeModel
        {
            Date = DateTime.Now,
            Code = new MvtCodeModel { Code = "50", Movement = "Gong Bu", Value = 0.1 },
            Score = 4.9
        });
        vm.HasUnsyncedChanges = true;
        vm.IsConnected = false;

        // Act 1: Reconnect to server
        await vm.HandleDeviceConnectedAsync("device-server");

        // Assert 1: Messages sent to server (POSITION + SYNC_STATE)
        Assert.True(vm.IsConnected);
        Assert.False(vm.IsServerOffline);
        await _bluetoothClient.Received().SendMessage("device-server", $"{Constants.Message.JUDGE_POSITION}A_1");
        await _bluetoothClient.Received().SendMessage("device-server", Arg.Is<string>(s => s.StartsWith(Constants.Message.JUDGE_SYNC_STATE)));

        // Act 2: Receive JUDGE_SYNC_ACK from server
        await vm.HandleMessageAsync(Constants.Message.JUDGE_SYNC_ACK);

        // Assert 2: HasUnsyncedChanges is cleared
        Assert.False(vm.HasUnsyncedChanges);
    }

    [Fact]
    public async Task ServerStopped_ShouldNotifyJudgeAndTriggerOfflineMode()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.HandleDeviceConnectedAsync("device-server");
        vm.SelectedJudge = new JudgeModel { Name = "JUGE 1", Number = 1, Group = "A", Score = 4.8 };
        vm.Score = 4.8;
        Assert.True(vm.IsConnected);

        // Act
        await vm.HandleMessageAsync(Constants.Message.SERVER_STOPPED);

        // Assert
        Assert.False(vm.IsConnected);
        Assert.True(vm.IsServerOffline);
        Assert.True(vm.IsJudging);
        Assert.Equal(4.8, vm.Score);
        Assert.NotNull(vm.SelectedJudge);
    }

    [Fact]
    public async Task Watchdog_WhenWithinTimeout_ShouldSendPing()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.HandleDeviceConnectedAsync("device-server");
        vm.LastServerContactTime = DateTime.UtcNow;

        // Act
        await vm.PerformWatchdogTickAsync();

        // Assert
        await _bluetoothClient.Received().SendMessage("device-server", Constants.Message.PING);
        Assert.True(vm.IsConnected);
    }

    [Fact]
    public async Task Watchdog_WhenTimeoutExceeded_ShouldDeclareServerDisconnected()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.HandleDeviceConnectedAsync("device-server");
        vm.SelectedJudge = new JudgeModel { Name = "JUGE 1", Number = 1, Group = "A", Score = 4.8 };
        vm.Score = 4.8;
        // Simulate silence from server for 15 seconds (exceeding 12-second timeout)
        vm.LastServerContactTime = DateTime.UtcNow - TimeSpan.FromSeconds(15);

        // Act
        await vm.PerformWatchdogTickAsync();

        // Assert
        Assert.False(vm.IsConnected);
        Assert.True(vm.IsServerOffline);
        Assert.NotNull(vm.SelectedJudge);
        Assert.Equal(4.8, vm.Score);
    }

    [Fact]
    public async Task Watchdog_WhenSinglePingFails_ShouldRemainConnectedAndIncrementFailureCount()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.HandleDeviceConnectedAsync("device-server");
        vm.SelectedJudge = new JudgeModel { Name = "JUGE 1", Number = 1, Group = "A", Score = 4.8 };
        vm.LastServerContactTime = DateTime.UtcNow;
        _bluetoothClient.When(b => b.SendMessage("device-server", Constants.Message.PING))
            .Do(_ => throw new Exception("Transient BLE write error"));

        // Act
        await vm.PerformWatchdogTickAsync();

        // Assert: Tolerates transient single failure without disconnecting
        Assert.True(vm.IsConnected);
        Assert.False(vm.IsServerOffline);
        Assert.Equal(1, vm.ConsecutiveFailedPings);
    }

    [Fact]
    public async Task Watchdog_WhenThreeConsecutivePingsFail_ShouldDeclareServerDisconnected()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.HandleDeviceConnectedAsync("device-server");
        vm.SelectedJudge = new JudgeModel { Name = "JUGE 1", Number = 1, Group = "A", Score = 4.8 };
        vm.LastServerContactTime = DateTime.UtcNow;
        _bluetoothClient.When(b => b.SendMessage("device-server", Constants.Message.PING))
            .Do(_ => throw new Exception("BLE socket broken"));

        // Act: 3 consecutive failed ticks
        await vm.PerformWatchdogTickAsync(); // 1st failure
        Assert.True(vm.IsConnected);
        await vm.PerformWatchdogTickAsync(); // 2nd failure
        Assert.True(vm.IsConnected);
        await vm.PerformWatchdogTickAsync(); // 3rd failure

        // Assert: Disconnects on 3rd failure
        Assert.False(vm.IsConnected);
        Assert.True(vm.IsServerOffline);
        Assert.NotNull(vm.SelectedJudge);
    }

    [Fact]
    public async Task PongReceived_ShouldUpdateLastServerContactTime()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.HandleDeviceConnectedAsync("device-server");
        var beforeTime = DateTime.UtcNow - TimeSpan.FromSeconds(3);
        vm.LastServerContactTime = beforeTime;

        // Act
        await vm.HandleMessageAsync(Constants.Message.PONG);

        // Assert
        Assert.True(vm.LastServerContactTime > beforeTime);
    }

    [Fact]
    public async Task Reconnect_WithOfflineScores_ShouldImmediatelySendAllOfflineScoresViaJudgeScoreWithoutRequiringNewInput()
    {
        // Arrange
        var vm = CreateViewModel();
        _mvtCodesRepo.GetAllAsync().Returns(MvtCodeModel.DefaultCodes);
        await vm.LoadRecognizedCodesAsync();

        vm.SelectedJudge = new JudgeModel { Name = "JUGE 1", Number = 1, Group = "A", Score = 5.0 };
        vm.Score = 5.0;
        vm.IsConnected = false;

        // Act 1: Record 2 codes while offline
        vm.TypeNumberCommand.Execute("5");
        vm.TypeNumberCommand.Execute("0"); // Gong Bu (0.1 deduction)
        vm.TypeNumberCommand.Execute("5");
        vm.TypeNumberCommand.Execute("1"); // Ma Bu (0.1 deduction)

        // Assert 1: Codes recorded locally and queued, no bluetooth messages yet
        Assert.Equal(4.8, vm.Score);
        Assert.Equal(2, vm.Codes.Count);
        Assert.Equal(2, vm.UnsyncedScores.Count);
        Assert.True(vm.HasUnsyncedChanges);
        await _bluetoothClient.DidNotReceiveWithAnyArgs().SendMessage(default!, default!);

        // Act 2: Reconnect to server
        await vm.HandleDeviceConnectedAsync("device-server");

        // Assert 2: All queued notes are sent immediately via JUDGE_SCORE without entering any new note
        await _bluetoothClient.Received().SendMessage("device-server", $"{Constants.Message.JUDGE_POSITION}A_1");
        await _bluetoothClient.Received().SendMessage("device-server", Arg.Is<string>(s => s.StartsWith(Constants.Message.JUDGE_SCORE) && s.Contains("50")));
        await _bluetoothClient.Received().SendMessage("device-server", Arg.Is<string>(s => s.StartsWith(Constants.Message.JUDGE_SCORE) && s.Contains("51")));
        Assert.Empty(vm.UnsyncedScores);
        Assert.False(vm.HasUnsyncedChanges);
        Assert.All(vm.Codes, c => Assert.True(c.IsSynced));
    }

    [Fact]
    public async Task Reconnect_WithOfflineSandaScores_ShouldImmediatelySendAllQueuedPointsToServer()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CurrentCompetition?.Type = CompetitionType.Sanda;
        vm.SelectedJudge = new JudgeModel { Name = "JUGE 1", Number = 1 };
        vm.Competitor1 = new CompetitorModel { Id = 1, FirstName = "John", LastName = "Red" };
        vm.Competitor2 = new CompetitorModel { Id = 2, FirstName = "Jane", LastName = "Blue" };
        vm.IsConnected = false;

        // Act 1: Give 2 Red points and 1 Blue point while offline
        vm.AddScoreCommand.Execute(1); // Red
        vm.AddScoreCommand.Execute(1); // Red
        vm.AddScoreCommand.Execute(2); // Blue

        // Assert 1: Points counted locally, 3 unsynced scores queued
        Assert.Equal(2, vm.SelectedJudge.RedPoints);
        Assert.Equal(1, vm.SelectedJudge.BluePoints);
        Assert.Equal(3, vm.UnsyncedScores.Count);
        Assert.True(vm.HasUnsyncedChanges);

        // Act 2: Reconnect to server
        await vm.HandleDeviceConnectedAsync("device-server");

        // Assert 2: All 3 points sent immediately via JUDGE_SCORE
        await _bluetoothClient.Received().SendMessage("device-server", $"{Constants.Message.JUDGE_POSITION}1");
        await _bluetoothClient.Received(2).SendMessage("device-server", Arg.Is<string>(s => s.StartsWith(Constants.Message.JUDGE_SCORE) && s.Contains("Red")));
        await _bluetoothClient.Received(1).SendMessage("device-server", Arg.Is<string>(s => s.StartsWith(Constants.Message.JUDGE_SCORE) && s.Contains("Blue")));
        Assert.Empty(vm.UnsyncedScores);
        Assert.False(vm.HasUnsyncedChanges);
    }
}
