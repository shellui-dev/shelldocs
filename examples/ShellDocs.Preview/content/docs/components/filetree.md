---
title: FileTree
description: Static folder / file visualisation for project layout diagrams.
category: Components
order: 50
---

# FileTree

`<FileTree>` renders a static folder-and-file tree — the kind of "here's what your project structure looks like" diagram you see in every framework's getting-started guide.

## Basic

```razor:preview
<FileTree>
    <FileTreeItem Name="my-docs" IsFolder="true">
        <FileTreeItem Name="content" IsFolder="true">
            <FileTreeItem Name="docs" IsFolder="true">
                <FileTreeItem Name="introduction.md" />
                <FileTreeItem Name="installation.md" />
                <FileTreeItem Name="meta.json" Comment="sidebar order" />
            </FileTreeItem>
        </FileTreeItem>
        <FileTreeItem Name="Components" IsFolder="true">
            <FileTreeItem Name="Pages" IsFolder="true">
                <FileTreeItem Name="DocsPage.razor" Highlight="true" Comment="routes /docs/*" />
                <FileTreeItem Name="Home.razor" />
            </FileTreeItem>
            <FileTreeItem Name="App.razor" />
        </FileTreeItem>
        <FileTreeItem Name="Program.cs" />
        <FileTreeItem Name="my-docs.csproj" />
    </FileTreeItem>
</FileTree>
```

## Props

- `Name` — the file or folder name shown next to the glyph
- `IsFolder` — draws the folder glyph and enables nested children
- `Comment` — muted italic comment shown to the right (e.g. `// sidebar order`)
- `Highlight` — soft warning-coloured background on the label to draw attention to a specific line
