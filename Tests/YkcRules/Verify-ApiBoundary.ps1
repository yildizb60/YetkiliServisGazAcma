param([string]$BaseUrl = 'http://127.0.0.1:55221')
$ErrorActionPreference = 'Stop'
if (-not ([uri]$BaseUrl).IsLoopback) { throw 'Only localhost verification is allowed.' }
$passed = 0
function Check($Condition, $Name) {
    if (-not $Condition) { throw "FAIL: $Name" }
    $script:passed++
    Write-Output "PASS: $Name"
}
function Post($Path, $Body, $Token) {
    $headers = @{}
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    Invoke-WebRequest "$BaseUrl$Path" -Method Post -ContentType 'application/json' -Headers $headers -Body ($Body | ConvertTo-Json -Depth 5) -SkipHttpErrorCheck
}
function Token($Email) {
    $r = Post '/api/auth/token' @{email=$Email; sifre='Demo123!'} $null
    $auth = $r.Content | ConvertFrom-Json
    if ($auth.dogrulama) {
        if ($auth.mesaj -notmatch 'Test SMS modu:.*?(\d{6})') { throw 'Local demo sign-in requires an unavailable live SMS code.' }
        $code = $Matches[1]
        $r = Post '/api/auth/sms-dogrula' @{dogrulama=$auth.dogrulama; kod=$code} $null
        $auth = $r.Content | ConvertFrom-Json
    }
    if ($r.StatusCode -ne 200 -or -not $auth.token) { throw 'Local demo sign-in failed.' }
    $auth.token
}
Check ((Post '/api/panel-kapsam/ykc-yetkileri' @{} $null).StatusCode -eq 401) 'Anonymous permission lookup denied'
$firm = Token 'test.sertifikalifirma@demo.com'
$staff = Token 'test.personel@demo.com'
$admin = Token 'test.geneladmin@demo.com'
$service = Token 'test.servis@demo.com'
$r = Post '/api/panel-kapsam/ykc-yetkileri' @{} $firm
Check ($r.StatusCode -eq 200) 'Firm can read its own permissions'
$permissions = $r.Content | ConvertFrom-Json
Check ($permissions.talepleriGorebilir -and $permissions.talepOlusturabilir) 'Firm retains request permissions'
Check (-not $permissions.atamaYapabilir -and -not $permissions.raporlariGorebilir) 'Firm does not gain staff permissions'
$r = Post '/api/panel-kapsam/ykc-yetkileri' @{} $service
Check ($r.StatusCode -eq 200 -and -not ($r.Content | ConvertFrom-Json).talepleriGorebilir) 'Service role does not gain YKC access'
$companies = (Post '/api/panel-kapsam/sirketler' @{} $staff).Content | ConvertFrom-Json
Check ($companies.Count -gt 0) 'Staff company assignments are available'
foreach ($company in $companies) {
    Check ((Post '/api/panel-kapsam/ykc-yetkileri' @{aktifSirketId=$company.id} $staff).StatusCode -eq 200) 'Assigned staff company is accepted'
}
Check ((Post '/api/panel-kapsam/ykc-yetkileri' @{aktifSirketId=[int]::MaxValue} $staff).StatusCode -eq 403) 'Out-of-scope staff company is denied'
Check ((Post '/api/panel-kapsam/kimlik' @{aktifSirketId=[int]::MaxValue} $firm).StatusCode -eq 403) 'Company identity endpoint rejects out-of-scope company'
Check ((Post '/api/panel-kapsam/ykc-yetkileri' @{aktifSirketId=[int]::MaxValue} $admin).StatusCode -eq 403) 'Even admin cannot select a nonexistent company'
$r = Post '/api/panel-kapsam/ykc-yetkileri' @{} $admin
Check ($r.StatusCode -eq 200 -and ($r.Content | ConvertFrom-Json).atamaYapabilir) 'General admin retains assignment permission'
Write-Output "$passed checks passed. No business-data writes were performed."
