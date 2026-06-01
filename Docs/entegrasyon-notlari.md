# Entegrasyon Notlari

## Online cihaz bilgileri servisi

- Servis tipi: SOAP
- WSDL: `http://onlinesvc.marmaragaz.com.tr/Test/Online.svc?wsdl`
- Kullanilan metot: `YS_CihazBilgileriGetir`
- SOAP action: `http://tempuri.org/IOnline/YS_CihazBilgileriGetir`
- Gonderilen alanlar:
  - `Firma`
  - `tesisatNo`
  - `sozlesmeNo`
- Donen temel alanlar:
  - `Cihazliste`
  - `HataKodu`
  - `HataMesaji`
  - `adres`
  - `cariad`
  - `carikod`
  - `sayacno`
  - `sozlesmeno`
  - `tesisatno`

Firma kodu yetkili servisin bagli oldugu il/sirket bilgisinden uretilir. Yalova ve Corlu icin online servis tarafinda `MARMARAGAZ_YALOVA` ve `MARMARAGAZ_CORLU` kodlari kullanilir.

## SMS OTP servisi

- Servis tipi: REST/JSON
- Token endpoint: `https://smsnviapi.ahlatci.com.tr/api/Token/oauth/get_token`
- SMS endpoint: `https://smsnviapi.ahlatci.com.tr/api/AhlSmsApi/Send_SMS_BULK`
- Kullanilan akis: OTP dogrulama kodu uretilir, hash olarak veritabanina kaydedilir, sonra Ahlatci SMS API'ye gonderilir.
- Gonderilen JSON govdesi Postman collection formatindadir:

```json
{
  "Numbers": "5300000000",
  "Message": "Dogrulama kodu metni",
  "Header": "CORUMGAZ",
  "CountryCode": "90",
  "Info": {
    "Company": "SCADA",
    "UserName": "ACEX",
    "AccessIP": "127.0.0.1"
  }
}
```

SMS header secimi firma bilgisine gore yapilir:

| Firma kodu | SMS header |
| --- | --- |
| `CORUMGAZ` | `CORUMGAZ` |
| `KARGAZ` | `KARGAZ` |
| `SURMELIGAZ` | `SURMELIGAZ` |
| `MARMARAGAZ_YALOVA` | `YALOVAGAZ` |
| `MARMARAGAZ_CORLU` | `CORLUGAZ` |

Gercek SMS gonderimi icin `Sms:Enabled=true`, `Sms:Provider=AhlatciSms` ve token bilgisi ya da token almak icin kullanici adi/parola ayarlari dolu olmalidir. Development ortaminda `Sms:Provider=Development` birakilirsa gercek SMS gitmez, kod ekranda test mesaji olarak doner.

Secret bilgiler repoya yazilmaz. Lokal calisma icin `appsettings.Local.json`, sunucu icin environment variable, user-secrets veya deployment secret store kullanilir.
