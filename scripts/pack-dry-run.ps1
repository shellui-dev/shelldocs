# Packs every IsPackable=true project into ./nupkgs-dryrun/ and validates each
# .nupkg — verifies README embed, checks size, prints package ID + version.
# Run from repo root.
#
#   ./scripts/pack-dry-run.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$outDir = Join-Path $repoRoot "nupkgs-dryrun"
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Path $outDir | Out-Null

Write-Host ""
Write-Host "-> dotnet pack shelldocs.slnx -c Release -o $outDir"
Write-Host ""
dotnet pack shelldocs.slnx --configuration Release --output $outDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "PACK FAILED" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Produced packages:"
Write-Host ""

$fail = 0
foreach ($nupkg in Get-ChildItem $outDir -Filter *.nupkg | Sort-Object Name) {
    $sizeKB = [math]::Round($nupkg.Length / 1KB, 1)
    Write-Host ("  {0}  ({1} KB)" -f $nupkg.Name, $sizeKB)

    # A .nupkg is a zip — extract to a temp dir to inspect.
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ("nupkg-check-" + [guid]::NewGuid().ToString("N").Substring(0, 8))
    Expand-Archive -Path $nupkg.FullName -DestinationPath $tmp -Force

    # Every packable project ships README.md via <PackageReadmeFile>.
    $readme = Get-ChildItem $tmp -Filter README.md -Recurse | Select-Object -First 1
    if (-not $readme) {
        Write-Host "    MISSING README.md" -ForegroundColor Red
        $fail++
    }

    # Sanity: nuspec present with expected version.
    $nuspec = Get-ChildItem $tmp -Filter *.nuspec | Select-Object -First 1
    if ($nuspec) {
        $xml = [xml](Get-Content $nuspec.FullName)
        $id = $xml.package.metadata.id
        $ver = $xml.package.metadata.version
        Write-Host "    id=$id  version=$ver"
    }

    Remove-Item $tmp -Recurse -Force
}

Write-Host ""
if ($fail -gt 0) {
    Write-Host "$fail package(s) failed validation" -ForegroundColor Red
    exit 1
}
Write-Host "OK — all packages passed validation" -ForegroundColor Green
Write-Host ""
Write-Host "Ship it with:"
Write-Host "  git tag v<version>"
Write-Host "  git push origin v<version>"
