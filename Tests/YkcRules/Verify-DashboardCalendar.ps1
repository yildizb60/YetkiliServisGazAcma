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
    $response = Post '/api/auth/token' @{email=$Email;sifre='Demo123!'} $null
    $auth = $response.Content | ConvertFrom-Json
    if ($auth.dogrulama -and $auth.mesaj -match 'Test SMS modu:.*?(\d{6})') {
        $response = Post '/api/auth/sms-dogrula' @{dogrulama=$auth.dogrulama;kod=$Matches[1]} $null
        $auth = $response.Content | ConvertFrom-Json
    }
    if ($response.StatusCode -ne 200 -or -not $auth.token) { throw 'Local demo sign-in failed; live SMS is not supported by this test.' }
    $auth.token
}
function Calendar($Body, $Token) {
    $response = Post '/api/ykc/takvim' $Body $Token
    if ($response.StatusCode -ne 200) { throw "Calendar response: $($response.StatusCode)" }
    $response.Content | ConvertFrom-Json
}
$staff = Token 'test.personel@demo.com'
$firm = Token 'test.sertifikalifirma@demo.com'
$admin = Token 'test.geneladmin@demo.com'
$service = Token 'test.servis@demo.com'
Check ((Post '/api/ykc/takvim' @{} $null).StatusCode -eq 401) 'Anonymous calendar denied'
Check ((Post '/api/ykc/takvim' @{} $firm).StatusCode -eq 200) 'Certified firm can access its appointment calendar'
Check ((Post '/api/ykc/takvim' @{} $service).StatusCode -eq 403) 'Service role cannot access the internal calendar'
Check ((Post '/api/ykc/takvim' @{aktifSirketId=[int]::MaxValue} $staff).StatusCode -eq 403) 'Calendar rejects an unauthorized company'
Check ((Post '/api/ykc/dashboard/ozet' @{aktifSirketId=[int]::MaxValue} $staff).StatusCode -eq 403) 'Dashboard rejects an unauthorized company'
Check ((Post '/api/ykc/takvim' @{aktifSirketId=[int]::MaxValue} $admin).StatusCode -eq 403) 'Admin cannot select a nonexistent company'
$body = @{baslangic='2026-09-01';bitis='2026-09-30'}
$calendar = Calendar $body $staff
$firmCalendar = Calendar $body $firm
Check ($firmCalendar.toplam -gt 0) 'Certified firm appointment calendar contains its scoped records'
Check (@($firmCalendar.kayitlar | Where-Object { $_.personel -or $_.ekip }).Count -eq 0) 'Certified firm calendar hides internal staff and team assignments'
$firmIgnoredInternalFilters = Calendar ($body + @{il='__other_city__';bolge='__other_region__';personel='__other_team__'}) $firm
Check ($firmIgnoredInternalFilters.toplam -eq $firmCalendar.toplam) 'Certified firm cannot narrow records by internal routing fields'
Check ($calendar.toplam -gt 0) 'Existing appointment data is available for semantic checks'
Check ($calendar.sayfaBoyutu -eq 25 -and $calendar.kayitlar.Count -le 25) 'Calendar page has a bounded record count'
Check (($calendar.gunler | Measure-Object toplam -Sum).Sum -eq $calendar.toplam) 'Month counts cover all filtered records'
$emptyFilters = Calendar ($body + @{il=' ';bolge=' ';personel=' ';musteri=' '}) $staff
Check ($emptyFilters.toplam -eq $calendar.toplam) 'Empty filters preserve every authorized appointment'
$sample = $calendar.kayitlar | Where-Object { $_.ekip -and $_.il -and $_.bolge -and $_.musteri } | Select-Object -First 1
Check ($null -ne $sample) 'Existing record has team, city, region and subscriber fields'
$combined = Calendar ($body + @{il=" $($sample.il) ";bolge=" $($sample.bolge) ";personel=" $($sample.ekip) ";musteri=$sample.musteri}) $staff
Check ($combined.toplam -gt 0) 'Combined city, region, team and subscriber filter matches'
Check (@($combined.kayitlar | Where-Object { $_.il -ne $sample.il -or $_.bolge -ne $sample.bolge -or $_.ekip -ne $sample.ekip -or $_.musteri -ne $sample.musteri }).Count -eq 0) 'Every combined result satisfies every filter'
$none = Calendar ($body + @{personel='__missing_team__'}) $staff
Check ($none.toplam -eq 0 -and $none.kayitlar.Count -eq 0 -and $none.gunler.Count -eq 0) 'Unmatched filter returns a truthful empty calendar'
$day = ([datetime]$sample.tarih).ToString('yyyy-MM-dd')
$daily = Calendar @{baslangic=$day;bitis=$day} $staff
Check ($daily.toplam -gt 0 -and @($daily.kayitlar | Where-Object { ([datetime]$_.tarih).ToString('yyyy-MM-dd') -ne $day }).Count -eq 0) 'Day selection never includes another day'
$companies = (Post '/api/panel-kapsam/sirketler' @{} $staff).Content | ConvertFrom-Json
Check ($companies.Count -gt 1) 'Multi-company test account is available'
$idsByCompany = @{}
foreach ($company in $companies) {
    $permission = (Post '/api/panel-kapsam/ykc-yetkileri' @{aktifSirketId=$company.id} $staff).Content | ConvertFrom-Json
    if (-not $permission.talepleriGorebilir) {
        Check ((Post '/api/ykc/takvim' ($body + @{aktifSirketId=$company.id}) $staff).StatusCode -eq 403) 'Company membership alone does not grant calendar permission'
        Check ((Post '/api/ykc/dashboard/ozet' @{aktifSirketId=$company.id} $staff).StatusCode -eq 403) 'Company membership alone does not grant dashboard permission'
        continue
    }
    $scoped = Calendar ($body + @{aktifSirketId=$company.id}) $staff
    Check ($scoped.filtre.aktifSirketId -eq $company.id) 'Calendar honors selected authorized company'
    $idsByCompany[$company.id] = @($scoped.kayitlar.id)
    $response = Post '/api/ykc/dashboard/ozet' @{aktifSirketId=$company.id} $staff
    Check ($response.StatusCode -eq 200) 'Dashboard accepts authorized company'
    $dashboard = $response.Content | ConvertFrom-Json
    Check (@($dashboard.sonTalepler | Where-Object { $_.sirketAdi -ne $company.sirketAdi }).Count -eq 0) 'Dashboard records belong to selected company'
}
foreach ($company in $companies) {
    $response = Post '/api/ykc/dashboard/ozet' @{aktifSirketId=$company.id} $admin
    $dashboard = $response.Content | ConvertFrom-Json
    Check ($response.StatusCode -eq 200 -and @($dashboard.sonTalepler | Where-Object { $_.sirketAdi -ne $company.sirketAdi }).Count -eq 0) 'Admin explicit company selection is also scoped'
    $scoped = Calendar ($body + @{aktifSirketId=$company.id}) $admin
    $idsByCompany[$company.id] = @($scoped.kayitlar.id)
}
$allIds = @($idsByCompany.Values | ForEach-Object { $_ } | Where-Object { $null -ne $_ })
Check ($allIds.Count -eq @($allIds | Sort-Object -Unique).Count) 'Different company calendars do not repeat one company records'
$dashboard = (Post '/api/ykc/dashboard/ozet' @{} $firm).Content | ConvertFrom-Json
Check ($dashboard.toplam -gt 0) 'Certified firm dashboard loads its records'
Check (@($dashboard.sonTalepler | Where-Object { $_.projedekiCihazBilgisi -or $_.eskiCihaz }).Count -eq 0) 'Firm dashboard keeps source-device information hidden'
Check ($dashboard.toplam -eq ($dashboard.incelemede + $dashboard.randevuSaha + $dashboard.tamamlanan + $dashboard.redIptal)) 'Dashboard stage counts reconcile with the total'
Write-Output "$passed checks passed. No YKC request data was changed."
