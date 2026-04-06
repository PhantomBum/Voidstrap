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
    }
}
