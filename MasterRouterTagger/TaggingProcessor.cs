using System.Text;
using System.Text.RegularExpressions;

namespace MasterRouterTagger;

public enum TaggingMode {
    AddRouteTag,
    RemoveRouteTag,
}

internal enum ApplyStatus {
    Applied,
    NoSelectedRange,
    NoNotes,
}

internal readonly record struct ApplyResult(ApplyStatus Status, int Changed);

internal static class TaggingProcessor {
    private static readonly Regex NumberBlock = new(@"^\[#\d{4}\]$", RegexOptions.Compiled);

    public static ApplyResult Apply(string tempFilePath, string tag, bool overwriteExistingTag, TaggingMode mode) {
        if (!File.Exists(tempFilePath)) {
            throw new FileNotFoundException($"Temp file not found: {tempFilePath}");
        }
        var normalizedTag = NormalizeTag(tag);
        if (mode == TaggingMode.AddRouteTag && string.IsNullOrWhiteSpace(normalizedTag)) {
            throw new ArgumentException("Tag is empty.", nameof(tag));
        }

        var encoding = new UTF8Encoding(false);
        var lines = File.ReadAllLines(tempFilePath, encoding);
        var hasPrevMarker = lines.Any(line => string.Equals(line.Trim(), "[#PREV]", StringComparison.Ordinal));
        var hasNextMarker = lines.Any(line => string.Equals(line.Trim(), "[#NEXT]", StringComparison.Ordinal));
        var noteCount = lines.Count(line => NumberBlock.IsMatch(line.Trim()));
        if (noteCount == 0) {
            return new ApplyResult(ApplyStatus.NoNotes, 0);
        }

        // OpenUtau legacy plugin API passes whole-track range when no note is selected.
        // In this mode there is usually no PREV/NEXT boundary marker, so we refuse to apply.
        if (!hasPrevMarker && !hasNextMarker) {
            return new ApplyResult(ApplyStatus.NoSelectedRange, 0);
        }

        var changed = 0;
        var inNumberedBlock = false;
        for (var i = 0; i < lines.Length; i++) {
            var line = lines[i];
            if (line.StartsWith("[#", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal)) {
                inNumberedBlock = NumberBlock.IsMatch(line);
                continue;
            }
            if (!inNumberedBlock || !line.StartsWith("Lyric=", StringComparison.Ordinal)) {
                continue;
            }
            var lyric = line.Substring("Lyric=".Length);
            var updated = mode == TaggingMode.RemoveRouteTag
                ? RemoveTagFromLyric(lyric)
                : ApplyTagToLyric(lyric, normalizedTag, overwriteExistingTag);
            if (!string.Equals(lyric, updated, StringComparison.Ordinal)) {
                lines[i] = $"Lyric={updated}";
                changed++;
            }
        }
        if (changed > 0) {
            File.WriteAllLines(tempFilePath, lines, encoding);
        }
        return new ApplyResult(ApplyStatus.Applied, changed);
    }

    public static string ApplyToLyric(string lyric, string tag, bool overwriteExistingTag, TaggingMode mode) {
        var normalizedTag = NormalizeTag(tag);
        return mode == TaggingMode.RemoveRouteTag
            ? RemoveTagFromLyric(lyric)
            : ApplyTagToLyric(lyric, normalizedTag, overwriteExistingTag);
    }

    private static string ApplyTagToLyric(string lyric, string tag, bool overwrite) {
        if (string.IsNullOrWhiteSpace(lyric)) {
            return lyric;
        }
        if (string.Equals(lyric, "R", StringComparison.OrdinalIgnoreCase)) {
            return lyric;
        }
        if (lyric.StartsWith('+')) {
            return lyric;
        }
        if (TryParseRouteTag(lyric, out _, out var body)) {
            if (!overwrite) {
                return lyric;
            }
            return $":{tag}:{body}";
        }
        return $":{tag}:{lyric}";
    }

    private static string RemoveTagFromLyric(string lyric) {
        if (string.IsNullOrWhiteSpace(lyric)) {
            return lyric;
        }
        if (string.Equals(lyric, "R", StringComparison.OrdinalIgnoreCase)) {
            return lyric;
        }
        if (lyric.StartsWith('+')) {
            return lyric;
        }
        return TryParseRouteTag(lyric, out _, out var body) ? body : lyric;
    }

    private static bool TryParseRouteTag(string lyric, out string route, out string body) {
        route = string.Empty;
        body = lyric;
        if (string.IsNullOrEmpty(lyric)) {
            return false;
        }
        if (lyric[0] == ':') {
            var second = lyric.IndexOf(':', 1);
            if (second > 1) {
                route = lyric.Substring(1, second - 1).Trim();
                body = lyric[(second + 1)..];
                return !string.IsNullOrWhiteSpace(route);
            }
        }
        if (lyric[0] == '@') {
            var split = lyric.IndexOf("::", StringComparison.Ordinal);
            if (split > 1) {
                route = lyric.Substring(1, split - 1).Trim();
                body = lyric[(split + 2)..];
                return !string.IsNullOrWhiteSpace(route);
            }
        }
        return false;
    }

    private static string NormalizeTag(string tag) {
        tag = tag.Trim();
        while (tag.StartsWith(':')) {
            tag = tag[1..];
        }
        while (tag.EndsWith(':')) {
            tag = tag[..^1];
        }
        return tag.Trim();
    }
}

