# MVC'den API katmanina gecis notlari

## Ne yapiyoruz?

MVC controller'larin veritabanina dogrudan gitmesini kademeli olarak azaltacagiz. Veritabani okuma/yazma islemleri API projesindeki endpointlere tasinacak. MVC tarafinda ise ekran, form ve yonlendirme kalacak.

Ornek eski akis:

```csharp
var markalar = await _context.Ys_Markalar.ToListAsync();
return View(markalar);
```

Hedef akis:

```csharp
var markalar = await _apiClient.MarkaListeAsync();
return View(markalar);
```

## Sirket standardi

Bu projede API endpointleri REST'teki GET/DELETE gibi metodlara ayrilmiyor. Sirket standardina uygun olarak islemler POST ile aciliyor:

- `POST /api/.../liste`
- `POST /api/.../getir`
- `POST /api/.../ekle`
- `POST /api/.../guncelle`
- `POST /api/.../sil`

## Ilk genisletilen API uclari

Dagitim sirketleri:

- `POST /api/dagitim-sirket/liste`
- `POST /api/dagitim-sirket/getir`
- `POST /api/dagitim-sirket/ekle`
- `POST /api/dagitim-sirket/guncelle`
- `POST /api/dagitim-sirket/sil`

Not: Dagitim sirketi kullaniciya secim olarak gosterilmez. Bu yapi sistem icinde sehir -> firma kodu eslesmesinin veritabani karsiligi olarak kullanilir.

Markalar:

- `POST /api/marka/liste`
- `POST /api/marka/getir`
- `POST /api/marka/ekle`
- `POST /api/marka/guncelle`
- `POST /api/marka/sil`

Yetkili servisler:

- `POST /api/yetkili-servisler/liste`
- `POST /api/yetkili-servisler/getir`
- `POST /api/yetkili-servisler/guncelle`
- `POST /api/yetkili-servisler/sil`

Sertifikalar:

- `POST /api/sertifika/firma-liste`
- `POST /api/sertifika/onay-bekleyenler`
- `POST /api/sertifika/onayla`
- `POST /api/sertifika/reddet`

## Neden MVC henuz baglanmadi?

Sistemi bozmamak icin once API tarafini buyutuyoruz. Bu asamada MVC eski haliyle calismaya devam eder. Sonraki adimda tek modul secip MVC'yi API'ye baglayacagiz.

En guvenli ilk baglama modulu:

1. Marka listeleme
2. Dagitim sirketi listeleme
3. Yetkili servis listeleme

Bu ucu okuma agirlikli oldugu icin risk dusuktur. Ekleme, guncelleme, silme ve onay islemlerini daha sonra tasimak daha guvenlidir.

## API dis servisler nerede duracak?

SMS API ve tesisat numarasi sorgulama servisi de API katmaninda durmali. MVC bu dis sistemleri dogrudan cagirmamali. MVC sadece kendi API'mize istek atar, bizim API'miz dis SMS veya tesisat API'sine gider.

Hedef:

```text
MVC -> YetkiliServisGazAcma.API -> SMS API
MVC -> YetkiliServisGazAcma.API -> Tesisat Servis API
```

## Services klasor duzeni

Services klasoru buyudugu icin dosyalar alt klasorlere ayrildi. Namespace ayni kaldigi icin calisma mantigi degismedi; sadece okunabilirlik artti.

```text
Business/Services
  ApiClients  -> MVC'nin API'ye istek atan client siniflari
  Domain      -> veritabani/is mantigi servisleri
  Sms         -> SMS ve OTP yardimci servisleri
  Panel       -> aktif sirket, panel kimligi ve filtre servisleri
  Export      -> PDF/Excel uretim servisleri
  Common      -> ortak sabitler ve yardimci siniflar
```

Basit ayrim:

```text
MarkaApiClient -> MVC'den API'ye gider
MarkaService   -> API tarafinda DB/is mantigini calistirir
```

## Sonraki adim

Bir sonraki guvenli is:

`MarkaController` icindeki listeleme islemini API client uzerinden okutmak.

Boylece MVC'de ilk kez veriyi dogrudan `_context` ile degil, API endpointi ile almis oluruz.

## Ilk baglanan MVC ekrani

Ilk baglanan ekran marka listesidir.

Akis:

```text
MarkaController.Index -> MarkaApiClient -> POST /api/marka/liste -> veritabani
```

Emniyetli gecis icin fallback vardir:

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC -> MarkaService -> DB
```

Bu sayede API projesi ayakta degilken marka ekrani bozulmaz. Development ortaminda API entegrasyonu aciktir; production/default ayarda kapali gelir.

Local API adresi:

```text
http://localhost:5057
```

Development ortaminda API projesinde HTTPS yonlendirmesi kapatildi. Bunun nedeni local MVC -> API cagrisinda sertifika sorununa takilmamak. Production ortaminda HTTPS yonlendirmesi calismaya devam eder.

## Ikinci baglanan MVC ekrani

Ikinci baglanan ekran dagitim sirketi listesidir.

Not: Bu ekran son kullanici akisi degildir. Yetkili servis kaydinda kullanici dagitim sirketi secmez; sadece sehir secer. Sistem firma kodunu `appsettings.json` icindeki `SehirFirmaKodlari` bolumunden bulur.

Akis:

```text
DagitimSirketController.Index -> DagitimSirketApiClient -> POST /api/dagitim-sirket/liste -> veritabani
```

Emniyetli gecis icin fallback vardir:

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC -> DagitimSirketService -> DB
```

Bu ekranda da liste endpointi MVC'nin eski davranisini koruyacak sekilde pasif kayitlari destekler:

```json
{
  "tumunuGetir": true
}
```

## Ucuncu baglanan MVC ekrani

Ucuncu baglanan ekran herkese acik yetkili servis listesidir.

Akis:

```text
YetkiliServislerController.Index -> YetkiliServisApiClient -> POST /api/yetkili-servisler/liste -> veritabani
```

Emniyetli gecis icin fallback vardir:

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC -> eski EF sorgusu -> DB
```

Desteklenen filtreler:

```json
{
  "il": "Corum",
  "ilce": "Merkez",
  "markaId": 1,
  "kategoriId": 2,
  "q": "servis"
}
```

## AdminPanel dashboard ayrimi

AdminPanel controller cok buyuk oldugu icin ilk guvenli ayrim dashboard ozetinden yapildi.

Controller icinden cikarilan sorumluluk:

- toplam devreye alma sayisi
- toplam yetkili servis sayisi
- onay bekleyen yetki belgesi sayisi
- suresi bitecek yetki belgesi sayisi
- bu ay devreye alma sayisi
- son yetki belgeleri
- son devreye almalar

Bu hesaplar artik `AdminDashboardService` icinde durur. MVC controller sadece sonucu alip ViewBag'e koyar.

API endpointi:

```text
POST /api/admin-panel/dashboard
```

Body:

```json
{
  "sirketId": 3
}
```

Not: Bu endpoint admin verisi dondurdugu icin token ister. Genel sistem admini tum sirketleri veya secili sirketi gorebilir. Sirket admini sadece kendi sirketini gorebilir. Personel ise sadece yetkili oldugu sirket kapsaminda veri alabilir.

MVC AdminPanel anasayfasi da API oncelikli hale getirildi.

Akis:

```text
AdminPanelController.Index -> AdminDashboardApiClient -> POST /api/admin-panel/dashboard -> AdminDashboardService -> veritabani
```

Bu endpoint korumali oldugu icin MVC tarafinda mevcut giris yapan kullanici icin API tokeni uretilir. Bunu `ApiJwtTokenService` yapar. API calismazsa development sirasinda ekran bozulmasin diye `AdminDashboardService` fallback olarak kullanilir.

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC -> AdminDashboardService -> DB
```

## AdminPanel kullanici listesi

AdminPanel'deki kullanici listesi de API oncelikli hale getirildi.

Endpoint:

```text
POST /api/admin-panel/kullanicilar/liste
```

Body:

```json
{
  "sirketId": 1,
  "q": "ali",
  "tip": "Personel",
  "durum": "Aktif",
  "bagli": "Corumgaz"
}
```

Akis:

```text
AdminPanelController.Kullanicilar -> AdminKullaniciApiClient -> POST /api/admin-panel/kullanicilar/liste -> veritabani
```

Bu endpoint de token ister. Genel sistem admini tum kapsami gorebilir, sirket admini kendi sirketini gorebilir, personel ise sadece kullanici yonetme veya tam yetkisi olan sirket kapsaminda liste alabilir.

## AdminPanel yetkili servis listesi

AdminPanel'deki yetkili servis listesi de controller icinden ayrildi.

Eklenen ortak servis:

```text
AdminYetkiliServisListeService
```

Endpoint:

```text
POST /api/admin-panel/yetkili-servisler/liste
```

Body:

```json
{
  "sirketId": 1,
  "q": "servis",
  "il": "Corum",
  "durum": 1,
  "devreyeSiralama": "azalan"
}
```

Akis:

```text
AdminPanelController.YetkiliServisler -> AdminYetkiliServisApiClient -> POST /api/admin-panel/yetkili-servisler/liste -> AdminYetkiliServisListeService -> veritabani
```

Emniyetli gecis icin fallback vardir:

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC -> AdminYetkiliServisListeService -> DB
```

Bu adimda controller icindeki arama, il, aktif/pasif durum ve devreye alma sayisina gore siralama mantigi servis/API katmanina alinmis oldu.

## AdminPanel yetkili servis detayi

AdminPanel'deki yetkili servis detay ekrani da API oncelikli hale getirildi.

Endpoint:

```text
POST /api/admin-panel/yetkili-servisler/getir
```

Body:

```json
{
  "id": 11,
  "sirketId": 1
}
```

Akis:

```text
AdminPanelController.YetkiliServisDetay -> AdminYetkiliServisApiClient -> POST /api/admin-panel/yetkili-servisler/getir -> AdminYetkiliServisListeService.GetirAsync -> veritabani
```

Bu endpoint admin detay ekraninin ihtiyaci olan firma bilgisi, hizmet turleri, son sertifikalar, subeler ve son devreye alma kayitlarini tek cevapta dondurur.

Emniyetli gecis icin fallback vardir:

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC -> AdminYetkiliServisListeService.GetirAsync -> DB
```

## Kayit ekrani referans veri ayrimi

Yetkili servis kayit ekranindaki marka ve urun kategori listeleri de API oncelikli hale getirildi.

Akis:

```text
KayitController.YetkiliServis -> MarkaApiClient -> POST /api/marka/liste
KayitController.YetkiliServis -> UrunKategoriApiClient -> POST /api/urun-kategorileri/liste
```

Emniyetli gecis icin fallback vardir:

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC -> eski EF sorgusu -> DB
```

Bu adim yazma islemini henuz tasimaz; sadece basvuru formunun ihtiyac duydugu liste verilerini API'den okur. Bir sonraki buyuk adim, yetkili servis basvuru kaydinin da API endpointine tasinmasidir.

## Yetkili servis basvuru kaydi

Yetkili servis basvuru formunun yazma islemi de API oncelikli hale getirildi.

Akis:

```text
KayitController.YetkiliServis POST -> YetkiliServisApiClient -> POST /api/yetkili-servisler/kayit -> YetkiliServisService -> veritabani
```

Endpoint:

```text
POST /api/yetkili-servisler/kayit
```

Body:

```json
{
  "firmaAdi": "Demo Servis",
  "yetkiliKisi": "Demo Yetkili",
  "telefon": "05550000000",
  "email": "demo@servis.com",
  "adres": "Demo adres",
  "faaliyetIli": "Çorum",
  "vergiNo": "1234567890",
  "vergiDairesi": "Çorum",
  "sifre": "Demo123!",
  "markaIdleri": [1, 2],
  "kategoriIdleri": [1]
}
```

Bu endpoint herkese aciktir cunku yetkili servis basvuru ekrani publictir. API, sehire gore dagitim sirketini bulur veya olusturur, firma kaydini acar, kullanici hesabini olusturur ve marka/kategori baglantilarini yazar.

Kullanici tarafinda gorunen secim:

```text
Sehir -> appsettings.json / SehirFirmaKodlari -> firma kodu -> Dag_Sirketler kaydi
```

Bu nedenle kayit ekraninda "dagitim sirketi" alani bulunmamalidir.

Emniyetli gecis icin fallback vardir:

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC -> YetkiliServisService -> DB
```

## Razor ortak panel yapisi

Panel sayfalarinda tekrar eden sol menu, ust bar, sirket secici ve bildirim alani ortak yapıya alinmaya baslandi.

Eklenen ortak dosyalar:

```text
Views/Shared/_PanelLayout.cshtml
Views/Shared/_PanelSidebar.cshtml
Views/Shared/_PanelTopbar.cshtml
Views/Shared/_NotificationPanel.cshtml
wwwroot/css/panel-layout.css
```

Yeni hedef:

```text
Sayfa View'i -> sadece sayfaya ait filtre/tablo/form icerigi
_PanelLayout -> sayfa kabugu
_PanelSidebar -> sol menu
_PanelTopbar -> baslik, kullanici bilgisi, sirket secici, bildirimler
panel-layout.css -> ortak panel tasarimi
```

Ortak panele tasinan AdminPanel sayfalari:

```text
Views/AdminPanel/DevreyeAlmaDetay.cshtml
Views/AdminPanel/DevreyeAlmalar.cshtml
Views/AdminPanel/Index.cshtml
Views/AdminPanel/KullaniciDuzenle.cshtml
Views/AdminPanel/KullaniciEkle.cshtml
Views/AdminPanel/Kullanicilar.cshtml
Views/AdminPanel/OnayBekleyenler.cshtml
Views/AdminPanel/PersonelEkle.cshtml
Views/AdminPanel/Personeller.cshtml
Views/AdminPanel/Profil.cshtml
Views/AdminPanel/Raporlar.cshtml
Views/AdminPanel/SertifikaUyarilari.cshtml
Views/AdminPanel/SubeDuzenle.cshtml
Views/AdminPanel/Subeler.cshtml
Views/AdminPanel/YetkiDuzenle.cshtml
Views/AdminPanel/Yetkiler.cshtml
Views/AdminPanel/YetkiliServisDetay.cshtml
Views/AdminPanel/YetkiliServisDuzenle.cshtml
Views/AdminPanel/YetkiliServisEkle.cshtml
Views/AdminPanel/YetkiliServisler.cshtml
```

AdminPanel klasorunde eski `Layout = null`, `<html>`, `<body>`, kopya sidebar ve kopya topbar markup'i temizlendi.

PersonelPanel ve YetkiliServisPanel sayfalari da ayni ortak panel layout'una tasindi. `_PanelSidebar.cshtml` artik mevcut path'e gore Admin, Personel veya Yetkili Servis menusunu uretiyor.

Tasinan panel klasorleri:

```text
Views/AdminPanel/*
Views/PersonelPanel/*
Views/YetkiliServisPanel/*
Views/Marka/*
Views/DagitimSirket/*
```

Bu panel ve admin yonetim ekranlarinda artik eski `Layout = null`, `<html>`, `<body>`, kopya sidebar ve kopya topbar markup'i bulunmuyor. Her sayfada yalnizca sayfaya ait filtre/tablo/form icerigi ve sayfaya ozel CSS kaldi.

Admin menusu de bu duzene gore genisletildi:

```text
_PanelSidebar.cshtml -> /Marka sayfalari icin aktif menu destegi
```

Dagitim sirketi menude ve yetki secim ekraninda gosterilmez; bu yapi sehir/firma kodu eslemesi icin arka planda tutulur. Eski personel sirket yonetimi URL'leri dogrudan yazilirsa yine yetki kontrolunden gecer.

Kalan `Layout = null` sayfalari panel disi akislardir:

```text
Views/Home/Index.cshtml
Views/Panel/SirketSec.cshtml
Views/YetkiliServisler/Index.cshtml
Views/DevreyeAlma/*
Views/Sertifika/*
```

Bu sayfalar panel layout'a degil, ayri bir public/auth layout duzenine alinmali.

## Razor public/auth layout yapisi

Giris ve yetkili servis kayit ekranlari icin tekrar eden HTML kabugu ortak dosyalara alindi.

Eklenen ortak dosyalar:

```text
Views/Shared/_AuthLayout.cshtml
Views/Shared/_PublicLayout.cshtml
```

Tasinan sayfalar:

```text
Views/Giris/Index.cshtml
Views/Giris/SmsDogrula.cshtml
Views/Kayit/YetkiliServis.cshtml
```

Bu sayfalarda artik `Layout = null`, `<html>`, `<head>` ve `<body>` tekrar etmiyor. Sayfalar sadece kendi form/icerik bolumunu ve sayfaya ozel CSS'i tutuyor.
