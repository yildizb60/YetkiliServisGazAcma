# API test rehberi

## Local adresler

MVC uygulamasi:

```text
https://localhost:7161
```

API uygulamasi:

```text
https://localhost:7249
http://localhost:5057
```

Swagger ekrani API uygulamasinin kok adresindedir:

```text
https://localhost:7249
```

veya local HTTP profilinde:

```text
http://localhost:5057
```

Swagger JSON:

```text
/swagger/v1/swagger.json
```

## Token alma

Swagger'da once su endpoint calistirilir:

```text
POST /api/auth/token
```

Ornek body:

```json
{
  "email": "test.geneladmin@demo.com",
  "sifre": "Demo123!"
}
```

Demo kullanici yoksa daha once olusturulan sistem admini ile denenebilir:

```json
{
  "email": "admin@corumgaz.com",
  "sifre": "admin sifresi"
}
```

Basarili cevapta `token` alani gelir.

## Swagger'da token kullanma

Swagger ustundeki `Authorize` butonuna basilir.

Deger kismina su formatta girilir:

```text
Bearer TOKEN_BURAYA
```

Sonra token isteyen endpointler denenebilir.

Ornek:

```text
POST /api/yetkili-servisler/getir
```

Body:

```json
{
  "id": 2
}
```

## Token gerektirmeyen basit testler

Bu endpointler ilk kontrol icin uygundur:

```text
POST /api/marka/liste
POST /api/dagitim-sirket/liste
POST /api/urun-kategorileri/liste
POST /api/yetkili-servisler/liste
```

`POST /api/marka/liste` icin tum markalari, pasifler dahil almak istersen:

```json
{
  "tumunuGetir": true
}
```

## 401 alirsan

Kontrol sirasi:

1. API projesi yeniden baslatildi mi?
2. Token yeni mi? Eski token suresi dolmus olabilir.
3. Swagger Authorize alanina `Bearer ` kelimesiyle birlikte mi girildi?
4. Token aldigin API adresi ile endpoint denedigin API adresi ayni mi?
5. Kullanici rolunde endpoint icin gereken yetki var mi?

API tarafinda `/api/...` istekleri artik `/Account/Login` sayfasina yonlendirilmemeli. Token yoksa `401`, yetki yoksa `403` donmelidir.

## Canliya cikarken

Canlida MVC ve API ayri adreslerde olabilir:

```text
https://panel.firma.com
https://api.firma.com
```

MVC tarafinda API adresi buradan degistirilir:

```json
{
  "ApiIntegration": {
    "Enabled": true,
    "BaseUrl": "https://api.firma.com",
    "AllowDatabaseFallback": false,
    "TimeoutSeconds": 5
  }
}
```

Canli icin dikkat edilecekler:

- `Jwt:Key` guclu ve gizli olmali.
- `Jwt:Issuer` ve `Jwt:Audience` MVC/API tarafinda uyumlu olmali.
- API HTTPS ile calismali.
- Dis mobil uygulama veya baska panel kullanacaksa CORS ayari eklenmeli.
- SMS API ve tesisat sorgu API'si MVC'ye degil, API katmanina baglanmali.
- `ApiIntegration:AllowDatabaseFallback=false` kalmali. Boylece API'ye tasinan akislarda Web sunucusu sessizce veritabanina donmez.
