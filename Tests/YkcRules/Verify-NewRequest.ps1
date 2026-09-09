param([string]$BaseUrl = 'http://127.0.0.1:55192')
$ErrorActionPreference = 'Stop'
if (-not ([uri]$BaseUrl).IsLoopback) { throw 'Only an isolated local test API is allowed.' }

function Post-Json($Path, $Body, $Token) {
    $headers = @{}
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    $response = Invoke-WebRequest -Uri "$BaseUrl$Path" -Method Post -Headers $headers -ContentType 'application/json' -Body ($Body | ConvertTo-Json -Depth 5) -SkipHttpErrorCheck
    $data = if ($response.Content) { $response.Content | ConvertFrom-Json } else { $null }
    return [pscustomobject]@{ Status = [int]$response.StatusCode; Data = $data }
}
function Check($Condition, $Name) {
    if (-not $Condition) { throw "FAIL: $Name" }
    Write-Output "PASS: $Name"
}
function Token($Email) {
    $result = Post-Json '/api/auth/token' @{ email = $Email; sifre = 'Demo123!' } $null
    if ($result.Status -ne 200 -or -not $result.Data.token) { throw "Demo login failed: $Email" }
    return $result.Data.token
}

$query = @{ tesisatNo = '9000001'; sozlesmeNo = '900001' }
$anonymous = Post-Json '/api/ykc/tesisat-sorgula' $query $null
Check ($anonymous.Status -eq 401) 'Anonymous query is denied'
$firm = Token 'test.sertifikalifirma@demo.com'
$result = Post-Json '/api/ykc/tesisat-sorgula' $query $firm
Check ($result.Status -eq 200 -and $result.Data.basarili -and $result.Data.cihazlar.Count -eq 2) 'Firm receives two fixture device choices'
Check (($result.Data.cihazlar | Where-Object { $_.cihazMarka -or $_.cihazKapasite -or $_.projeNo }).Count -eq 0) 'Firm API does not disclose protected source details'
Check (($result.Data.cihazlar | Where-Object { -not $_.sorguReferansi }).Count -eq 0) 'Every choice has a server query reference'
$mismatch = Post-Json '/api/ykc/tesisat-sorgula' @{ tesisatNo = '9000002'; sozlesmeNo = '900001' } $firm
Check (-not $mismatch.Data.basarili -and $mismatch.Data.cihazlar.Count -eq 0) 'Mismatched SOAP installation is rejected before issuing references'
$invalid = Post-Json '/api/ykc/tesisat-sorgula' @{ tesisatNo = '-1'; sozlesmeNo = '900001' } $firm
Check (-not $invalid.Data.basarili) 'Negative installation number is rejected by API'

# Intentionally invalid reference and incomplete data: no successful create is attempted.
$forged = Post-Json '/api/ykc/talepler/olustur' @{ sorguReferansi = 'invalid-local-test-reference' } $firm
Check ($forged.Status -eq 400 -and -not $forged.Data.basarili) 'Create without a valid source query is denied'
$personnel = Token 'test.personel@demo.com'
$deniedQuery = Post-Json '/api/ykc/tesisat-sorgula' $query $personnel
Check ($deniedQuery.Status -eq 403) 'Full-authority personnel cannot use the firm query workflow'
$deniedCreate = Post-Json '/api/ykc/talepler/olustur' @{} $personnel
Check ($deniedCreate.Status -eq 403) 'Full-authority personnel cannot create a firm request'
Write-Output '9 API checks passed. No successful business-data mutation was attempted.'
