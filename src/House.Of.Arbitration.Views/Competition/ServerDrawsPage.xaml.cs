using House.Of.Arbitration.ViewModels.Competition;

namespace House.Of.Arbitration.Views.Competition;

public partial class ServerDrawsPage
{
    public ServerDrawsPage(ServerDrawsViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }

    private void OnDragStarting(object sender, DragStartingEventArgs e)
    {
        var gestures = (sender as Border)?.GestureRecognizers;
        var dragGesture = gestures?.OfType<DragGestureRecognizer>().FirstOrDefault();
        if (dragGesture?.DragStartingCommandParameter is ServerKnockoutSlotItem slot)
        {
            e.Data.Text = "slot";
            e.Data.Properties["slot"] = slot;
            if (BindingContext is ServerDrawsViewModel vm)
            {
                vm.DraggedSlot = slot;
                vm.IsDragging = true;
            }
        }
    }

    private void OnDragCompleted(object sender, DropCompletedEventArgs e)
    {
        if (BindingContext is ServerDrawsViewModel vm)
        {
            vm.IsDragging = false;
            vm.DraggedSlot = null;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        if (sender is Border border)
        {
            border.Stroke = Colors.DeepSkyBlue;
            border.StrokeThickness = 3;
        }
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.Stroke = Colors.Gray;
            border.StrokeThickness = 1;
        }
    }

    private void OnDrop(object sender, DropEventArgs e)
    {
        if (sender is Border border)
        {
            border.Stroke = Colors.Gray;
            border.StrokeThickness = 1;
        }

        if (BindingContext is ServerDrawsViewModel vm)
        {
            vm.IsDragging = false;
            var dropGesture = (sender as Border)?.GestureRecognizers.OfType<DropGestureRecognizer>().FirstOrDefault();
            if (dropGesture?.DropCommandParameter is ServerKnockoutSlotItem targetSlot)
            {
                _ = vm.DropOnSlotCommand.ExecuteAsync(targetSlot);
            }
        }
    }
}
