using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterRouter {
    internal static class BridgeHintResolver {
        private static readonly string[] EnglishDigraphs = {
            "ch", "sh", "th", "ph", "wh", "qu", "ng", "zh", "ts", "dz",
        };

        private static readonly string[] KoreanOnsetMap = {
            "g", "kk", "n", "d", "tt", "r", "m", "b", "pp", "s", "ss",
            "", "j", "jj", "ch", "k", "t", "p", "h",
        };

        private static readonly string[] KoreanVowelMap = {
            "a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", "wa", "wae",
            "oe", "yo", "u", "wo", "we", "wi", "yu", "eu", "ui", "i",
        };

        private static readonly HashSet<char> SmallKana = new() {
            '\u3083', '\u3085', '\u3087', '\u3041', '\u3043', '\u3045', '\u3047', '\u3049', '\u3095',
            '\u30E3', '\u30E5', '\u30E7', '\u30A1', '\u30A3', '\u30A5', '\u30A7', '\u30A9', '\u30EE',
        };

        private static readonly Dictionary<char, string> JaSingleMap = BuildJaSingleMap();
        private static readonly Dictionary<string, string> JaDigraphMap = BuildJaDigraphMap();

        public static string NormalizeManualHint(string? rawHint) {
            if (string.IsNullOrWhiteSpace(rawHint)) {
                return string.Empty;
            }
            var token = rawHint
                .Split(new[] { ' ', '\t', ',', ';', '/', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            return token?.Trim() ?? string.Empty;
        }

        public static string ResolveAutoHint(string? nextPhoneticHint, string? nextLyric, string? nextLanguage) {
            var tokenFromHint = NormalizeManualHint(nextPhoneticHint);
            if (!string.IsNullOrEmpty(tokenFromHint)) {
                return tokenFromHint;
            }
            var lyric = (nextLyric ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(lyric)) {
                return string.Empty;
            }
            var language = NormalizeLanguage(nextLanguage);
            return language switch {
                "JA" => ResolveJapanese(lyric),
                "KO" => ResolveKorean(lyric),
                "EN" => ResolveEnglish(lyric),
                _ => ResolveByAsciiFallback(lyric),
            };
        }

        private static string NormalizeLanguage(string? language) {
            if (string.IsNullOrWhiteSpace(language)) {
                return string.Empty;
            }
            var token = language.Trim().ToUpperInvariant();
            if (token.StartsWith("JA", StringComparison.Ordinal)) {
                return "JA";
            }
            if (token.StartsWith("KO", StringComparison.Ordinal)) {
                return "KO";
            }
            if (token.StartsWith("EN", StringComparison.Ordinal)) {
                return "EN";
            }
            return token;
        }

        private static string ResolveByAsciiFallback(string lyric) {
            var first = lyric.FirstOrDefault(ch => !char.IsWhiteSpace(ch));
            return first == default ? string.Empty : first.ToString();
        }

        private static string ResolveEnglish(string lyric) {
            var lowered = lyric.ToLowerInvariant();
            var index = 0;
            while (index < lowered.Length && !char.IsLetter(lowered[index])) {
                index++;
            }
            if (index >= lowered.Length) {
                return ResolveByAsciiFallback(lyric);
            }
            var slice = lowered[index..];
            foreach (var digraph in EnglishDigraphs) {
                if (slice.StartsWith(digraph, StringComparison.Ordinal)) {
                    return digraph;
                }
            }
            return slice[0].ToString();
        }

        private static string ResolveJapanese(string lyric) {
            var chars = lyric.Where(ch => !char.IsWhiteSpace(ch)).ToArray();
            if (chars.Length == 0) {
                return string.Empty;
            }
            var first = chars[0];
            if (chars.Length > 1 && SmallKana.Contains(chars[1])) {
                var digraph = $"{first}{chars[1]}";
                if (JaDigraphMap.TryGetValue(digraph, out var mappedDigraph)) {
                    return mappedDigraph;
                }
            }
            if (JaSingleMap.TryGetValue(first, out var mapped)) {
                return mapped;
            }
            return first.ToString();
        }

        private static string ResolveKorean(string lyric) {
            var chars = lyric.Where(ch => !char.IsWhiteSpace(ch)).ToArray();
            if (chars.Length == 0) {
                return string.Empty;
            }
            var first = chars[0];
            if (first < 0xAC00 || first > 0xD7A3) {
                return ResolveByAsciiFallback(lyric);
            }
            var syllableIndex = first - 0xAC00;
            var onset = syllableIndex / 588;
            var vowel = (syllableIndex % 588) / 28;
            if (onset >= 0 && onset < KoreanOnsetMap.Length) {
                var onsetHint = KoreanOnsetMap[onset];
                if (!string.IsNullOrEmpty(onsetHint)) {
                    return onsetHint;
                }
            }
            if (vowel >= 0 && vowel < KoreanVowelMap.Length) {
                return KoreanVowelMap[vowel];
            }
            return first.ToString();
        }

        private static Dictionary<char, string> BuildJaSingleMap() {
            var map = new Dictionary<char, string>();
            void Add(char hira, char kata, string roman) {
                map[hira] = roman;
                map[kata] = roman;
            }

            Add('\u3042', '\u30A2', "a");
            Add('\u3044', '\u30A4', "i");
            Add('\u3046', '\u30A6', "u");
            Add('\u3048', '\u30A8', "e");
            Add('\u304A', '\u30AA', "o");

            Add('\u304B', '\u30AB', "k");
            Add('\u304D', '\u30AD', "k");
            Add('\u304F', '\u30AF', "k");
            Add('\u3051', '\u30B1', "k");
            Add('\u3053', '\u30B3', "k");

            Add('\u304C', '\u30AC', "g");
            Add('\u304E', '\u30AE', "g");
            Add('\u3050', '\u30B0', "g");
            Add('\u3052', '\u30B2', "g");
            Add('\u3054', '\u30B4', "g");

            Add('\u3055', '\u30B5', "s");
            Add('\u3057', '\u30B7', "sh");
            Add('\u3059', '\u30B9', "s");
            Add('\u305B', '\u30BB', "s");
            Add('\u305D', '\u30BD', "s");

            Add('\u3056', '\u30B6', "z");
            Add('\u3058', '\u30B8', "j");
            Add('\u305A', '\u30BA', "z");
            Add('\u305C', '\u30BC', "z");
            Add('\u305E', '\u30BE', "z");

            Add('\u305F', '\u30BF', "t");
            Add('\u3061', '\u30C1', "ch");
            Add('\u3064', '\u30C4', "ts");
            Add('\u3066', '\u30C6', "t");
            Add('\u3068', '\u30C8', "t");

            Add('\u3060', '\u30C0', "d");
            Add('\u3062', '\u30C2', "j");
            Add('\u3065', '\u30C5', "z");
            Add('\u3067', '\u30C7', "d");
            Add('\u3069', '\u30C9', "d");

            Add('\u306A', '\u30CA', "n");
            Add('\u306B', '\u30CB', "n");
            Add('\u306C', '\u30CC', "n");
            Add('\u306D', '\u30CD', "n");
            Add('\u306E', '\u30CE', "n");

            Add('\u306F', '\u30CF', "h");
            Add('\u3072', '\u30D2', "h");
            Add('\u3075', '\u30D5', "f");
            Add('\u3078', '\u30D8', "h");
            Add('\u307B', '\u30DB', "h");

            Add('\u3070', '\u30D0', "b");
            Add('\u3073', '\u30D3', "b");
            Add('\u3076', '\u30D6', "b");
            Add('\u3079', '\u30D9', "b");
            Add('\u307C', '\u30DC', "b");

            Add('\u3071', '\u30D1', "p");
            Add('\u3074', '\u30D4', "p");
            Add('\u3077', '\u30D7', "p");
            Add('\u307A', '\u30DA', "p");
            Add('\u307D', '\u30DD', "p");

            Add('\u307E', '\u30DE', "m");
            Add('\u307F', '\u30DF', "m");
            Add('\u3080', '\u30E0', "m");
            Add('\u3081', '\u30E1', "m");
            Add('\u3082', '\u30E2', "m");

            Add('\u3084', '\u30E4', "y");
            Add('\u3086', '\u30E6', "y");
            Add('\u3088', '\u30E8', "y");

            Add('\u3089', '\u30E9', "r");
            Add('\u308A', '\u30EA', "r");
            Add('\u308B', '\u30EB', "r");
            Add('\u308C', '\u30EC', "r");
            Add('\u308D', '\u30ED', "r");

            Add('\u308F', '\u30EF', "w");
            Add('\u3092', '\u30F2', "o");
            Add('\u3093', '\u30F3', "n");

            return map;
        }

        private static Dictionary<string, string> BuildJaDigraphMap() {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            void Add(char hiraBase, char kataBase, string roman) {
                map[$"{hiraBase}\u3083"] = roman;
                map[$"{hiraBase}\u3085"] = roman;
                map[$"{hiraBase}\u3087"] = roman;
                map[$"{kataBase}\u30E3"] = roman;
                map[$"{kataBase}\u30E5"] = roman;
                map[$"{kataBase}\u30E7"] = roman;
            }

            Add('\u304D', '\u30AD', "ky");
            Add('\u304E', '\u30AE', "gy");
            Add('\u3057', '\u30B7', "sh");
            Add('\u3058', '\u30B8', "j");
            Add('\u3061', '\u30C1', "ch");
            Add('\u3062', '\u30C2', "j");
            Add('\u306B', '\u30CB', "ny");
            Add('\u3072', '\u30D2', "hy");
            Add('\u3073', '\u30D3', "by");
            Add('\u3074', '\u30D4', "py");
            Add('\u307F', '\u30DF', "my");
            Add('\u308A', '\u30EA', "ry");

            return map;
        }
    }
}
