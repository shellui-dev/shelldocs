using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using ShellDocs.Components;

namespace ShellDocs.Tests;

// HtmlRenderer output is what a prerendered page ships with before any Blazor runtime.
internal sealed class ComponentRenderHarness
{
    public LogSink Logs { get; } = new();
    public IServiceProvider Services { get; }

    public ComponentRenderHarness(Action<ShellDocsOptions>? configure = null, string uri = "http://localhost/")
    {
        var services = new ServiceCollection();
        services.AddShellDocs(configure);
        services.AddSingleton(Logs);
        services.AddSingleton(typeof(ILogger<>), typeof(SinkLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddScoped<NavigationManager>(_ => new StubNav(uri));
        services.AddSingleton<IJSRuntime, NoopJs>();
        Services = services.BuildServiceProvider();
    }

    public async Task<string> RenderAsync<T>(Dictionary<string, object?>? parameters = null) where T : IComponent
    {
        await using var scope = Services.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var view = ParameterView.FromDictionary(parameters ?? new Dictionary<string, object?>());
            var output = await renderer.RenderComponentAsync<T>(view);
            return output.ToHtmlString();
        });
    }

    public sealed class LogSink
    {
        private readonly List<string> _messages = new();
        public IReadOnlyList<string> Messages { get { lock (_messages) return _messages.ToList(); } }
        public void Add(string m) { lock (_messages) _messages.Add(m); }
    }

    private sealed class SinkLogger<T> : ILogger<T>
    {
        private readonly LogSink _sink;
        public SinkLogger(LogSink sink) => _sink = sink;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _sink.Add($"{logLevel}: {formatter(state, exception)}");
    }

    private sealed class StubNav : NavigationManager
    {
        public StubNav(string uri) => Initialize("http://localhost/", uri);
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }
}
