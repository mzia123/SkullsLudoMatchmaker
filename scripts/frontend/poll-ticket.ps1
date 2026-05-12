# Poll a ticket until matched or timed out
# Usage: .\poll-ticket.ps1 -TicketId "abc123" -IntervalSec 3 -MaxPolls 10

param(
    [Parameter(Mandatory)][string]$TicketId,
    [int]$IntervalSec = 3,
    [int]$MaxPolls = 20,
    [string]$BaseUrl = "http://localhost:80"
)

$uri = "$BaseUrl/api/matchmaking/tickets/$TicketId"

for ($i = 1; $i -le $MaxPolls; $i++) {
    $r = Invoke-RestMethod -Method GET -Uri $uri
    Write-Host "[$i/$MaxPolls] status=$($r.status)" -NoNewline
    if ($r.status -eq "matched") {
        Write-Host " connection=$($r.connection) players=$($r.playerCount)" -ForegroundColor Green
        return $r
    }
    if ($r.status -eq "timeout") {
        Write-Host " - no match found" -ForegroundColor Red
        return $r
    }
    Write-Host ""
    Start-Sleep -Seconds $IntervalSec
}
Write-Host "Gave up after $MaxPolls polls."
