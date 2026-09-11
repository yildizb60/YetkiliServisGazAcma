# Mobil Imza: Toplanti Notu ve API Sozlesmesi

## Mudure Kisa Cevap

"Imza uygulamasi icin uc ayri API ucu hazirladik: bekleyen belgeler listesi,
Base64 PDF ve imza koordinatlarini veren belge paketi, imzali sonucu alan bildirim.
Belge ID, surum ve SHA-256 ozetiyle takip ediliyor. Imzali PDF kaydedilince
bekleyen listeden cikiyor; ayni bildirimin tekrar gelmesi ikinci kayit olusturmuyor.
Canli baglanti henuz acik degil. Onur ile kimlik dogrulama, koordinat profili ve
sonuc dosyasinin aktarim bicimini kesinlestirmemiz gerekiyor."

## Uclar

Tum uclar API projesindedir. MVC ekranlari veritabanina baglanmaz.

| Islem | Uc |
| --- | --- |
| Bekleyen belgeler | `GET /api/entegrasyon/imza/bekleyenler?sonId=0&adet=25` |
| PDF ve koordinatlar | `GET /api/entegrasyon/imza/belgeler/{belgeId}/paket` |
| Imzali sonuc | `POST /api/entegrasyon/imza/belgeler/{belgeId}/imzalandi` |

`belgeId`, imza surecinin sabit ID'sidir; talep ID'si veya dosya ID'si degildir.
Liste en fazla 100 kayit verir. `sonrakiId` ile sonraki sayfa okunabilir;
bir sonraki tarama tekrar `sonId=0` ile baslatilir. Listeyi okumak belgeyi kuyruktan silmez.
Eski gonder/sorgula uclari personelin panel islemleridir; bu uc dis entegrasyon ucunun yerine gecmez.

## Paket JSON'u

Asagidaki degerler SOZLESME ORNEGIDIR; koordinatlar gercek formda onaylanmis degildir.
Canli ayarlara kopyalanmamali. Gercek cevapta Base64, imzaya gonderilirken
saklanan PDF'nin ayni byte'laridir; yeniden tasarlanmis bir PDF uretilmez.

```json
{
  "belge": {
    "belgeId": 101, "talepId": 36, "sirketId": 1,
    "belgeVersiyonu": 1, "belgeHash": "64_KARAKTER_SHA256", "dosyaAdi": "Cihaz_Degisim_Formu_36.pdf"
  },
  "pdfBase64": "PDF_BASE64",
  "sablonSurumu": "WordV2",
  "koordinatBirimi": "pt",
  "koordinatBaslangici": "top-left",
  "imzaAlanlari": [
    { "imzaciSiraNo": 1, "sayfa": 2, "x": 120, "y": 50, "genislik": 90, "yukseklik": 20, "kontrolNo": 1 },
    { "imzaciSiraNo": 2, "sayfa": 2, "x": 220, "y": 50, "genislik": 90, "yukseklik": 20, "kontrolNo": 1 },
    { "imzaciSiraNo": 3, "sayfa": 2, "x": 320, "y": 50, "genislik": 90, "yukseklik": 20, "kontrolNo": 1 }
  ],
  "imzacilar": [
    { "siraNo": 1, "rol": "Sertifikalı Firma Yetkilisi", "adSoyad": "..." },
    { "siraNo": 2, "rol": "Dağıtım Şirketi Yetkilisi", "adSoyad": "..." },
    { "siraNo": 3, "rol": "Abone / Kullanıcı", "adSoyad": "..." }
  ]
}
```

Sayfalar 1'den baslar. `pt`: 1/72 inc; baslangic sol ust kosede, X saga, Y asagi artar.
Mevcut form 595.3 x 841.9 pt. Onur alt-sol baslangic kullaniyorsa
`yAlt = sayfaYuksekligi - y - yukseklik` donusumu uygulanir.
`kontrolNo=0` genel imza alani; 1-5 ilgili kontrolun alanlari. Yalniz son yapilan
kontrolun alanlari gonderilir, gelecekteki kontrollerin alanlari gonderilmez.
Her belge icin kullanilan koordinatlar ve sablon surumu kuyruga alma aninda sabitlenir.

## Sonuc Bildirimi

```json
{
  "belgeVersiyonu": 1,
  "kaynakBelgeHash": "PAKETTEKI_64_KARAKTER_SHA256",
  "dosyaAdi": "Cihaz_Degisim_Formu_36_imzali.pdf",
  "dosyaUrl": "https://ONAYLANACAK_IMZA_SUNUCUSU/belge/101",
  "pdfBase64": "IMZALI_PDF_BASE64",
  "imzalar": [
    { "siraNo": 1, "imzaTarihi": "2026-09-11T12:00:00+03:00" },
    { "siraNo": 2, "imzaTarihi": "2026-09-11T12:01:00+03:00" },
    { "siraNo": 3, "imzaTarihi": "2026-09-11T12:02:00+03:00" }
  ]
}
```

Su anki taslakta imzali PDF Base64 zorunlu, `dosyaUrl` istege baglidir ve sunucu bu
URL'ye istek atmaz. **Onur yalniz dosya adi + URL donecekse bu kisim birlikte
netlestirilmeli:** izinli indirme sunucusu, kimlik dogrulamasi ve guvenli dosya alma
adaptoru gelmeden URL-only bildirim tamamlandi sayilmaz; 400 doner.
URL arsiv konumu olarak kullanilmaz; alinan PDF ozel dosya alaninda tutulur.

200: kaydedildi veya birebir tekrar. 400: gecersiz dosya/imzaci.
401: entegrasyon kimligi yok/gecersiz/kapali. 404: belge bu sirkete ait degil veya yok.
409: kaynak surum/hash uyusmazligi, kapanmis/eski surec veya farkli sonuc PDF'si.
Eksik/hatalı bildirim bekleyen belgeyi silmez. Talebin saha/operasyon kapanisi ayri kalir.

## Guvenlik ve Depolama

- Varsayilan kapali. Mevcut Demo modu degismedi; sayfalar calismaya devam eder.
- Onerilen ilk sozlesme: HTTPS + `X-Imza-Key`; anahtarin yalniz SHA-256 ozeti ayarda tutulur.
- Her entegrasyon anahtarinin izinli sirketleri tanimlanir. Personel/yonetici giris token'i bu uclari acmaz.
- PDF boyutu en fazla 10 MB; Base64, dosya adi, PDF baslangic/bitis isaretleri ve imzaci listesi kontrol edilir.
- Bu kontroller **kriptografik e-imza dogrulamasi degildir**. Sertifika zinciri, zaman damgasi,
  iptal kontrolu ve PAdES dogrulama sorumlulugu Onur ile kararlastirilmali.
- PDF: API `App_Data/ykc-belgeler/{talepId}`. Koordinat manifesti: `App_Data/imza-paketleri`.
- DB'de PDF/Base64 yok; mevcut `Ykc_ImzaSurecleri`, `Ykc_FormDosyalari`, `Ykc_Imzacilar`
  kayitlariyla ID, dosya yolu, durum ve hash tutulur. Yeni tablo/migration eklenmedi.
- Ayni bildirim tekrar kabul edilebilir; farkli PDF ile ayni belgeyi ezme engellenir.
- Canliya acmadan once anahtar dagitimi/yenileme, istek sinirlama, TLS ve imza dogrulama karari tamamlanmali.

## Acilis Kosullari

`YkcImza:Provider=Mobil`, `YkcImza:Mobil:Enabled=true`.
`YkcImza:Mobil:Istemciler`: `Ad`, `ApiKeySha256`, `SirketIdleri`.
`YkcImza:Mobil:SablonSurumu` ve `ImzaAlanlari`: gercek PDF'de Onur ile onaylanacak.
Eksik/yanlis koordinat profiliyle gonderim reddedilir. Bu turda canli ayarlar acilmadi.

## Tekrar Test

```powershell
dotnet build Tests/MobilImza/MobilImza.csproj -m:1 -o _verify/imza-tests -p:UseSharedCompilation=false
dotnet _verify/imza-tests/MobilImza.dll
dotnet _verify/imza-tests/MobilImza.dll --local-database
```

Son komut yalniz yerel SQL Server'da ayri test kaydi olusturur ve finally blogunda
kaldirir; mevcut talep/belgeleri degistirmez. Gercek Onur imzasi test edilmis sayilmaz.

## 11 Eylul Yerel Dogrulama Sonucu

- API ve MVC izole derlemeleri: 0 hata, 0 uyari.
- 31 otomatik kontrol gecti: anahtar/sirket siniri, PDF ve koordinat paketi,
  hatali bildirim reddi, eszamanli iki bildirimde tek sonuc, tekrar bildirimi ve
  imzalanan belgenin listeden cikmasi dahil. Ayrilan yerel test kayitlari temizlendi.
- 4 HTTP kontrolu gecti: uc entegrasyon ucunda yetkisiz istege 401 ve Swagger'da
  kullanici JWT'sinden ayri entegrasyon kimligi.
- Gercek Onur uygulamasi, onayli form koordinatlari ve kriptografik imza
  dogrulamasi bu testlerin kapsamina girmez. Canli entegrasyon kapali kaldi.
