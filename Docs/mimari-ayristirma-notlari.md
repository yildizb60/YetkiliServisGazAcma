# API - Web ayrisma notlari

## Yapilan ayrisma

Projede artik uc katmanli publish yapisi vardir:

```text
YetkiliServisGazAcma.API   -> API controllerlari, Swagger, JWT
YetkiliServisGazAcma       -> MVC/Web ekranlari, Razor viewlar, wwwroot
YetkiliServisGazAcma.Core  -> ortak entity, AppDbContext ve ortak is servisleri
```

API projesi artik MVC/Web projesini referans almaz.

```text
API -> Core
Web -> Core
API -X-> Web
Web -X-> API proje referansi
```

Web, API projesini proje referansi olarak degil HTTP uzerinden kullanir.

```text
Web -> ApiIntegration:BaseUrl -> API
```

## Mudur talep kontrolu

| Talep | Durum |
| --- | --- |
| API ve Web bagimsiz publish edilebilir | Tamamlandi |
| API projesi MVC/Web projesini referans almamali | Tamamlandi |
| Swagger MVC uzerinden ayaga kalkmamali | Tamamlandi |
| Swagger sadece API'de olmali | Tamamlandi |
| API sunucusunda MVC ekran kodu calismamali | Tamamlandi |
| Web sunucusunda API kodu calismamali | Tamamlandi |
| Ortak kod tek yerde tutulmali | Tamamlandi, Core projesine alindi |
| Web veri islemlerini tamamen API uzerinden yapmali | Kademeli Faz 2 |

## Publish kontrolu

API Release publish sonucu API paketinde Web DLL'i yoktur:

```text
YetkiliServisGazAcma.API.dll
YetkiliServisGazAcma.Core.dll
```

Web Release publish sonucu Web paketinde API DLL'i yoktur:

```text
YetkiliServisGazAcma.dll
YetkiliServisGazAcma.Core.dll
```

Bu nedenle API ve Web ayri sunuculara publish edilebilir.

## Fallback kurali

`ApiIntegration:AllowDatabaseFallback` ayari eklendi.

Canli ayrik mimaride bu ayar `false` kalmalidir:

```json
{
  "ApiIntegration": {
    "Enabled": true,
    "BaseUrl": "https://api.firma.com",
    "AllowDatabaseFallback": false
  }
}
```

Bu sekilde API'ye tasinmis ekranlarda API cevap vermezse Web sessizce veritabanina donmez. Hata gorunur hale gelir ve ayrik mimari korunur.

## Faz 2 hedefi

Faz 2, MVC controllerlarindaki dogrudan veritabani islemlerini ekran ekran API endpointlerine tasimaktir.

Oncelikli siralama:

1. AdminPanel yazma islemleri
2. PersonelPanel onay ve rapor islemleri
3. Yetkili servis panel sube/marka islemleri
4. Devreye alma kaydi ve raporlari
5. Kimlik/giris akisinin API tabanli hale getirilmesi

Bu is ekran ekran yapilmalidir. Tek hamlede tum controllerlari tasimak demo ve canliya alma acisindan risklidir.
