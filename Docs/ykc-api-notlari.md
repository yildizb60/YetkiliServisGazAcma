# YKC API Notlari

YKC, yakici cihaz sureci icin ayrilan teknik moduldur. Kullanici ekranlarinda bu modul "Cihaz Degisim Talebi" olarak gorunur. Mevcut `ys-devreyeal` akisi aynen korunur.

## Veritabani

YKC tabloları `Ykc_` on ekiyle baslar:

- `Ykc_Talepler`
- `Ykc_FormDosyalari`
- `Ykc_Atamalar`
- `Ykc_IslemGecmisi`

Scriptler:

- `DatabaseScripts/2026-07-31_ykc_tablolari.sql`
- `DatabaseScripts/2026-08-03_ykc_surec_alanlari.sql`

## Ana API uclari

Base route:

```text
/api/ykc
```

Talepler:

```text
POST /api/ykc/tesisat-sorgula
POST /api/ykc/talepler/liste
POST /api/ykc/talepler/getir
POST /api/ykc/talepler/olustur
POST /api/ykc/talepler/atama-yap
POST /api/ykc/talepler/durum-guncelle
POST /api/ykc/talepler/dosya-kaydet
POST /api/ykc/talepler/form-yukle
POST /api/ykc/talepler/fr265-word
```

Mobil/CRM187 hazir liste uclari:

```text
POST /api/ykc/dogalgaz-mobile/talepler/liste
POST /api/ykc/crm187/talepler/liste
```

## Web ekranlari

Panel tarafinda eklenen cihaz degisim talebi ekranlari:

```text
GET  /ykc/talepler
GET  /ykc/yeni
POST /ykc/tesisat-sorgula
POST /ykc/yeni
GET  /ykc/detay/{id}
GET  /ykc/fr265/onizle/{id}
GET  /ykc/fr265/indir/{id}
POST /ykc/atama-yap
POST /ykc/durum-guncelle
POST /ykc/form-yukle
```

Bu ekranlar Admin, Personel ve Yetkili Servis panel menulerine baglandi. Web tarafinda veritabani erisimi yoktur; cihaz degisim talebi islemleri `YkcApiClient` ile API'ye gider.

## Tesisat sorgulama

`/ykc/yeni` ekraninda tesisat no ve sozlesme no girilince `YS_CihazBilgileriGetir` metodu kullanilir. Firma kodu kullanicinin bagli oldugu il/sirket bilgisinden `SehirFirmaKoduService` ile uretilir.

Basarili sorguda:

- Tesisat, sozlesme, abone, sayac, musteri ve adres alanlari otomatik dolar.
- Servisten gelen `Cihazliste` kayitlari "Projedeki Cihazlar" secim alaninda listelenir.
- Secilen cihaz eski cihaz bilgilerine aktarilir ve servisten gelen alanlar kilitlenir.
- Baca tipi servisten donmedigi icin manuel girilir.

Servis hata donerse veya cihaz listesi bos gelirse ekran manuel girise izin verir. Boylece online servis gecici olarak cevap vermese bile cihaz degisim talebi akisi tamamen durmaz.

## FR265 form akisi

Talep olustuktan sonra detay ekraninda FR265 aksiyonlari gorunur:

- `FR265 Onizle`: Kayitli talep bilgileriyle dijital form gorunumu acar.
- `Word Indir`: Proje icindeki `FR265_Yakici_Cihaz_Degisim_Formu.docx` sablonunu doldurup indirir.
- `Form Dosyasi Yukle`: Firma indirdigi formu imzalatip PDF/gorsel olarak geri yukler.

Word formunda otomatik dolan alanlar:

- Firma unvani
- Yetki belgesi kayit izi
- Musteri adi, tesisat no ve adres
- Eski/projedeki cihaz tipi, marka, baca tipi, kapasite
- Yeni cihaz tipi, marka, baca tipi, kapasite
- Sertifikali firma yetkilisi adi ve tarih

Tuketim noktasi, baglanti nesnesi ve gercek yetki belgesi numarasi mevcut veritabaninda ayri alan olarak tutulmadigi icin simdilik bos birakilir. Bu alanlar servis/veri modeli netlesince ayni forma otomatik baglanacaktir.

## Durum modeli

Durumlar `YkcDurumDegerleri` ile tutulur:

- `TalepAlindi`: Beklemede.
- `AtamaBekliyor`: Ic tesisat incelemesinde.
- `Atandi`: Randevu verildi.
- `SahaIsleminde`: Saha isleminde.
- `Tamamlandi`: Tamamlandi.
- `Reddedildi`: Zorunlu aciklama ile reddedildi.
- `Iptal`: Zorunlu aciklama ile iptal edildi.

`IptalTarihi`, `IptalEdenKullaniciId`, `IptalAciklama` ve SAP/WM entegrasyonuna hazir `Aufnr` alani simdiden tutulur.

## Atama mantigi

Atama iki sekilde kullanilabilir:

- Sistem bolge/kullanici tipine gore otomatik karar verir.
- Personel ekrandan atamayi manuel degistirir.

`AtananKullaniciTipi` veya `CallCenterTetiklenecekMi` bilgisine gore hedef uygulama belirlenir:

- `CRM187`: 187/acil ekip veya callcenter tetikleme gereken isler.
- `DOGALGAZ_MOBILE_APP`: muhendis/ic tesisat mobil uygulamasina dusecek isler.
- `YONETIM_PANELI`: henuz mobil/CRM187 ayrimi net olmayan veya panelde takip edilecek isler.

## Onemli is kurallari

- Marka yetkisi kontrolu cihaz degisim talebi surecinde uygulanmaz. Bu kural mevcut devreye alma surecinden farklidir.
- Eski projeden gelen cihaz tipi, baca tipi veya kapasite doluysa, yeni cihaz bilgisiyle uyumu kontrol edilir.
- Eski projede ilgili alan bos ise kontrol atlanir. Bu, eski/migrasyon kayitlari icin gereklidir.
- Yeni cihaz icin cihaz tipi, marka, baca tipi ve kapasite zorunludur.
- Ic tesisat onayi/atamasi icin randevu tarihi, randevu saati, bolge ve ekip zorunludur.
- Randevu/atama yapilabilmesi icin talep once `AtamaBekliyor` yani "Ic tesisat incelemesinde" durumuna alinmalidir.
- Tamamlanan, reddedilen veya iptal edilen talepler tekrar atanamaz.
- Form yuklemede sadece PDF, JPG/JPEG ve PNG dosyalari kabul edilir.
- Yetkili servis kullanicisi sadece `FIRMA_FORMU` yukleyebilir; `SAHA_ISLAK_IMZALI_FORM` yalnizca ic operasyon rolleri icindir.
- Baca tipi secenekleri mudurden netlestikten sonra liste/initial servislerine eklenecektir.

## Siradaki guvenli adim

1. Web ve API birlikte yeniden baslatilip `/ykc/yeni` tesisat sorgulama akisi test edilir.
2. Tuketim noktasi, baglanti nesnesi ve gercek yetki belgesi numarasi alanlari netlesince FR265 formuna baglanir.
3. Mobil uygulama ve CRM187 taraflari netlesince ilgili endpointler bu altyapi uzerinden dis sisteme acilir.
4. Baca tipi ve ekip/randevu kaynagi netlesince form alanlari secimli hale getirilir.
