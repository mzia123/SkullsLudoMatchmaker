# Create a matchmaking ticket
# Usage: .\create-ticket.ps1 -PlayerId "alice" -Mmr 1500 -Queue "quickplay"
# Usage: .\create-ticket.ps1 -PlayerId "bob" -Mmr 800 -Queue "practice"

param(
    [Parameter(Mandatory)][string]$PlayerId,
    [Parameter(Mandatory)][double]$Mmr,
    [Parameter(Mandatory)][ValidateSet("practice","quickplay")][string]$Queue,
    [string]$BaseUrl = "http://localhost:80"
)

$body = @{ playerId = $PlayerId; mmr = $Mmr; queue = $Queue } | ConvertTo-Json
Invoke-RestMethod -Method POST -Uri "$BaseUrl/api/matchmaking/tickets" -ContentType "application/json" -Body $body
