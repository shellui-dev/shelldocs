## Rules for AI agents

Read these before changing a ShellDocs site.

1. **Pages are Markdown.** They live in `content/docs/` with `title` and `description` frontmatter; `meta.json` in each folder sets the sidebar order. `shelldocs add page|guide|component <name>` scaffolds one.
2. **Use ShellDocs' components in Markdown; don't write HTML look-alikes.** `<Callout>`, `<Tabs>` / `<Tab>`, `<Steps>` / `<Step>`, `<CardGrid>` / `<LinkCard>`, `<CodeGroup>`, `<FileTree>`, `<TypeTable>` and `<AutoTypeTable>`. When the site's own library shadows a name, the `Docs` alias (`<DocsCallout>`) still reaches the built-in.
3. **Live examples:** a ```` ```razor:preview ```` fence, `<ComponentPreview Component="X">`, or `<DemoPreview Component="X" />` for a demo component under `DemoSourceRoot`. A component used in Markdown must be registered (`RegisterComponent<T>()` or `RegisterComponentsFromAssembly<T>()`).
4. **Configuration is `AddShellDocs(o => …)` in `Program.cs`:** navigation, versions (`AddVersion`), redirects, sync switches and `EnableLlmsTxt`.
5. **Static sites come from `shelldocs build`.** Don't edit the `publish/` output by hand.
