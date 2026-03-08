using System.Text.RegularExpressions;
using Migration.Intelligence.CodeAnalysis.Heuristics;
using Migration.Intelligence.CodeAnalysis.Visitors;
using Migration.Intelligence.Contracts.Analysis;
using Migration.Intelligence.Contracts.Common;
using Migration.Intelligence.Contracts.Orchestration;

namespace Migration.Intelligence.CodeAnalysis.Services;

public sealed class EndpointAnalyzer
{
    private static readonly Regex HttpAttributeRegex =
        new("\\[Http(?<method>Get|Post|Put|Patch|Delete|Head|Options)(?:\\(\\s*\\\"(?<route>[^\\\"]*)\\\"\\s*\\))?\\]", RegexOptions.Compiled);

    private static readonly Regex ActionRegex =
        new("public\\s+(?:async\\s+)?(?:Task(?:<[^>]+>)?|IActionResult|ActionResult(?:<[^>]+>)?|IResult)\\s+(?<name>[A-Za-z0-9_]+)\\s*\\(", RegexOptions.Compiled);

    private readonly ControllerVisitor _controllerVisitor;
    private readonly ControllerHeuristics _controllerHeuristics;

    public EndpointAnalyzer(ControllerVisitor controllerVisitor, ControllerHeuristics controllerHeuristics)
    {
        _controllerVisitor = controllerVisitor;
        _controllerHeuristics = controllerHeuristics;
    }

    public async Task<List<EndpointContract>> AnalyzeEndpointsAsync(
        RepositoryInventoryContract inventory,
        CancellationToken cancellationToken = default)
    {
        var endpoints = new List<EndpointContract>();

        foreach (var sourceFile in inventory.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_controllerVisitor.TryExtractControllerName(sourceFile.RelativePath, out var controllerName))
            {
                continue;
            }

            var fullPath = Path.Combine(inventory.SourceRootPath, sourceFile.RelativePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var lines = await File.ReadAllLinesAsync(fullPath, cancellationToken);
            var pendingMethod = EndpointHttpMethod.Unknown;
            var pendingRoute = string.Empty;
            var actionAdded = false;

            foreach (var line in lines)
            {
                var attrMatch = HttpAttributeRegex.Match(line);
                if (attrMatch.Success)
                {
                    pendingMethod = ParseMethod(attrMatch.Groups["method"].Value);
                    pendingRoute = attrMatch.Groups["route"].Value;
                    continue;
                }

                var actionMatch = ActionRegex.Match(line);
                if (!actionMatch.Success)
                {
                    continue;
                }

                var actionName = actionMatch.Groups["name"].Value;
                var routePrefix = _controllerHeuristics.ToRoutePrefix(controllerName);
                var normalizedRoute = string.IsNullOrWhiteSpace(pendingRoute)
                    ? $"{routePrefix}/{actionName.ToLowerInvariant()}"
                    : NormalizeRoute(routePrefix, pendingRoute);

                endpoints.Add(new EndpointContract
                {
                    ControllerName = controllerName,
                    ActionName = actionName,
                    Route = normalizedRoute,
                    HttpMethod = pendingMethod == EndpointHttpMethod.Unknown
                        ? InferMethodFromAction(actionName)
                        : pendingMethod
                });

                actionAdded = true;
                pendingMethod = EndpointHttpMethod.Unknown;
                pendingRoute = string.Empty;
            }

            if (!actionAdded)
            {
                endpoints.Add(new EndpointContract
                {
                    ControllerName = controllerName,
                    ActionName = "Index",
                    Route = $"{_controllerHeuristics.ToRoutePrefix(controllerName)}/index",
                    HttpMethod = EndpointHttpMethod.Get
                });
            }
        }

        return endpoints;
    }

    private static string NormalizeRoute(string routePrefix, string route)
    {
        var trimmedPrefix = routePrefix.TrimEnd('/');
        var trimmedRoute = route.Trim();

        if (trimmedRoute.StartsWith("/", StringComparison.Ordinal))
        {
            return trimmedRoute;
        }

        return $"{trimmedPrefix}/{trimmedRoute}";
    }

    private static EndpointHttpMethod ParseMethod(string value)
    {
        return Enum.TryParse<EndpointHttpMethod>(value, ignoreCase: true, out var method)
            ? method
            : EndpointHttpMethod.Unknown;
    }

    private static EndpointHttpMethod InferMethodFromAction(string actionName)
    {
        if (actionName.StartsWith("Get", StringComparison.OrdinalIgnoreCase)) return EndpointHttpMethod.Get;
        if (actionName.StartsWith("Post", StringComparison.OrdinalIgnoreCase)) return EndpointHttpMethod.Post;
        if (actionName.StartsWith("Put", StringComparison.OrdinalIgnoreCase)) return EndpointHttpMethod.Put;
        if (actionName.StartsWith("Patch", StringComparison.OrdinalIgnoreCase)) return EndpointHttpMethod.Patch;
        if (actionName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)) return EndpointHttpMethod.Delete;
        return EndpointHttpMethod.Get;
    }
}
