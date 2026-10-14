using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace ShellDocs.Components.Llms;

/* Serves LlmsTxtFiles before routing, so the paths never reach the docs page route.
   Text is text/plain because some browsers download text/markdown instead of showing
   it; the agent files are attachments. Links use SiteUrl, or the request's origin. */
internal sealed class LlmsTxtMiddleware
{
    // Sent by `shelldocs build` (from the same machine) with the export's --site-url,
    // and its --base-href for root-relative links when there is no site URL.
    public const string ExportSiteUrlHeader = "X-ShellDocs-Site-Url";
    public const string ExportBasePathHeader = "X-ShellDocs-Base-Path";

    private readonly RequestDelegate _next;
    private readonly LlmsTxtFiles _files;
    private readonly ShellDocsOptions _options;

    public LlmsTxtMiddleware(RequestDelegate next, LlmsTxtFiles files, ShellDocsOptions options)
    {
        _next = next;
        _files = files;
        _options = options;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method)) return _next(context);

        var path = request.Path.Value ?? "";
        if (string.Equals(path, LlmsTxtFiles.ManifestPath, StringComparison.OrdinalIgnoreCase))
            return context.Response.WriteAsJsonAsync(_files.AllPaths());
        if (!path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith(".mdc", StringComparison.OrdinalIgnoreCase))
            return _next(context);

        var file = _files.Get(path, BaseUrl(context));
        if (file is null) return _next(context);

        var bytes = Encoding.UTF8.GetBytes(file.Body);
        var response = context.Response;
        response.ContentType = "text/plain; charset=utf-8";
        response.ContentLength = bytes.Length;
        response.Headers[HeaderNames.CacheControl] = "no-cache";
        if (file.IsDownload)
            response.Headers[HeaderNames.ContentDisposition] = $"attachment; filename=\"{Path.GetFileName(file.Path)}\"";
        return HttpMethods.IsHead(request.Method) ? Task.CompletedTask : response.Body.WriteAsync(bytes).AsTask();
    }

    private string BaseUrl(HttpContext context)
    {
        var request = context.Request;
        var remote = context.Connection.RemoteIpAddress;
        var local = remote is null || IPAddress.IsLoopback(remote);
        if (local && (request.Headers.ContainsKey(ExportSiteUrlHeader) || request.Headers.ContainsKey(ExportBasePathHeader)))
        {
            var site = request.Headers[ExportSiteUrlHeader].ToString();
            if (site.Length > 0) return site.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(_options.SiteUrl)) return _options.SiteUrl.TrimEnd('/');
            return request.Headers[ExportBasePathHeader].ToString().TrimEnd('/');
        }
        if (!string.IsNullOrWhiteSpace(_options.SiteUrl)) return _options.SiteUrl.TrimEnd('/');
        return $"{request.Scheme}://{request.Host}{request.PathBase}";
    }
}

internal sealed class LlmsTxtStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseMiddleware<LlmsTxtMiddleware>();
        next(app);
    };
}
