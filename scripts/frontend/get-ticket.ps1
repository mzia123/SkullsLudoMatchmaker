# Poll a ticket's status (handles 404 cleanly)
# Usage: .\get-ticket.ps1 -TicketId abc123
# Override base URL via -BaseUrl or $env:SKULLS_FRONTEND_URL

param(
    [Parameter(Mandatory)][string]$TicketId,
    [string]$BaseUrl = $(if ($env:SKULLS_FRONTEND_URL) { $env:SKULLS_FRONTEND_URL } else { "http://localhost:19503" })
)

try {
    Invoke-RestMethod -Method GET -Uri "$BaseUrl/api/matchmaking/tickets/$TicketId"
}
catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 404) {
        [pscustomobject]@{ ticketId = $TicketId; status = "not_found" }
    } else {
        throw
    }
}
