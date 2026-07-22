namespace ShellDocs.Components;

/* Marker attribute — put this on a public ComponentBase-derived type to keep it
   out of ShellDocsOptions.RegisterComponentsFromAssembly(...) scans. Use for
   internal-shaped components that happen to be `public` for testing / other
   assemblies but shouldn't be reachable from markdown authoring. */
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ShellDocsIgnoreAttribute : Attribute
{
}
