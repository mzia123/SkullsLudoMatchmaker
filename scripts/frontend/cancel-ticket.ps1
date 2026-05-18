# Cancel a matchmaking ticket
# Usage: .\cancel-ticket.ps1 -TicketId abc123 -PlayerId alice
# Override base URL via -BaseUrl or $env:SKULLS_FRONTEND_URL

param(
    [Parameter(Mandatory)][string]$TicketId,
    [string]$PlayerId = $(if ($env:SKULLS_DEBUG_PLAYER_ID) { $env:SKULLS_DEBUG_PLAYER_ID } else { "dev-player" }),
    [string]$Token = $env:SKULLS_UNITY_ID_TOKEN,
    [string]$BaseUrl = $(if ($env:SKULLS_FRONTEND_URL) { $env:SKULLS_FRONTEND_URL } else { "http://localhost:19503" })
)

. "$PSScriptRoot\_auth-headers.ps1"

$headers = Get-SkullsFrontendHeaders -PlayerId $PlayerId -Token $Token

try {
    Invoke-WebRequest -Method DELETE -Uri "$BaseUrl/api/matchmaking/tickets/$TicketId" -Headers $headers |
        Select-Object StatusCode, StatusDescription
}
catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 404) {
        [pscustomobject]@{ ticketId = $TicketId; status = "not_found" }
    } else {
        throw
    }
}
