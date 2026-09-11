param([string]$WebUrl='http://127.0.0.1:55222', [string]$ApiUrl='http://127.0.0.1:55221')
$ErrorActionPreference='Stop'
if (-not ([uri]$WebUrl).IsLoopback -or -not ([uri]$ApiUrl).IsLoopback) { throw 'Local verification only.' }
$count=0
function Check($condition, $name) {
    if (-not $condition) { throw "FAIL: $name" }
    $script:count++
    Write-Output "PASS: $name"
}
$login = Invoke-WebRequest "$WebUrl/giris" -SessionVariable session
$csrf = ($login.InputFields | Where-Object name -eq '__RequestVerificationToken').value
$badCsrf = Invoke-WebRequest "$WebUrl/giris" -Method Post -WebSession $session -Body @{kullaniciAdi='test.personel@demo.com';sifre='Demo123!'} -SkipHttpErrorCheck
Check ($badCsrf.StatusCode -eq 400) 'MVC rejects login without antiforgery token'
$smsStep = Invoke-WebRequest "$WebUrl/giris" -Method Post -WebSession $session -Body @{
    kullaniciAdi='test.personel@demo.com'; sifre='Demo123!'; __RequestVerificationToken=$csrf
}
Check ($smsStep.Content -match 'action="/giris/sms-dogrula"') 'MVC API login requires SMS verification'
$smsHtml = [Net.WebUtility]::HtmlDecode($smsStep.Content)
if ($smsHtml -notmatch 'doğrulama kodu\s+(\d{6})') { throw 'Local MVC test SMS code was not rendered.' }
$smsCode = $Matches[1]
$smsCsrf = ($smsStep.InputFields | Where-Object name -eq '__RequestVerificationToken').value
$company = Invoke-WebRequest "$WebUrl/giris/sms-dogrula" -Method Post -WebSession $session -Body @{
    kod=$smsCode; __RequestVerificationToken=$smsCsrf
}
Check ($company.Content -match 'data-company-step') 'MVC API login returns company selection'
$profile = Invoke-WebRequest "$WebUrl/personel-panel/profil" -WebSession $session
Check ([Net.WebUtility]::HtmlDecode($profile.Content) -match 'Demo Çok Şirketli Personel') 'MVC profile obtains current user through API'
$auth = Invoke-RestMethod "$ApiUrl/api/auth/token" -Method Post -ContentType 'application/json' -Body (@{email='test.personel@demo.com';sifre='Demo123!'} | ConvertTo-Json)
if ($auth.dogrulama) {
    if ($auth.mesaj -notmatch 'Test SMS modu:.*?(\d{6})') { throw 'Local API test SMS code was not returned.' }
    $apiSmsCode = $Matches[1]
    $auth = Invoke-RestMethod "$ApiUrl/api/auth/sms-dogrula" -Method Post -ContentType 'application/json' -Body (@{dogrulama=$auth.dogrulama;kod=$apiSmsCode} | ConvertTo-Json)
}
$token = $auth.token
$null = Invoke-RestMethod "$ApiUrl/api/auth/sifre-degistir" -Method Post -Headers @{Authorization="Bearer $token"} -ContentType 'application/json' -Body (@{mevcutSifre='Demo123!';yeniSifre='Demo123!'} | ConvertTo-Json)
$expired = Invoke-WebRequest "$WebUrl/personel-panel/profil" -WebSession $session -MaximumRedirection 5
Check ($expired.StatusCode -eq 200 -and $expired.Content -match 'id="login-form"') 'Revoked API token returns to login without redirect loop'
$again = Invoke-WebRequest "$WebUrl/giris" -WebSession $session -MaximumRedirection 5
Check ($again.StatusCode -eq 200) 'Expired authentication cookie is removed'
$logout = Invoke-WebRequest "$WebUrl/cikis" -WebSession $session -MaximumRedirection 5
Check ($logout.StatusCode -eq 200 -and $logout.Content -match 'id="login-form"') 'Logout remains reachable after session expiry'
Write-Output "$count MVC authentication checks passed. Demo password kept unchanged; its security stamp was rotated."
