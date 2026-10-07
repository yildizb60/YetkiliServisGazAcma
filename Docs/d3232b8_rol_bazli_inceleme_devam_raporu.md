# Rol Bazlı İnceleme: Devam Raporu

**Teslim tarihi:** 07.10.2026. **İncelenen sürüm:** `d3232b812f205402b9d3a8c9151b1fa66388b72a`.

Bu belge, [ilk inceleme raporunun](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_rol_bazli_proje_incelemesi.md) ekidir. İlk rapor değiştirilmedi. Yarım kalan gezinin devamında gerçekleştirilen işlemler, kullanıcı açısından sonuçları ve yeni bulgular burada teslim ediliyor. İlk rapordaki teknik testler bu turda yeniden çalıştırılmış gibi gösterilmemiştir.

## 1. Yönetici Özeti

**Sonuç:** Yeni bir tasarım veya modül eklemekten önce mevcut rapordan listeye geçişi ve yetkiye göre işlem bağlantılarını düzeltmek gerekiyor. Günlük işte kullanıcıyı yanlış yere götüren iki yeni P2 bulgu doğrulandı:

| Bulgu | Somut sonuç | Yönetici açısından etkisi |
|---|---|---|
| D01: Rapor bağlantıları seçili kapsamı korumuyor. | Genel adminde Kargaz raporu 1 kayıt gösterirken açılan YKC listesi 45 kayıt gösterdi. Şirket admininde dönem raporu 6 devreye alma gösterirken bağlantı 33 kaydı açtı. | Gösterge ile incelenen iş listesi eşleşmiyor; kullanıcı filtreyi yeniden kurmak zorunda kalıyor. |
| D02: Yalnız rapor yetkili personele erişemeyeceği detay bağlantısı gösteriliyor. | Rapor özeti açılıyor, dört sekme çalışıyor; ancak "Talep Detayını Aç" bağlantısı "Yetkiniz yok" sayfasına götürüyor. | Kullanıcı kendisine sunulan düğmeye bastığı için hata yaptığını veya yetkisinin bozulduğunu düşünüyor. |

Bu iki bulgu **yetki sızıntısı olarak sınıflandırılmadı**. D01'de genel yöneticinin zaten erişebildiği kayıtların filtresi kayboluyor. D02'de sunucu erişimi doğru engelliyor; sorun arayüzde erişilemeyen işlem sunulması.

Olumlu sonuçlar da gerçek işlemle doğrulandı: gerekçesiz uygunsuz kontrol kaydedilmiyor; gerekçeli sonuçtan sonra ikinci kontrol randevusu adımı gösteriliyor; aynı gerekçe firma ekranında görünüyor. Belge reddi personel ve servis ekranlarında tutarlı. SürmeliGAZ'da YKC yetkisi bulunmayan personelin takvimi sade ve YKC verisiz açılıyor.

**Tamamlanma sınırı:** Bu teslim, eksik bırakılan devam raporudur; bütün sayfalardaki bütün düğmelerin, bütün rollerle ve gerçek dış servislerle uçtan uca kabulünün tamamlandığı anlamına gelmez. Kalan senaryolar 9. bölümde tek tek belirtilmiştir.

### Güvenli Test Ortamı

- Önceki incelemenin aynı committen oluşturulmuş izole kopyası ve sentetik LocalDB verileri kullanıldı. Gerçek `https://localhost:7161` uygulamasında kayıt değiştirilmedi.
- Test MVC/API/yerel kaynak servisi sırasıyla `127.0.0.1:55410`, `55411`, `55412` üzerinde çalıştı. Giriş, uygulamanın normal giriş ve test SMS doğrulama adımlarıyla yapıldı.
- Gerçek SMS/e-posta gönderilmedi, gerçek belge imzalanmadı, gerçek saha işlemi yapılmadı. Kullanıcı yetkileri genişletilmedi.
- Uygulama kodu, bağımlılıkları ve şeması bu devam incelemesinde değiştirilmedi. Önceden bulunan `TestDataSeed.cs` değişikliği ve ayrı Sonar işi kapsamında yapılmış Regex zaman aşımı düzenlemesi korunuyor; bunlar incelenen snapshot'a dahil değil.
- Aşağıdaki kayıt numaraları, sayılar, kişi ve firma bilgileri test ortamına aittir; gerçek işletme performansı olarak yorumlanmamalıdır.

## 2. Committeki Yeniliklerin Ek Doğrulaması

| Mevcut özellik | Devam turundaki kanıt | Sonuç / sınır |
|---|---|---|
| Yetkiye göre takvim | SürmeliGAZ'da yalnız belge yetkili personel; ayrıca Çorumgaz'da yalnız rapor yetkili personel ile giriş yapıldı. | Sade takvim gösteriliyor; operasyonel YKC randevu bilgisi sunulmuyor. Yeniden tasarım gerekmiyor. |
| Son uygunsuzluk gerekçesi | Personel test talebi 4'e yeni uygunsuz sonuç kaydetti; ardından firma hesabıyla aynı kayıt açıldı. | Son gerekçe üstte, yeniden randevu beklenirken de görünüyor. Kontrol sonucu ve geçmiş korunuyor. |
| Şube yan paneli | Şirket adminiyle test şubesi 4 düzenleme paneli açıldı. | Şube ve bağlı firma doğru doluyor; başka şirket seçimi sunulmuyor. Bu turda kaydetme yapılmadı. |
| Kompakt personel yetki listesi | Şirket admininde 51 personel listesi arandı, rapor personelinin düzenleme paneli açıldı. | Şirket kapsamı sabit; izinler mevcut menülerle ilişkili gösteriliyor. Yetki değiştirilmedi. |
| Rapor özeti | Genel admin ve yalnız rapor personelinde dört detay sekmesi ayrı açıldı. | Tesisat/abone, firma, cihaz karşılaştırması, imza durumu okunabiliyor. Kapasite ve baca alanları korunuyor. D02 yalnız detay bağlantısını etkiliyor. |

## 3. Sayfa × Rol × İşlem Kapsamı

**K:** Kod incelendi. **T:** Gerçek uygulama tarayıcıda açıldı. **İ:** Belirtilen işlem kullanıldı. **H/S/F:** Önceki rapordaki HTTP, SQL ve fixture testleri; bu turda tekrar çalıştırılmış sayılmaz. Sayfanın açılması tüm işlemlerinin denenmesi değildir.

| Rol ve sayfa | Gerçekte yapılan işlem | Kullanıcının gördüğü / zorlandığı alan | Yönetici değerlendirmesi | Kanıt |
|---|---|---|---|---|
| Genel admin, `/AdminPanel/raporlar` | Genel Bakış, Kırılımlar ve Rapor Çıktıları açıldı; Kargaz filtresinden YKC listesine geçildi. | 1 kayıtlık rapor, 45 kayıtlık liste açıyor. | Karar verilen kapsam takip ekranında kayboluyor; D01. | K,T,İ |
| Şirket admini, `/AdminPanel/raporlar` | Üç sekme ayrı açıldı; devreye alma kayıt bağlantısı kullanıldı. | Şirket sabit; seçili dönemde 6 kayıt var, bağlantı 33 kaydı açıyor. | Şirket izolasyonu ile tarih kapsamı farklı konular; dönem geçişi düzeltilmeli. | K,T,İ |
| Genel admin / rapor personeli, `/ykc/raporlar` | Kayıt özeti ve dört sekmesi ayrı açıldı. | İmza başlamamış kayıtta durum açık; cihaz karşılaştırmasında kapasite/baca bilgisi var. | Yeni bir detay ekranına gerek yok; mevcut özet kullanılabilir. | K,T,İ |
| Yalnız rapor personeli, `/ykc/raporlar` → `/ykc/detay/45` | "Talep Detayını Aç" tıklandı. | "Yetkiniz yok" sayfası geliyor. | Yetkiyi genişletmeden bağlantı sunumu düzeltilmeli; D02. | K,T,İ |
| Yalnız rapor personeli, `/ykc/talepler` | Doğrudan URL açıldı. | Erişim engelleniyor. | Rapor izni, talep yönetim izni sayılmıyor; bu engel korunmalı. | K,T |
| Personel, `/personel-panel`, SürmeliGAZ | Normal girişte şirket seçildi; ana sayfa ve belge listesi açıldı. | Sade Takvim; YKC bilgisi yok. Boş belge listesi ve 0 bekleyen tutarlı. | Yetkisiz modülü boş ama işlevselmiş gibi sunmuyor. | T,İ |
| Personel, şirket değiştirme | SürmeliGAZ'dan Çorumgaz'a geçildi. | Erişilebilir ana sayfa açılıyor, şirket bağlamı değişiyor. | Eski şirketin yetkisiz ekranında bırakılmıyor. | T,İ |
| Personel, `/personel-panel/onay-bekleyenler` | Sentetik yenileme belgesi 3, gerekçeyle reddedildi; dört durum sekmesi açıldı. | Bekleyen 1→0, reddedilen 0→1; toplam 3, onaylanan 2. Karar ve işlemi yapan kişi geçmişte. | Belge kararı ile sayaçlar aynı kapsamda tutarlı. | T,İ |
| Personel, `/ykc/detay/45` | İncelemeye alma ve randevu kaydı kullanıldı; ertesi turda kayıt yeniden açıldı. | 09:15 kabul edilmiyor; 09:00 kaydediliyor. Planlanan tarih/saat kalıcı. Randevu saati gelmeden kontrole geçme düğmesi kapalı. | Kullanıcı saat kuralını girişte öğreniyor; erken teknik kontrol engeli görünür. | T,İ |
| Personel, `/ykc/detay/4` | Geçmiş tarihli randevudan kontrole geçildi. Önce gerekçesiz, sonra gerekçeli "Uygun Değil" kaydı denendi. | Gerekçesiz kayıt reddedildi; gerekçeli kayıttan sonra "2. kontrol için randevu planlayın" gösterildi. | Sonraki sorumluluk belli; otomatik saat ataması yapılmıyor, personel planlıyor. | T,İ |
| Sertifikalı firma, `/ykc/detay/4` | Personelin kaydettiği aynı test talebi firma hesabından açıldı. | "Kontrol randevusu planlanacak", son sonuç ve gerekçe üstte; altta kontrol sonucu ve işlem geçmişi mevcut. | Firma gerekçeyi aramıyor; ayrı kart veya yeni sayfa eklemeye gerek yok. | T,İ |
| Şirket admini, `/AdminPanel/yetkiler` | 51 kişilik liste içinde rapor personeli arandı, düzenleme paneli açılıp kapatıldı. | Kişi/e-posta ayrımı ve şirket kapsamı anlaşılır; yalnız kendi şirketinin izinleri sunuluyor. | Günlük yetki incelemesi yapılabilir; gerçek izin değiştirme bu turda denenmedi. | T,İ |
| Şirket admini, `/AdminPanel/subeler` | Şube 4 düzenleme paneli açılıp kapatıldı. | Seçili firma/şube bilgileri doğru; liste bağlamı korunuyor. | Ortak yan panel korunmalı. Sunucu hatası sonrası veri koruma önceki B10 kapsamında ayrıca denenmeli. | T,İ |
| Genel admin, servis/kullanıcı yönetimi | Servis satır detayı, düzenleme/yeni kayıt formu ve kullanıcı düzenleme paneli açıldı. | Firma, şirket ve rol alanları görülebiliyor. Kaydetme yapılmadı. | Yalnız form gezisi; kayıt doğruluğu ve yetki etkisi tamamen doğrulanmış sayılmaz. | T |
| Yetkili servis, `/ys-panel/index` | Belge reddinden sonra normal giriş yapıldı. | Ay içi 6 cihaz, 1 geçerli belge, son işlemler ve günlük takvim gösteriliyor. | Reddedilmiş yenileme, eski geçerli belgeyi dashboard'da geçersiz göstermiyor. | T |
| Yetkili servis, `/ys-yetki-belgesi/index` | Belge geçmişi açıldı. | 2 belge: 1 onaylı, 1 reddedilmiş. Ret gerekçesi satırda doğrudan okunuyor. | Personelin kararı servis tarafına doğru yansıyor; bu turda ret sonrası yeni devreye alma kaydı gönderilmedi. | T |

### Rapor Modalindeki Dört Sekme

| Sekme | Kontrol edilen sonuç | Kullanım kararı |
|---|---|---|
| Tesisat ve Abone Bilgileri | Seçilen kaydın abone/tesisat/sözleşme alanları okunuyor. | Ana listeyi ek alanlarla büyütmeye gerek yok. |
| Firma Bilgileri | İlgili firma ve şirket bilgileri ayrı sekmede. | Rol ve şirket kapsamı korunmalı; gereksiz tekrar eklenmemeli. |
| Cihaz Karşılaştırması | Projedeki/yeni cihaz, kapasite ve baca alanları korunuyor. | Bu alanlar kaldırılmamalı; kaynakta null olan değer bir hata gibi sunulmamalı. |
| İmza Durumu | İmza başlamamış kayıtta bu durum açıkça belirtiliyor. | Nihai imzalı belge kabulü ayrıca dış sağlayıcıyla test edilmeli. |

### İşlem Sonucunun Firma Ekranındaki Kanıtı

Aşağıdaki görüntü, personelin bu testte kaydettiği gerekçenin firma hesabında okunabildiğini gösterir. Veriler sentetiktir.

![Yeni kaydedilen uygunsuzluk gerekçesinin firma görünümü](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/firma-kaydedilen-uygunsuzluk.jpg)

Diğer kanıtlar: [personelde ikinci kontrol adımı](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/personel-uygunsuzluk-sonrasi.jpg), [belge ret sonucu](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/personel-belge-ret-sonucu.jpg), [serviste ret gerekçesi](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/servis-belge-ret-gerekcesi.jpg).

## 4. Yeni Öncelikli Hatalar

### D01 — P2: Rapordan Listeye Geçerken Filtre Kaybı

**Sınıf:** Çalıştırılarak doğrulanmış hata. **Roller:** Genel admin, şirket admini. **Sayfa:** `/AdminPanel/raporlar`, Rapor Çıktıları.

**Kod:** [YKC kayıt bağlantısı](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Views/AdminPanel/Raporlar.cshtml:247) tarihleri taşıyor, şirketi taşımıyor. [Devreye alma bağlantısı](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Views/AdminPanel/Raporlar.cshtml:255) tarih ve şirket parametrelerini taşımıyor. Aynı bölümdeki PDF/Excel bağlantılarında bu parametreler mevcut.

**Beklenen:** Rapordaki sayıdan açılan liste aynı izinli şirket ve dönem kapsamını kullanmalı.

**Mevcut davranış:** Kargaz'ın 1 YKC kaydı yerine genel liste 45 kayıt açılıyor. Çorumgaz şirket admininde 01–07 Ekim dönemi için 6 devreye alma yerine bütün tarihlerden 33 kayıt açılıyor. Sayılar, sabit sentetik kayıt kümesinde gözlendi.

**Kullanıcı etkisi:** Sayının yanlış olduğundan şüpheleniliyor; yönetici filtreyi yeniden kuruyor ve yanlış kapsamı dışa aktarabilir. İncelenen geçişte yetkisiz şirket erişimi kanıtı yok.

**Tekrar üretim:** Genel adminle Raporlar → Kargaz → Rapor Çıktıları → Cihaz değişim kayıtlarını aç. İkinci varyant: şirket adminiyle 01–07 Ekim → Rapor Çıktıları → Devreye alma kayıtlarını aç.

**Önerilen çözüm:** Listeye geçişte tarih ve şirket bağlamını uçtan uca koru. Yalnız URL'ye parametre eklemek yeterli sayılmamalı; hedef controller/API'nin parametreyi izinli şirket kapsamına daralttığı doğrulanmalı. Genel adminin filtre seçimi ile personelin aktif şirketi birbirine karıştırılmamalı.

**Kabul testi:** Veri değişmiyorken gösterge toplamı ile açılan listenin filtreli toplamı eşleşmeli. Genel admin/şirket admini/personel varyantları ayrı test edilmeli; yabancı şirket parametresi erişim genişletememeli. Başlangıç/bitiş filtreleri, PDF ve Excel de aynı kümeyi kullanmalı.

Kanıt: [Kargaz'dan 45 kayıtlık listeye geçiş](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/yonetici-rapordan-listeye-45.jpg), [raporda 6 kayıt](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/yonetici-donem-6-kayit.jpg), [bağlantıda 33 kayıt](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/yonetici-donemsiz-33-kayit.jpg).

### D02 — P2: Rapor Personeline Erişemediği Detay Düğmesi Sunulması

**Sınıf:** Çalıştırılarak doğrulanmış hata. **Rol:** `RAPOR_GOR` ve `YKC_RAPOR_GOR` bulunan, talep görüntüleme yetkisi bulunmayan personel. **Sayfa:** `/ykc/raporlar` kayıt özeti.

**Kod:** [Koşulsuz detay bağlantısı](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Views/Ykc/Raporlar.cshtml:550). [Detay action'ındaki yetki kontrolü](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Controllers/YkcController.cs:413), `TalepleriGorebilir` yokken erişimi reddediyor.

**Beklenen:** Rapor okuyabilen ama talep yönetim detayına giremeyen kişiye çalışmayacak bir işlem sunulmamalı.

**Mevcut davranış:** Rapor listesi ve dört sekmeli özet çalışıyor. "Talep Detayını Aç" tıklandığında "Yetkiniz yok" sayfası açılıyor. Doğrudan talep listesi erişimi de engelleniyor; bu ikinci davranış doğru.

**Kullanıcı etkisi:** Okuma işi gereksiz bir hata ekranıyla kesiliyor; kullanıcı rapor sayfasına geri dönmek zorunda kalıyor.

**Tekrar üretim:** Yalnız rapor personeliyle giriş → YKC raporları → test talebi 45'in göz düğmesi → Talep Detayını Aç.

**Önerilen çözüm:** Hedef eylemin mevcut izin kontrolüne göre bağlantıyı göstermemek yeterli. Salt okunur özet zaten mevcut. Bu sorunu çözmek için personele ek talep yetkisi verilmemeli veya sunucu kontrolü kaldırılmamalı.

**Kabul testi:** Rapor-only hesap dört sekmeyi okuyabilmeli, erişemeyeceği detay bağlantısını görmemeli. Talep görüntüleme + rapor yetkili hesap bağlantıyı kullanabilmeli. Doğrudan yetkisiz detay URL'si engellenmeye devam etmeli.

Kanıt: [Rapor bağlantısının yetki engeline götürmesi](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/rapor-personeli-detay-yetki-engeli.jpg).

## 5. Rol Bazında Kullanım Sonuçları

- **Sertifikalı firma:** Durum, son kontrol sonucu ve gerekçe aynı yerde. "Şimdi ne olacak?" sorusunu randevu planlanacak ifadesi yanıtlıyor. Yeni gerekçe kutusu veya farklı talep ekranı önermiyorum. Kaynak proje alanının JSON gizliliği ise ilk rapordaki B04 olarak ayrı bir güvenlik konusu.
- **Personel:** Gerekçe doğrulaması ve sonraki kontrol adımı anlaşılır. Yeni randevu otomatik atanmış gibi gösterilmiyor. Sınırlı yetkili kullanıcıların bağlantıları gerçek eylem izinleriyle eşleştirilmeli; D02 bunun somut örneği.
- **Yetkili servis:** Belge ret gerekçesi doğrudan geçmişte, geçerli belge sayısı dashboard'da. Belge kararının nedenini öğrenmek için personele sorması gerekmiyor. Devreye alma sürecine personel onayı eklenmemeli.
- **Şirket admini:** Yetki ve şube panellerinde şirket bağlamı net. Raporun dönem kapsamı listeye taşınmadığında işler gereksiz uzuyor. Öncelik yeni kart değil D01.
- **Genel admin / müdür:** Ortalama tamamlanma, ilk kontrolde uygunluk, tekrar randevu oranı ve firma/bölge/ekip kırılımları mevcut. "Raporlar yalnız Excel'de işe yarıyor" demek doğru değil. Ancak özetten açılan liste aynı kapsamı korumadan bu göstergelerden güvenilir günlük takip yapmak zorlaşıyor.

Test verilerinde %0 ilk uygunluk veya %100 tekrar randevu görünmesi işletmenin gerçek başarısını göstermez; veri kümesi kontrol senaryoları için oluşturulmuştur.

## 6. Yeni Sayfa / İşlev Gereksinimi

Bu devam turunda zorunlu yeni bir sayfa ihtiyacı doğrulanmadı. Gerekçe, belge geçmişi, rapor ayrıntıları ve yetki düzenleme mevcut ekranlarda bulunuyor. D01 ve D02 mevcut sayfalarda çözülebilir.

WhatsApp, stok yönetimi, GPS veya yeni takvim görünümü bu bulguların çözümü değil. Kullanıcı ihtiyacı ve veri kaynağı doğrulanmadan zorunlu modül olarak önerilmemeli. Dış entegrasyonların işletim ve hata görünürlüğü ilk rapordaki üretim kabul listesinde ayrı kalıyor.

## 7. Sadeleştirilecek Yapılar

- Yetkinin izin vermediği detay bağlantısını kaldır; veri/alan silme veya yetki genişletme yapma.
- Rapor filtrelerinin ayrı bağlantılarda farklı şekilde oluşturulmasını ortak, tipli kapsam aktarımıyla tutarlı hale getir. İş kuralını Razor/JavaScript'e taşımadan API kapsam denetimini koru.
- Kapasite, baca, karar gerekçesi ve geçmiş kayıtlarını "sadeleştirme" gerekçesiyle kaldırma. Bunlar operasyonel kanıt.
- Ortak yan panel ve tabloları roller için tekrar kopyalama. Rol farklarını mevcut ortak bileşenin izin/veri sözleşmesi üzerinden koru.

## 8. Korunacak Davranışlar

- Belge yetkisi olup YKC yetkisi bulunmayan personelde sade takvim ve doğru menü kapsamı.
- 00/30 saat dilimi doğrulaması ve randevu zamanı gelmeden teknik kontrolü başlatamama.
- Uygunsuz kontrol için gerekçe zorunluluğu; gerekçenin firma tarafından görülebilmesi.
- Yeni kontrol/randevu planlanırken önceki sonucun ve işlem geçmişinin korunması.
- Rapor-only kullanıcıya talep yönetimi URL'sinde sunucu tarafı erişim engeli.
- Yenileme belgesi reddedildiğinde eski onaylı belgeyi geçmişten silmeme ve geçerli belge olarak göstermeye devam etme.
- Yönetici raporundaki mevcut KPI ve kırılımlar; kapasite/baca içeren cihaz karşılaştırması.

## 9. Çalıştırılanlar ve Kalan Doğrulamalar

Bu turda gerçek uygulamada normal rol girişleri, şirket geçişi, belge reddi, talep inceleme/randevu adımı, teknik kontrol doğrulaması/kaydı, karşı rolden sonuç okuma, rapor sekmeleri, rapor bağlantıları ve yönetim yan panelleri kullanıldı. Hedef snapshot'taki önceki derleme/SQL/HTTP/fixture sonuçları ilk raporda; bu turda yeniden tüm test paketlerinin geçtiği iddia edilmiyor.

| Kalan senaryo | Mevcut kanıt | Tamamlanması gereken kabul |
|---|---|---|
| 5. uygunsuz kontrol sonrası yeni dönem | İlk raporda kod/SQL iş kuralı doğrulaması var. | Beş kontrolün tamamını farklı randevularla UI'dan yapıp yeni döneme geçiş ve eski geçmişi birlikte doğrulamak. |
| Uygunsuz sonuçtan sonra uygun kontrol | Kod/SQL/gezinme testleri mevcut. | Firma ekranındaki eski uyarının kalkmasını yeni uygun sonucun UI kaydından sonra görmek. |
| Randevu aralığı / çakışma | 00/30 doğrulaması UI'da; çakışma kuralları önceki teknik testlerde. | Aynı ekip/personelin dolu saatini iki gerçek oturumdan eşzamanlı kaydetme kabulü. |
| İmza ve nihai tamamlama | Demo/kontrat ve teknik testler mevcut. | Gerçek sağlayıcı hata/iptal/zaman aşımı, imzalı dosya değişmezliği ve sonuç belgesi kabulü; kurum kontrollü ortamında. |
| Yeni servis hesabının ilk kurulumu | Tamamlanmış hesap ve kod/fixture/SQL incelendi. | Boş hesaptan bütün kurulum adımları; parola ve sözleşme kabulü kullanıcı kontrolünde. |
| Belge yükleme ve dosya işlemleri | Mevcut belge, ret, sahiplik ve binary çıktı testleri mevcut. | Gerçek UI upload, bozuk/büyük dosya, depolama/DB hata telafisi ve her rolün tüm çıktı düğmeleri. |
| Yetki kaldırılması / personel pasifleştirme | Önceki HTTP/SQL kapsamı mevcut. | Açık yan panel ve açık kayıt üzerinde gerçek iki oturumlu UI kabulü; yetki verme/kaldırma ayrıca onay gerektirir. |
| Servis yönetimi izin kombinasyonu | Kod ve teknik kapsam kontrolleri. | Yalnız servis yönetimi yetkili personelin günlük yönetim akışını ayrıca bitirmek. |
| Şube sunucu hatasından dönüş | İlk rapor B10 kod bulgusu. | Hatalı POST sonrasında alan değerleri, odak ve tekrar kaydetme davranışını gerçek UI'da doğrulamak. |
| Tüm ekran boyutları ve erişilebilirlik | İlk raporda masaüstü/mobil örnekleri, yatay taşma bulgusu ve bazı klavye kontrolleri. | Her modülü 320/375 piksel, tablet ve gerçek %200 yakınlaştırmada test etmek; ölçülmüş kontrast kontrolü. |
| Gerçek dış kaynak, SMS, üretim TLS/depolama | Yerel fixture ve demo sağlayıcı kullanıldı. | Üretim benzeri kurum ortamında kesinti, kimlik bilgisi, log maskeleme ve geri yükleme kabulü. |

Bu satırlar doğrulanmış yeni hata değil, kapsam boşluklarıdır. Oturum/hesap/parola veya gerçek servis engelleri görülmeden "başarısız" diye etiketlenmez.

## 10. Uygulama Sırası ve Kabul

Bu rapor düzeltme uygulama yetkisi değildir. Önerilen somut sıra:

| Sıra | İş | Bağımlılık ve kabul |
|---|---|---|
| 1 | İlk rapor B01: servis kullanıcısının ortak marka kataloğunu değiştirmesini engelle. | Yetki/sahiplik kuralı API'de; farklı firmalarla HTTP negatif test. |
| 2 | İlk rapor B02: aynı firma seçimlerinde marka/kategori geçmişini ve tarihlerini koru. | Eski/yeni API yolları aynı iş kuralını kullanmalı; değişmeyen ilişkilerde ID/tarih korunmalı. |
| 3 | İlk rapor B03: YKC tekrar gönderiminden ikinci kayıt oluşmasını engelle. | Kalıcı istek/kaynak eşlemesi ve eşzamanlılık testi; tekrar gönderimde tek talep. |
| 4 | İlk rapor B04: firma JSON yanıtından personele özel kaynak proje alanını çıkar. | Normal detay/listede allowlist; resmi FR265 kapsamı kurum kuralından bağımsız değiştirilmemeli. |
| 5 | D01: rapor → liste → çıktı kapsamını eşitle. | 1→1 ve 6→6 testleri; genel admin, şirket admini ve personelde farklı şirket yetki kontrolleri. |
| 6 | D02: rapor-only detay bağlantısını doğru yetkiye bağla. | Dört sekme çalışmalı; yetkisiz detay düğmesi görünmemeli; doğrudan URL engeli korunmalı. |
| 7 | İlk rapor B05/B06/B10: dosya hata ayrımı, filtreli dönüş, şube formunun hata sonrası veri koruması. | Hata nedenini anlaşılır göster; filtre/sayfa ve girilen form alanlarını kaybetme. |
| 8 | İlk rapor B07 ve kalan kabul listesi. | Mobilde veriyi kesmeden ortak tablo iyileştirmesi; ardından 9. bölümdeki senaryolar için rol bazlı kabul. |

### Yönetici Gözüyle Canlı Kullanım Öncesi Görmek İstediğim Temel Düzeltmeler

Önce ortak veriyi ve yetki geçmişini koru, mükerrer talebi engelle, firmaya özel veri sınırını sağlamlaştır. Ardından rapordaki sayının açtığı listeyle eşleşmesini ve personele yalnız yapabileceği işlemin gösterilmesini düzelt. Mevcut takvim, ortak tablolar, şube yan paneli ve gerekçe görünümü korunabilir. Bu aşamada sayfa sayısını artırmak, bu somut sorunları çözmekten daha düşük öncelikli.
