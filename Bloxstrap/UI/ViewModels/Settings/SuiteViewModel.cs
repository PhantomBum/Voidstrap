using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Input;
using Voidstrap;
using Voidstrap.Utility;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class SuiteViewModel : NotifyPropertyChangedViewModel
    {
        public ICommand ExportSuiteBundleCommand => new RelayCommand(ExportSuiteBundle);
        public ICommand ImportSuiteBundleCommand => new RelayCommand(ImportSuiteBundle);

        private static void ExportSuiteBundle()
        {
            if (!Paths.Initialized)
            {
                Frontend.ShowMessageBox("Voidstrap is not fully initialized yet.", MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title = "Export Voidstrap Suite backup",
                Filter = "Voidstrap Suite (*.voidstrap.json)|*.voidstrap.json|JSON (*.json)|*.json|All files|*.*",
                DefaultExt = ".json",
                FileName = $"voidstrap-suite-{DateTime.UtcNow:yyyy-MM-dd}.voidstrap.json"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                VoidstrapSuiteBundle.ExportToFile(dlg.FileName);
                Frontend.ShowMessageBox(
                    "Suite backup saved.\n\nIt contains launcher settings, window state, and FastFlags (mods ClientAppSettings).",
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Export failed:\n{ex.Message}", MessageBoxImage.Error);
            }
        }

        private static void ImportSuiteBundle()
        {
            if (!Paths.Initialized)
            {
                Frontend.ShowMessageBox("Voidstrap is not fully initialized yet.", MessageBoxImage.Information);
                return;
            }

            var confirm = Frontend.ShowMessageBox(
                "Importing a suite backup will replace your current launcher settings, saved UI state, and FastFlags (including ClientAppSettings under mods).\n\nContinue?",
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo);

            if (confirm != MessageBoxResult.Yes)
                return;

            var dlg = new OpenFileDialog
            {
                Title = "Import Voidstrap Suite backup",
                Filter = "Voidstrap Suite (*.voidstrap.json;*.json)|*.voidstrap.json;*.json|All files|*.*"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                VoidstrapSuiteBundle.ImportFromFile(dlg.FileName);
                Frontend.ShowMessageBox(
                    "Suite backup imported.\n\nRestart Voidstrap so every part of the UI reloads cleanly.",
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Import failed:\n{ex.Message}", MessageBoxImage.Error);
            }
        }
    }
}
