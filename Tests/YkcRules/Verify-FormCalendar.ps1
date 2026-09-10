param([string]$BaseUrl = 'http://127.0.0.1:55221', [int]$TalepId = 25)
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
    $r = Post '/api/auth/token' @{ email = $Email; sifre = 'Demo123!' } $null
    if ($r.StatusCode -ne 200) { throw 'Local demo sign-in failed.' }
    ($r.Content | ConvertFrom-Json).token
}
$firm = Token 'test.sertifikalifirma@demo.com'
$staff = Token 'test.personel@demo.com'
$admin = Token 'test.geneladmin@demo.com'
$service = Token 'test.servis@demo.com'
Check ((Post '/api/ykc/talepler/form-pdf' @{id=$TalepId} $null).StatusCode -eq 401) 'Anonymous PDF denied'
Check ((Post '/api/ykc/talepler/form-pdf' @{id=$TalepId} $service).StatusCode -eq 403) 'Unrelated service role cannot read YKC form'
Check ((Post '/api/ykc/takvim' @{} $firm).StatusCode -eq 403) 'Firm cannot read internal calendar'
Check ((Post '/api/ys-devreyeal/gecmis' @{} $admin).StatusCode -eq 403) 'General admin does not impersonate a service role'
$detail = (Post '/api/ykc/talepler/form-verisi' @{id=$TalepId} $firm).Content | ConvertFrom-Json
$pdf = Post '/api/ykc/talepler/form-pdf' @{id=$TalepId} $firm
Check ($pdf.StatusCode -eq 200 -and $pdf.Headers.'Content-Type' -match 'application/pdf') 'Owner receives official PDF'
Check ($pdf.Headers.'Cache-Control' -match 'no-store') 'Personal document is not publicly cached'
if ($detail.imzaSureci.nihaiDosyaId) {
    $download = Post '/api/ykc/talepler/dosya-indir' @{id=$detail.imzaSureci.nihaiDosyaId} $firm
    Check ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([byte[]]$pdf.Content)) -eq [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([byte[]]$download.Content))) 'Signed preview and downloaded file are identical bytes'
}
$calendar = (Post '/api/ykc/takvim' @{baslangic='2026-09-07';bitis='2026-09-13'} $staff).Content | ConvertFrom-Json
Check ($calendar.sayfaBoyutu -eq 25 -and $calendar.kayitlar.Count -le 25) 'Calendar response is bounded'
Check (($calendar.gunler | Measure-Object toplam -Sum).Sum -eq $calendar.toplam) 'Week counts include all filtered appointments, not just one page'
$empty = (Post '/api/ykc/takvim' @{baslangic='2026-09-07';bitis='2026-09-13';musteri='__no_such_customer__'} $staff).Content | ConvertFrom-Json
Check ($empty.toplam -eq 0 -and $empty.kayitlar.Count -eq 0) 'Customer filter reaches the database query'
$dashboardResponse = Post '/api/admin-panel/dashboard' @{} $admin
Check ($dashboardResponse.StatusCode -eq 200) 'Dashboard read is authorized with the general-admin role'
$dashboard = $dashboardResponse.Content | ConvertFrom-Json
$recent = $dashboard.sonDevreyeAlmalar | Select-Object -First 1
Check ($null -ne $recent.PSObject.Properties['musteriAdi'] -and $null -ne $recent.PSObject.Properties['devreyeAlmaTarihi']) 'Dashboard API carries customer and commissioning date'
Write-Output "$passed checks passed. No business-data writes were performed."
