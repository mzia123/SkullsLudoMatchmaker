# Bootstrap mTLS between the Director and the Agones allocator.
# Run ONCE per cluster. Idempotent: safe to re-run.
#
# Strategy:
#   1. If Helm-provisioned `allocator-client.default` exists, copy it into
#      `allocator-client-tls` so we have a stable name + key layout.
#      Otherwise, generate a CA + client cert, allowlist the CA on the allocator,
#      and create `allocator-client-tls` from the new cert.
#   2. Copy the allocator's server CA from `agones-system/allocator-tls-ca`
#      into `<ns>/allocator-server-ca` so the Director can pin the server cert.
#
# Requirements: kubectl on PATH; openssl on PATH only if the Helm secret is absent.
# Usage: .\scripts\setup-allocator-mtls.ps1
#        .\scripts\setup-allocator-mtls.ps1 -Namespace default

param(
    [string]$Namespace = "default",
    [string]$AgonesNamespace = "agones-system",
    [string]$ClientName = "skulls-ludo-director",
    [int]$DaysValid = 3650,
    [string]$WorkDir = (Join-Path $env:TEMP "agones-mtls")
)

$ErrorActionPreference = "Stop"

function Test-Command($name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "$name is required on PATH"
    }
}

function Get-SecretField($ns, $name, $field) {
    $jsonPath = "{.data.$($field -replace '\.','\.')}"
    $val = kubectl get secret $name -n $ns -o jsonpath="$jsonPath" 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $val) { return $null }
    return $val
}

Test-Command kubectl

# 1. Discover or generate the client cert/key.
$existingCrt = Get-SecretField $Namespace 'allocator-client.default' 'tls.crt'
$existingKey = Get-SecretField $Namespace 'allocator-client.default' 'tls.key'

New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null

if ($existingCrt -and $existingKey) {
    Write-Host "Reusing existing $Namespace/allocator-client.default (Helm-provisioned)" -ForegroundColor Cyan
    [IO.File]::WriteAllBytes((Join-Path $WorkDir 'client.crt'), [Convert]::FromBase64String($existingCrt))
    [IO.File]::WriteAllBytes((Join-Path $WorkDir 'client.key'), [Convert]::FromBase64String($existingKey))
}
else {
    Test-Command openssl
    Write-Host "Generating fresh CA + client cert in $WorkDir" -ForegroundColor Cyan
    Push-Location $WorkDir
    try {
        openssl genrsa -out ca.key 4096 | Out-Null
        openssl req -x509 -new -nodes -sha256 -key ca.key -days $DaysValid `
            -out ca.crt -subj "/CN=skulls-ludo-allocator-ca" | Out-Null

        openssl genrsa -out client.key 4096 | Out-Null
        openssl req -new -sha256 -key client.key `
            -out client.csr -subj "/CN=$ClientName" | Out-Null
        openssl x509 -req -in client.csr -CA ca.crt -CAkey ca.key -CAcreateserial `
            -out client.crt -days $DaysValid -sha256 | Out-Null

        Write-Host "Allowlisting client CA in $AgonesNamespace/allocator-client-ca" -ForegroundColor Cyan
        $caB64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $WorkDir 'ca.crt')))
        $patch = @{ data = @{ 'skulls-ludo-director.crt' = $caB64 } } | ConvertTo-Json -Compress
        kubectl patch secret allocator-client-ca -n $AgonesNamespace --type merge -p $patch
    }
    finally {
        Pop-Location
    }
}

# 2. Create allocator-client-tls in target namespace.
Write-Host "Creating $Namespace/allocator-client-tls" -ForegroundColor Cyan
kubectl create secret tls allocator-client-tls `
    -n $Namespace `
    --cert=(Join-Path $WorkDir 'client.crt') `
    --key=(Join-Path $WorkDir 'client.key') `
    --dry-run=client -o yaml | kubectl apply -f -

# 3. Copy server CA from agones-system to target namespace.
Write-Host "Copying allocator server CA into $Namespace/allocator-server-ca" -ForegroundColor Cyan
$serverCa = Get-SecretField $AgonesNamespace 'allocator-tls-ca' 'tls-ca.crt'
if (-not $serverCa) {
    $serverCa = Get-SecretField $AgonesNamespace 'allocator-tls-ca' 'ca.crt'
}
if (-not $serverCa) {
    throw "Could not read allocator server CA from $AgonesNamespace/allocator-tls-ca"
}
[IO.File]::WriteAllBytes((Join-Path $WorkDir 'server-ca.crt'), [Convert]::FromBase64String($serverCa))

kubectl create secret generic allocator-server-ca `
    -n $Namespace `
    --from-file=ca.crt=(Join-Path $WorkDir 'server-ca.crt') `
    --dry-run=client -o yaml | kubectl apply -f -

Write-Host ""
Write-Host "Done. Director can now reach agones-allocator over mTLS." -ForegroundColor Green
Write-Host "Restart the Director to pick up the new secrets:" -ForegroundColor Yellow
Write-Host "  kubectl rollout restart deployment/skulls-ludo-director -n $Namespace" -ForegroundColor Yellow
