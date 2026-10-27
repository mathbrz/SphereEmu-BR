# Puts the game files the server needs to run next to the code, taken from a full clone of the original project's
# repository (https://github.com/knelse/SphereEmu) at the commit this code is based on.
# Nothing here is hosted by this project, and the server does not need that repository once the files are in place.
# The default source is that repository. If it is gone or private, point -Source (or the SPHEREEMU_ASSETS_SOURCE
# environment variable) at any full clone you hold, as a path or a URL. Run it once from the repository root:
#   powershell -ExecutionPolicy Bypass -File Tools\setup-assets.ps1 [-Source <path or URL>]
# Needs: git. Takes about 3.8 GB. Re-running skips what is already there unless -Force is given.
param(
    [string]$Commit = 'b88f5fd2184d03eedd6b9a69a43654083c5cdbd1',
    [string]$Source = $(if ($env:SPHEREEMU_ASSETS_SOURCE) { $env:SPHEREEMU_ASSETS_SOURCE } else { 'https://github.com/knelse/SphereEmu.git' }),
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'git is required.' }

# Folders and files that hold game data and world content (not code).
$items = @(
    'Godot/Models', 'Godot/Textures', 'Godot/Terrain', 'Godot/World',
    'Sphere.GameDataDecode', '.generated', 'sphereclient_patched.exe',
    # Terrain, navigation and ground data the server reads at run time (the terrain scene needs the multimeshes and monsters stand still without it)
    'GodotAssetSource/.gdignore', 'GodotAssetSource/TerrainBake/.gdignore',
    'GodotAssetSource/TerrainBake/GeneratedNavMeshes', 'GodotAssetSource/TerrainBake/GeneratedIndoorNavMeshes',
    'GodotAssetSource/TerrainBake/GroundChunks', 'GodotAssetSource/TerrainBake/GroundMeshes',
    'GodotAssetSource/TerrainBake/GroundShapes', 'GodotAssetSource/TerrainBake/GeneratedMultiMeshes'
)

$missing = $items | Where-Object { $Force -or -not (Test-Path (Join-Path $root $_)) }
if (-not $missing) { Write-Host 'All game files are already present.'; return }
Write-Host "Missing: $($missing -join ', ')"

$cache = Join-Path $root '.assets-cache'
if (Test-Path $cache) { Remove-Item $cache -Recurse -Force }
New-Item -ItemType Directory -Path $cache | Out-Null
Push-Location $cache
try {
    git init -q
    git remote add origin $Source
    git config core.sparseCheckout true
    git config core.longpaths true
    git sparse-checkout init --no-cone
    $patterns = $missing | ForEach-Object { '/' + $_ + $(if ($_ -like '*.exe' -or $_ -like '*.gdignore') { '' } else { '/' }) }
    git sparse-checkout set --no-cone @patterns
    Write-Host "Fetching from $Source (this takes a while)..."
    git fetch --depth 1 --filter=blob:none origin $Commit
    if ($LASTEXITCODE -ne 0) {
        throw "Could not fetch commit $Commit from $Source. If that repository is gone or private, run again with " +
              "-Source <path or URL of a full clone you hold> (or set SPHEREEMU_ASSETS_SOURCE). " +
              "A plain clone made with 'git clone --mirror' before it closed works."
    }
    git -c advice.detachedHead=false checkout FETCH_HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Checkout of the game files failed.' }
} finally {
    Pop-Location
}

foreach ($item in $missing) {
    $from = Join-Path $cache $item
    $to = Join-Path $root $item
    if (-not (Test-Path $from)) { Write-Warning "Not found upstream: $item"; continue }
    if (Test-Path $to) { Remove-Item $to -Recurse -Force }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $to) | Out-Null
    Move-Item -Path $from -Destination $to
    Write-Host "Added $item"
}

Remove-Item $cache -Recurse -Force
Write-Host 'Done. Build with: dotnet build SphServer.sln'
