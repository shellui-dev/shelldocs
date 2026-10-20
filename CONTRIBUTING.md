# Contributing to ShellDocs

Thanks for helping. Bug reports, fixes and docs improvements are all welcome. For anything bigger than a fix, open an issue first so we can agree on the shape before you write it.

ShellDocs is in alpha: minor versions may break APIs until `1.0`.

## Setup

You need the .NET 10 SDK (the version is pinned in `global.json`).

```bash
dotnet build shelldocs.slnx
dotnet test shelldocs.slnx
```

CI runs the same two commands in Release, and a pull request should pass both with no new warnings.

## Trying a change

`examples/ShellDocs.Preview` is a docs site wired to the projects in this repo, so changes show up without packing anything:

```bash
dotnet run --project examples/ShellDocs.Preview
```

Anything in the chrome or in `shelldocs.js` also has to work in a static export. Build one with the CLI from source into the example's `publish/` (ignored by git) and serve it:

```bash
dotnet run --project src/ShellDocs.CLI -- build --dir examples/ShellDocs.Preview
dotnet run --project src/ShellDocs.CLI -- preview --dir examples/ShellDocs.Preview
```

Add `--base-href /sub/` to the build to check a site served from a sub-path.

## The static-host rule

Static exports have no Blazor runtime, so `@onclick` handlers and component state do nothing there. Chrome that must work on a static host follows one pattern:

- the server renders the initial state into `data-*` attributes and classes;
- `shelldocs.js` handles the interaction with delegated, document-level listeners;
- state that a Blazor circuit replaces when it starts is restored by `shelldocs.js`.

`docs/ARCHITECTURE.md` describes how each piece of chrome does this. Interactive Blazor is fine for demos and previews, which are allowed to need a running app.

## Commits and pull requests

- Branch off `main` and open the pull request against it.
- Use [Conventional Commits](https://www.conventionalcommits.org/): `fix(markdown): …`, `feat(cli): …`, `docs: …`, `chore: …`.
- One logical change per commit, and each commit builds and passes the tests on its own.
- Add tests for what you fix or add (`tests/ShellDocs.Tests`).
- Add a line to `CHANGELOG.md` under `[Unreleased]`, saying what changed for someone using ShellDocs.
- Update `README.md` or `docs/ARCHITECTURE.md` when behaviour they describe changes.
- Keep code comments to what isn't obvious from the code.

Releases are cut by the maintainers (see `docs/RELEASING.md`).

## License

By contributing you agree that your contributions are licensed under the [MIT License](LICENSE).
