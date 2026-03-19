#region Imports
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
using System.Linq;
#endregion

namespace House.Of.Arbitration.Views.Wizard.Competition.Steps;

public partial class DragDropPage
{
    public DragDropPage(DragDropPageViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }

    private void DropGestureRecognizer_Drop(object sender, DropEventArgs e)
    {
        var data = e.Data.Properties["Text"].ToString();
        var frame = (sender as Element) as Frame;
        var list = (frame?.Content as VerticalStackLayout);
        list?.Children.Add(new Label
        {
            Text = data ?? string.Empty,
            TextColor = Colors.Black
        });
    }

    private void DragGestureRecognizer_DragStarting(object sender, DragStartingEventArgs e)
    {
        var label = (sender as Element) as Label;

        if (label != null)
        {
            e.Data.Properties.Add("Text", label.Text);
        }
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

                    if (BindingContext is DragDropPageViewModel vm)
                    {
                        vm.DraggedSlot = slot;
                        vm.IsDragging = true;
                    }
                }
            }
        }
    }

    private void OnDrop(object sender, DropEventArgs e)
    {
        if (BindingContext is DragDropPageViewModel vm)
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

            // Robustness: ensure we have the dragged slot
            //if (vm.DraggedSlot == null && e.Data.Properties.TryGetValue("slot", out var draggedSlotObj) && draggedSlotObj is BracketSlotViewModel draggedSlot)
            //{
            //    vm.DraggedSlot = draggedSlot;
            //}

            //if (sender is DropGestureRecognizer recognizer && recognizer.DropCommandParameter is BracketSlotViewModel targetSlot)
            //{
            //    // Reset visual feedback for the target
            //    if (recognizer.Parent is Border border)
            //    {
            //        border.Stroke = Colors.Gray;
            //        border.StrokeThickness = 1;
            //    }

            //    //vm.DropCommand.Execute(targetSlot);
            //}
        }
    }


    //    private void OnDragOver(object sender, DragEventArgs e)
    //    {
    //        e.AcceptedOperation = DataPackageOperation.Copy;

    //#if WINDOWS
    //        if (e.PlatformArgs != null && e.PlatformArgs.DragEventArgs != null)
    //        {
    //            // Hide everything that can block the hit testing
    //            e.PlatformArgs.DragEventArgs.DragUIOverride.IsGlyphVisible = false;
    //            e.PlatformArgs.DragEventArgs.DragUIOverride.IsCaptionVisible = false;
    //            e.PlatformArgs.DragEventArgs.DragUIOverride.IsContentVisible = false;
    //        }
    //#endif

    //        // Visual feedback for the specific hover target
    //        if (sender is DropGestureRecognizer recognizer && recognizer.Parent is Border border)
    //        {
    //            border.Stroke = Colors.DeepSkyBlue;
    //            border.StrokeThickness = 3;
    //        }
    //    }
}
