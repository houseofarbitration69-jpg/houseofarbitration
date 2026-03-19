#region Imports
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
        if (sender is DragGestureRecognizer recognizer && recognizer.DragStartingCommandParameter is BracketSlotViewModel slot)
        {
            // On Windows, DataPackage must not be empty for the drag operation to be valid
            e.Data.Text = "slot";
            e.Data.Properties["slot"] = slot;

            if (BindingContext is DrawPageViewModel vm)
            {
                vm.DraggedSlot = slot;
                vm.IsDragging = true;
            }
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;

#if WINDOWS
        if (e.PlatformArgs != null && e.PlatformArgs.DragEventArgs != null)
        {
            // Hide everything that can block the hit testing
            e.PlatformArgs.DragEventArgs.DragUIOverride.IsGlyphVisible = false;
            e.PlatformArgs.DragEventArgs.DragUIOverride.IsCaptionVisible = false;
            e.PlatformArgs.DragEventArgs.DragUIOverride.IsContentVisible = false;
        }
#endif
        
        // Visual feedback for the specific hover target
        if (sender is DropGestureRecognizer recognizer && recognizer.Parent is Border border)
        {
            border.Stroke = Colors.DeepSkyBlue;
            border.StrokeThickness = 3;
        }
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        // Reset visual feedback
        if (sender is DropGestureRecognizer recognizer && recognizer.Parent is Border border)
        {
            if (BindingContext is DrawPageViewModel vm && vm.IsDragging)
            {
                border.Stroke = Colors.LightBlue;
                border.StrokeThickness = 2;
            }
            else
            {
                border.Stroke = Colors.Gray;
                border.StrokeThickness = 1;
            }
        }
    }

    private void OnDrop(object sender, DropEventArgs e)
    {
        if (BindingContext is DrawPageViewModel vm)
        {
            vm.IsDragging = false;

            // Robustness: ensure we have the dragged slot
            if (vm.DraggedSlot == null && e.Data.Properties.TryGetValue("slot", out var draggedSlotObj) && draggedSlotObj is BracketSlotViewModel draggedSlot)
            {
                vm.DraggedSlot = draggedSlot;
            }

            if (sender is DropGestureRecognizer recognizer && recognizer.DropCommandParameter is BracketSlotViewModel targetSlot)
            {
                // Reset visual feedback for the target
                if (recognizer.Parent is Border border)
                {
                    border.Stroke = Colors.Gray;
                    border.StrokeThickness = 1;
                }

                vm.DropCommand.Execute(targetSlot);
            }
        }
    }
}
