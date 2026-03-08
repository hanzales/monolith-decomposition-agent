using System.Text.RegularExpressions;
using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Core.Utilities;

namespace Migration.Intelligence.Design.Services;

internal static partial class DesignDomainResolver
{
    private static readonly HashSet<string> UnknownTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "unknown", "n/a", "na", "none", "null", "-", "?"
    };

    public static string ResolveDomain(MigrationIntelligenceContract intelligence, string requestedDomain)
    {
        if (string.IsNullOrWhiteSpace(requestedDomain))
        {
            throw new ArgumentException("Domain candidate cannot be empty.", nameof(requestedDomain));
        }

        var requested = requestedDomain.Trim();
        var candidates = CollectDomainCandidates(intelligence);
        var requestedNormalized = NormalizeDomainToken(requested);

        var hierarchyAliasMatch = ResolveFromDomainHierarchy(intelligence.DomainHierarchies, requested, requestedNormalized);
        if (!string.IsNullOrWhiteSpace(hierarchyAliasMatch))
        {
            return hierarchyAliasMatch;
        }

        var exact = candidates.FirstOrDefault(candidate =>
            candidate.Equals(requested, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(exact))
        {
            return exact;
        }

        var normalizedMatch = candidates.FirstOrDefault(candidate =>
            NormalizeDomainToken(candidate).Equals(requestedNormalized, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(normalizedMatch))
        {
            return normalizedMatch;
        }

        // Handle consolidated domain roots like PriceOffer -> Price.
        var rootPrefixMatch = candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Normalized = NormalizeDomainToken(candidate)
            })
            .Where(item =>
                item.Normalized.Length >= 4
                && requestedNormalized.Length > item.Normalized.Length
                && requestedNormalized.StartsWith(item.Normalized, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Normalized.Length)
            .ThenBy(item => item.Candidate, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (rootPrefixMatch is not null)
        {
            return rootPrefixMatch.Candidate;
        }

        var fuzzyMatch = candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = StringSimilarityUtility.CalculateNormalizedSimilarity(
                    requestedNormalized,
                    NormalizeDomainToken(candidate))
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Candidate, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return fuzzyMatch is not null && fuzzyMatch.Score >= 0.6
            ? fuzzyMatch.Candidate
            : requested;
    }

    public static bool IsDomainMatch(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return left.Equals(right, StringComparison.OrdinalIgnoreCase)
               || NormalizeDomainToken(left).Equals(NormalizeDomainToken(right), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUnknown(string value)
    {
        return string.IsNullOrWhiteSpace(value) || UnknownTokens.Contains(value.Trim());
    }

    public static List<string> DistinctOrdered(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private static List<string> CollectDomainCandidates(MigrationIntelligenceContract intelligence)
    {
        return DistinctOrdered(
        [
            .. intelligence.BusinessDomainCandidates,
            .. intelligence.DomainHierarchies.Select(item => item.Domain),
            .. intelligence.ServiceDossiers.Select(item => item.CandidateName),
            .. intelligence.EndpointMappings.Select(item => item.DomainCandidate),
            .. intelligence.ExecutionChains.Select(item => item.DomainCandidate),
            .. intelligence.RepositoryTableMappings.Select(item => item.DomainCandidate),
            .. intelligence.DependencyMatrix.Select(item => item.FromDomain),
            .. intelligence.DependencyMatrix.Select(item => item.ToDomain),
            .. intelligence.TableOwnerships.Select(item => item.OwnerDomain),
            .. intelligence.HangfireJobs.Select(item => item.DomainOwner)
        ]);
    }

    private static string NormalizeDomainToken(string value)
    {
        var token = NonAlphaNumericRegex().Replace(value.Trim(), string.Empty);
        return token.ToLowerInvariant();
    }

    private static string? ResolveFromDomainHierarchy(
        IEnumerable<DomainHierarchyContract> domainHierarchies,
        string requested,
        string requestedNormalized)
    {
        foreach (var hierarchy in domainHierarchies)
        {
            if (hierarchy.Domain.Equals(requested, StringComparison.OrdinalIgnoreCase))
            {
                return hierarchy.Domain;
            }

            if (NormalizeDomainToken(hierarchy.Domain).Equals(requestedNormalized, StringComparison.OrdinalIgnoreCase))
            {
                return hierarchy.Domain;
            }

            if (hierarchy.Subdomains.Any(subdomain =>
                    NormalizeDomainToken(subdomain).Equals(requestedNormalized, StringComparison.OrdinalIgnoreCase)))
            {
                return hierarchy.Domain;
            }

            var similarSubdomain = hierarchy.Subdomains.Any(subdomain =>
                StringSimilarityUtility.CalculateNormalizedSimilarity(
                    NormalizeDomainToken(subdomain),
                    requestedNormalized) >= 0.82);
            if (similarSubdomain)
            {
                return hierarchy.Domain;
            }
        }

        return null;
    }

    [GeneratedRegex("[^A-Za-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonAlphaNumericRegex();
}
