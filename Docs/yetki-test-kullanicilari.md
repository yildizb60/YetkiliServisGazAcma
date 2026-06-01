# Yetki Test Kullanıcıları

Bu kullanıcılar yalnızca `Development` ortamında ve `TestData:SeedDemoUsers=true` iken uygulama açılışında otomatik oluşturulur.

Ortak şifre:

```text
Demo123!
```

| Rol | Kullanıcı | Senaryo |
| --- | --- | --- |
| Genel Sistem Admini | `test.geneladmin@demo.com` | Tüm şirketleri ve tüm yönetim ekranlarını test eder. |
| Şirket Admini | `test.sirketadmin@demo.com` | Kargaz şirket yöneticisi olarak sadece kendi şirket kapsamını test eder. |
| Personel | `test.personel@demo.com` | Kargaz'da tam yetki, Corumgaz'da rapor görme, Surmeligaz'da yetki belgesi onay yetkisi test edilir. |
| Yetkili Servis | `test.servis@demo.com` | Kargaz'a bağlı demo yetkili servis hesabıdır. |

SMS Development modunda olduğu için girişte gerçek SMS gitmez; OTP kodu giriş ekranında `Test SMS modu: doğrulama kodu ...` olarak görünür.
