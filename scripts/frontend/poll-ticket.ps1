# Poll a ticket until matched, timed out, or max polls reached.
# Usage: .\poll-ticket.ps1 -TicketId abc123
#        .\poll-ticket.ps1 -TicketId abc123 -IntervalSec 3 -MaxPolls 10
# Override base URL via -BaseUrl or $env:SKULLS_FRONTEND_URL

param(
    [Parameter(Mandatory)][string]$TicketId,
    [int]$IntervalSec = 3,
    [int]$MaxPolls = 20,
    [string]$BaseUrl = $(if ($env:SKULLS_FRONTEND_URL) { $env:SKULLS_FRONTEND_URL } else { "http://localhost:19503" })
)

$uri = "$BaseUrl/api/matchmaking/tickets/$TicketId"

for ($i = 1; $i -le $MaxPolls; $i++) {
    try {
        $r = Invoke-RestMethod -Method GET -Uri $uri
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 404) {
            Write-Host "[$i/$MaxPolls] ticket not found" -ForegroundColor Red
            return
        }
        throw
    }

    Write-Host "[$i/$MaxPolls] status=$($r.status)" -NoNewline

    switch ($r.status) {
        "matched" {
            Write-Host " connection=$($r.connection) players=$($r.playerCount)" -ForegroundColor Green
            return $r
        }
        "timeout" {
            Write-Host " - no match found" -ForegroundColor Red
            return $r
        }
        default { Write-Host "" }
    }

    Start-Sleep -Seconds $IntervalSec
}

Write-Host "Gave up after $MaxPolls polls." -ForegroundColor Yellow
