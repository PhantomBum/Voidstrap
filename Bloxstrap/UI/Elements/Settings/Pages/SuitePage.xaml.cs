using System.Windows;
using Voidstrap.UI.ViewModels.Settings;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class SuitePage
    {
        public SuitePage()
        {
            InitializeComponent();
            DataContext = new SuiteViewModel();
        }

        private void SuitePage_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SuiteViewModel vm)
                vm.RefreshProfileStats();
        }
    }
}
