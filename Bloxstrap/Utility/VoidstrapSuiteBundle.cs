using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Voidstrap.Models.Persistable;
using Voidstrap;

namespace Voidstrap.Utility
{
    /// <summary>
    /// Single-file backup/restore for launcher settings, UI state, FastFlags, and optional snapshots.
    /// </summary>
    internal static class VoidstrapSuiteBundle
    {
        /// <summary>Current bundle format written by export. Imports accept any 2.x.</summary>
        public const string FormatVersion = "2.1";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true
        };

        public sealed class BundleDocument
        {
            public string FormatVersion { get; set; } = VoidstrapSuiteBundle.FormatVersion;
            public string Product { get; set; } = "Voidstrap";
            public DateTime ExportedAtUtc { get; set; }
            public string? AppVersion { get; set; }
            public string? HostUser { get; set; }
            public string? ExportNote { get; set; }
            /// <summary>SHA-256 (hex) of SettingsJson + newline + StateJson + newline + FastFlagsJson.</summary>
            public string? ContentFingerprint { get; set; }
            public string SettingsJson { get; set; } = "{}";
            public string StateJson { get; set; } = "{}";
            public string FastFlagsJson { get; set; } = "{}";
            public string? TabsConfigJson { get; set; }
            public string? RobloxStateJson { get; set; }
        }

        public sealed class SuiteExportOptions
        {
            public bool IncludeWorkspaceTabs { get; set; } = true;
            public bool IncludeRobloxState { get; set; } = true;
            public string? ExportNote { get; set; }
        }

        public sealed class BundlePreview
        {
            public string FormatVersion { get; init; } = "";
            public DateTime ExportedAtUtc { get; init; }
            public string? Product { get; init; }
            public string? AppVersion { get; init; }
            public string? HostUser { get; init; }
            public string? ExportNote { get; init; }
            public bool HasTabsSnapshot { get; init; }
            public bool HasRobloxStateSnapshot { get; init; }
            public int EstimatedFlagCount { get; init; }
            public bool FingerprintPresent { get; init; }
            public bool FingerprintMatches { get; init; }
        }

        public static string ComputePayloadFingerprint(string settingsJson, string stateJson, string fastFlagsJson)
        {
            string payload = settingsJson + "\n" + stateJson + "\n" + fastFlagsJson;
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash);
        }

        public static void ExportToFile(string path, SuiteExportOptions? options = null)
        {
            options ??= new SuiteExportOptions();

            if (!Paths.Initialized)
                throw new InvalidOperationException("Voidstrap data paths are not initialized.");

            var doc = new BundleDocument
            {
                ExportedAtUtc = DateTime.UtcNow,
                AppVersion = App.Version,
                HostUser = Environment.UserName,
                ExportNote = string.IsNullOrWhiteSpace(options.ExportNote) ? null : options.ExportNote.Trim(),
                SettingsJson = JsonSerializer.Serialize(App.Settings.Prop, JsonOpts),
                StateJson = JsonSerializer.Serialize(App.State.Prop, JsonOpts),
                FastFlagsJson = JsonSerializer.Serialize(App.FastFlags.Prop, JsonOpts)
            };

            doc.ContentFingerprint = ComputePayloadFingerprint(doc.SettingsJson, doc.StateJson, doc.FastFlagsJson);

            if (options.IncludeWorkspaceTabs)
            {
                string tabsPath = Path.Combine(Paths.Base, "TabsConfig.json");
                if (File.Exists(tabsPath))
                    doc.TabsConfigJson = File.ReadAllText(tabsPath);
            }

            if (options.IncludeRobloxState)
                doc.RobloxStateJson = JsonSerializer.Serialize(App.RobloxState.Prop, JsonOpts);

            string json = JsonSerializer.Serialize(doc, JsonOpts);
            File.WriteAllText(path, json);
        }

        public static BundlePreview TryReadPreview(string path)
        {
            string text = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<BundleDocument>(text)
                ?? throw new InvalidOperationException("The file is not a valid Voidstrap suite bundle.");

            if (string.IsNullOrWhiteSpace(doc.FormatVersion) || !doc.FormatVersion.StartsWith("2.", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unsupported bundle format: '{doc.FormatVersion}'. Expected 2.x.");

            int flagCount = 0;
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(doc.FastFlagsJson);
                flagCount = dict?.Count ?? 0;
            }
            catch
            {
                /* ignore */
            }

            string fp = ComputePayloadFingerprint(doc.SettingsJson, doc.StateJson, doc.FastFlagsJson);
            bool fpPresent = !string.IsNullOrEmpty(doc.ContentFingerprint);
            bool fpOk = fpPresent && string.Equals(doc.ContentFingerprint, fp, StringComparison.OrdinalIgnoreCase);

            return new BundlePreview
            {
                FormatVersion = doc.FormatVersion,
                ExportedAtUtc = doc.ExportedAtUtc,
                Product = doc.Product,
                AppVersion = doc.AppVersion,
                HostUser = doc.HostUser,
                ExportNote = doc.ExportNote,
                HasTabsSnapshot = !string.IsNullOrEmpty(doc.TabsConfigJson),
                HasRobloxStateSnapshot = !string.IsNullOrEmpty(doc.RobloxStateJson),
                EstimatedFlagCount = flagCount,
                FingerprintPresent = fpPresent,
                FingerprintMatches = fpOk
            };
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

            if (!string.IsNullOrEmpty(doc.TabsConfigJson))
            {
                string tabsPath = Path.Combine(Paths.Base, "TabsConfig.json");
                File.WriteAllText(tabsPath, doc.TabsConfigJson);
            }

            if (!string.IsNullOrEmpty(doc.RobloxStateJson))
            {
                var rbx = JsonSerializer.Deserialize<RobloxState>(doc.RobloxStateJson) ?? new RobloxState();
                App.RobloxState.Prop = rbx;
                App.RobloxState.Save();
            }
        }
    }
}
