# Points the game client at the local server (127.0.0.1) using the *.local copies.
param([string]$GameDir = "C:\Games\Sphere World of the Chosen")
$g = $GameDir

if (Get-Process sphereclient*, sphere -ErrorAction SilentlyContinue) {
    Write-Host "Close the game before switching the connection." -ForegroundColor Yellow
    return
}

foreach ($f in "connect.cfg", "connectn.cfg") {
    if (-not (Test-Path "$g\$f.local")) {
        Write-Host "$f.local is missing (run Use-Official.ps1 once while the game points at 127.0.0.1)." -ForegroundColor Red
        return
    }
}

foreach ($f in "connect.cfg", "connectn.cfg") {
    Copy-Item "$g\$f.local" "$g\$f" -Force
}

Get-Content "$g\connect.cfg", "$g\connectn.cfg" | Select-String 'MAIN_URL'
Write-Host "Connection: LOCAL server (127.0.0.1)." -ForegroundColor Green
