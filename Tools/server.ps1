# Starts, stops and checks the server of this repository.
#   powershell -ExecutionPolicy Bypass -File Tools\server.ps1 status
#   powershell -ExecutionPolicy Bypass -File Tools\server.ps1 start [-Build]
#   powershell -ExecutionPolicy Bypass -File Tools\server.ps1 stop
#   powershell -ExecutionPolicy Bypass -File Tools\server.ps1 restart [-Build]
# It only touches the Godot processes started from THIS repository folder, and it refuses to stop or restart while a
# player is connected (positions are saved on logout) unless -Force is given.
param(
    [Parameter(Position = 0)][ValidateSet('status', 'start', 'stop', 'restart')][string]$Action = 'status',
    [switch]$Build,
    [switch]$Force,
    [string]$GodotPath = $env:GODOT_PATH,
    [int]$WaitSeconds = 120
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Get-Port {
    $settings = Join-Path $root 'appsettings.json'
    if (Test-Path $settings) {
        $text = (Get-Content $settings -Raw) -replace '(?m)^\s*//.*$', ''
        if ($text -match '"Port"\s*:\s*"?(\d+)"?') { return [int]$Matches[1] }
    }
    return 25860
}

function Find-Godot {
    if ($GodotPath -and (Test-Path $GodotPath)) { return $GodotPath }
    $candidates = @(
        Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_*\*console.exe" -ErrorAction SilentlyContinue
        Get-ChildItem "$env:ProgramFiles\Godot*\*console.exe" -ErrorAction SilentlyContinue
    )
    $found = $candidates | Where-Object { $_ } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($found) { return $found.FullName }
    $onPath = Get-Command 'Godot*_console.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($onPath) { return $onPath.Source }
    throw 'Godot Mono console executable not found. Pass -GodotPath or set the GODOT_PATH environment variable.'
}

function Get-RepoProcesses {
    Get-CimInstance Win32_Process -Filter "Name like 'Godot%'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Replace('"', '').Contains("--path $root") }
}

function Get-State($port) {
    [pscustomobject]@{
        Listening = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue).Count -gt 0
        Players   = @(Get-NetTCPConnection -LocalPort $port -State Established -ErrorAction SilentlyContinue).Count
        Processes = @(Get-RepoProcesses).Count
    }
}

function Show-State($port) {
    $s = Get-State $port
    "Port $port : listening=$($s.Listening), players connected=$($s.Players), godot processes for this folder=$($s.Processes)"
    $log = Get-ChildItem (Join-Path $root 'logs') -Filter 'server_*.log' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime | Select-Object -Last 1
    if ($log) { "Newest log: $($log.Name), created $($log.CreationTime.ToString('HH:mm:ss'))" }
}

function Stop-Server($port) {
    $s = Get-State $port
    if ($s.Players -gt 0 -and -not $Force) {
        throw "$($s.Players) player(s) connected. Ask them to log out from the game menu first (or use -Force)."
    }
    Get-RepoProcesses | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
    Start-Sleep -Seconds 2
    'Server stopped.'
}

function Start-Server($port) {
    if ((Get-State $port).Listening) { 'Server is already running.'; return }
    if ($Build) {
        Push-Location $root
        try { dotnet build SphServer.sln | Select-Object -Last 4 } finally { Pop-Location }
    }

    $godot = Find-Godot
    $before = Get-Date
    Start-Process -FilePath $godot -ArgumentList '--headless', '--path', $root `
        -RedirectStandardOutput (Join-Path $root 'godot-run.log') -RedirectStandardError (Join-Path $root 'godot-run.err') `
        -WindowStyle Hidden
    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        $log = Get-ChildItem (Join-Path $root 'logs') -Filter 'server_*.log' -ErrorAction SilentlyContinue |
            Where-Object { $_.CreationTime -ge $before.AddSeconds(-2) } | Sort-Object LastWriteTime | Select-Object -Last 1
        if ($log -and (Select-String -Path $log.FullName -Pattern 'Server up' -Quiet)) {
            "Server is up on port $port (log $($log.Name))."
            return
        }
    }

    throw "The server did not report 'Server up' in $WaitSeconds s. Look at godot-run.err and the newest file in logs\."
}

$port = Get-Port
switch ($Action) {
    'status'  { Show-State $port }
    'stop'    { Stop-Server $port }
    'start'   { Start-Server $port }
    'restart' { Stop-Server $port; Start-Server $port }
}
