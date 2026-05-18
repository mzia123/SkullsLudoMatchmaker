# Create a matchmaking ticket
# Usage: .\create-ticket.ps1 -Mmr 1500 -Queue quickplay-nonteam -PlayerId alice
#        .\create-ticket.ps1 -Mmr 800 -Queue practice-team -Token $env:SKULLS_UNITY_ID_TOKEN
# Override base URL via -BaseUrl or $env:SKULLS_FRONTEND_URL

param(
    [Parameter(Mandatory)][double]$Mmr,
    [Parameter(Mandatory)][string]$Queue,
    [string]$PlayerId = $(if ($env:SKULLS_DEBUG_PLAYER_ID) { $env:SKULLS_DEBUG_PLAYER_ID } else { "dev-player" }),
    [string]$Token = $env:SKULLS_UNITY_ID_TOKEN,
    [string]$BaseUrl = $(if ($env:SKULLS_FRONTEND_URL) { $env:SKULLS_FRONTEND_URL } else { "http://localhost:19503" })
)

. "$PSScriptRoot\_auth-headers.ps1"

$body = @{ mmr = $Mmr; queue = $Queue } | ConvertTo-Json
$headers = Get-SkullsFrontendHeaders -PlayerId $PlayerId -Token $Token
Invoke-RestMethod -Method POST -Uri "$BaseUrl/api/matchmaking/tickets" `
    -ContentType "application/json" -Body $body -Headers $headers
