using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Voidstrap;
using Voidstrap.Utility;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class SuiteViewModel : NotifyPropertyChangedViewModel
    {
        private string _exportNoteDraft = "";
        private string _profileStats = "";
        private string _lastPreviewSummary = "";

        public SuiteViewModel()
        {
            RefreshProfileStats();
        }

        public string ExportNoteDraft
        {
            get => _exportNoteDraft;
            set => SetProperty(ref _exportNoteDraft, value);
        }

        public bool SuiteExportIncludeWorkspaceTabs
        {
            get => App.Settings.Prop.SuiteExportIncludeWorkspaceTabs;
            set
            {
                if (App.Settings.Prop.SuiteExportIncludeWorkspaceTabs == value) return;
                App.Settings.Prop.SuiteExportIncludeWorkspaceTabs = value;
                App.Settings.Save();
                OnPropertyChanged();
            }
        }

        public bool SuiteExportIncludeRobloxState
        {
            get => App.Settings.Prop.SuiteExportIncludeRobloxState;
            set
            {
                if (App.Settings.Prop.SuiteExportIncludeRobloxState == value) return;
                App.Settings.Prop.SuiteExportIncludeRobloxState = value;
                App.Settings.Save();
                OnPropertyChanged();
            }
        }

        public string ProfileStats
        {
            get => _profileStats;
            private set => SetProperty(ref _profileStats, value);
        }

        public string LastPreviewSummary
        {
            get => _lastPreviewSummary;
            private set => SetProperty(ref _lastPreviewSummary, value);
        }

        public ICommand ExportSuiteBundleCommand => new RelayCommand(ExportSuiteBundle);
        public ICommand ImportSuiteBundleCommand => new RelayCommand(ImportSuiteBundle);
        public ICommand QuickExportToSavedBackupsCommand => new RelayCommand(QuickExportToSavedBackups);
        public ICommand ValidateBundleCommand => new RelayCommand(ValidateBundle);
        public ICommand OpenDataFolderCommand => new RelayCommand(OpenDataFolder);
        public ICommand OpenSavedBackupsFolderCommand => new RelayCommand(OpenSavedBackupsFolder);
        public ICommand OpenLogsFolderCommand => new RelayCommand(OpenLogsFolder);
        public ICommand CopyDataPathCommand => new RelayCommand(CopyDataPath);
        public ICommand RefreshProfileStatsCommand => new RelayCommand(RefreshProfileStats);

        public void RefreshProfileStats()
        {
            if (!Paths.Initialized)
            {
                ProfileStats = "Data paths are not initialized yet.";
                return;
            }

            try
            {
                var opts = new JsonSerializerOptions { WriteIndented = false };
                int flags = App.FastFlags.Prop.Count;
                int mods = App.State.Prop.ModManifest?.Count ?? 0;
                long settingsLen = JsonSerializer.Serialize(App.Settings.Prop, opts).Length;
                long stateLen = JsonSerializer.Serialize(App.State.Prop, opts).Length;
                long ffLen = JsonSerializer.Serialize(App.FastFlags.Prop, opts).Length;
                long total = settingsLen + stateLen + ffLen;

                bool tabs = File.Exists(Path.Combine(Paths.Base, "TabsConfig.json"));
                ProfileStats =
                    $"FastFlags (entries): {flags}\n" +
                    $"Mod manifest entries: {mods}\n" +
                    $"Approx. profile JSON size: {total:N0} bytes (settings + state + flags)\n" +
                    $"Workspace tabs file: {(tabs ? "present" : "not created yet")}\n" +
                    $"Data folder: {Paths.Base}";
            }
            catch (Exception ex)
            {
                ProfileStats = $"Could not compute stats: {ex.Message}";
            }
        }

        private void ExportSuiteBundle()
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

            string remembered = App.Settings.Prop.SuiteLastExportFolder;
            if (!string.IsNullOrWhiteSpace(remembered) && Directory.Exists(remembered))
                dlg.InitialDirectory = remembered;

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                string? dir = Path.GetDirectoryName(dlg.FileName);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    App.Settings.Prop.SuiteLastExportFolder = dir;
                    App.Settings.Save();
                }

                var options = new VoidstrapSuiteBundle.SuiteExportOptions
                {
                    IncludeWorkspaceTabs = SuiteExportIncludeWorkspaceTabs,
                    IncludeRobloxState = SuiteExportIncludeRobloxState,
                    ExportNote = string.IsNullOrWhiteSpace(ExportNoteDraft) ? null : ExportNoteDraft.Trim()
                };

                VoidstrapSuiteBundle.ExportToFile(dlg.FileName, options);

                Frontend.ShowMessageBox(
                    "Suite backup saved.\n\n" +
                    "It contains launcher settings, UI state, FastFlags (mods ClientAppSettings), and optional snapshots you enabled.\n" +
                    "A content fingerprint was embedded for integrity checks (Validate bundle).",
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Export failed:\n{ex.Message}", MessageBoxImage.Error);
            }
        }

        private void QuickExportToSavedBackups()
        {
            if (!Paths.Initialized)
            {
                Frontend.ShowMessageBox("Voidstrap is not fully initialized yet.", MessageBoxImage.Information);
                return;
            }

            try
            {
                Directory.CreateDirectory(Paths.SavedBackups);
                string path = Path.Combine(Paths.SavedBackups, $"voidstrap-suite-quick-{DateTime.UtcNow:yyyyMMdd-HHmmss}.voidstrap.json");
                var options = new VoidstrapSuiteBundle.SuiteExportOptions
                {
                    IncludeWorkspaceTabs = SuiteExportIncludeWorkspaceTabs,
                    IncludeRobloxState = SuiteExportIncludeRobloxState,
                    ExportNote = string.IsNullOrWhiteSpace(ExportNoteDraft) ? null : ExportNoteDraft.Trim()
                };
                VoidstrapSuiteBundle.ExportToFile(path, options);
                App.Settings.Prop.SuiteLastExportFolder = Paths.SavedBackups;
                App.Settings.Save();
                Frontend.ShowMessageBox($"Quick backup written to:\n{path}", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Quick export failed:\n{ex.Message}", MessageBoxImage.Error);
            }
        }

        private void ImportSuiteBundle()
        {
            if (!Paths.Initialized)
            {
                Frontend.ShowMessageBox("Voidstrap is not fully initialized yet.", MessageBoxImage.Information);
                return;
            }

            var confirm = Frontend.ShowMessageBox(
                "Importing a suite backup will replace your current launcher settings, saved UI state, FastFlags (including ClientAppSettings under mods), and optional snapshots stored in the file.\n\nContinue?",
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
                var preview = VoidstrapSuiteBundle.TryReadPreview(dlg.FileName);
                VoidstrapSuiteBundle.ImportFromFile(dlg.FileName);

                string extra =
                    $"\n\nBundle: {preview.FormatVersion}, exported {preview.ExportedAtUtc:u} UTC" +
                    (string.IsNullOrEmpty(preview.AppVersion) ? "" : $"\nCreated with app version: {preview.AppVersion}") +
                    (string.IsNullOrEmpty(preview.ExportNote) ? "" : $"\nNote: {preview.ExportNote}") +
                    (preview.HasTabsSnapshot ? "\nWorkspace tabs snapshot was applied." : "") +
                    (preview.HasRobloxStateSnapshot ? "\nRoblox state snapshot was applied." : "");

                Frontend.ShowMessageBox(
                    "Suite backup imported." + extra + "\n\nRestart Voidstrap so every part of the UI reloads cleanly.",
                    MessageBoxImage.Information);
                RefreshProfileStats();
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Import failed:\n{ex.Message}", MessageBoxImage.Error);
            }
        }

        private void ValidateBundle()
        {
            if (!Paths.Initialized)
            {
                Frontend.ShowMessageBox("Voidstrap is not fully initialized yet.", MessageBoxImage.Information);
                return;
            }

            var dlg = new OpenFileDialog
            {
                Title = "Validate Voidstrap Suite backup",
                Filter = "Voidstrap Suite (*.voidstrap.json;*.json)|*.voidstrap.json;*.json|All files|*.*"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                var p = VoidstrapSuiteBundle.TryReadPreview(dlg.FileName);
                string fpLine = !p.FingerprintPresent
                    ? "No fingerprint (older 2.0 export)."
                    : p.FingerprintMatches
                        ? "Content fingerprint: OK (matches file contents)."
                        : "Content fingerprint: MISMATCH — file may be damaged or edited.";

                LastPreviewSummary =
                    $"Format {p.FormatVersion} · {p.EstimatedFlagCount} flags · " +
                    $"{(p.HasTabsSnapshot ? "tabs " : "")}{(p.HasRobloxStateSnapshot ? "roblox state " : "")}";

                Frontend.ShowMessageBox(
                    $"Valid 2.x bundle.\n\n" +
                    $"Exported: {p.ExportedAtUtc:u} UTC\n" +
                    $"Product: {p.Product}\n" +
                    $"App version: {p.AppVersion ?? "(not recorded)"}\n" +
                    $"Host: {p.HostUser ?? "(not recorded)"}\n" +
                    $"Note: {p.ExportNote ?? "(none)"}\n" +
                    $"FastFlags (approx.): {p.EstimatedFlagCount}\n" +
                    $"Tabs snapshot: {(p.HasTabsSnapshot ? "yes" : "no")}\n" +
                    $"Roblox state snapshot: {(p.HasRobloxStateSnapshot ? "yes" : "no")}\n\n" +
                    fpLine,
                    p.FingerprintPresent && !p.FingerprintMatches ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LastPreviewSummary = "";
                Frontend.ShowMessageBox($"Validation failed:\n{ex.Message}", MessageBoxImage.Error);
            }
        }

        private static void OpenDataFolder()
        {
            if (!Paths.Initialized)
            {
                Frontend.ShowMessageBox("Paths are not initialized yet.", MessageBoxImage.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Base,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(ex.Message, MessageBoxImage.Error);
            }
        }

        private static void OpenSavedBackupsFolder()
        {
            if (!Paths.Initialized) return;
            try
            {
                Directory.CreateDirectory(Paths.SavedBackups);
                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.SavedBackups,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(ex.Message, MessageBoxImage.Error);
            }
        }

        private static void OpenLogsFolder()
        {
            if (!Paths.Initialized) return;
            try
            {
                Directory.CreateDirectory(Paths.Logs);
                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Logs,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(ex.Message, MessageBoxImage.Error);
            }
        }

        private static void CopyDataPath()
        {
            if (!Paths.Initialized)
            {
                Frontend.ShowMessageBox("Paths are not initialized yet.", MessageBoxImage.Information);
                return;
            }

            try
            {
                Clipboard.SetText(Paths.Base);
                Frontend.ShowMessageBox("Data folder path copied to the clipboard.", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(ex.Message, MessageBoxImage.Error);
            }
        }
    }
}
