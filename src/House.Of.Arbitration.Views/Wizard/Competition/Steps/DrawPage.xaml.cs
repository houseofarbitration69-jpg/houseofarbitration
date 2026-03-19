#region Imports
using CommunityToolkit.Maui.Core;
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
using System.Linq;
#endregion

namespace House.Of.Arbitration.Views.Wizard.Competition.Steps;

public partial class DrawPage
{
    public DrawPage(DrawPageViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }

    private void OnDragStarting(object sender, DragStartingEventArgs e)
    {
        var gestures = (sender as Border)?.GestureRecognizers;

        if (gestures != null)
        {
            var dragGesture = gestures.FirstOrDefault(g => g is DragGestureRecognizer);

            if (dragGesture != null)
            {
                var slot = ((DragGestureRecognizer)dragGesture).DragStartingCommandParameter as BracketSlotViewModel;

                if (slot != null)
                {
                    e.Data.Text = "slot";
                    e.Data.Properties["slot"] = slot;

                    if (BindingContext is DrawPageViewModel vm)
                    {
                        vm.DraggedSlot = slot;
                        vm.IsDragging = true;
                    }
                }
            }
        }
    }

    private void OnDragCompleted(object sender, DropCompletedEventArgs e)
    {
        if (BindingContext is DrawPageViewModel vm)
        {
            vm.IsDragging = false;
            vm.DraggedSlot = null;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;

        var border = (sender as Border);
        if (border != null)
        {
            border.Stroke = Colors.DeepSkyBlue;
            border.StrokeThickness = 3;
        }
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        var border = (sender as Border);

        if (border != null)
        {
            border.Stroke = Colors.Gray;
            border.StrokeThickness = 1;
        }
    }

    private void OnDrop(object sender, DropEventArgs e)
    {
        if (BindingContext is DrawPageViewModel vm)
        {
            vm.IsDragging = false;

            var gestures = (sender as Border)?.GestureRecognizers;

            if (gestures != null)
            {
                var dropGesture = gestures.FirstOrDefault(g => g is DropGestureRecognizer);

                if (dropGesture != null)
                {
                    var slot = ((DropGestureRecognizer)dropGesture).DropCommandParameter as BracketSlotViewModel;

                    if (slot != null)
                    {
                        if (sender is Border border)
                        {
                            border.Stroke = Colors.Gray;
                            border.StrokeThickness = 1;
                        }

                        vm.DropCommand.Execute(slot);
                    }
                }
            }
        }
    }
}
