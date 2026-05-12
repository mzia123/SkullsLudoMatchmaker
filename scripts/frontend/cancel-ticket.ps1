# Cancel a matchmaking ticket
# Usage: .\cancel-ticket.ps1 -TicketId "abc123"

param(
    [Parameter(Mandatory)][string]$TicketId,
    [string]$BaseUrl = "http://localhost:80"
)

Invoke-WebRequest -Method DELETE -Uri "$BaseUrl/api/matchmaking/tickets/$TicketId" | Select-Object StatusCode, StatusDescription
