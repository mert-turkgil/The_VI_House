using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace VIHouse.WebUI.Routing;

/// <summary>
/// Development-only startup check that the URL surface is what the design says it is: every
/// controller action is attribute-routed (there is no conventional route to catch one that is not),
/// and every template's literal segments are lowercase slugs. Route parameters ({id}, {slug}) are
/// ignored — their values are whatever the data says. Throwing here is deliberate: a shadowed Index
/// or a /Admin/Users URL would otherwise only be noticed by whoever clicks it in production.
/// </summary>
public static partial class RouteTemplateAssertions
{
    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex Parameters();

    public static void Run(IServiceProvider services)
    {
        var descriptors = services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items;
        var problems = new List<string>();
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in descriptors.OfType<ControllerActionDescriptor>())
        {
            var template = action.AttributeRouteInfo?.Template;
            if (template is null)
            {
                problems.Add($"{action.ControllerName}.{action.ActionName} has no route template.");
                continue;
            }

            var literal = Parameters().Replace(template, "");
            if (literal != literal.ToLowerInvariant())
            {
                problems.Add($"{action.ControllerName}.{action.ActionName}: template '{template}' is not lowercase.");
            }

            // A leftover bare [HttpGet] next to an [HttpGet("new")] is the same template twice on
            // two actions — an AmbiguousMatchException, but only once someone requests it.
            var methods = string.Join(",", (action.ActionConstraints ?? [])
                .OfType<Microsoft.AspNetCore.Mvc.ActionConstraints.HttpMethodActionConstraint>()
                .SelectMany(c => c.HttpMethods).OrderBy(m => m));
            var key = $"{methods} {template}";
            var owner = $"{action.ControllerName}.{action.ActionName}";
            if (seen.TryGetValue(key, out var other) && other != owner)
            {
                problems.Add($"{owner} and {other} both answer {methods} '{template}'.");
            }
            seen[key] = owner;
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "Route templates violate the clean-URL rules:\n  " + string.Join("\n  ", problems));
        }
    }
}
