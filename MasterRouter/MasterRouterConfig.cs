using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Serilog;

namespace MasterRouter {
    internal sealed class MasterRouterConfig {
        public const string FileName = "master-router.config.json";

        public string? Primary { get; init; }
        public string? JaFallback { get; init; }
        public string? QuickTag { get; init; }
        public Dictionary<string, string> Aliases { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public static MasterRouterConfig Load(string pluginsDir) {
            var path = Path.Combine(pluginsDir ?? string.Empty, FileName);
            if (string.IsNullOrWhiteSpace(pluginsDir) || !File.Exists(path)) {
                return new MasterRouterConfig();
            }
            try {
                var json = File.ReadAllText(path);
                var model = JsonSerializer.Deserialize<MasterRouterConfigModel>(json, new JsonSerializerOptions {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                });
                var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (model?.Aliases != null) {
                    foreach (var pair in model.Aliases) {
                        var key = NormalizeKey(pair.Key);
                        var value = pair.Value?.Trim();
                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value)) {
                            aliases[key] = value;
                        }
                    }
                }
                return new MasterRouterConfig {
                    Primary = model?.Primary?.Trim(),
                    JaFallback = model?.JaFallback?.Trim(),
                    QuickTag = model?.QuickTag?.Trim(),
                    Aliases = aliases,
                };
            } catch (Exception e) {
                Log.Warning(e, $"MasterRouter: Failed to load config {path}");
                return new MasterRouterConfig();
            }
        }

        public string ResolveAliasOrSelf(string route) {
            var key = NormalizeKey(route);
            if (string.IsNullOrEmpty(key)) {
                return route;
            }
            return Aliases.TryGetValue(key, out var mapped) ? mapped : route;
        }

        public string GetQuickTagOrDefault() {
            if (!string.IsNullOrWhiteSpace(QuickTag)) {
                return QuickTag;
            }
            if (!string.IsNullOrWhiteSpace(Primary)) {
                return Primary;
            }
            return "ja";
        }

        private static string NormalizeKey(string? value) {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private sealed class MasterRouterConfigModel {
            public string? Primary { get; set; }
            public string? JaFallback { get; set; }
            public string? QuickTag { get; set; }
            public Dictionary<string, string>? Aliases { get; set; }
        }
    }
}

