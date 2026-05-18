# Shared auth headers for frontend test scripts (dot-source from sibling scripts).
# Production: set $env:SKULLS_UNITY_ID_TOKEN to a Unity idToken (Bearer).
# Dev bypass (Matchmaker:UnityAuth:Enabled=false): set $DebugPlayerId or $env:SKULLS_DEBUG_PLAYER_ID.

function Get-SkullsFrontendHeaders {
    param(
        [string]$PlayerId = $(if ($env:SKULLS_DEBUG_PLAYER_ID) { $env:SKULLS_DEBUG_PLAYER_ID } else { "dev-player" }),
        [string]$Token = $env:SKULLS_UNITY_ID_TOKEN
    )

    $headers = @{}
    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    } else {
        $headers["X-Debug-Player-Id"] = $PlayerId
    }
    return $headers
}
