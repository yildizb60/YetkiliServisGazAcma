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
$companyAdmin = Token 'test.sirketadmin@demo.com'
$service = Token 'test.servis@demo.com'
$publicBrands = Post '/api/marka/liste' @{tumunuGetir=$false} $null
Check ($publicBrands.StatusCode -eq 200) 'Public registration can read active brands'
Check ((Post '/api/marka/liste' @{tumunuGetir=$true} $null).StatusCode -eq 401) 'Anonymous caller cannot read the brand management list'
Check ((Post '/api/marka/liste' @{tumunuGetir=$true} $service).StatusCode -eq 403) 'Service role cannot read the brand management list'
Check ((Post '/api/marka/liste' @{tumunuGetir=$true} $staff).StatusCode -eq 200) 'Authorized staff can read the brand management list'
$firstBrand = @($publicBrands.Content | ConvertFrom-Json) | Select-Object -First 1
if ($firstBrand) {
    Check ((Post '/api/marka/getir' @{id=$firstBrand.id} $service).StatusCode -eq 403) 'Service role cannot open a brand management record'
}
$r = Post '/api/panel-kapsam/ykc-yetkileri' @{} $firm
Check ($r.StatusCode -eq 200) 'Firm can read its own permissions'
$permissions = $r.Content | ConvertFrom-Json
Check ($permissions.talepleriGorebilir -and $permissions.talepOlusturabilir) 'Firm retains request permissions'
Check (-not $permissions.atamaYapabilir -and -not $permissions.raporlariGorebilir) 'Firm does not gain staff permissions'
Check ((Post '/api/ykc/talepler/liste' @{} $firm).StatusCode -eq 200) 'Firm can read its own YKC requests'
Check ((Post '/api/ykc/talepler/rapor' @{} $firm).StatusCode -eq 403) 'Firm cannot read internal YKC reports'
$firmRequests = (Post '/api/ykc/talepler/liste' @{sayfaBoyutu=100} $firm).Content | ConvertFrom-Json
$firmSample = @($firmRequests.talepler | Select-Object -First 1)
if ($firmSample.Count -gt 0) {
    $adminRequests = (Post '/api/ykc/talepler/liste' @{sayfaBoyutu=100} $admin).Content | ConvertFrom-Json
    $otherFirmRequests = @($adminRequests.talepler | Where-Object {
        $_.sirketAdi -eq $firmSample[0].sirketAdi -and $_.firmaAdi -ne $firmSample[0].firmaAdi
    })
    foreach ($request in $otherFirmRequests) {
        $detailResponse = Post '/api/ykc/talepler/getir' @{id=$request.id} $admin
        if ($detailResponse.StatusCode -ne 200) { continue }
        $file = @((($detailResponse.Content | ConvertFrom-Json).dosyalar) | Select-Object -First 1)
        if ($file.Count -eq 0) { continue }
        Check ((Post '/api/ykc/talepler/dosya-indir' @{id=$file[0].id} $firm).StatusCode -eq 403) 'Firm cannot download another firm file in the same company'
        break
    }
}
$r = Post '/api/panel-kapsam/ykc-yetkileri' @{} $service
Check ($r.StatusCode -eq 200 -and -not ($r.Content | ConvertFrom-Json).talepleriGorebilir) 'Service role does not gain YKC access'
Check ((Post '/api/ykc/talepler/liste' @{} $service).StatusCode -eq 403) 'Service role cannot read YKC requests'
Check ((Post '/api/ykc/talepler/rapor' @{} $service).StatusCode -eq 403) 'Service role cannot read YKC reports'
$companies = (Post '/api/panel-kapsam/sirketler' @{} $staff).Content | ConvertFrom-Json
Check ($companies.Count -gt 0) 'Staff company assignments are available'
$adminCompanies = @((Post '/api/panel-kapsam/sirketler' @{} $companyAdmin).Content | ConvertFrom-Json)
Check ($adminCompanies.Count -eq 1) 'Company admin has one assigned company'
$ownCompanyId = $adminCompanies[0].id
Check ((Post '/api/admin-panel/subeler/liste' @{sirketId=$ownCompanyId} $companyAdmin).StatusCode -eq 200) 'Company admin can read its own branches'
Check ((Post '/api/admin-panel/yetki-belgeleri/onay-listesi' @{sirketId=$ownCompanyId} $companyAdmin).StatusCode -eq 200) 'Company admin can read its own approvals'
Check ((Post '/api/ykc/talepler/liste' @{sirketId=$ownCompanyId} $companyAdmin).StatusCode -eq 200) 'Company admin can read its own YKC requests'
Check ((Post '/api/ykc/talepler/rapor' @{sirketId=$ownCompanyId} $companyAdmin).StatusCode -eq 200) 'Company admin can read its own YKC reports'
$otherCompany = @($companies | Where-Object { $_.id -ne $ownCompanyId } | Select-Object -First 1)
if ($otherCompany.Count -gt 0) {
    Check ((Post '/api/admin-panel/subeler/liste' @{sirketId=$otherCompany[0].id} $companyAdmin).StatusCode -eq 403) 'Company admin cannot read another company branches'
    Check ((Post '/api/admin-panel/yetki-belgeleri/onay-listesi' @{sirketId=$otherCompany[0].id} $companyAdmin).StatusCode -eq 403) 'Company admin cannot read another company approvals'
    Check ((Post '/api/ykc/talepler/liste' @{sirketId=$otherCompany[0].id} $companyAdmin).StatusCode -eq 403) 'Company admin cannot read another company YKC requests'
    Check ((Post '/api/ykc/talepler/rapor' @{sirketId=$otherCompany[0].id} $companyAdmin).StatusCode -eq 403) 'Company admin cannot read another company YKC reports'
}
foreach ($company in $companies) {
    Check ((Post '/api/panel-kapsam/ykc-yetkileri' @{aktifSirketId=$company.id} $staff).StatusCode -eq 200) 'Assigned staff company is accepted'
    $permissionResponse = Post '/api/personel-panel/yetkilerim' @{sirketId=$company.id} $staff
    Check ($permissionResponse.StatusCode -eq 200) 'Staff permissions can be read for an assigned company'
    $permissions = @((($permissionResponse.Content | ConvertFrom-Json).yetkiler))
    $canManageUsers = $permissions -contains 'TAM_YETKI' -or $permissions -contains 'KULLANICI_YONET'
    $canApprove = $permissions -contains 'TAM_YETKI' -or $permissions -contains 'YETKI_BELGESI_ONAY'
    $canReport = $permissions -contains 'TAM_YETKI' -or $permissions -contains 'RAPOR_GOR'
    $canViewYkc = $permissions -contains 'TAM_YETKI' -or $permissions -contains 'YKC_TALEP_GOR'
    $canReportYkc = $permissions -contains 'TAM_YETKI' -or $permissions -contains 'YKC_RAPOR_GOR'
    $canAssignYkc = $permissions -contains 'TAM_YETKI' -or $permissions -contains 'YKC_ATAMA_YAP'
    $canSignYkc = $permissions -contains 'TAM_YETKI' -or $permissions -contains 'YKC_FR265_IMZA_ISLEM'
    $serviceListStatus = (Post '/api/admin-panel/yetkili-servisler/liste' @{sirketId=$company.id} $staff).StatusCode
    Check ($serviceListStatus -eq $(if ($canManageUsers) { 200 } else { 403 })) 'Authorized-service list enforces KULLANICI_YONET for the selected company'
    $branchListStatus = (Post '/api/admin-panel/subeler/liste' @{sirketId=$company.id} $staff).StatusCode
    Check ($branchListStatus -eq $(if ($canManageUsers) { 200 } else { 403 })) 'Branch list enforces KULLANICI_YONET for the selected company'
    $approvalStatus = (Post '/api/admin-panel/yetki-belgeleri/onay-listesi' @{sirketId=$company.id} $staff).StatusCode
    Check ($approvalStatus -eq $(if ($canApprove) { 200 } else { 403 })) 'Approval queue enforces YETKI_BELGESI_ONAY for the selected company'
    $approvalHistoryStatus = (Post '/api/admin-panel/yetki-belgeleri/onay-gecmisi' @{sirketId=$company.id} $staff).StatusCode
    Check ($approvalHistoryStatus -eq $(if ($canApprove) { 200 } else { 403 })) 'Approval history enforces YETKI_BELGESI_ONAY for the selected company'
    $reportStatus = (Post '/api/ic-tesisat/devreye-almalar/liste' @{sirketId=$company.id} $staff).StatusCode
    Check ($reportStatus -eq $(if ($canReport) { 200 } else { 403 })) 'Commissioning report enforces RAPOR_GOR for the selected company'
    $alertsStatus = (Post '/api/admin-panel/yetki-belgeleri/uyarilar' @{sirketId=$company.id} $staff).StatusCode
    Check ($alertsStatus -eq $(if ($canReport -or $canApprove) { 200 } else { 403 })) 'Document alerts require report or approval permission'
    $ykcListStatus = (Post '/api/ykc/talepler/liste' @{sirketId=$company.id} $staff).StatusCode
    Check ($ykcListStatus -eq $(if ($canViewYkc) { 200 } else { 403 })) 'YKC request list follows selected-company YKC_TALEP_GOR'
    $ykcReportStatus = (Post '/api/ykc/talepler/rapor' @{sirketId=$company.id} $staff).StatusCode
    Check ($ykcReportStatus -eq $(if ($canReportYkc) { 200 } else { 403 })) 'YKC report follows selected-company YKC_RAPOR_GOR'
    $adminYkcList = (Post '/api/ykc/talepler/liste' @{sirketId=$company.id} $admin).Content | ConvertFrom-Json
    $sampleRequest = @($adminYkcList.talepler | Select-Object -First 1)
    if ($sampleRequest.Count -gt 0) {
        $detailStatus = (Post '/api/ykc/talepler/getir' @{id=$sampleRequest[0].id} $staff).StatusCode
        Check ($detailStatus -eq $(if ($canViewYkc) { 200 } else { 403 })) 'YKC request detail follows its own company grant'
        $teamsStatus = (Post '/api/ykc/talepler/ekipler' @{id=$sampleRequest[0].id} $staff).StatusCode
        Check ($teamsStatus -eq $(if ($canAssignYkc) { 200 } else { 403 })) 'YKC team options follow the request company assignment grant'
        $invalidAppointment = Post '/api/ykc/talepler/atama-yap' @{talepId=$sampleRequest[0].id} $staff
        Check ($invalidAppointment.StatusCode -eq $(if ($canAssignYkc) { 400 } else { 403 })) 'YKC appointment requires its company grant and valid scheduling data'
        $invalidState = Post '/api/ykc/talepler/durum-guncelle' @{talepId=$sampleRequest[0].id; durum=-999} $staff
        Check ($invalidState.StatusCode -eq $(if ($canAssignYkc) { 400 } else { 403 })) 'YKC status change requires its company grant and a valid transition'
        $emptyControl = Post '/api/ykc/talepler/kontroller-kaydet' @{talepId=$sampleRequest[0].id; kontroller=@()} $staff
        Check ($emptyControl.StatusCode -eq $(if ($canSignYkc) { 400 } else { 403 })) 'YKC FR265 control requires its company grant and control values'
        Check ((Post '/api/ykc/talepler/ekipler' @{id=$sampleRequest[0].id} $firm).StatusCode -eq 403) 'Certified firm cannot access internal YKC teams'
    }
}
Check ((Post '/api/admin-panel/yetki-belgeleri/onay-listesi' @{} $service).StatusCode -eq 403) 'Service role cannot read the approval queue'
Check ((Post '/api/ic-tesisat/devreye-almalar/liste' @{} $firm).StatusCode -eq 403) 'Certified firm cannot read internal commissioning reports'
$staffYkc = (Post '/api/panel-kapsam/ykc-yetkileri' @{} $staff).Content | ConvertFrom-Json
Check ((Post '/api/ykc/talepler/liste' @{} $staff).StatusCode -eq $(if ($staffYkc.talepleriGorebilir) { 200 } else { 403 })) 'Staff YKC request list follows its default company grant'
Check ((Post '/api/ykc/talepler/rapor' @{} $staff).StatusCode -eq $(if ($staffYkc.raporlariGorebilir) { 200 } else { 403 })) 'Staff YKC report follows its default company grant'
Check ((Post '/api/panel-kapsam/ykc-yetkileri' @{aktifSirketId=[int]::MaxValue} $staff).StatusCode -eq 403) 'Out-of-scope staff company is denied'
Check ((Post '/api/panel-kapsam/kimlik' @{aktifSirketId=[int]::MaxValue} $firm).StatusCode -eq 403) 'Company identity endpoint rejects out-of-scope company'
Check ((Post '/api/panel-kapsam/ykc-yetkileri' @{aktifSirketId=[int]::MaxValue} $admin).StatusCode -eq 403) 'Even admin cannot select a nonexistent company'
$r = Post '/api/panel-kapsam/ykc-yetkileri' @{} $admin
Check ($r.StatusCode -eq 200 -and ($r.Content | ConvertFrom-Json).atamaYapabilir) 'General admin retains assignment permission'
Check ((Post '/api/ykc/talepler/liste' @{} $admin).StatusCode -eq 200) 'General admin can read YKC requests'
Check ((Post '/api/ykc/talepler/rapor' @{} $admin).StatusCode -eq 200) 'General admin can read YKC reports'
Write-Output "$passed checks passed. No business-data writes were performed."
