using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
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

public class ServerTimeAccelerationAndBotTests
{
    private ServerViewModel CreateServerViewModel()
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

        drawOrderRepo.GetAllAsync(Arg.Any<string[]>()).Returns((IReadOnlyList<DrawOrderModel>)new List<DrawOrderModel>());
        drawPoolsRepo.GetAllAsync(Arg.Any<string[]>()).Returns((IReadOnlyList<DrawPoolsModel>)new List<DrawPoolsModel>());

        return new ServerViewModel(
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
            refereeDataRepo,
            Substitute.For<IDrawPdfExportService>(),
            Substitute.For<IFileDownloadService>()
        );
    }

    //[Theory]
    //[InlineData(1, "x1")]
    //[InlineData(2, "x2")]
    //[InlineData(4, "x4")]
    //[InlineData(6, "x6")]
    //[InlineData(8, "x8")]
    //public void ServerViewModel_TimeSpeedMultiplier_ValidValuesUpdateMultiplierAndDisplay(int multiplier, string expectedDisplay)
    //{
    //    var vm = CreateServerViewModel();

    //    vm.SetTimeSpeedMultiplierCommand.Execute(multiplier);

    //    Assert.Equal(multiplier, vm.TimeSpeedMultiplier);
    //    Assert.Equal(expectedDisplay, vm.TimeSpeedDisplay);
    //}

    //[Theory]
    //[InlineData("1", 1)]
    //[InlineData("2", 2)]
    //[InlineData("4", 4)]
    //[InlineData("6", 6)]
    //[InlineData("8", 8)]
    //public void ServerViewModel_SetTimeSpeedMultiplier_ParsesStringParameter(string param, int expectedMultiplier)
    //{
    //    var vm = CreateServerViewModel();

    //    vm.SetTimeSpeedMultiplierCommand.Execute(param);

    //    Assert.Equal(expectedMultiplier, vm.TimeSpeedMultiplier);
    //}

    //[Fact]
    //public void ServerViewModel_TimeSpeedMultiplier_IgnoresInvalidValues()
    //{
    //    var vm = CreateServerViewModel();
    //    vm.TimeSpeedMultiplier = 2;

    //    vm.SetTimeSpeedMultiplierCommand.Execute(3); // non autorisé
    //    Assert.Equal(2, vm.TimeSpeedMultiplier);

    //    vm.SetTimeSpeedMultiplierCommand.Execute(10); // non autorisé
    //    Assert.Equal(2, vm.TimeSpeedMultiplier);
    //}

    //[Fact]
    //public void ServerViewModel_CycleTimeSpeedMultiplier_CyclesThroughSequence()
    //{
    //    var vm = CreateServerViewModel();
    //    Assert.Equal(1, vm.TimeSpeedMultiplier);

    //    vm.CycleTimeSpeedMultiplierCommand.Execute(null);
    //    Assert.Equal(2, vm.TimeSpeedMultiplier);

    //    vm.CycleTimeSpeedMultiplierCommand.Execute(null);
    //    Assert.Equal(4, vm.TimeSpeedMultiplier);

    //    vm.CycleTimeSpeedMultiplierCommand.Execute(null);
    //    Assert.Equal(6, vm.TimeSpeedMultiplier);

    //    vm.CycleTimeSpeedMultiplierCommand.Execute(null);
    //    Assert.Equal(8, vm.TimeSpeedMultiplier);

    //    vm.CycleTimeSpeedMultiplierCommand.Execute(null);
    //    Assert.Equal(1, vm.TimeSpeedMultiplier);
    //}

    //[Fact]
    //public void ServerViewModel_JudgeBotToggleAndStrategy_UpdatesProperties()
    //{
    //    var vm = CreateServerViewModel();
    //    Assert.False(vm.IsJudgeBotEnabled);
    //    Assert.Contains("OFF", vm.JudgeBotButtonText, StringComparison.OrdinalIgnoreCase);

    //    vm.ToggleJudgeBotCommand.Execute(null);
    //    Assert.True(vm.IsJudgeBotEnabled);
    //    Assert.Contains("ACTIF", vm.JudgeBotButtonText, StringComparison.OrdinalIgnoreCase);

    //    // Cycle strategy
    //    Assert.Equal("balanced", vm.JudgeBotStrategy);
    //    vm.CycleJudgeBotStrategyCommand.Execute(null);
    //    Assert.Equal("red", vm.JudgeBotStrategy);
    //    Assert.Contains("red", vm.JudgeBotButtonText, StringComparison.OrdinalIgnoreCase);

    //    vm.CycleJudgeBotStrategyCommand.Execute(null);
    //    Assert.Equal("blue", vm.JudgeBotStrategy);

    //    vm.CycleJudgeBotStrategyCommand.Execute(null);
    //    Assert.Equal("random", vm.JudgeBotStrategy);

    //    vm.CycleJudgeBotStrategyCommand.Execute(null);
    //    Assert.Equal("balanced", vm.JudgeBotStrategy);
    //}

    //[Fact]
    //public async Task ServerViewModel_ProcessJudgeBotTick_ScoresPointsWhenEnabled()
    //{
    //    var vm = CreateServerViewModel();
    //    var match = new DrawKnockoutModel
    //    {
    //        Id = 1,
    //        Competitor1 = new CompetitorModel { Id = 10, FirstName = "Jean", LastName = "Valjean" },
    //        Competitor2 = new CompetitorModel { Id = 20, FirstName = "Javert", LastName = "Inspecteur" },
    //        CurrentRound = 1
    //    };
    //    vm.CurrentDraw = match;
    //    vm.IsJudgeBotEnabled = true;

    //    // Run multiple ticks to ensure probabilistic action triggers
    //    for (int i = 0; i < 50; i++)
    //    {
    //        await vm.ProcessJudgeBotTickAsync();
    //    }

    //    double totalRed = vm.Judges.Sum(j => j.RedPoints);
    //    double totalBlue = vm.Judges.Sum(j => j.BluePoints);

    //    Assert.True(totalRed + totalBlue > 0, "Le robot juge aurait dû attribuer des points sur 50 ticks.");
    //}

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public async Task ArbitrationServerSession_TimeSpeedMultiplier_SetsMultiplier(int mult)
    {
        using var server = new WindowsBluetoothServer();
        var session = new ArbitrationServerSession(server);

        await session.SetTimeSpeedMultiplierAsync(mult);

        Assert.Equal(mult, session.TimeSpeedMultiplier);
        Assert.Equal($"x{mult}", session.TimeSpeedDisplay);
    }

    [Fact]
    public void ArbitrationServerSession_JudgeBot_ToggleAndConfigure()
    {
        using var server = new WindowsBluetoothServer();
        var session = new ArbitrationServerSession(server);

        Assert.False(session.IsJudgeBotEnabled);

        session.ToggleJudgeBot();
        Assert.True(session.IsJudgeBotEnabled);

        session.SetJudgeBot(true, "red");
        Assert.Equal("red", session.JudgeBotStrategy);

        session.SetJudgeBot(false);
        Assert.False(session.IsJudgeBotEnabled);
    }

    [Fact]
    public async Task ArbitrationServerSession_ProcessJudgeBotTick_AwardsPoints()
    {
        using var server = new WindowsBluetoothServer();
        var session = new ArbitrationServerSession(server);

        var match = new MatchItem
        {
            Id = 1,
            Category = "Senior -70kg",
            RedCompetitor = "Dupont",
            BlueCompetitor = "Durand",
            CurrentRound = 1,
            MaxRounds = 3
        };

        await session.LoadCompetitionAsync(
            new CompetitionModel { Id = 1, Name = "Test", Type = CompetitionType.Sanda, Date = DateTime.Today },
            new List<MatchItem> { match },
            new List<CategoryModel> { new CategoryModel { Id = 1, Type = CategoryType.Sanda } }
        );

        session.SetJudgeBot(true, "balanced");

        for (int i = 0; i < 50; i++)
        {
            await session.ProcessJudgeBotTickAsync();
        }

        double totalRed = session.Judges.Sum(j => j.RedPoints);
        double totalBlue = session.Judges.Sum(j => j.BluePoints);

        Assert.True(totalRed + totalBlue > 0, "Le robot CLI aurait dû attribuer des points sur 50 ticks.");
    }

    [Fact]
    public void JudgeBotManager_AddBotsAndCoordinate()
    {
        using var manager = new JudgeBotManager();

        using var client1 = new WindowsBluetoothClient();
        using var session1 = new JudgeSession(client1);
        session1.JudgePosition = 1;
        var bot1 = manager.AddBot(client1, session1, speedMultiplier: 2, strategy: "red");

        using var client2 = new WindowsBluetoothClient();
        using var session2 = new JudgeSession(client2);
        session2.JudgePosition = 2;
        var bot2 = manager.AddBot(client2, session2, speedMultiplier: 2, strategy: "red");

        Assert.Equal(2, manager.Count);
        Assert.Equal(2, manager.Bots.Count);

        manager.SetSpeedMultiplierAll(6);
        Assert.All(manager.Bots, b => Assert.Equal(6, b.SpeedMultiplier));

        manager.SetStrategyAll("blue");
        Assert.All(manager.Bots, b => Assert.Equal("blue", b.Strategy));

        manager.StopAll();
        Assert.All(manager.Bots, b => Assert.False(b.IsRunning));
    }
}
