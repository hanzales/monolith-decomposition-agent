using System.Text.RegularExpressions;
using Migration.Intelligence.Contracts.Discovery;
using Migration.Intelligence.Contracts.Domain;
using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Core.Abstractions;
using Migration.Intelligence.Core.Options;
using Migration.Intelligence.DomainInference.Heuristics;

namespace Migration.Intelligence.DomainInference.Services;

public sealed partial class DomainInferenceEngine : IDomainInferenceEngine
{
    private const int MaxSourceHintsPerService = 8;

    private static readonly HashSet<string> IgnoredTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "test", "tests", "testing", "backup", "sample", "samples", "example", "examples", "temp", "tmp",
        "readme", "docs", "doc", "node", "node_modules", "packages", "package", "obj", "bin", "generated",
        "legacy", "old"
    };

    private static readonly HashSet<string> StructuralTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "core", "domain", "data", "helper", "helpers", "common", "shared", "contracts", "contract",
        "abstractions", "abstraction", "infrastructure", "application", "library", "libraries", "lib", "libs",
        "model", "models", "dto", "dtos", "entity", "entities", "service", "services", "module", "modules",
        "ui", "src", "source", "workflow", "workflows", "client", "clients",
        "repository", "repositories", "project", "projects", "solution", "solutions"
    };

    private static readonly HashSet<string> ControllerNoiseTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "base", "home", "config", "health", "status", "error", "errors", "ping"
    };

    private static readonly HashSet<string> PathNoiseSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "src", "source", "code", "app", "apps", "package", "packages", "node_modules", "obj", "bin", "dist"
    };

    public async Task<List<ServiceBlueprintContract>> InferServicesAsync(
        RepositoryInventoryContract inventory,
        CodeInsightsContract insights,
        AnalysisOptions options,
        CancellationToken cancellationToken = default)
    {
        var inferred = new Dictionary<string, CandidateEvidence>(StringComparer.OrdinalIgnoreCase);
        var organizationPrefixes = DetectOrganizationPrefixes(inventory.Projects.Select(x => x.Name));

        foreach (var docPath in options.ArchitectureMarkdownPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(docPath))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(docPath, cancellationToken);
            foreach (var serviceName in ExtractServiceNames(content))
            {
                AddEvidence(
                    inferred,
                    serviceName,
                    $"architecture-doc:{Path.GetFileName(docPath)}",
                    $"Mentioned in architecture markdown ({Path.GetFileName(docPath)}).",
                    score: 12);
            }
        }

        var projectFileCounts = BuildProjectSourceFileCountMap(inventory);
        foreach (var project in inventory.Projects)
        {
            if (IsLikelyNonServiceProject(project))
            {
                continue;
            }

            var primaryCandidate = ExtractPrimaryCandidate(project.Name, organizationPrefixes);
            if (!string.IsNullOrWhiteSpace(primaryCandidate))
            {
                var sourceFileCount = projectFileCounts.GetValueOrDefault(project.RelativePath, 0);
                var sizeBoost = Math.Min(8, sourceFileCount / 150);
                AddEvidence(
                    inferred,
                    primaryCandidate,
                    $"project:{project.Name}",
                    $"Derived from project naming ({project.Name}).",
                    score: 6 + sizeBoost);
            }

            var secondaryCandidate = ExtractSecondaryCandidate(project.Name, organizationPrefixes);
            if (!string.IsNullOrWhiteSpace(secondaryCandidate)
                && !secondaryCandidate.Equals(primaryCandidate, StringComparison.OrdinalIgnoreCase))
            {
                AddEvidence(
                    inferred,
                    secondaryCandidate,
                    $"project-subdomain:{project.Name}",
                    $"Derived from project subdomain naming ({project.Name}).",
                    score: 4);
            }

            var pathCandidate = ExtractPathCandidate(project.RelativePath, organizationPrefixes);
            if (!string.IsNullOrWhiteSpace(pathCandidate))
            {
                AddEvidence(
                    inferred,
                    pathCandidate,
                    $"project-path:{project.RelativePath}",
                    $"Derived from project path ({project.RelativePath}).",
                    score: 4);
            }
        }

        var controllerStemCounts = ExtractControllerStemCounts(inventory.SourceFiles, organizationPrefixes);
        foreach (var (stem, count) in controllerStemCounts
                     .OrderByDescending(x => x.Value)
                     .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(12))
        {
            if (count < 3)
            {
                continue;
            }

            AddEvidence(
                inferred,
                stem,
                $"controller:{stem} ({count})",
                "Derived from controller naming frequency.",
                score: 2 + Math.Min(5, count));
        }

        foreach (var folder in insights.TopLevelSourceFolders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = ExtractPrimaryCandidate(folder, organizationPrefixes);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                AddEvidence(
                    inferred,
                    candidate,
                    $"source-folder:{folder}",
                    $"Derived from source folder signal ({folder}).",
                    score: 3);
            }
        }

        var selected = inferred.Values
            .Where(x => x.Score >= options.DomainInference.MinimumConfidenceScore
                        || x.SourceHints.Any(y => y.StartsWith("architecture-doc:", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.ServiceName, StringComparer.OrdinalIgnoreCase)
            .Take(options.DomainInference.MaxServiceCount)
            .Select(ToBlueprint)
            .ToList();

        if (selected.Count == 0)
        {
            selected.Add(new ServiceBlueprintContract
            {
                ServiceName = "Core",
                BoundedContext = "CoreContext",
                Description = "No service hints found, created default Core service.",
                SourceHints = new List<string> { "fallback" },
                ConfidenceScore = 1
            });
        }

        var clusterBuilder = new DomainClusterBuilder();
        var clusters = clusterBuilder.BuildClusters(selected);

        var scoringService = new MigrationScoringService(
            new NamingHeuristics(),
            new CohesionHeuristics(),
            new CouplingHeuristics(),
            new DataOwnershipHeuristics());

        var scoreCards = scoringService.Score(selected, clusters, inventory, insights);
        var scoreContracts = scoringService.ToContracts(scoreCards);

        var recommendationService = new RecommendationService();
        var recommendations = recommendationService.BuildRecommendations(scoreContracts);

        var scoreByService = scoreContracts
            .GroupBy(score => score.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(score => score.OverallScore)
                    .ThenBy(score => score.ServiceName, StringComparer.OrdinalIgnoreCase)
                    .First(),
                StringComparer.OrdinalIgnoreCase);
        var recommendationByService = recommendations
            .GroupBy(recommendation => recommendation.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(recommendation => recommendation.ActionItems.Count)
                    .ThenBy(recommendation => recommendation.ServiceName, StringComparer.OrdinalIgnoreCase)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        return selected
            .Select(service => EnrichWithScoring(service, scoreByService, recommendationByService))
            .OrderByDescending(x => x.ConfidenceScore)
            .ThenBy(x => x.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ServiceBlueprintContract EnrichWithScoring(
        ServiceBlueprintContract service,
        IReadOnlyDictionary<string, MigrationScoreContract> scoreByService,
        IReadOnlyDictionary<string, MigrationRecommendationContract> recommendationByService)
    {
        scoreByService.TryGetValue(service.ServiceName, out var score);
        recommendationByService.TryGetValue(service.ServiceName, out var recommendation);

        var sourceHints = service.SourceHints.ToList();
        var scoringHints = new List<string>();
        if (score is not null)
        {
            scoringHints.Add($"score:overall={score.OverallScore}");
            scoringHints.Add($"score:dependency={score.DependencyHealthScore}");
            scoringHints.Add($"score:legacy={score.LegacyRiskScore}");
        }

        var allowedBaseHintCount = Math.Max(0, MaxSourceHintsPerService - scoringHints.Count);
        if (sourceHints.Count > allowedBaseHintCount)
        {
            sourceHints = sourceHints.Take(allowedBaseHintCount).ToList();
        }

        sourceHints.AddRange(scoringHints);

        var confidenceFromScore = score is null
            ? 0
            : (int)Math.Round(score.OverallScore / 8.0, MidpointRounding.AwayFromZero);

        var description = service.Description;
        if (score is not null)
        {
            description = $"{description} DependencyHealth={score.DependencyHealthScore}, LegacyRisk={score.LegacyRiskScore}.";
        }

        if (recommendation is not null)
        {
            description = $"{description} {recommendation.Summary}";
        }

        return new ServiceBlueprintContract
        {
            ServiceName = service.ServiceName,
            BoundedContext = service.BoundedContext,
            Description = description,
            SourceHints = sourceHints,
            ConfidenceScore = Math.Max(service.ConfidenceScore, confidenceFromScore)
        };
    }

    private static void AddEvidence(
        IDictionary<string, CandidateEvidence> container,
        string rawName,
        string sourceHint,
        string reason,
        int score)
    {
        var normalizedName = NormalizeName(rawName);
        if (string.IsNullOrWhiteSpace(normalizedName) || IsIgnoredOrStructural(normalizedName))
        {
            return;
        }

        if (container.TryGetValue(normalizedName, out var existing))
        {
            existing.Add(sourceHint, reason, score);
            return;
        }

        var evidence = new CandidateEvidence(normalizedName);
        evidence.Add(sourceHint, reason, score);
        container[normalizedName] = evidence;
    }

    private static ServiceBlueprintContract ToBlueprint(CandidateEvidence evidence)
    {
        return new ServiceBlueprintContract
        {
            ServiceName = evidence.ServiceName,
            BoundedContext = $"{evidence.ServiceName}Context",
            Description = evidence.BuildDescription(),
            SourceHints = evidence.SourceHints.ToList(),
            ConfidenceScore = evidence.Score
        };
    }

    private static Dictionary<string, int> BuildProjectSourceFileCountMap(RepositoryInventoryContract inventory)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var sourcePaths = inventory.SourceFiles
            .Where(file => file.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(x => NormalizePath(x.RelativePath))
            .ToList();

        foreach (var project in inventory.Projects)
        {
            var projectRoot = NormalizePath(Path.GetDirectoryName(project.RelativePath) ?? string.Empty);
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                counts[project.RelativePath] = 0;
                continue;
            }

            var expectedPrefix = $"{projectRoot}/";
            var sourceCount = sourcePaths.Count(path =>
                path.Equals(projectRoot, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase));

            counts[project.RelativePath] = sourceCount;
        }

        return counts;
    }

    private static Dictionary<string, int> ExtractControllerStemCounts(
        IEnumerable<FileContract> sourceFiles,
        ISet<string> organizationPrefixes)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in sourceFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file.RelativePath);
            if (!fileName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rawStem = fileName[..^"Controller".Length];
            var normalizedStem = NormalizeName(rawStem);
            if (string.IsNullOrWhiteSpace(normalizedStem) || ControllerNoiseTokens.Contains(normalizedStem))
            {
                continue;
            }

            if (IsIgnoredOrStructural(normalizedStem))
            {
                continue;
            }

            var normalizedTokens = Tokenize(rawStem)
                .Select(ToCanonicalToken)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (normalizedTokens.Count > 0 && organizationPrefixes.Contains(normalizedTokens[0]))
            {
                normalizedStem = NormalizeName(string.Concat(normalizedTokens.Skip(1)));
            }

            if (string.IsNullOrWhiteSpace(normalizedStem) || ControllerNoiseTokens.Contains(normalizedStem))
            {
                continue;
            }

            counts[normalizedStem] = counts.GetValueOrDefault(normalizedStem, 0) + 1;
        }

        return counts;
    }

    private static HashSet<string> DetectOrganizationPrefixes(IEnumerable<string> projectNames)
    {
        var firstTokens = projectNames
            .Select(Tokenize)
            .Where(tokens => tokens.Count > 0)
            .Select(tokens => ToCanonicalToken(tokens[0]))
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();

        if (firstTokens.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var threshold = Math.Max(2, (int)Math.Ceiling(firstTokens.Count * 0.6));
        return firstTokens
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() >= threshold)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsLikelyNonServiceProject(ProjectContract project)
    {
        var normalizedName = NormalizeName(project.Name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return true;
        }

        if (normalizedName.EndsWith("Test", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith("Tests", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tokens = Tokenize(project.Name)
            .Select(ToCanonicalToken)
            .ToList();

        return tokens.Any(IgnoredTokens.Contains);
    }

    private static string? ExtractPathCandidate(string relativePath, ISet<string> organizationPrefixes)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        // Ignore the project file name at the end.
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var segment = parts[i];
            if (PathNoiseSegments.Contains(segment) || IsVersionSegment(segment))
            {
                continue;
            }

            var candidate = ExtractPrimaryCandidate(segment, organizationPrefixes);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ExtractPrimaryCandidate(string value, ISet<string> organizationPrefixes)
    {
        var segments = Regex.Split(value, @"[^A-Za-z0-9]+")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (segments.Count == 0)
        {
            return null;
        }

        var index = 0;
        while (index < segments.Count && organizationPrefixes.Contains(ToCanonicalToken(segments[index])))
        {
            index++;
        }

        for (var i = index; i < segments.Count; i++)
        {
            var candidate = NormalizeName(segments[i]);
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (IsIgnoredOrStructural(candidate))
            {
                continue;
            }

            if (!IsCandidateToken(candidate))
            {
                continue;
            }

            return candidate;
        }

        var tokens = Tokenize(string.Concat(segments.Skip(index)))
            .Select(ToCanonicalToken)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (tokens.Count == 0)
        {
            return null;
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (IgnoredTokens.Contains(token))
            {
                continue;
            }

            if (StructuralTokens.Contains(token))
            {
                continue;
            }

            if (!IsCandidateToken(token))
            {
                continue;
            }

            return token;
        }

        return null;
    }

    private static string? ExtractSecondaryCandidate(string value, ISet<string> organizationPrefixes)
    {
        var segments = Regex.Split(value, @"[^A-Za-z0-9]+")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (segments.Count < 2)
        {
            return null;
        }

        var index = 0;
        while (index < segments.Count && organizationPrefixes.Contains(ToCanonicalToken(segments[index])))
        {
            index++;
        }

        for (var i = index + 1; i < segments.Count; i++)
        {
            var candidate = NormalizeName(segments[i]);
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (IsIgnoredOrStructural(candidate))
            {
                continue;
            }

            if (!IsCandidateToken(candidate))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static bool IsCandidateToken(string token)
    {
        return token.Length >= 3;
    }

    private static IEnumerable<string> ExtractServiceNames(string markdown)
    {
        foreach (Match match in ServiceBulletRegex().Matches(markdown))
        {
            yield return match.Groups["name"].Value;
        }

        foreach (Match match in ServiceHeadingRegex().Matches(markdown))
        {
            yield return match.Groups["name"].Value;
        }
    }

    private static bool IsIgnoredOrStructural(string value)
    {
        return IgnoredTokens.Contains(value) || StructuralTokens.Contains(value);
    }

    private static bool IsVersionSegment(string segment)
    {
        if (segment.Length < 2 || (segment[0] != 'v' && segment[0] != 'V'))
        {
            return false;
        }

        return segment[1..].All(char.IsDigit);
    }

    private static string NormalizePath(string value)
    {
        return value.Replace('\\', '/').Trim('/');
    }

    private static List<string> Tokenize(string value)
    {
        var parts = Regex.Split(value, @"[^A-Za-z0-9]+")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var tokens = new List<string>();
        foreach (var part in parts)
        {
            tokens.AddRange(SplitCamelAndDigits(part));
        }

        return tokens;
    }

    private static IEnumerable<string> SplitCamelAndDigits(string token)
    {
        if (token.Length == 0)
        {
            yield break;
        }

        var start = 0;
        for (var i = 1; i < token.Length; i++)
        {
            var current = token[i];
            var previous = token[i - 1];
            var next = i + 1 < token.Length ? token[i + 1] : '\0';

            var isBoundary =
                (char.IsUpper(current) && char.IsLower(previous))
                || (char.IsDigit(current) && !char.IsDigit(previous))
                || (!char.IsDigit(current) && char.IsDigit(previous))
                || (char.IsUpper(current) && char.IsUpper(previous) && next != '\0' && char.IsLower(next));

            if (!isBoundary)
            {
                continue;
            }

            yield return token[start..i];
            start = i;
        }

        yield return token[start..];
    }

    private static string ToCanonicalToken(string token)
    {
        if (token.Length == 0)
        {
            return string.Empty;
        }

        if (token.Length == 1)
        {
            return token.ToUpperInvariant();
        }

        if (token.All(char.IsUpper))
        {
            return token.Length <= 3
                ? token
                : char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant();
        }

        if (token.Any(char.IsUpper) && token.Any(char.IsLower))
        {
            return char.ToUpperInvariant(token[0]) + token[1..];
        }

        return char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant();
    }

    private static string NormalizeName(string rawName)
    {
        var cleaned = Regex.Replace(rawName.Trim(), @"[^A-Za-z0-9]+", " ");
        var tokens = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var normalized = string.Concat(tokens.Select(ToPascalToken));
        return normalized.EndsWith("Service", StringComparison.OrdinalIgnoreCase) && normalized.Length > "Service".Length + 2
            ? normalized[..^"Service".Length]
            : normalized;
    }

    private static string ToPascalToken(string token)
    {
        if (token.Any(char.IsUpper) && token.Any(char.IsLower))
        {
            return char.ToUpperInvariant(token[0]) + token[1..];
        }

        if (token.Length == 1)
        {
            return token.ToUpperInvariant();
        }

        return char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant();
    }

    private sealed class CandidateEvidence
    {
        private readonly HashSet<string> _sourceHintSet = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _reasonSet = new(StringComparer.OrdinalIgnoreCase);

        public CandidateEvidence(string serviceName)
        {
            ServiceName = serviceName;
        }

        public string ServiceName { get; }
        public int Score { get; private set; }
        public List<string> SourceHints { get; } = new();

        public void Add(string sourceHint, string reason, int score)
        {
            Score += Math.Max(0, score);

            if (_sourceHintSet.Add(sourceHint) && SourceHints.Count < MaxSourceHintsPerService)
            {
                SourceHints.Add(sourceHint);
            }

            _reasonSet.Add(reason);
        }

        public string BuildDescription()
        {
            if (_reasonSet.Count == 0)
            {
                return "Service candidate inferred from repository structure.";
            }

            return string.Join(" ", _reasonSet.Take(3));
        }
    }

    [GeneratedRegex(@"^\s*[-*]\s*service\s*:\s*(?<name>.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ServiceBulletRegex();

    [GeneratedRegex(@"^\s*#{1,6}\s*(?:service|microservice)\s*[:\-]\s*(?<name>.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ServiceHeadingRegex();
}
