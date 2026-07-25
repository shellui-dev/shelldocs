# Releasing ShellDocs

Runbook for cutting a NuGet release. First time = read start to finish; steady state = jump to "Steady-state release" at the bottom.

## One-time setup (before the very first release)

We use **NuGet Trusted Publishing** — the workflow requests a short-lived (1-hour) API key from nuget.org via OIDC on each run. No long-lived API key stored as a secret. Official docs: <https://learn.microsoft.com/nuget/nuget-org/trusted-publishing>.

### 1. Verify package IDs are available on NuGet

Run once, before you register anything, so you don't discover a naming collision at t=publish:

```powershell
foreach ($id in "ShellDocs.CLI","ShellDocs.Components","ShellDocs.Core","ShellDocs.Markdown","ShellDocs.Templates","ShellDocs.Tokens") {
    Write-Host "-- $id"
    dotnet nuget search $id --exact-match --source https://api.nuget.org/v3/index.json | Select-String $id
}
```

If any ID is taken by another author, decide: rename (`ShellUI.ShellDocs.*`?) or reach out to the owner. Do NOT publish under a different-looking name and hope no one notices — that's how brand-confusion issues start.

### 2. Create the `release` GitHub environment

Repo → Settings → Environments → **New environment** → name it exactly `release`.

Nothing to configure inside (optional: add required reviewers if you want a manual gate on each publish). The environment's existence is what the workflow's `environment: release` line references, and matching against the TP policy in step 3 is what proves this workflow is what it says it is.

### 3. Register a Trusted Publishing policy on NuGet

1. Sign in at [nuget.org](https://www.nuget.org/) → click your username → **Trusted Publishing** → **Add**
2. Choose the owner (individual user OR organization — the policy applies to all packages owned by that account)
3. Fill (all values are case-insensitive):
   - **Repository Owner:** `shellui-dev` (the GitHub organization/user name)
   - **Repository:** `shelldocs`
   - **Workflow File:** `release.yml` — **filename only**, no `.github/workflows/` prefix
   - **Environment:** `release` — must match `environment: release` in our workflow. If you skip this, remove `environment: release` from the workflow too, or the policy match will fail.
4. Save.

**Note on private repos:** first-time policies for private GitHub repos are provisional for 7 days. NuGet needs to see one successful publish (which carries GitHub's repository + owner IDs in the OIDC token) to lock the policy permanently. If no publish happens in 7 days, the policy goes inactive — you'd re-activate it and try again.

### 4. Add the `NUGET_USER` secret

The workflow's `NuGet/login@v1` action needs your **nuget.org profile username** (NOT email, NOT the GitHub org name — the visible profile name you sign in with, e.g. what shows on `nuget.org/profiles/<name>`).

Repo → Settings → Secrets and variables → Actions → New repository secret:
- **Name:** `NUGET_USER`
- **Value:** your nuget.org profile name

### 5. Local pack dry-run

Confirm the pack works locally before trusting CI. From repo root:

```powershell
./scripts/pack-dry-run.ps1
```

The script packs every `IsPackable=true` project into `./nupkgs-dryrun/`, prints IDs + sizes, and verifies `README.md` is embedded in each. Any missing README or unexpected package = fix before releasing.

### 6. First release — expect the 7-day provisional window

The very first `git push origin v0.1.0-alpha` triggers the workflow, which does OIDC exchange, publishes, and locks the policy permanently. Watch the Actions tab — if OIDC exchange fails, the most likely causes (in order) are: `NUGET_USER` secret missing or wrong, TP policy's `Workflow File` field includes a path prefix (should be just `release.yml`), or workflow's `environment: release` doesn't match the policy's Environment field.

## Steady-state release

Once the one-time setup is done, cutting a release is three commands.

### 1. Bump the version

Edit `Directory.Build.props` → `<Version>0.X.Y[-suffix]</Version>`. That propagates to every packable project via the shared props file.

For a prerelease bump: `0.1.0-alpha` → `0.1.1-alpha` (patch) or `0.2.0-alpha` (minor).
For the first stable: strip the `-alpha` suffix → `1.0.0`.

### 2. Update `CHANGELOG.md`

Move the entries out of `[Unreleased]` into a new dated section (`[0.1.1-alpha] — YYYY-MM-DD`). Update the comparison links at the bottom.

### 3. Commit, tag, push

```bash
git add Directory.Build.props CHANGELOG.md
git commit -m "chore: release 0.X.Y[-suffix]"
git tag "v0.X.Y[-suffix]"
git push
git push origin "v0.X.Y[-suffix]"
```

The tag push triggers `.github/workflows/release.yml`:
1. Builds Release
2. Runs the test suite
3. Packs every `IsPackable=true` project
4. Pushes each `.nupkg` to nuget.org (`--skip-duplicate` so re-runs are safe)
5. Creates a GitHub Release from the tag with auto-generated notes

Watch the run under Actions. If NuGet push fails on one package (e.g. `409 Conflict — already exists`), `--skip-duplicate` handles it silently; a real failure (bad API key, network) will surface as a red X.

## Dry-run without publishing

To validate the whole workflow without shipping to NuGet, go to Actions → Release → Run workflow → check "Pack and validate only". Runs build + pack, skips the push step.

## After the release

- Verify the packages appear at `https://www.nuget.org/packages/ShellDocs.CLI/`, etc. (indexing takes a few minutes)
- Test the install locally: `dotnet tool install -g ShellDocs.CLI --prerelease` in a scratch directory
- Announce as appropriate (blog post / X / whatever). Alpha releases are usually announced only internally
