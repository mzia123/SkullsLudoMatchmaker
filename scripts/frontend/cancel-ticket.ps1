# Cancel a matchmaking ticket
# Usage: .\cancel-ticket.ps1 -TicketId abc123
# Override base URL via -BaseUrl or $env:SKULLS_FRONTEND_URL

param(
    [Parameter(Mandatory)][string]$TicketId,
    [string]$BaseUrl = $(if ($env:SKULLS_FRONTEND_URL) { $env:SKULLS_FRONTEND_URL } else { "http://localhost:19503" })
)

try {
    Invoke-WebRequest -Method DELETE -Uri "$BaseUrl/api/matchmaking/tickets/$TicketId" |
        Select-Object StatusCode, StatusDescription
}
catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 404) {
        [pscustomobject]@{ ticketId = $TicketId; status = "not_found" }
    } else {
        throw
    }
}
