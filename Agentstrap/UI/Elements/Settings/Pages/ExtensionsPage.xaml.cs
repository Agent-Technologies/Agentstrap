namespace Agentstrap.UI.Elements.Settings.Pages
{
    using Agentstrap.UI.ViewModels.Settings;

    /// <summary>
    /// Interaction logic for ExtensionsPage.xaml
    /// </summary>
    public partial class ExtensionsPage
    {
        public ExtensionsPage()
        {
            DataContext = new ExtensionsViewModel();
            InitializeComponent();
        }

        private void FilterChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DataContext is not ExtensionsViewModel vm) return;
            vm.Search = SearchBox?.Text ?? "";
            vm.Category = (CategoryBox?.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "All categories";
            vm.StatusFilter = (StatusBox?.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "All";
        }

        private void FilterChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (DataContext is ExtensionsViewModel vm) vm.Search = SearchBox.Text;
        }
    }
}
