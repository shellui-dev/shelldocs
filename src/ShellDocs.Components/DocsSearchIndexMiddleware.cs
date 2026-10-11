using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using ShellDocs.Core;

namespace ShellDocs.Components;

/* The search index as shelldocs.js reads it: { versions: [{ id, root, latest }],
   entries: [{ u, t, d, s, k, b, v }] }. k is "p" (page) or "h" (heading); v is the
   id of the version that owns the entry, absent for entries outside every version. */
internal sealed class DocsSearchIndexFile
{
    private readonly Lazy<(byte[] Bytes, string ETag)> _file;

    public DocsSearchIndexFile(SearchIndex index, DocsVersionResolver versions)
    {
        _file = new(() =>
        {
            var bytes = Build(index, versions);
            return (bytes, "\"" + Convert.ToHexString(SHA256.HashData(bytes), 0, 8) + "\"");
        });
    }

    public byte[] Bytes => _file.Value.Bytes;
    public string ETag => _file.Value.ETag;

    internal static byte[] Build(SearchIndex index, DocsVersionResolver versions)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            json.WriteStartArray("versions");
            foreach (var v in versions.Versions)
            {
                json.WriteStartObject();
                json.WriteString("id", v.Id);
                json.WriteString("root", UrlPath.Normalize(v.RootUrl));
                if (v.IsLatest) json.WriteBoolean("latest", true);
                json.WriteEndObject();
            }
            json.WriteEndArray();

            json.WriteStartArray("entries");
            foreach (var e in index.Entries)
            {
                json.WriteStartObject();
                json.WriteString("u", e.Url);
                json.WriteString("t", e.Title);
                if (!string.IsNullOrEmpty(e.Description)) json.WriteString("d", e.Description);
                if (!string.IsNullOrEmpty(e.Section)) json.WriteString("s", e.Section);
                json.WriteString("k", e.Kind == SearchEntryKind.Page ? "p" : "h");
                if (!string.IsNullOrEmpty(e.Body)) json.WriteString("b", e.Body);
                if (versions.FindContaining(e.Url) is { } owner) json.WriteString("v", owner.Id);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return stream.ToArray();
    }
}

// Serves the index to shelldocs.js, and to `shelldocs build`, which copies it into the export.
internal sealed class DocsSearchIndexMiddleware
{
    public const string IndexPath = "/_shelldocs/search.json";

    private readonly RequestDelegate _next;
    private readonly DocsSearchIndexFile _file;

    public DocsSearchIndexMiddleware(RequestDelegate next, DocsSearchIndexFile file)
    {
        _next = next;
        _file = file;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            return _next(context);
        if (!string.Equals(request.Path.Value, IndexPath, StringComparison.OrdinalIgnoreCase))
            return _next(context);

        var response = context.Response;
        response.Headers[HeaderNames.ETag] = _file.ETag;
        response.Headers[HeaderNames.CacheControl] = "no-cache";
        if (request.Headers[HeaderNames.IfNoneMatch] == _file.ETag)
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            return Task.CompletedTask;
        }

        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength = _file.Bytes.Length;
        return HttpMethods.IsHead(request.Method) ? Task.CompletedTask : response.Body.WriteAsync(_file.Bytes).AsTask();
    }
}

internal sealed class DocsSearchStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseMiddleware<DocsSearchIndexMiddleware>();
        next(app);
    };
}
