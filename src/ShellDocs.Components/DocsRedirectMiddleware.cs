using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ShellDocs.Components;

/* Answers DocsRedirects before routing, so a version root whose last segment has a
   dot (/docs/v0.2.1, which `{*Path:nonfile}` routes can't match) redirects too.
   Also serves the full map to `shelldocs build`, which writes it as static pages. */
internal sealed class DocsRedirectMiddleware
{
    public const string MapPath = "/_shelldocs/redirects.json";

    private readonly RequestDelegate _next;
    private readonly DocsRedirects _redirects;

    public DocsRedirectMiddleware(RequestDelegate next, DocsRedirects redirects)
    {
        _next = next;
        _redirects = redirects;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            return _next(context);

        var path = request.Path.Value ?? "/";
        if (string.Equals(path, MapPath, StringComparison.OrdinalIgnoreCase))
            return context.Response.WriteAsJsonAsync(_redirects.All().Select(r => new { from = r.From, to = r.To }));

        var target = _redirects.Resolve(path);
        if (target is null) return _next(context);

        context.Response.StatusCode = target.Permanent ? StatusCodes.Status301MovedPermanently : StatusCodes.Status302Found;
        context.Response.Headers.Location = request.PathBase + target.Url + request.QueryString;
        return Task.CompletedTask;
    }
}

// Puts the middleware first in the pipeline without a Program.cs change.
internal sealed class DocsRedirectStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseMiddleware<DocsRedirectMiddleware>();
        next(app);
    };
}
