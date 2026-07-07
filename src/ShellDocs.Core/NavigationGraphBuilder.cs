namespace ShellDocs.Core;

public static class NavigationGraphBuilder
{
    public static NavigationGraph Build(string contentRoot)
    {
        var abs = System.IO.Path.GetFullPath(contentRoot);
        if (!Directory.Exists(abs))
        {
            throw new DirectoryNotFoundException($"Content root not found: {contentRoot}");
        }

        var rootNode = new NavigationNode
        {
            Title = "",
            Url = "/",
            Kind = NodeKind.Section,
            Path = abs
        };

        var children = BuildFolder(abs, abs, urlPrefix: "");
        LinkChildren(rootNode, children);
        return new NavigationGraph(rootNode);
    }

    private static List<NavigationNode> BuildFolder(string folder, string root, string urlPrefix)
    {
        var meta = ReadMeta(folder);
        var mdFiles = Directory.GetFiles(folder, "*.md", SearchOption.TopDirectoryOnly);
        var subfolders = Directory.GetDirectories(folder);

        var slugToNode = mdFiles.ToDictionary(
            path => SlugFrom(path),
            path => BuildPage(path, root, urlPrefix),
            StringComparer.OrdinalIgnoreCase);

        var folderNameToChildren = subfolders.ToDictionary(
            path => System.IO.Path.GetFileName(path),
            path => (folderPath: path, children: BuildFolder(path, root, CombineUrl(urlPrefix, System.IO.Path.GetFileName(path)))),
            StringComparer.OrdinalIgnoreCase);

        // No meta.json: alphabetical ordering, subfolders inline as sections.
        if (meta is null)
        {
            var ordered = new List<NavigationNode>();
            foreach (var (name, tuple) in folderNameToChildren.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                var section = new NavigationNode
                {
                    Title = TitleFromFolderName(name),
                    Kind = NodeKind.Section,
                    Path = tuple.folderPath
                };
                LinkChildren(section, tuple.children);
                ordered.Add(section);
            }
            ordered.AddRange(slugToNode.Values.OrderBy(n => n.Order).ThenBy(n => n.Title, StringComparer.OrdinalIgnoreCase));
            return ordered;
        }

        var result = new List<NavigationNode>();
        foreach (var entry in meta.Pages)
        {
            var node = ResolveEntry(entry, slugToNode, folderNameToChildren, root, urlPrefix);
            if (node is not null) result.Add(node);
        }
        return result;
    }

    private static NavigationNode? ResolveEntry(
        MetaJsonEntry entry,
        Dictionary<string, NavigationNode> slugToNode,
        Dictionary<string, (string folderPath, List<NavigationNode> children)> folderNameToChildren,
        string root,
        string urlPrefix)
    {
        switch (entry)
        {
            case MetaJsonPageRef pageRef:
                if (slugToNode.TryGetValue(pageRef.Slug, out var page)) return page;
                if (folderNameToChildren.TryGetValue(pageRef.Slug, out var folder))
                {
                    var section = new NavigationNode
                    {
                        Title = TitleFromFolderName(pageRef.Slug),
                        Kind = NodeKind.Section,
                        Path = folder.folderPath
                    };
                    LinkChildren(section, folder.children);
                    return section;
                }
                return null;

            case MetaJsonDivider:
                return new NavigationNode { Kind = NodeKind.Divider };

            case MetaJsonSubsection sub:
                var subNode = new NavigationNode
                {
                    Title = sub.Title,
                    Kind = NodeKind.Section
                };
                var subChildren = new List<NavigationNode>();
                foreach (var e in sub.Pages)
                {
                    var child = ResolveEntry(e, slugToNode, folderNameToChildren, root, urlPrefix);
                    if (child is not null) subChildren.Add(child);
                }
                LinkChildren(subNode, subChildren);
                return subNode;

            default:
                return null;
        }
    }

    private static NavigationNode BuildPage(string path, string root, string urlPrefix)
    {
        var raw = File.ReadAllText(path);
        var parsed = FrontmatterParser.Parse(raw);
        var slug = SlugFrom(path);
        var url = slug == "index" ? EnsureLeadingSlash(urlPrefix) : CombineUrl(urlPrefix, slug);
        if (url == "") url = "/";

        return new NavigationNode
        {
            Url = url,
            Title = parsed.Frontmatter.GetValue<string>("title") ?? TitleFromSlug(slug),
            Description = parsed.Frontmatter.GetValue<string>("description"),
            Category = parsed.Frontmatter.GetValue<string>("category"),
            Order = parsed.Frontmatter.GetValue<int>("order"),
            Path = path,
            Kind = NodeKind.Page
        };
    }

    private static MetaJson? ReadMeta(string folder)
    {
        var metaPath = System.IO.Path.Combine(folder, "meta.json");
        return File.Exists(metaPath) ? MetaJson.Parse(File.ReadAllText(metaPath)) : null;
    }

    private static void LinkChildren(NavigationNode parent, IReadOnlyList<NavigationNode> children)
    {
        foreach (var child in children) child.Parent = parent;
        parent.Children = children;
    }

    private static string SlugFrom(string mdPath)
        => System.IO.Path.GetFileNameWithoutExtension(mdPath);

    private static string TitleFromSlug(string slug)
        => string.Join(' ', slug.Split('-').Select(w => w.Length == 0 ? w : char.ToUpper(w[0]) + w[1..]));

    private static string TitleFromFolderName(string name) => TitleFromSlug(name);

    private static string EnsureLeadingSlash(string s) => s.StartsWith('/') ? s : "/" + s;

    private static string CombineUrl(string prefix, string segment)
    {
        var p = prefix.TrimEnd('/');
        var s = segment.Trim('/');
        return string.IsNullOrEmpty(p) ? "/" + s : p + "/" + s;
    }
}
