#region Imports
using House.Of.Arbitration.ViewModels.Competition;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views.Competition;

public partial class DrawsManagementPage : BasePage<DrawsManagementViewModel>
{
	public DrawsManagementPage(DrawsManagementViewModel viewModel) : base(viewModel)
	{
		InitializeComponent();

		viewModel.ScrollToItemRequested += (item) =>
		{
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				try
				{
					await Task.Yield();
					MatchesCollectionView.ScrollTo(item, position: ScrollToPosition.Center, animate: true);
				}
				catch
				{
					// Fallback
				}
			});
		};

		viewModel.ScrollToRequested += (index) =>
		{
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				try
				{
					await Task.Yield();
					MatchesCollectionView.ScrollTo(index, position: ScrollToPosition.Center, animate: true);
				}
				catch
				{
					// Fallback if needed
				}
			});
		};
	}
}
