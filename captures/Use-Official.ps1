# Points the game client at the official server by restoring your saved *.official connection files.
# Before it restores them, it keeps the local one (127.0.0.1) as *.local for Use-Local.ps1.
# Make the *.official copies once, from the connect.cfg and connectn.cfg the game was installed with.
param([string]$GameDir = "C:\Games\Sphere World of the Chosen")
$g = $GameDir

if (Get-Process sphereclient*, sphere -ErrorAction SilentlyContinue) {
    Write-Host "Close the game before switching the connection." -ForegroundColor Yellow
    return
}

foreach ($f in "connect.cfg", "connectn.cfg") {
    if (-not (Test-Path "$g\$f.official")) {
        Write-Host "$f.official is missing, nothing was changed." -ForegroundColor Red
        return
    }
}

foreach ($f in "connect.cfg", "connectn.cfg") {
    $current = Get-Content "$g\$f" -Raw
    if ($current -match '127\.0\.0\.1') {
        Copy-Item "$g\$f" "$g\$f.local" -Force
    }

    Copy-Item "$g\$f.official" "$g\$f" -Force
}

Get-Content "$g\connect.cfg", "$g\connectn.cfg" | Select-String 'MAIN_URL'
Write-Host "Connection: OFFICIAL server." -ForegroundColor Green
