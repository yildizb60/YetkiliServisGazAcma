# Public API ve SMS Notlari

## Yetkili servis listeleme

Dis sistemlerde yetkili servis listesini gostermek icin API projesindeki public uclar kullanilir.

Filtre secenekleri:

```http
POST /api/yetkili-servisler/filtre-secenekleri
```

Ornek body:

```json
{
  "il": "Corum"
}
```

Yetkili servis listesi:

```http
POST /api/yetkili-servisler/liste
GET /api/yetkili-servisler?il=Corum&ilce=&markaId=&kategoriId=&q=&page=1&pageSize=20
```

Ornek body:

```json
{
  "il": "Corum",
  "ilce": null,
  "sirketId": null,
  "markaId": null,
  "kategoriId": null,
  "q": null,
  "page": 1,
  "pageSize": 20
}
```

Ornek cevap:

```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 125,
  "totalPages": 7,
  "items": []
}
```

`pageSize` en fazla 100 kayit olacak sekilde sinirlanir. Bu sayede dis site tum yetkili servisleri tek istekte cekmek yerine sayfali olarak tuketir.

Bu iki uc token istemez. Canli ortamda tam adres localhost degil, API'nin yayinlandigi domain olmalidir. Ornek:

```text
https://api.firma.com/api/yetkili-servisler/liste
```

Farkli bir web sitesi tarayicidan bu API'yi cagiracaksa, API projesindeki `Cors:AllowedOrigins` listesine o sitenin domaini eklenmelidir.

MVC/Web projesi API'ye `ApiIntegration:BaseUrl` ayariyla baglanir. Development ortaminda bu adres `localhost` olabilir; canlida mutlaka API sunucusunun domaini kullanilmalidir. Ornek:

```json
{
  "ApiIntegration": {
    "BaseUrl": "https://api.firma.com/"
  }
}
```

## Guvenlik notlari

Public yetkili servis uclari rate limit altindadir. Varsayilan limit:

```json
{
  "RateLimiting": {
    "PublicPermitLimit": 120,
    "PublicQueueLimit": 20
  }
}
```

Swagger development ortaminda aciktir. Canlida varsayilan olarak kapali gelir. Gerekirse API sunucusunda `Swagger:Enabled=true` yapilarak acilabilir.

## SMS akisi

SMS dogrulama su an Web/MVC projesindedir. Bunun sebebi giris akisinin session kullanmasidir. API stateless calistigi icin kullanici girisindeki bekleyen SMS kullanici bilgisi Web tarafinda tutulur.

SMS altyapisi Core katmanindadir ve hem Web hem API ayni servis kaydini kullanir. API tarafinda SMS gonderimi gerektiren yeni bir endpoint acilirsa ayni `AhlatciSmsProvider` ve ayni appsettings yapisi kullanilacaktir.

Kullanilan siniflar:

- `SmsDogrulamaService`: kod uretir, dogrular, DB'ye kaydeder.
- `AhlatciSmsProvider`: Ahlatci SMS API'ye istek atar.
- `NullSmsProvider`: test/kapali modda gercek SMS gondermez.
- `SmsServiceCollectionExtensions`: Web ve API icin ortak SMS DI kaydini yapar.
- `SmsDogrulamaKodlari`: dogrulama kodu hash kaydi.
- `SmsGonderimLoglari`: SMS gonderim log kaydi.

Development/test modu:

```json
{
  "Sms": {
    "Enabled": true,
    "TestMode": true,
    "Provider": "Development"
  }
}
```

Bu modda telefona SMS gitmez. Kod ekranda `Test SMS modu: dogrulama kodu ...` olarak gorunur.

Gercek SMS modu:

```json
{
  "Sms": {
    "Enabled": true,
    "TestMode": false,
    "Provider": "AhlatciSms",
    "Username": "...",
    "Password": "..."
  }
}
```

Gercek kullanici adi/sifre bilgileri repoya yazilmaz. Sunucuda veya gelistirici bilgisayarinda `appsettings.Local.json` ya da environment variable ile verilir.

SMS header bilgisi firma/sehir bilgisine gore secilir. Eslesmeler `Sms:CompanyHeaders` ve `SehirFirmaKoduService` uzerinden yonetilir.
