namespace ShellDocs.Components;

// Excludes a public component from RegisterComponentsFromAssembly scans, for
// render machinery that shouldn't be reachable from markdown.
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ShellDocsIgnoreAttribute : Attribute
{
}
