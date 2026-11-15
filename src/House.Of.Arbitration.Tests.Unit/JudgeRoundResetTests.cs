using System.Text.Json;
using CommunityToolkit.Maui;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Judge.Cli;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Server.Cli;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class JudgeRoundResetTests
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

    [Fact]
    public async Task JudgeViewModel_ReceivingNewRound_ResetsScoresAndUpdatesCurrentRound()
    {
        var vm = CreateJudgeViewModel();

        // 1. Initialiser le match au round 1
        var matchRound1 = new MatchInfoData { Id = 10, Type = RoundType.Knockouts, Round = 1 };
        await vm.HandleMessageAsync($"{Constants.Message.MATCH_INFO}{JsonSerializer.Serialize(matchRound1)}");

        Assert.Equal(1, vm.CurrentRound);

        // Le juge attribue des scores pour le round 1
        vm.RedScore = 3;
        vm.BlueScore = 2;

        // 2. Le serveur lance le Round 2 du même match (Id = 10)
        var matchRound2 = new MatchInfoData { Id = 10, Type = RoundType.Knockouts, Round = 2 };
        await vm.HandleMessageAsync($"{Constants.Message.MATCH_INFO}{JsonSerializer.Serialize(matchRound2)}");

        // Assert : Les notes doivent être remises à 0 et le round doit être 2
        Assert.Equal(2, vm.CurrentRound);
        Assert.Equal(0, vm.RedScore);
        Assert.Equal(0, vm.BlueScore);
    }

    [Fact]
    public async Task JudgeViewModel_ReceivingSameRound_DoesNotResetScores()
    {
        var vm = CreateJudgeViewModel();

        // 1. Match au round 1
        var matchRound1 = new MatchInfoData { Id = 10, Type = RoundType.Knockouts, Round = 1 };
        await vm.HandleMessageAsync($"{Constants.Message.MATCH_INFO}{JsonSerializer.Serialize(matchRound1)}");

        vm.RedScore = 4;
        vm.BlueScore = 1;

        // 2. Re-réception du même round 1 (ex: ping/reconnect)
        await vm.HandleMessageAsync($"{Constants.Message.MATCH_INFO}{JsonSerializer.Serialize(matchRound1)}");

        // Assert : scores conservés
        Assert.Equal(1, vm.CurrentRound);
        Assert.Equal(4, vm.RedScore);
        Assert.Equal(1, vm.BlueScore);
    }

    [Fact]
    public void JudgeSession_ReceivingNewRound_ResetsPointsAndUpdatesCurrentRound()
    {
        using var client = new WindowsBluetoothClient();
        using var session = new JudgeSession(client);

        // Match initial round 1
        var matchRound1 = new MatchInfoData { Id = 25, Type = RoundType.Knockouts, Round = 1 };
        string msgR1 = $"{Constants.Message.MATCH_INFO}{JsonSerializer.Serialize(matchRound1)}";

        // Simuler réception message
        var onMsgMethod = typeof(JudgeSession).GetMethod("OnBluetoothMessageReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(onMsgMethod);

        onMsgMethod.Invoke(session, new object?[] { null, msgR1 });

        Assert.NotNull(session.CurrentMatch);
        Assert.Equal(1, session.CurrentMatch.Round);

        // Attribuer des points
        session.RedPoints = 5;
        session.BluePoints = 3;

        // Réception du round 2 du même match
        var matchRound2 = new MatchInfoData { Id = 25, Type = RoundType.Knockouts, Round = 2 };
        string msgR2 = $"{Constants.Message.MATCH_INFO}{JsonSerializer.Serialize(matchRound2)}";

        onMsgMethod.Invoke(session, new object?[] { null, msgR2 });

        // Assert
        Assert.NotNull(session.CurrentMatch);
        Assert.Equal(2, session.CurrentMatch.Round);
        Assert.Equal(0, session.RedPoints);
        Assert.Equal(0, session.BluePoints);
    }

    [Fact]
    public async Task ArbitrationServerSession_NextRoundAsync_SetsRoundToTwoAndResetsSession()
    {
        using var server = new WindowsBluetoothServer();
        using var session = new ArbitrationServerSession(server);

        // Passer au round 2
        bool advanced = await session.NextRoundAsync();

        Assert.True(advanced);
        Assert.NotNull(session.CurrentMatch);
        Assert.Equal(2, session.CurrentMatch.CurrentRound);

        // Données sérialisées dans MATCH_INFO
        var matchInfo = new MatchInfoData
        {
            Id = session.CurrentMatch.Id,
            Type = session.CurrentMatch.Type,
            Round = session.CurrentMatch.CurrentRound
        };

        Assert.Equal(2, matchInfo.Round);
    }
}
