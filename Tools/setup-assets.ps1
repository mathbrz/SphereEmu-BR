# Fetches the game files the server needs to run from the original project's PUBLIC repository
# (https://github.com/knelse/SphereEmu) at the commit this code is based on, and puts them in this repository.
# Nothing here is hosted by this project. Run it once from the repository root:
#   powershell -ExecutionPolicy Bypass -File Tools\setup-assets.ps1
# Needs: git. Downloads about 3.8 GB. Re-running skips what is already there unless -Force is given.
param(
    [string]$Commit = 'b88f5fd2184d03eedd6b9a69a43654083c5cdbd1',
    [string]$Source = 'https://github.com/knelse/SphereEmu.git',
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
    Write-Host 'Downloading from the original repository (this takes a while)...'
    git fetch --depth 1 --filter=blob:none origin $Commit
    git -c advice.detachedHead=false checkout FETCH_HEAD
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
