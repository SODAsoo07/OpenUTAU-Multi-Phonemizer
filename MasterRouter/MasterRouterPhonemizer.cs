using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenUtau.Api;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using Serilog;

namespace MasterRouter {
    [Phonemizer("Master Router Phonemizer-Unofficial ", "ROUTER", "SODAsoo")]
    public class MasterRouterPhonemizer : Phonemizer {
        private USinger? singer;
        private UProject? project;
        private UTrack? track;
        private MasterRouterConfig config = new();
        private readonly Dictionary<string, PhonemizerFactory> factoriesByTag = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PhonemizerFactory> factoriesByType = new(StringComparer.Ordinal);
        private PhonemizerFactory[] allFactories = Array.Empty<PhonemizerFactory>();
        private readonly Dictionary<string, Phonemizer> delegateCache = new(StringComparer.Ordinal);
        private readonly Dictionary<int, PhonemizerFactory> routeByPosition = new();

        public override void SetSinger(USinger singer) {
            this.singer = singer;
        }

        public override void SetUp(Note[][] notes, UProject project, UTrack track) {
            this.project = project;
            this.track = track;
            config = MasterRouterConfig.Load(PluginDir);
            RebuildFactoryIndex();
            routeByPosition.Clear();

            var decisions = new RouteDecision[notes.Length];
            PhonemizerFactory? inheritedFactory = null;
            for (var i = 0; i < notes.Length; i++) {
                Note? prev = i > 0 ? notes[i - 1][0] : null;
                Note? next = i < notes.Length - 1 ? notes[i + 1][0] : null;
                var prevIsNeighbour = i > 0
                    && notes[i - 1].Length > 0
                    && notes[i].Length > 0
                    && notes[i - 1][^1].position + notes[i - 1][^1].duration >= notes[i][0].position;
                var nextIsNeighbour = i < notes.Length - 1
                    && notes[i].Length > 0
                    && notes[i + 1].Length > 0
                    && notes[i][^1].position + notes[i][^1].duration >= notes[i + 1][0].position;
                decisions[i] = DecideRoute(notes[i], inheritedFactory, prev, prevIsNeighbour, next, nextIsNeighbour);
                if (decisions[i].Factory != null) {
                    inheritedFactory = decisions[i].Factory;
                    if (decisions[i].PreparedNotes.Length > 0) {
                        routeByPosition[decisions[i].PreparedNotes[0].position] = decisions[i].Factory!;
                    }
                }
            }

            var grouped = new Dictionary<string, (PhonemizerFactory factory, List<Note[]> notes)>(StringComparer.Ordinal);
            for (var i = 0; i < notes.Length; i++) {
                var decision = decisions[i];
                if (notes[i].Length == 0) {
                    continue;
                }
                if (decision.Factory == null) {
                    continue;
                }
                var key = decision.Factory.type.FullName ?? decision.Factory.ToString();
                if (!grouped.TryGetValue(key, out var item)) {
                    item = (decision.Factory, new List<Note[]>());
                }
                item.notes.Add(decision.PreparedNotes);
                grouped[key] = item;
            }

            foreach (var pair in grouped.Values) {
                var delegatePhonemizer = GetDelegate(pair.factory);
                if (delegatePhonemizer == null) {
                    continue;
                }
                InitializeDelegate(delegatePhonemizer);
                foreach (var noteGroup in pair.notes) {
                    ApplyDelegateSpecificPreparations(delegatePhonemizer, pair.factory, noteGroup);
                }
                try {
                    delegatePhonemizer.SetUp(pair.notes.ToArray(), project, track);
                } catch (Exception e) {
                    Log.Warning(e, $"MasterRouter: delegate setup failed: {pair.factory}");
                }
            }
        }

        public override Result Process(Note[] notes, Note? prev, Note? next, Note? prevNeighbour, Note? nextNeighbour, Note[] prevs) {
            if (notes.Length == 0) {
                return new Result { phonemes = Array.Empty<Phoneme>() };
            }

            var inherited = TryGetRouteFactoryFromContextNote(prev);
            var current = DecideRoute(
                notes,
                inherited,
                prev,
                prevNeighbour.HasValue,
                next,
                nextNeighbour.HasValue);
            var factory = current.Factory;
            if (factory == null) {
                var stripped = RouteTagParser.StripIfTagged(notes[0].lyric ?? string.Empty);
                var fallbackLyric = RouteTagParser.StripBridge(stripped);
                return MakeSimpleResult(string.IsNullOrWhiteSpace(fallbackLyric) ? "a" : fallbackLyric);
            }
            routeByPosition[current.PreparedNotes[0].position] = factory;

            var prevDecision = DecideContextRoute(prev, null);
            var prevNeighbourDecision = DecideContextRoute(prevNeighbour, null);
            var nextDecision = DecideContextRoute(next, factory);
            var nextNeighbourDecision = DecideContextRoute(nextNeighbour, factory);

            bool prevBoundary = IsCrossFactoryBoundary(factory, prevDecision?.Factory)
                || IsCrossFactoryBoundary(factory, prevNeighbourDecision?.Factory);
            bool nextBoundary = IsCrossFactoryBoundary(factory, nextDecision?.Factory)
                || IsCrossFactoryBoundary(factory, nextNeighbourDecision?.Factory);

            var delegatedPrev = prevBoundary ? null : StripRoutingMetadataFromNeighbour(prev);
            var delegatedPrevNeighbour = prevBoundary ? null : StripRoutingMetadataFromNeighbour(prevNeighbour);
            var delegatedPrevs = prevBoundary
                ? Array.Empty<Note>()
                : prevs.Select(StripRoutingMetadataFromNote).ToArray();

            Note? delegatedNext = null;
            Note? delegatedNextNeighbour = null;
            if (!nextBoundary) {
                delegatedNext = StripRoutingMetadataFromNeighbour(next);
                delegatedNextNeighbour = StripRoutingMetadataFromNeighbour(nextNeighbour);
            } else if (TryBuildBridgeNeighbour(current, nextNeighbourDecision ?? nextDecision, out var bridgeNeighbour)) {
                delegatedNext = bridgeNeighbour;
                delegatedNextNeighbour = bridgeNeighbour;
            }

            var delegatePhonemizer = GetDelegate(factory);
            if (delegatePhonemizer == null) {
                return MakeSimpleResult("error");
            }

            InitializeDelegate(delegatePhonemizer);
            ApplyDelegateSpecificPreparations(delegatePhonemizer, factory, current.PreparedNotes);
            try {
                return delegatePhonemizer.Process(
                    current.PreparedNotes,
                    delegatedPrev,
                    delegatedNext,
                    delegatedPrevNeighbour,
                    delegatedNextNeighbour,
                    delegatedPrevs);
            } catch (Exception e) {
                if (CanRetryWithLocalSetup(e, delegatePhonemizer, current.PreparedNotes)) {
                    try {
                        return delegatePhonemizer.Process(
                            current.PreparedNotes,
                            delegatedPrev,
                            delegatedNext,
                            delegatedPrevNeighbour,
                            delegatedNextNeighbour,
                            delegatedPrevs);
                    } catch (Exception retryError) {
                        Log.Warning(retryError, $"MasterRouter: delegate process retry failed: {factory}");
                    }
                }
                Log.Warning(e, $"MasterRouter: delegate process failed: {factory}");
                return MakeSimpleResult("error");
            }
        }

        public override void CleanUp() {
            foreach (var phonemizer in delegateCache.Values) {
                try {
                    phonemizer.CleanUp();
                } catch (Exception e) {
                    Log.Warning(e, "MasterRouter: delegate cleanup failed");
                }
            }
        }

        private void RebuildFactoryIndex() {
            factoriesByTag.Clear();
            factoriesByType.Clear();
            allFactories = PhonemizerFactory.GetAll()
                .Where(factory => factory != null)
                .Where(factory => factory.type != GetType())
                .ToArray();

            foreach (var factory in allFactories) {
                AddFactoryTag(factory, factory.tag);
                AddFactoryType(factory);
            }
        }

        private void AddFactoryTag(PhonemizerFactory factory, string? key) {
            var normalized = Normalize(key);
            if (string.IsNullOrEmpty(normalized)) {
                return;
            }
            if (!factoriesByTag.TryGetValue(normalized, out var existing)) {
                factoriesByTag[normalized] = factory;
                return;
            }
            factoriesByTag[normalized] = ChoosePreferredFactory(existing, factory);
        }

        private void AddFactoryType(PhonemizerFactory factory) {
            var fullName = factory.type.FullName;
            if (!string.IsNullOrWhiteSpace(fullName) && !factoriesByType.ContainsKey(fullName)) {
                factoriesByType[fullName] = factory;
            }
        }

        private RouteDecision DecideRoute(
            Note[] notes,
            PhonemizerFactory? inheritedFactory,
            Note? prevContext,
            bool prevIsNeighbour,
            Note? nextContext,
            bool nextIsNeighbour) {
            var prepared = notes.ToArray();
            var first = prepared[0];
            var lyric = first.lyric ?? string.Empty;
            string workingLyric = lyric;
            string route = string.Empty;
            bool hasRouteTag = false;

            if (RouteTagParser.TryParse(lyric, out var parsedRoute, out var body)) {
                hasRouteTag = true;
                route = parsedRoute;
                workingLyric = body;
            }

            var hasBridge = RouteTagParser.TryParseBridge(workingLyric, out var lyricBody, out var bridgeHint);
            first.lyric = lyricBody;
            prepared[0] = first;

            if (hasRouteTag) {
                if (TryResolveFactory(route, out var explicitFactory) && explicitFactory != null) {
                    return new RouteDecision(explicitFactory, prepared, new BridgeDirective(hasBridge, bridgeHint));
                }
                var fallbackFactory = GetFallbackFactoryByLyric(lyricBody);
                return new RouteDecision(fallbackFactory, prepared, new BridgeDirective(hasBridge, bridgeHint));
            }

            if (ShouldUseFallbackForIsolatedMiddle(prevContext, prevIsNeighbour, nextContext, nextIsNeighbour, hasBridge)) {
                var fallbackFactory = GetFallbackFactoryByLyric(lyricBody);
                return new RouteDecision(fallbackFactory, prepared, new BridgeDirective(hasBridge, bridgeHint));
            }

            var inheritedOrPrimary = inheritedFactory ?? GetPrimaryFactory() ?? GetFirstAvailableFactory();
            return new RouteDecision(inheritedOrPrimary, prepared, new BridgeDirective(hasBridge, bridgeHint));
        }

        private bool TryResolveFactory(string identifier, out PhonemizerFactory? factory) {
            factory = null;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return TryResolveFactoryInternal(identifier, visited, out factory);
        }

        private bool TryResolveFactoryInternal(string identifier, HashSet<string> visited, out PhonemizerFactory? factory) {
            factory = null;
            var normalized = Normalize(identifier);
            if (string.IsNullOrEmpty(normalized) || !visited.Add(normalized)) {
                return false;
            }

            var target = config.ResolveAliasOrSelf(identifier);
            var targetNormalized = Normalize(target);
            if (!string.IsNullOrEmpty(targetNormalized)
                && !string.Equals(targetNormalized, normalized, StringComparison.OrdinalIgnoreCase)
                && TryResolveFactoryInternal(target, visited, out factory)) {
                return true;
            }

            return factoriesByTag.TryGetValue(normalized, out factory);
        }

        private PhonemizerFactory? GetPrimaryFactory() {
            if (TryResolveFactory(config.Primary ?? string.Empty, out var configured) && configured != null) {
                return configured;
            }
            if (!string.IsNullOrWhiteSpace(singer?.DefaultPhonemizer)
                && factoriesByType.TryGetValue(singer.DefaultPhonemizer, out var singerDefault)
                && singerDefault != null) {
                return singerDefault;
            }
            if (TryResolveFactory("DEFAULT", out var defaultFactory) && defaultFactory != null) {
                return defaultFactory;
            }
            return null;
        }

        private PhonemizerFactory? GetJaFallbackFactory() {
            if (TryResolveFactory(config.JaFallback ?? string.Empty, out var configured) && configured != null) {
                return configured;
            }
            var exact = allFactories.FirstOrDefault(factory =>
                string.Equals(factory.tag?.Trim(), "JA VCV & CVVC", StringComparison.OrdinalIgnoreCase));
            if (exact != null) {
                return exact;
            }
            var byLanguage = allFactories.FirstOrDefault(factory =>
                string.Equals(factory.language, "JA", StringComparison.OrdinalIgnoreCase));
            if (byLanguage != null) {
                return byLanguage;
            }
            return allFactories.FirstOrDefault(factory =>
                factory.tag?.StartsWith("JA ", StringComparison.OrdinalIgnoreCase) == true);
        }

        private PhonemizerFactory? GetFirstAvailableFactory() {
            return allFactories.FirstOrDefault();
        }

        private PhonemizerFactory? GetFallbackFactoryByLyric(string lyricBody) {
            return RouteTagParser.StartsWithKana(lyricBody)
                ? GetJaFallbackFactory() ?? GetPrimaryFactory()
                : GetPrimaryFactory() ?? GetJaFallbackFactory();
        }

        private Phonemizer? GetDelegate(PhonemizerFactory factory) {
            var key = factory.type.FullName ?? factory.ToString();
            if (delegateCache.TryGetValue(key, out var phonemizer)) {
                return phonemizer;
            }
            try {
                phonemizer = factory.Create();
                if (phonemizer != null) {
                    delegateCache[key] = phonemizer;
                }
                return phonemizer;
            } catch (Exception e) {
                Log.Warning(e, $"MasterRouter: failed to create delegate {factory}");
                return null;
            }
        }

        private void InitializeDelegate(Phonemizer phonemizer) {
            if (singer != null) {
                phonemizer.SetSinger(singer);
            }
            phonemizer.SetTiming(timeAxis);
        }

        private static Note? StripRoutingMetadataFromNeighbour(Note? note) {
            if (note == null) {
                return null;
            }
            return StripRoutingMetadataFromNote(note.Value);
        }

        private static Note StripRoutingMetadataFromNote(Note note) {
            var stripped = RouteTagParser.StripIfTagged(note.lyric ?? string.Empty);
            note.lyric = RouteTagParser.StripBridge(stripped);
            return note;
        }

        private static bool IsCrossFactoryBoundary(PhonemizerFactory? current, PhonemizerFactory? other) {
            if (current == null || other == null) {
                return false;
            }
            return current.type != other.type;
        }

        private bool TryBuildBridgeNeighbour(RouteDecision current, RouteDecision? next, out Note bridge) {
            bridge = default;
            if (next == null || next.Value.PreparedNotes.Length == 0 || !current.Bridge.HasMarker) {
                return false;
            }

            var manualHint = BridgeHintResolver.NormalizeManualHint(current.Bridge.Hint);
            string hint = manualHint;
            if (string.IsNullOrEmpty(hint)) {
                var nextNote = next.Value.PreparedNotes[0];
                var nextLanguage = GetLanguageHintKey(next.Value.Factory);
                hint = BridgeHintResolver.ResolveAutoHint(nextNote.phoneticHint, nextNote.lyric, nextLanguage);
            }
            if (string.IsNullOrWhiteSpace(hint)) {
                return false;
            }

            bridge = next.Value.PreparedNotes[0];
            bridge.lyric = hint;
            bridge.phoneticHint = hint;
            return true;
        }

        private static string GetLanguageHintKey(PhonemizerFactory? factory) {
            if (factory == null) {
                return string.Empty;
            }
            if (!string.IsNullOrWhiteSpace(factory.language)) {
                return factory.language.Trim();
            }
            var tag = factory.tag?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(tag)) {
                return string.Empty;
            }
            var tokens = tag.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens) {
                var normalized = token.Trim().ToUpperInvariant();
                if (normalized is "JA" or "EN" or "KO") {
                    return normalized;
                }
            }
            return string.Empty;
        }

        private static string Normalize(string? text) {
            return text?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private RouteDecision? DecideContextRoute(Note? note, PhonemizerFactory? inheritedFactory) {
            if (!note.HasValue) {
                return null;
            }
            var decision = DecideRoute(new[] { note.Value }, inheritedFactory, null, false, null, false);
            if (routeByPosition.TryGetValue(note.Value.position, out var cachedFactory)) {
                decision = decision with { Factory = cachedFactory };
            }
            return decision;
        }

        private PhonemizerFactory? TryGetRouteFactoryFromContextNote(Note? note) {
            if (!note.HasValue) {
                return null;
            }
            if (routeByPosition.TryGetValue(note.Value.position, out var cachedFactory)) {
                return cachedFactory;
            }
            if (RouteTagParser.TryParse(note.Value.lyric ?? string.Empty, out var route, out _)
                && TryResolveFactory(route, out var explicitFactory)
                && explicitFactory != null) {
                return explicitFactory;
            }
            return null;
        }

        private bool ShouldUseFallbackForIsolatedMiddle(
            Note? prevContext,
            bool prevIsNeighbour,
            Note? nextContext,
            bool nextIsNeighbour,
            bool hasBridgeHint) {
            if (hasBridgeHint) {
                return false;
            }
            if (!prevContext.HasValue || !nextContext.HasValue) {
                return false;
            }
            if (prevIsNeighbour || nextIsNeighbour) {
                return false;
            }
            var prevFactory = TryResolveExplicitFactoryFromLyric(prevContext.Value.lyric);
            var nextFactory = TryResolveExplicitFactoryFromLyric(nextContext.Value.lyric);
            if (prevFactory == null || nextFactory == null) {
                return false;
            }
            return prevFactory.type != nextFactory.type;
        }

        private PhonemizerFactory? TryResolveExplicitFactoryFromLyric(string lyric) {
            if (!RouteTagParser.TryParse(lyric, out var route, out _)) {
                return null;
            }
            if (TryResolveFactory(route, out var factory) && factory != null) {
                return factory;
            }
            return null;
        }

        private PhonemizerFactory ChoosePreferredFactory(PhonemizerFactory existing, PhonemizerFactory incoming) {
            var singerDefaultType = singer?.DefaultPhonemizer;
            if (!string.IsNullOrWhiteSpace(singerDefaultType)) {
                if (string.Equals(incoming.type.FullName, singerDefaultType, StringComparison.Ordinal)) {
                    return incoming;
                }
                if (string.Equals(existing.type.FullName, singerDefaultType, StringComparison.Ordinal)) {
                    return existing;
                }
            }
            var existingG2p = existing.type.Name.Contains("G2P", StringComparison.OrdinalIgnoreCase);
            var incomingG2p = incoming.type.Name.Contains("G2P", StringComparison.OrdinalIgnoreCase);
            if (existingG2p != incomingG2p) {
                return incomingG2p ? incoming : existing;
            }
            return existing;
        }

        private bool CanRetryWithLocalSetup(Exception error, Phonemizer delegatePhonemizer, Note[] notes) {
            if (project == null || track == null) {
                return false;
            }
            if (!error.Message.Contains("Part result not found", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            try {
                ApplyDelegateSpecificPreparations(delegatePhonemizer, null, notes);
                delegatePhonemizer.SetUp(new[] { notes }, project, track);
                return true;
            } catch (Exception setupError) {
                Log.Warning(setupError, "MasterRouter: local delegate setup retry failed");
                return false;
            }
        }

        private void ApplyDelegateSpecificPreparations(Phonemizer delegatePhonemizer, PhonemizerFactory? factory, Note[] notes) {
            if (notes.Length == 0) {
                return;
            }
            if (!IsDiffSingerFactory(factory, delegatePhonemizer)) {
                return;
            }
            var g2p = TryGetDiffSingerG2p(delegatePhonemizer);
            if (g2p == null) {
                return;
            }
            for (var i = 0; i < notes.Length; i++) {
                var note = notes[i];
                if (!string.IsNullOrWhiteSpace(note.phoneticHint)) {
                    continue;
                }
                var lyric = (note.lyric ?? string.Empty).Trim();
                if (!ShouldPromoteCaseSensitiveLyric(lyric, g2p)) {
                    continue;
                }
                note.phoneticHint = lyric;
                notes[i] = note;
            }
        }

        private static bool IsDiffSingerFactory(PhonemizerFactory? factory, Phonemizer delegatePhonemizer) {
            if (factory != null) {
                if (factory.tag?.StartsWith("DIFFS", StringComparison.OrdinalIgnoreCase) == true) {
                    return true;
                }
                if (factory.type.FullName?.Contains("DiffSinger", StringComparison.OrdinalIgnoreCase) == true) {
                    return true;
                }
            }
            return delegatePhonemizer.GetType().FullName?.Contains("DiffSinger", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static IG2p? TryGetDiffSingerG2p(Phonemizer delegatePhonemizer) {
            Type? type = delegatePhonemizer.GetType();
            while (type != null) {
                var field = type.GetField("g2p", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field?.GetValue(delegatePhonemizer) is IG2p g2p) {
                    return g2p;
                }
                type = type.BaseType;
            }
            return null;
        }

        private static bool ShouldPromoteCaseSensitiveLyric(string lyric, IG2p g2p) {
            if (string.IsNullOrEmpty(lyric) || lyric.Any(char.IsWhiteSpace)) {
                return false;
            }
            if (!lyric.Any(char.IsLetter)) {
                return false;
            }
            if (string.Equals(lyric, lyric.ToLowerInvariant(), StringComparison.Ordinal)) {
                return false;
            }
            return g2p.IsValidSymbol(lyric);
        }

        private readonly record struct RouteDecision(PhonemizerFactory? Factory, Note[] PreparedNotes, BridgeDirective Bridge);
        private readonly record struct BridgeDirective(bool HasMarker, string Hint);
    }
}

