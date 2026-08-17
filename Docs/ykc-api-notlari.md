# YKC API Notlari

YKC, yakici cihaz sureci icin ayrilan teknik moduldur. Kullanici ekranlarinda bu modul "Cihaz Degisim Talebi" olarak gorunur. Mevcut `ys-devreyeal` akisi aynen korunur.

## Veritabani

YKC tabloları `Ykc_` on ekiyle baslar:

- `Ykc_Talepler`
- `Ykc_FormDosyalari`
- `Ykc_Atamalar`
- `Ykc_IslemGecmisi`
- `Ykc_Fr265Kontroller`
- `Ykc_ImzaSurecleri`
- `Ykc_Imzacilar`

Scriptler:

- `DatabaseScripts/2026-07-31_ykc_tablolari.sql`
- `DatabaseScripts/2026-08-03_ykc_surec_alanlari.sql`
- `DatabaseScripts/2026-08-14_ykc_dijital_imza_modeli.sql`
- `DatabaseScripts/2026-08-17_ykc_imza_akis_duzeltmesi.sql`

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
POST /api/ykc/talepler/kontroller-kaydet
POST /api/ykc/talepler/dosya-kaydet
POST /api/ykc/talepler/form-yukle
POST /api/ykc/imza/entegrasyon
POST /api/ykc/talepler/imzaya-gonder
POST /api/ykc/talepler/imza-durum-sorgula
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
POST /ykc/atama-yap
POST /ykc/durum-guncelle
POST /ykc/kontroller-kaydet
POST /ykc/imzaya-gonder
POST /ykc/imza-durum-sorgula
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

Servis basarili cevap verir ancak kayit bulamazsa ekran kontrollu manuel girise izin verir. Servise ulasilamazsa veya servis hata donerse manuel giris acilmaz; kullaniciya teknik hata gosterilir. Boylece servis arizasi "kayit bulunamadi" gibi kaydedilmez.

## FR265 form akisi

Talep olustuktan sonra detay ekraninda FR265 akisi su sekilde ilerler:

1. `FR265 Onizle`, kayitli talep ve kontrol bilgileriyle dijital form gorunumunu acar.
2. Gercek imza provider'i yapilandirilmissa `Imza Uygulamasina Gonder`, guncel Word belgesini bir kez uretir ve private storage'da kilitli snapshot olarak saklar.
3. Snapshot'in gercek SHA-256 degeri talep, imza sureci ve dosya kaydina yazilir; provider'a ayni baytlar gonderilir.
4. `Imza Durumunu Guncelle`, provider'daki imzaci ve belge durumunu sorgular.
5. Provider tamamlanmis belgeyi dondurdugunde belge `FR265_IMZALI_NIHAI` olarak private storage'a alinir ve indirilebilir hale gelir.

Provider yapilandirilmamissa gonderim aksiyonu pasiftir ve sistem hicbir kaydi imzalanmis gibi gostermez. Imzasiz FR265 kullaniciya indirilmez; imzaya giden belge, gonderim aninda private storage'da kilitlenen snapshot'tir. Indirilebilen FR265 yalnizca saglayicidan donen imzali nihai belgedir.

Word formunda otomatik dolan alanlar:

- Firma unvani
- Yetki belgesi kayit izi
- Musteri adi, tesisat no ve adres
- Eski/projedeki cihaz tipi, marka, baca tipi, kapasite
- Yeni cihaz tipi, marka, baca tipi, kapasite
- Ikinci el cihaz bilgisi
- Bes dijital kontrolun sonucu ve aciklamasi
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

Atama manueldir. Ic tesisat yoneticisi veya bu yetkiye sahip personel, tesisattan gelen bolgeye uygun 187/acil ya da muhendis ekibini kendisi secer. Sistem otomatik ekip atamaz.

`AtananKullaniciTipi` veya `CallCenterTetiklenecekMi` bilgisine gore hedef uygulama belirlenir:

- `CRM187`: 187/acil ekip veya callcenter tetikleme gereken isler.
- `DOGALGAZ_MOBILE_APP`: muhendis/ic tesisat mobil uygulamasina dusecek isler.
- `YONETIM_PANELI`: henuz mobil/CRM187 ayrimi net olmayan veya panelde takip edilecek isler.

Bu hedefler kullaniciya tercih alani olarak gosterilmez; secilen ekip/kullanici tipinden arka planda turetilir.

## Onemli is kurallari

- Marka yetkisi kontrolu cihaz degisim talebi surecinde uygulanmaz. Bu kural mevcut devreye alma surecinden farklidir.
- Eski projeden gelen cihaz tipi, baca tipi veya kapasite doluysa, yeni cihaz bilgisiyle uyumu kontrol edilir.
- Eski projede ilgili alan bos ise kontrol atlanir. Bu, eski/migrasyon kayitlari icin gereklidir.
- Yeni cihaz icin cihaz tipi, marka, baca tipi ve kapasite zorunludur.
- Ic tesisat onayi/atamasi icin randevu tarihi, randevu saati, bolge ve ekip zorunludur.
- Randevu/atama yapilabilmesi icin talep once `AtamaBekliyor` yani "Ic tesisat incelemesinde" durumuna alinmalidir.
- Tamamlanan, reddedilen veya iptal edilen talepler tekrar atanamaz.
- Kullanici yuklemesinde sadece `TEKNIK_EK` ve ic operasyon rolleri icin `FR265_IMZALI_ADAY` kabul edilir.
- `FR265_IMZALI_ADAY` yalnizca PDF olabilir ve imza surecini tamamlamaz.
- `FR265_TASLAK` gercek Word baytlarinin private storage snapshot'idir. Hash bu dosyanin SHA-256 degeridir.
- `FR265_IMZAYA_GONDERILEN`, saglayicinin kabul ettigi kilitli snapshot'tir.
- `FR265_IMZALI_NIHAI` yalnizca dijital imza saglayicisindan donen nihai belge icin uretilir.
- Talep ancak provider belge kimligi, tamamlanmis imza sureci ve bu surece bagli nihai dosya birlikte varsa tamamlanabilir.
- FR265'in sayfa sayisi kodda sabitlenmez; guncel Word sablonu kac sayfaysa uretilen belge onu korur.
- Baca tipi secenekleri mudurden netlestikten sonra liste/initial servislerine eklenecektir.

## Siradaki guvenli adim

1. Web ve API birlikte yeniden baslatilip `/ykc/yeni` tesisat sorgulama akisi test edilir.
2. Tuketim noktasi, baglanti nesnesi ve gercek yetki belgesi numarasi alanlari netlesince FR265 formuna baglanir.
3. Dijital imza saglayicisi netlesince `IYkcImzaProvider` icin gercek adapter yazilir; mevcut gonderim ve durum sorgulama endpointleri degismez.
4. Mobil uygulama ve CRM187 taraflari netlesince ilgili endpointler bu altyapi uzerinden dis sisteme acilir.
5. Baca tipi ve ekip/randevu kaynagi netlesince form alanlari secimli hale getirilir.
