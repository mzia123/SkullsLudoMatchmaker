# Check frontend health
# Usage: .\health-check.ps1
# Override base URL via -BaseUrl or $env:SKULLS_FRONTEND_URL

param(
    [string]$BaseUrl = $(if ($env:SKULLS_FRONTEND_URL) { $env:SKULLS_FRONTEND_URL } else { "http://localhost:19503" })
)

Invoke-RestMethod -Method GET -Uri "$BaseUrl/healthz"
