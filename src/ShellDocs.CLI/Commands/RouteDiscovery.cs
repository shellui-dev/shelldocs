using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ShellDocs.CLI.Commands;

// Finds the app's own @page routes so `build` prerenders pages that live outside content/.
internal static class RouteDiscovery
{
    // The Blazor template's error and not-found pages, which aren't site pages.
    private static readonly HashSet<string> TemplatePages = new(StringComparer.OrdinalIgnoreCase) { "/Error", "/not-found" };

    // Read from metadata rather than loaded, so the app's dependencies needn't resolve here.
    internal static IReadOnlyList<string> ReadRouteTemplates(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata) return [];

        var md = pe.GetMetadataReader();
        var templates = new List<string>();
        foreach (var handle in md.CustomAttributes)
        {
            var attr = md.GetCustomAttribute(handle);
            if (attr.Parent.Kind != HandleKind.TypeDefinition || !IsRouteAttribute(md, attr.Constructor)) continue;

            var blob = md.GetBlobReader(attr.Value);
            if (blob.ReadUInt16() != 1) continue;
            if (blob.ReadSerializedString() is { Length: > 0 } template) templates.Add(template);
        }
        return templates;
    }

    // Routes with parameters can't be listed; those pages come from --extra-url.
    internal static IReadOnlyList<string> PrerenderableRoutes(IEnumerable<string> templates) =>
        templates
            .Select(NormalizeUrl)
            .Where(t => !t.Contains('{') && !TemplatePages.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal static string NormalizeUrl(string url)
    {
        var trimmed = url.Trim();
        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }

    private static bool IsRouteAttribute(MetadataReader md, EntityHandle ctor)
    {
        if (ctor.Kind != HandleKind.MemberReference) return false;
        var parent = md.GetMemberReference((MemberReferenceHandle)ctor).Parent;
        if (parent.Kind != HandleKind.TypeReference) return false;
        var type = md.GetTypeReference((TypeReferenceHandle)parent);
        return md.StringComparer.Equals(type.Name, "RouteAttribute")
            && md.StringComparer.Equals(type.Namespace, "Microsoft.AspNetCore.Components");
    }
}
