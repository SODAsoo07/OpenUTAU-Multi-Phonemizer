using System;
using System.Globalization;

namespace MasterRouter {
    internal static class RouteTagParser {
        public static bool TryParse(string? lyric, out string route, out string body) {
            route = string.Empty;
            body = lyric ?? string.Empty;
            if (string.IsNullOrEmpty(lyric)) {
                return false;
            }
            // Preferred syntax: :route:lyric
            if (lyric[0] == ':') {
                var second = lyric.IndexOf(':', 1);
                if (second > 1) {
                    var candidate = lyric.Substring(1, second - 1).Trim();
                    if (!string.IsNullOrEmpty(candidate)) {
                        route = candidate;
                        body = lyric[(second + 1)..];
                        return true;
                    }
                }
            }
            // Backward compatibility: @route::lyric
            if (lyric[0] == '@') {
                var split = lyric.IndexOf("::", StringComparison.Ordinal);
                if (split > 1) {
                    var candidate = lyric.Substring(1, split - 1).Trim();
                    if (!string.IsNullOrEmpty(candidate)) {
                        route = candidate;
                        body = lyric[(split + 2)..];
                        return true;
                    }
                }
            }
            return false;
        }

        public static string StripIfTagged(string lyric) {
            return TryParse(lyric, out _, out var body) ? body : lyric;
        }

        public static bool TryParseBridge(string lyric, out string body, out string hint) {
            body = lyric ?? string.Empty;
            hint = string.Empty;
            if (string.IsNullOrEmpty(lyric)) {
                return false;
            }
            var index = lyric.LastIndexOf('>');
            if (index < 0) {
                return false;
            }
            body = lyric.Substring(0, index);
            hint = lyric[(index + 1)..].Trim();
            return true;
        }

        public static string StripBridge(string lyric) {
            return TryParseBridge(lyric, out var body, out _) ? body : lyric;
        }

        public static bool StartsWithKana(string lyric) {
            if (string.IsNullOrEmpty(lyric)) {
                return false;
            }
            foreach (var ch in lyric) {
                if (char.IsWhiteSpace(ch)) {
                    continue;
                }
                var category = char.GetUnicodeCategory(ch);
                if (category is UnicodeCategory.ConnectorPunctuation
                    or UnicodeCategory.DashPunctuation
                    or UnicodeCategory.OpenPunctuation
                    or UnicodeCategory.ClosePunctuation
                    or UnicodeCategory.InitialQuotePunctuation
                    or UnicodeCategory.FinalQuotePunctuation
                    or UnicodeCategory.OtherPunctuation
                    or UnicodeCategory.MathSymbol
                    or UnicodeCategory.CurrencySymbol
                    or UnicodeCategory.ModifierSymbol
                    or UnicodeCategory.OtherSymbol
                    or UnicodeCategory.SpaceSeparator
                    or UnicodeCategory.LineSeparator
                    or UnicodeCategory.ParagraphSeparator
                    or UnicodeCategory.Control) {
                    continue;
                }
                return IsKana(ch);
            }
            return false;
        }

        private static bool IsKana(char c) {
            return (c >= '\u3040' && c <= '\u309F')
                || (c >= '\u30A0' && c <= '\u30FF')
                || (c >= '\uFF66' && c <= '\uFF9D');
        }
    }
}

