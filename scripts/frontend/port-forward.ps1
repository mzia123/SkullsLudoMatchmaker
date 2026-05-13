# Forward the frontend service to a local port (default avoids common 8080 conflicts).
# Usage: .\port-forward.ps1
#        .\port-forward.ps1 -LocalPort 9090

param(
    [int]$LocalPort = 19503,
    [string]$Service = "skulls-ludo-frontend",
    [string]$Namespace = "default"
)

Write-Host "Forwarding $Namespace/$Service:80 -> localhost:$LocalPort" -ForegroundColor Cyan
Write-Host "Set scripts BaseUrl: -BaseUrl http://localhost:$LocalPort" -ForegroundColor Yellow
Write-Host "Or export: `$env:SKULLS_FRONTEND_URL = 'http://localhost:$LocalPort'" -ForegroundColor Yellow
Write-Host ""

kubectl port-forward -n $Namespace "svc/$Service" "${LocalPort}:80"
