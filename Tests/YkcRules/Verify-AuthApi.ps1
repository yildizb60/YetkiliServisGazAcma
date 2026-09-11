param(
    [string]$BaseUrl = 'http://127.0.0.1:55221',
    [string]$SmsBaseUrl = 'http://127.0.0.1:55223'
)
$ErrorActionPreference = 'Stop'
if (-not ([uri]$BaseUrl).IsLoopback -or -not ([uri]$SmsBaseUrl).IsLoopback) { throw 'Local verification only.' }
$count = 0
function Check($condition, $name) {
    if (-not $condition) { throw "FAIL: $name" }
    $script:count++
    Write-Output "PASS: $name"
}
function Request($base, $method, $path, $body, $token) {
    $params = @{Uri="$base/api/auth/$path"; Method=$method; SkipHttpErrorCheck=$true}
    if ($null -ne $body) { $params.Body = $body | ConvertTo-Json -Depth 8; $params.ContentType='application/json; charset=utf-8' }
    if ($token) { $params.Headers=@{Authorization="Bearer $token"} }
    Invoke-WebRequest @params
}
function Json($response) { $response.Content | ConvertFrom-Json }
function SmsLogin {
    $r = Request $SmsBaseUrl Post 'token' @{email='test.personel@demo.com'; sifre='Demo123!'} $null
    if ($r.StatusCode -ne 200) { throw 'SMS demo login failed. Start the dedicated Development API with SMS TestMode=true.' }
    $data = Json $r
    if (-not $data.mesaj.StartsWith('Test SMS modu:')) { throw 'Only Development SMS test codes may be used.' }
    @{challenge=$data.dogrulama; code=([regex]::Match($data.mesaj,'[0-9]{4,8}')).Value; data=$data}
}

$mvc = Join-Path $PSScriptRoot '../../YetkiliServisGazAcma'
$direct = Get-ChildItem $mvc -Recurse -File -Include *.cs,*.cshtml |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    Select-String -Pattern 'AppDbContext|UserManager<|SignInManager<|RoleManager<|AddEntityFrameworkStores|AddDbContext|new SqlConnection|Jwt:Key|AddSmsServices'
Check (-not $direct) 'MVC source has no direct Identity/SQL or JWT signing dependency'
Check ((Request $BaseUrl Get 'me' $null $null).StatusCode -eq 401) 'Anonymous profile access denied'
Check ((Request $BaseUrl Put 'profil' @{adSoyad='Denied'; email='invalid@example.com'} $null).StatusCode -eq 401) 'Anonymous profile update denied'
Check ((Request $BaseUrl Post 'token' @{email='nonexistent-auth-audit@example.invalid';sifre='wrong'} $null).StatusCode -eq 401) 'Unknown credentials rejected'
$login = Json (Request $BaseUrl Post 'token' @{email='test.personel@demo.com'; sifre='Demo123!'} $null)
Check ($login.basarili -and $login.token -and $login.kullanici.id) 'API issues token and safe user DTO'
$token = $login.token
$me = Request $BaseUrl Get 'me' $null $token
Check ($me.StatusCode -eq 200) 'Authenticated user can read own profile'
Check ($me.Content -notmatch 'passwordHash|securityStamp|concurrencyStamp|lockoutEnd') 'Identity secrets are not serialized'
$user = (Json $me).kullanici
$bad = Request $BaseUrl Put 'profil' @{adSoyad=''; email='not-an-email'} $token
Check ($bad.StatusCode -eq 400) 'Profile field validation enforced in API'
$validProfile = @{id='someone-else'; adSoyad=$user.adSoyad; email=$user.email; phoneNumber=$user.phoneNumber; kullaniciTipi=4; roller=@('GenelSistemAdmin'); sirketId=999999}
$profile = Json (Request $BaseUrl Put 'profil' $validProfile $token)
Check ($profile.basarili -and $profile.kullanici.id -eq $user.id) 'Profile target comes from token, not supplied user ID'
Check ($profile.kullanici.kullaniciTipi -eq $user.kullaniciTipi -and $profile.kullanici.sirketId -eq $user.sirketId) 'Profile cannot elevate type or company'
Check ((Compare-Object $profile.kullanici.roller $user.roller).Count -eq 0) 'Profile cannot elevate roles'
$token = $profile.token
Check ((Request $BaseUrl Post 'sifre-degistir' @{mevcutSifre='incorrect';yeniSifre='Demo123!'} $token).StatusCode -eq 400) 'Password change requires current password'
Check ((Request $BaseUrl Post 'sifre-degistir' @{mevcutSifre='Demo123!';yeniSifre='x'} $token).StatusCode -eq 400) 'Password policy enforced in API'
# Same demo password avoids leaving altered credentials; the security stamp must still rotate.
$changed = Json (Request $BaseUrl Post 'sifre-degistir' @{mevcutSifre='Demo123!';yeniSifre='Demo123!'} $token)
Check ($changed.basarili -and $changed.token) 'Demo password change returns replacement token'
Check ((Request $BaseUrl Get 'me' $null $token).StatusCode -eq 401) 'Old token revoked after password change'
Check ((Request $BaseUrl Get 'me' $null $changed.token).StatusCode -eq 200) 'Replacement token works'
Check ((Request $BaseUrl Get 'me' $null ($changed.token + 'tampered')).StatusCode -eq 401) 'Tampered token rejected'

$sms = SmsLogin
Check (-not $sms.data.token -and $sms.challenge) 'SMS-required credentials do not issue an access token'
Check ((Request $SmsBaseUrl Post 'sms-dogrula' @{dogrulama='tampered';kod=$sms.code} $null).StatusCode -eq 400) 'Forged challenge rejected'
Check ((Request $SmsBaseUrl Post 'sms-dogrula' @{dogrulama=$sms.challenge;kod='00000000'} $null).StatusCode -eq 400) 'Wrong verification code rejected'
Check ((Request $SmsBaseUrl Post 'sifre-yenile' @{dogrulama=$sms.challenge;kod=$sms.code;yeniSifre='Demo123!'} $null).StatusCode -eq 400) 'Login challenge cannot reset password'
$verified = Json (Request $SmsBaseUrl Post 'sms-dogrula' @{dogrulama=$sms.challenge;kod=$sms.code} $null)
Check ($verified.basarili -and $verified.token) 'Correct SMS code completes login'
Check ((Request $SmsBaseUrl Post 'sms-dogrula' @{dogrulama=$sms.challenge;kod=$sms.code} $null).StatusCode -eq 400) 'Used SMS code cannot be replayed'
$reset = Json (Request $SmsBaseUrl Post 'sifre-unuttum' @{kullaniciAdi='test.personel@demo.com'} $null)
if (-not $reset.mesaj.StartsWith('Test SMS modu:')) { throw 'Reset requires Development test SMS.' }
$resetCode = ([regex]::Match($reset.mesaj,'[0-9]{4,8}')).Value
Check ((Request $SmsBaseUrl Post 'sms-dogrula' @{dogrulama=$reset.dogrulama;kod=$resetCode} $null).StatusCode -eq 400) 'Reset challenge cannot grant login'
Check ((Request $SmsBaseUrl Post 'sifre-yenile' @{dogrulama=$reset.dogrulama;kod=$resetCode;yeniSifre='x'} $null).StatusCode -eq 400) 'Weak reset password rejected before consuming code'
$result = Json (Request $SmsBaseUrl Post 'sifre-yenile' @{dogrulama=$reset.dogrulama;kod=$resetCode;yeniSifre='Demo123!'} $null)
Check ($result.basarili) 'Password reset completes with correct purpose and code'
Check ((Request $SmsBaseUrl Post 'sifre-yenile' @{dogrulama=$reset.dogrulama;kod=$resetCode;yeniSifre='Demo123!'} $null).StatusCode -eq 400) 'Reset challenge cannot be replayed'
Check ((Request $BaseUrl Get 'me' $null $changed.token).StatusCode -eq 401) 'Password reset revokes previous API session'
$limited = SmsLogin
for ($i=0; $i -lt 5; $i++) { $null = Request $SmsBaseUrl Post 'sms-dogrula' @{dogrulama=$limited.challenge;kod='00000000'} $null }
Check ((Request $SmsBaseUrl Post 'sms-dogrula' @{dogrulama=$limited.challenge;kod=$limited.code} $null).StatusCode -eq 400) 'Correct code is rejected after attempt limit'
Write-Output "$count checks passed. Only existing demo account metadata/stamp and test OTP records were written. No real SMS, business records or schema changes."
