# MVC'den API katmanina gecis notlari

## Ne yapiyoruz?

MVC controller'larin veritabanina dogrudan gitmesini kademeli olarak azaltacagiz. Veritabani okuma/yazma islemleri API projesindeki endpointlere tasinacak. MVC tarafinda ise ekran, form ve yonlendirme kalacak.

Guncel mimari:

```text
YetkiliServisGazAcma.API   -> API controllerlari, Swagger, JWT
YetkiliServisGazAcma       -> MVC/Web ekranlari
YetkiliServisGazAcma.Core  -> ortak entity, AppDbContext ve ortak is servisleri
```

API artik MVC/Web projesini referans almaz. API ve Web ayri publish edilebilir.

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

Urun kategorileri:

- `POST /api/urun-kategorileri/liste`

Varsayilan davranis sadece sistemde kullanilan hizmet turlerini dondurur:

```text
Kombi
Sofbenler
Merkezi Kazan
```

Tum aktif kategoriler gerekiyorsa body icinde `tumunuGetir: true` gonderilir:

```json
{
  "tumunuGetir": true
}
```

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

Fallback sadece `ApiIntegration:AllowDatabaseFallback=true` ise vardir:

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC hata verir, DB fallback yapmaz
```

Canli ayrik mimaride `AllowDatabaseFallback=false` kalmalidir. Bu durumda API kapaliysa MVC sessizce veritabanina donmez; hata gorunur olur.

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

Fallback sadece `ApiIntegration:AllowDatabaseFallback=true` ise vardir:

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC hata verir, DB fallback yapmaz
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

Fallback sadece `ApiIntegration:AllowDatabaseFallback=true` ise vardir:

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC hata verir, DB fallback yapmaz
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
API kapaliysa: MVC hata verir, DB fallback yapmaz
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

Fallback sadece `ApiIntegration:AllowDatabaseFallback=true` ise vardir:

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC hata verir, DB fallback yapmaz
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

Fallback sadece `ApiIntegration:AllowDatabaseFallback=true` ise vardir:

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC hata verir, DB fallback yapmaz
```

## Kayit ekrani referans veri ayrimi

Yetkili servis kayit ekranindaki marka ve urun kategori listeleri de API oncelikli hale getirildi.

Akis:

```text
KayitController.YetkiliServis -> MarkaApiClient -> POST /api/marka/liste
KayitController.YetkiliServis -> UrunKategoriApiClient -> POST /api/urun-kategorileri/liste
```

Fallback sadece `ApiIntegration:AllowDatabaseFallback=true` ise vardir:

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC hata verir, DB fallback yapmaz
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

Fallback sadece `ApiIntegration:AllowDatabaseFallback=true` ise vardir:

```text
API calisiyorsa: MVC -> API -> DB
API kapaliysa: MVC hata verir, DB fallback yapmaz
```

## Faz 2 - PersonelPanel API gecisi

Ilk Faz 2 parcasi PersonelPanel uzerinden baslatildi. Bu adimda ekran tasarimi degistirilmeden veri akisi API'ye alindi.

API'ye baglanan PersonelPanel akislari:

```text
PersonelPanel.DevreyeAlmalar       -> AdminRaporApiClient -> /api/admin-panel/devreye-almalar/liste
PersonelPanel.DevreyeAlmaDetay     -> AdminRaporApiClient -> /api/admin-panel/devreye-almalar/getir
PersonelPanel.DevreyeAlmaPdf/Excel -> AdminRaporApiClient -> /api/admin-panel/devreye-almalar/getir
PersonelPanel.OnayBekleyenler      -> AdminYetkiBelgesiOnayApiClient -> /api/admin-panel/yetki-belgeleri/onay-listesi
PersonelPanel.Onayla/Reddet        -> YetkiBelgesiApiClient -> yetki belgesi onay/red API akisi
PersonelPanel.Raporlar             -> AdminRaporApiClient -> /api/admin-panel/raporlar/ozet
```

Rapor ozeti API cevabi PersonelPanel icin genisletildi:

```text
DevreyeTamamlanan
DevreyeBekleyen
DevreyeIptal
ChartMarkaLabels
ChartMarkaData
```

Bu alanlar mevcut AdminPanel rapor ekranini bozmaz; sadece PersonelPanel rapor kartlari ve marka grafigi icin kullanilir.

Not: Veritabani/entity tarafinda eski teknik tablo ve servis adlari korunur. Yeni MVC client ve ekrana anlatilan akis "Yetki Belgesi" adini kullanir. Eski teknik adlarin toplu temizligi ayri bir isimlendirme/refactor adimi olarak ele alinmalidir.

## Faz 2 - AdminPanel yetkili servis gecisi

AdminPanel yetkili servis ekraninda yazma akislarinin bir bolumu API'ye tasindi.

API'ye baglanan AdminPanel akislari:

```text
AdminPanel.YetkiliServisDetay   -> AdminYetkiliServisApiClient -> /api/admin-panel/yetkili-servisler/getir
AdminPanel.YetkiliServisEkle    -> AdminYetkiliServisApiClient -> /api/admin-panel/yetkili-servisler/ekle
AdminPanel.YetkiliServisDuzenle -> AdminYetkiliServisApiClient -> /api/admin-panel/yetkili-servisler/guncelle
AdminPanel.YetkiliServisSil     -> AdminYetkiliServisApiClient -> /api/admin-panel/yetkili-servisler/sil
```

Mevcut `/api/yetkili-servisler/kayit` public basvuru akisi olarak kalir ve sifre ister. AdminPanel ekleme ekrani bu endpoint'i kullanmaz; admin token'i ile calisan `/api/admin-panel/yetkili-servisler/ekle` endpoint'i firma/marka/kategori tanimi yapar ve kullanici sifresi istemez.

## Faz 2 - AdminPanel kullanici durum/sil gecisi

Kullanici ve personel yonetiminde dusuk riskli yazma islemleri API'ye tasindi.

API'ye baglanan AdminPanel akislari:

```text
AdminPanel.PersonelDurum   -> AdminKullaniciApiClient -> /api/admin-panel/kullanicilar/durum
AdminPanel.PersonelSil     -> AdminKullaniciApiClient -> /api/admin-panel/kullanicilar/sil
AdminPanel.KullaniciDurum  -> AdminKullaniciApiClient -> /api/admin-panel/kullanicilar/durum
AdminPanel.KullaniciSil    -> AdminKullaniciApiClient -> /api/admin-panel/kullanicilar/sil
```

Bu endpointler aktif sirket kapsamini ve kullanici yonetim yetkisini API tarafinda da kontrol eder. Personel ekranindan gelen isteklerde `SadecePersonel=true` gonderilir; boylece personel sayfasindan farkli tipte kullanici silinmesi veya pasiflestirilmesi engellenir.

## Faz 2 - Marka ve dagitim sirketi gecisi

Kucuk yonetim controllerlari icin yazma ve tekil getirme akislarinin API baglantisi tamamlandi.

API'ye baglanan Web akislari:

```text
MarkaController.Index      -> MarkaApiClient -> /api/marka/liste
MarkaController.Duzenle    -> MarkaApiClient -> /api/marka/getir
MarkaController.Ekle       -> MarkaApiClient -> /api/marka/ekle
MarkaController.Guncelle   -> MarkaApiClient -> /api/marka/guncelle
MarkaController.Sil        -> MarkaApiClient -> /api/marka/sil

DagitimSirket.Index        -> DagitimSirketApiClient -> /api/dagitim-sirket/liste
DagitimSirket.Duzenle      -> DagitimSirketApiClient -> /api/dagitim-sirket/getir
DagitimSirket.Ekle         -> DagitimSirketApiClient -> /api/dagitim-sirket/ekle
DagitimSirket.Guncelle     -> DagitimSirketApiClient -> /api/dagitim-sirket/guncelle
DagitimSirket.Sil          -> DagitimSirketApiClient -> /api/dagitim-sirket/sil
```

Marka API yazma endpointleri personel icin `MARKA_YONET` veya `TAM_YETKI` kontrolu yapar. Dagitim sirketi guncelleme endpointi, genel sistem yoneticisine ek olarak kendi sirketi kapsamindaki sirket admini veya `DAGITIM_SIRKET_YONET` / `TAM_YETKI` olan personele izin verir. Dagitim sirketi ekle/sil ise genel sistem yonetimi olarak kalir.

Bu akislarda MVC tarafinda veritabani fallback'i yoktur. API yaniti alinamazsa ekran hata mesaji verir; MVC ayni islemi dogrudan veritabanindan tekrar denemez.

Dagitim sirketi Web controller'indaki kalan bildirim sayisi okumasi da dashboard API client'a tasindi. Bu controller artik `AppDbContext` tasimaz.

Marka Web controller'indaki kalan bildirim sayisi okumasi da dashboard API client'a tasindi. Bu controller artik `AppDbContext` tasimaz.

## Faz 2 - Personel yonetimi ilk gecis

Admin personel listeleme ve personel olusturma akislari API hattina alindi.

API'ye baglanan Web akislari:

```text
AdminPanel/personeller       -> AdminKullaniciApiClient -> /api/admin-panel/kullanicilar/liste (Tip=Personel)
AdminPanel/personeller/ekle  -> AdminKullaniciApiClient -> /api/admin-panel/personeller/ekle
Personel formu sirketleri    -> AdminKullaniciApiClient -> /api/admin-panel/kullanicilar/sirket-secenekleri
```

Bu akislarda Web tarafinda personel kaydi icin `_userManager.CreateAsync` veya dogrudan DB yazimi kullanilmaz. API ulasilamazsa form hata mesaji verir ve islem veritabanina MVC tarafindan yazilmaz.

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
