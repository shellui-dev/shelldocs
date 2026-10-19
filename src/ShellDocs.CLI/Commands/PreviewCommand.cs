using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace ShellDocs.CLI.Commands;

// `shelldocs preview` serves a `shelldocs build` export the way static hosts do,
// so it can be checked before deploying.
internal static class PreviewCommand
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static int Run(string dir, string output, int port, string? baseHref)
    {
        var root = Path.GetFullPath(Path.Combine(dir, output));
        if (!File.Exists(Path.Combine(root, "index.html")))
        {
            AnsiConsole.MarkupLine($"[red]error:[/] no static site in [yellow]{root}[/]");
            AnsiConsole.MarkupLine("[dim]run [cyan]shelldocs build[/] first, or point --output at its output folder.[/]");
            return 1;
        }

        var basePath = NormalizeBase(baseHref ?? ReadBaseHref(Path.Combine(root, "index.html")));
        var url = $"http://localhost:{port}{basePath}";
        AnsiConsole.MarkupLine($"[dim]shelldocs preview →[/] [cyan]{root}[/]");
        if (basePath != "/") AnsiConsole.MarkupLine($"[dim]base href:[/] [cyan]{basePath}[/]");
        AnsiConsole.MarkupLine($"[green]serving[/] on [cyan]{url}[/] [dim](Ctrl+C to stop)[/]");

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.Run(context => Serve(context, root, basePath));
        app.Run();
        return 0;
    }

    private static async Task Serve(HttpContext context, string root, string basePath)
    {
        var path = context.Request.Path.Value ?? "/";
        if (basePath != "/" && path == "/")
        {
            context.Response.Redirect(basePath);
            return;
        }

        var (file, status) = Resolve(root, basePath, path);
        context.Response.StatusCode = status;
        // A rebuild should show on the next reload.
        context.Response.Headers.CacheControl = "no-cache";
        if (file is null)
        {
            await context.Response.WriteAsync("404 Not Found");
            return;
        }
        context.Response.ContentType = ContentTypes.TryGetContentType(file, out var type) ? type : "application/octet-stream";
        await context.Response.SendFileAsync(file);
    }

    /* Like GitHub Pages, Netlify and Cloudflare Pages: a file, else the folder's
       index.html, else {path}.html, else 404.html with a 404 status. */
    internal static (string? File, int Status) Resolve(string root, string basePath, string requestPath)
    {
        root = Path.GetFullPath(root);
        var notFound = Path.Combine(root, "404.html");
        (string?, int) NotFound() => (File.Exists(notFound) ? notFound : null, StatusCodes.Status404NotFound);

        var path = Uri.UnescapeDataString(requestPath);
        if (basePath != "/")
        {
            if (path + "/" == basePath) path = basePath;
            if (!path.StartsWith(basePath, StringComparison.Ordinal)) return NotFound();
            path = "/" + path[basePath.Length..];
        }

        var full = Path.GetFullPath(Path.Combine(root, path.TrimStart('/')));
        // Nothing outside the export, whatever the request says (`/../`).
        if (full != root && !full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return NotFound();

        if (File.Exists(full)) return (full, StatusCodes.Status200OK);
        var index = Path.Combine(full, "index.html");
        if (Directory.Exists(full) && File.Exists(index)) return (index, StatusCodes.Status200OK);
        var html = full.TrimEnd(Path.DirectorySeparatorChar) + ".html";
        if (File.Exists(html)) return (html, StatusCodes.Status200OK);
        return NotFound();
    }

    // "/my-repo" or "my-repo/" → "/my-repo/".
    internal static string NormalizeBase(string? baseHref)
    {
        var trimmed = (baseHref ?? "").Trim().Trim('/');
        return trimmed.Length == 0 ? "/" : "/" + trimmed + "/";
    }

    private static string? ReadBaseHref(string indexHtml)
    {
        var match = Regex.Match(File.ReadAllText(indexHtml), @"<base\s+href\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }
}
