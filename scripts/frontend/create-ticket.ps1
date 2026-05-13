# Create a matchmaking ticket
# Usage: .\create-ticket.ps1 -PlayerId alice -Mmr 1500 -Queue quickplay
#        .\create-ticket.ps1 -PlayerId bob   -Mmr 800  -Queue practice
# Override base URL via -BaseUrl or $env:SKULLS_FRONTEND_URL

param(
    [Parameter(Mandatory)][string]$PlayerId,
    [Parameter(Mandatory)][double]$Mmr,
    [Parameter(Mandatory)][ValidateSet("practice","quickplay")][string]$Queue,
    [string]$BaseUrl = $(if ($env:SKULLS_FRONTEND_URL) { $env:SKULLS_FRONTEND_URL } else { "http://localhost:19503" })
)

$body = @{ playerId = $PlayerId; mmr = $Mmr; queue = $Queue } | ConvertTo-Json
Invoke-RestMethod -Method POST -Uri "$BaseUrl/api/matchmaking/tickets" -ContentType "application/json" -Body $body
