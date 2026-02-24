namespace House.Of.Arbitration.Maui.Designer
{
    public partial class MainPage : BasePage
    {
        int count = 0;

        public MainPage()
        {
            InitializeComponent();
            IsBackButtonVisible = false; // Home page doesn't need a back button
        }

        private void OnCounterClicked(object? sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }

        private void OnToggleMenuClicked(object sender, EventArgs e)
        {
            IsMenuVisible = !IsMenuVisible;
        }
    }
}
