# Poll a ticket's status
# Usage: .\get-ticket.ps1 -TicketId "abc123"

param(
    [Parameter(Mandatory)][string]$TicketId,
    [string]$BaseUrl = "http://localhost:80"
)

Invoke-RestMethod -Method GET -Uri "$BaseUrl/api/matchmaking/tickets/$TicketId"
