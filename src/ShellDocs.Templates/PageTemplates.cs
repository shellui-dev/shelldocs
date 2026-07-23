namespace ShellDocs.Templates;

/* Starter markdown emitted by `shelldocs add`.
   Each method returns a self-contained .md string with frontmatter, a title,
   and a light section skeleton. TODOs mark spots the author should fill in. */
public static class PageTemplates
{
    public static string ComponentPage(string displayName) => $$"""
---
title: {{displayName}}
description: TODO — short one-line description of {{displayName}}.
category: Components
order: 100
---

# {{displayName}}

`<{{displayName}}>` TODO — one-paragraph intro explaining what this component is for and when to use it.

## Basic

```razor:preview
<{{displayName}} />
```

## Props

<TypeTable>
<TypeRow Name="TODO" Type="string" Default="" Description="TODO — describe this prop." />
</TypeTable>

## Notes

- TODO
""";

    public static string GuidePage(string displayName) => $$"""
---
title: {{displayName}}
description: TODO — short one-line description of this guide.
category: Guides
order: 100
---

# {{displayName}}

TODO — one-paragraph intro to what this guide covers and who it's for.

## Steps

<Steps>
<Step Title="First step">
TODO — first step body.
</Step>
<Step Title="Next step">
TODO.
</Step>
</Steps>

## Next

- TODO
""";

    public static string BlankPage(string displayName) => $$"""
---
title: {{displayName}}
description: TODO
order: 100
---

# {{displayName}}

TODO
""";
}
