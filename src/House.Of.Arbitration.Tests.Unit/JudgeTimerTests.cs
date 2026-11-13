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

public class JudgeTimerTests
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

    [Theory]
    [InlineData("TIMER_SET:01:30", "01:30")]
    [InlineData("TIMER_SET:1:30", "01:30")]
    [InlineData("TIMER_SET:02:00", "02:00")]
    [InlineData("TIMER_SET:2:00", "02:00")]
    [InlineData("TIMER_SET:00:45\r\n", "00:45")]
    [InlineData(" TIMER_SET:03:00 ", "03:00")]
    public async Task HandleMessageAsync_TimerSet_UpdatesTimeLeftDisplay(string message, string expectedDisplay)
    {
        var vm = CreateViewModel();

        await vm.HandleMessageAsync(message);

        Assert.Equal(expectedDisplay, vm.TimeLeftDisplay);
    }

    [Fact]
    public async Task HandleMessageAsync_TimerStop_SetsTimeToZero()
    {
        var vm = CreateViewModel();
        await vm.HandleMessageAsync("TIMER_SET:01:30");
        Assert.Equal("01:30", vm.TimeLeftDisplay);

        await vm.HandleMessageAsync("TIMER_STOP");

        Assert.Equal("00:00", vm.TimeLeftDisplay);
    }

    [Fact]
    public async Task HandleMessageAsync_TimerPause_DoesNotThrowEvenIfTimerNull()
    {
        var vm = CreateViewModel();

        await vm.HandleMessageAsync("TIMER_PAUSE");

        // Should not throw
        Assert.NotNull(vm.TimeLeftDisplay);
    }

    [Fact]
    public async Task HandleMessageAsync_TimerStart_DoesNotThrowEvenIfTimerNull()
    {
        var vm = CreateViewModel();

        await vm.HandleMessageAsync("TIMER_START");

        // Should not throw
        Assert.NotNull(vm.TimeLeftDisplay);
    }
}
