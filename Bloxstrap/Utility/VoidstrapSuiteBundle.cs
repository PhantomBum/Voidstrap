using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Voidstrap.Models.Persistable;
using Voidstrap;

namespace Voidstrap.Utility
{
    /// <summary>
    /// Single-file backup/restore for launcher settings, UI state, and FastFlags (ClientAppSettings.json).
    /// </summary>
    internal static class VoidstrapSuiteBundle
    {
        public const string FormatVersion = "2.0";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true
        };

        public sealed class BundleDocument
        {
            public string FormatVersion { get; set; } = VoidstrapSuiteBundle.FormatVersion;
            public string Product { get; set; } = "Voidstrap";
            public DateTime ExportedAtUtc { get; set; }
            public string SettingsJson { get; set; } = "{}";
            public string StateJson { get; set; } = "{}";
            public string FastFlagsJson { get; set; } = "{}";
        }

        public static void ExportToFile(string path)
        {
            if (!Paths.Initialized)
                throw new InvalidOperationException("Voidstrap data paths are not initialized.");

            var doc = new BundleDocument
            {
                ExportedAtUtc = DateTime.UtcNow,
                SettingsJson = JsonSerializer.Serialize(App.Settings.Prop, JsonOpts),
                StateJson = JsonSerializer.Serialize(App.State.Prop, JsonOpts),
                FastFlagsJson = JsonSerializer.Serialize(App.FastFlags.Prop, JsonOpts)
            };

            string json = JsonSerializer.Serialize(doc, JsonOpts);
            File.WriteAllText(path, json);
        }

        public static void ImportFromFile(string path)
        {
            if (!Paths.Initialized)
                throw new InvalidOperationException("Voidstrap data paths are not initialized.");

            string text = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<BundleDocument>(text)
                ?? throw new InvalidOperationException("The file is not a valid Voidstrap suite bundle.");

            if (string.IsNullOrWhiteSpace(doc.FormatVersion) || !doc.FormatVersion.StartsWith("2.", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unsupported bundle format: '{doc.FormatVersion}'. Expected 2.x.");

            var settings = JsonSerializer.Deserialize<AppSettings>(doc.SettingsJson) ?? new AppSettings();
            App.Settings.Prop = settings;
            App.Settings.Save();

            var state = JsonSerializer.Deserialize<State>(doc.StateJson) ?? new State();
            App.State.Prop = state;
            App.State.Save();

            var flags = JsonSerializer.Deserialize<Dictionary<string, object>>(doc.FastFlagsJson) ?? new Dictionary<string, object>();
            App.FastFlags.Prop = flags;
            App.FastFlags.Save();
        }
    }
}
