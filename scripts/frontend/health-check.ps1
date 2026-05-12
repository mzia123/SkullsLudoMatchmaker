# Check frontend health
# Usage: .\health-check.ps1

param(
    [string]$BaseUrl = "http://localhost:80"
)

Invoke-RestMethod -Method GET -Uri "$BaseUrl/healthz"
