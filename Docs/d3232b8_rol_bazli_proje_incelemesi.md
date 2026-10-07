# d3232b8 Rol, Kullanılabilirlik ve Yönetici İncelemesi

**Tarih:** 06.10.2026. **İncelenen sürüm:** `d3232b812f205402b9d3a8c9151b1fa66388b72a`.

Bu belge bir incelemedir; uygulama düzeltmesi veya yayın onayı değildir. Bulgular kurumsal operasyon, veri doğruluğu, rol izolasyonu ve günlük kullanım açısından değerlendirilmiştir. Kullanıcı tarafından kapsam dışı bırakılan süreç için hiçbir durum, alan veya özellik önerilmemiştir.

## 1. Yönetici Özeti

**Genel sonuç:** Uygulamanın MVC -> API -> iş servisi ayrımı, şirket kapsamı ve ana operasyon akışları korunmaya değer. Baştan yazma, framework değiştirme veya takvimi yeniden tasarlama gereği görmedim. Son committeki ortak ekran ve yetki düzenlemelerinin önemli kısmı gerçek uygulamada çalışıyor. Bununla birlikte, dört P1 bulgu canlı kullanım öncesinde ele alınmalı:

| Öncelik | Bulgu | Yönetici açısından sonuç |
|---|---|---|
| P1 | B01: Servis kullanıcısı ortak marka kataloğunu değiştirebiliyor. | Bir firmanın işlemi, diğer firmaların kullandığı referans veriyi etkiliyor. |
| P1 | B02: Firma kaydını aynı seçimlerle kaydetmek marka/kategori ilişkilerini yeniden oluşturuyor. | Yetki geçmişi ve geçerlilik tarihleri ilgisiz bir düzenlemede değişebiliyor. |
| P1 | B03: Aynı YKC oluşturma isteği iki ayrı talep oluşturabiliyor. | Çift iş emri, hatalı iş yükü ve rapor sayısı oluşabilir. |
| P1 | B04: Sertifikalı firmanın normal detay API yanıtında kaynak proje numarası kalıyor. | Ekranda gizleme, API düzeyinde gizlilik sağlamıyor. |

**Doğrulanmış P0 bulgu yok.** Bu, üretim ortamının bütün güvenlik ve entegrasyon kabul testlerini geçtiği anlamına gelmez. Gerçek SMS, dış tesisat servisi, imza sağlayıcısı, üretim TLS/depolama ve geri yükleme doğrulanmadı.

Diğer somut sorunlar: YKC dosya hatasının bağlantı kesintisi gibi sunulması; talep detayından dönüşte filtre/sayfa kaybı; dar ekranda ortak devreye alma tablosunun yatay kaydırma istemesi; görüntüleme yetkili personele "yönet" ifadesi gösterilmesi; varsayılan İngilizce gizlilik metni; bazı test beklentilerinin güncel firma takvimiyle uyuşmaması.

### Sürüm ve Güvenli Ortam

- Çalışma dalı `coderabbit-review-20260921`; başlangıç ve bitiş HEAD'i hedef commit. Önceki commit ile fark: 44 dosya, 1.917 eklenen ve 723 kaldırılan satır. İnceleme yalnız bu diff ile sınırlı tutulmadı.
- Mevcut çalışma alanında `YetkiliServisGazAcma.API/Infrastructure/TestDataSeed.cs` commit edilmemiş değişiklik içeriyordu. Bu dosyaya dokunulmadı; onun içeriği hedef sürümle karıştırılmadı.
- Hedef commit `git archive` ile ayrı geçici klasöre çıkarıldı. Kullanıcının çalışan `7161` uygulaması ve veritabanı değiştirilmedi.
- MVC `127.0.0.1:55410`, API `127.0.0.1:55411`, yerel SOAP test servisi `127.0.0.1:55412` üzerinden çalıştırıldı. Gerçek dış servise istek gönderilmedi. Bu HTTP adresleri yerel test içindir; üretim taşıma güvenliğini kanıtlamaz.
- Ayrı LocalDB veritabanında sentetik kayıtlar kullanıldı. Normal giriş, SMS test sağlayıcısının doğrulama kodu ve gerçek yetkilendirme zinciri kullanıldı; kimlik doğrulama atlanmadı.
- Genel admin, şirket admini, çok şirketli personel, yetkili servis, sertifikalı firma; ayrıca görüntüleme, atama, teknik kontrol, rapor, sıfır yetki ve yalnız eski `SuperAdmin` rolü için ayrı hesaplar oluşturuldu. 51 personel, uzun içerikler, farklı firma/şirket kayıtları ve geçerli eski belge + onay bekleyen yenileme senaryosu hazırlandı.
- Uygulama kodu/bağımlılığı/şeması değişmedi. Yalnız inceleme raporu, route envanteri ve sentetik ekran kanıtları çalışma alanına eklendi. Commit/push yapılmadı.

## 2. Bu Committeki Yeniliklerin Doğrulaması

| Çalışma | Kod / gerçek ekran sonucu | Sınır |
|---|---|---|
| Cihaz seçerken devreye alma yetkisi | Ocak seçildiğinde kategori engeli hemen gösterildi; Kombi seçilerek form dolduruldu ve kayıt tamamlandı. | Gerçek marka/yetki dış sağlayıcısı kullanılmadı. |
| Roller arası ortak devreye alma tablosu | Admin, şirket admini, personel ve servis listeleri açıldı. Serviste seçim sütunu yok; yönetim/personelde var. Tesisat/sözleşme ayrı; rozetler kompakt. | Dar ekran yatay kaydırma B07. Bütün çıktı düğmeleri tarayıcıdan tek tek indirilmedi. |
| Bekleyen iş kısayolları | Atama hesabında inceleme/randevu; teknik kontrol hesabında tamamlama; yalnız görüntüleme ve sıfır yetkide işlem kısayolu yok. HTTP sayaçları, aynı kapsamdaki filtreli liste toplamlarıyla eşleşti. | Tamamlama için olumlu kayıt şartları SQL testinde; gerçek imzalı kayıtla canlı sağlayıcı kabulü yapılmadı. |
| Yetkiye göre takvim | Kargaz'a geçiş kullanıcıyı erişilebilir ana sayfaya taşıdı. YKC yetkisi yokken sade "Takvim" geldi; randevu adları, kayıt sayıları ve işlem filtreleri yoktu. | SürmeliGAZ varyantı ayrıca tarayıcıda seçilmedi; aynı yetki dalı kod/gezinme testlerinde incelendi. |
| Son uygunsuzluk gerekçesi | Yeni randevu bekleyen firma detayında son gerekçe görünüyordu; uzun metin klavyeyle açıldı. Sonraki uygun kontrolün eski gerekçeyi güncel uyarıdan çıkarması testlerde geçti. | Sonraki uygun kontrol bütün akış boyunca tarayıcıda kaydedilmedi; SQL/gezinme kanıtı. |
| Şube yan paneli | Serviste ekleme/kaydetme ve düzenleme paneli çalıştı. Escape kapattı, odak düzenleme bağlantısına döndü. Yönetim ve şirket admininde ortak görünüm açıldı. | Hata sonrası girilen alanları koruma B10; her rolde silme/pasifleştirme yapılmadı. |
| Personel yetki listesi | 51 personel 10'luk sayfalarda; şirket admininde yalnız şirket kapsamı. Düzenleme paneli ve şirket seçimi açıldı. | Tarayıcıda gerçek erişim yetkisi değiştirilmedi; yetki geçmişi SQL testinde. |
| Rehber/giriş/kayıt uyumu | Üç gerçek sayfa açıldı; servis rehberi yalnız aktif servis kaydı gösterdi, eski açıklama metni görünmedi. | Kayıt formunun ikinci adımı tarayıcı gezisinde tamamlanmadı; fixture/SQL doğrulaması ayrı. |
| Genişletilen testler | Çözüm tekrar derlendi: 0 hata/0 uyarı. BrowserForms tekrar çalıştı: 123 başarılı/0 başarısız. | 123 test, üretim JS/CSS kullanan HTML fixture'ıdır; 123 gerçek uygulama sayfası değildir. |

Örnek korunan davranışlar:

![YKC yetkisi bulunmayan personelde sade takvim](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/personel-yetkisiz-takvim.jpg)

![Cihaz seçiminde servis yetki engeli](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/servis-cihaz-yetki-engeli.jpg)

## 3. Sayfa × Rol × İşlem Kapsamı

**Gösterim:** K = koddan incelendi; T = gerçek uygulama tarayıcıda açıldı; İ = belirtilen işlem çalıştırıldı; H = gerçek HTTP/middleware testi; S = SQL/iş kuralı testi; F = tarayıcı fixture'ı. **T, sayfanın bütün düğmelerinin çalıştırıldığı anlamına gelmez.** E = dış ortam/servis nedeniyle uçtan uca doğrulanamadı.

URL'ler route yollarıdır; gerçek tarayıcı gezisi güvenli test MVC adresinde yapıldı. Aynı action'ın `/index`, Türkçe karakterli `/Düzenle` gibi alias'ları ayrı ürün sayfası sayılmadı. Modallar, satır detayları ve drawer'lar bağlı sayfanın işlemi olarak kaydedildi.

### 3.1. Herkese Açık Ekranlar ve Oturum

| URL / amaç | Rol | Ana işlem ve bağlı API/servis | Durum |
|---|---|---|---|
| `/` - genel ana sayfa | Anonim | Özet/giriş/rehber; HomeController, HomeOzetApiClient | K,T |
| `/giris` - giriş türleri | Tüm roller | VKN/e-posta + şifre, test SMS doğrulaması; AuthApiClient -> `/api/auth/*` | K,T,İ,H; bütün ana rollerle normal giriş |
| `/giris/sms-dogrula` ve girişte doğrulama adımı | Tüm roller | OTP tamamlama; AuthController | K,İ,H; ayrı GET yolu gezilmedi, giriş POST'u aynı URL'de adım gösterebilir |
| `/giris/sifre-unuttum`, POST `/giris/sifre-yenile` | Anonim | Hesap eşleştirme ve şifre yenileme; AuthApiClient | K,T,F; gerçek SMS/şifre değiştirme E |
| `/cikis` | Oturumlu | Oturum kapatma | K,İ,H |
| `/kayit/yetkili-servis` | Anonim | Firma/iletişim, kapsam/hesap adımları; YetkiliServisApiClient -> kayıt API | K,T,F,S; gerçek tarayıcıda ilk adım, son başvuru gönderilmedi |
| `/yetkili-servisler` | Anonim | İl/ilçe/marka/kategori filtreleri; rehber liste/filtre API'leri | K,T,H/S kapsam kontrolleri; her filtre birleşimi UI'da çalıştırılmadı |
| `/Home/Privacy` | Anonim | Gizlilik içeriği; statik Razor | K,T; B09 |
| `/yetkisiz-erisim`, `/Home/Error` | Her rol | Erişim/hata bilgilendirmesi | İlk yol K,T; Error K, hata yönlendirmeleri testlerde |
| `/panel/sirket-sec` | Şirket seçebilen roller | Seçili şirketi değiştirme; AktifSirketService, PanelKapsamApiClient | K,T,İ,H/S; personelin Kargaz geçişi gerçek UI'da |

### 3.2. Yetkili Servis

| URL / amaç | Rol/kapsam | Ana işlem ve bağlı API/servis | Durum |
|---|---|---|---|
| `/ys-panel`, `/ys-panel/index` | Yetkili Servis, kendi firma | Dashboard, bildirimler, işlem bağlantıları; YetkiliServisPanelApiClient | K,T |
| `/ys-panel/ilk-kurulum` | Kendi firma | Kurulum gereksinimi; `/api/ys-panel/ilk-kurulum` | K,T; tamamlanmış hesap dashboard'a döndü; boş yeni hesabın bütün kurulum UI adımları ayrıca çalıştırılmadı |
| `/ys-yetki-belgesi`, `/index` | Kendi firma | Geçerli/eski/yeni belge, yükleme/silme; YetkiBelgesiApiClient | K,T,S; eski geçerli + bekleyen yenileme engel olmadı; gerçek upload gönderilmedi |
| `/ys-yetki-belgesi/dosya/{id}` | Sahiplik + yetki | Yetki belgesi dosyası; `/api/yetki-belgesi/dosya-indir` | K,H/gezinti testleri; her gerçek dosya UI'da açılmadı |
| `/ys-devreyeal` | Kendi firma, geçerli belge/marka/kategori | Tesisat/cihaz sorgulama, yetki kontrolü, form aç/kapat, kaydet; YetkiliServisDevreyeAlmaApiClient | K,T,İ,H,S,F; sentetik Kombi doğrudan tamamlandı |
| POST `/ys-devreyeal/tesisat-sorgula`, `/marka-kontrol`, `/kaydet`, `/kaydet-json` | Aynı kapsam | Kaynak referansı ve son kayıt doğrulaması | K,İ,H,S; JSON yoluyla kayıt yapıldı; alternatif klasik kaydet ayrı UI turu yok |
| `/ys-devreyeal/gecmis#devreye-alma-detay-{id}` | Kendi firma | Filtre, satır detayı, tam model/adres, PDF/Excel | K,T,İ,H,S; başarı bağlantısı doğru kaydı açtı |
| `/ys-devreyeal/detay/{id}`, `/pdf/{id}`, `/excel/{id}` | Kendi kayıt | Ayrıntı/çıktı API'leri | K,H,S; yabancı kayıt 404; kendi PDF/Excel binary yanıtları alındı |
| `/ys-panel/raporlar` | Kendi firma | Tarih aralığı, dağılım ve işlem listesi; panel rapor API | K,T,S |
| `/ys-panel/raporlar/pdf`, `/pdf-toplu`, `/excel`, `/excel-toplu` | Kendi firma | Tarih/seçim kapsamlı çıktı | K,H/S; her MVC alias tarayıcıdan indirilmedi |
| `/ys-panel/markalar` | Kendi firma ilişkileri | Seçim, ekleme/düzenleme/silme; panel yönetim servisi | K,T,İ(H); B01/B02 |
| `/ys-panel/subeler`, `/subeler/duzenle/{id}` | Kendi firma | Ortak yan panel ekleme/düzenleme, durum/silme API'leri | K,T,İ; yeni şube kaydı, düzenleme açılışı, Escape/odak; durum/silme sadece K/S |
| `/ys-panel/profil` | Kendi hesap | İletişim/şifre formları; panel/Auth API | K,T; şifre veya profil kaydı değiştirilmedi |

**Akış sonucu:** Sorgu -> yetki engelini cihaz seçiminde öğrenme -> yetkili cihazı doldurma -> doğrudan tamamlanma -> geçmişte ilgili satırın açılması çalıştı. Personel onayı eklenmesi gerekmiyor. 30 dakikalık randevu kuralı bu devreye alma akışının adımı değil; YKC randevusuna aittir.

### 3.3. Sertifikalı Firma / YKC

| URL / amaç | Rol/kapsam | Ana işlem ve bağlı API/servis | Durum |
|---|---|---|---|
| `/ykc` | Sertifikalı Firma kendi firma; iç operasyon yetkili şirket | Ana sayfa, talep/randevu özeti; YkcApiClient | K,T; firma/personel varyantları |
| `/ykc/yeni` | Sertifikalı Firma | Kaynak sorgulama, cihaz seçimi, yeni cihaz alanları, uyarı, talep oluşturma | K,T,İ,H,S,F; kapasite artışında uyarı varken talep oluşturuldu |
| POST `/ykc/tesisat-sorgula`, `/cihaz-karsilastir`, `/yeni` | Aynı hesap/firma/şirket | Referans doğrulama; YkcApiController -> YkcTalepService | K,İ,H,S; geçersiz kaynak engellendi, tekrar gönderim B03 |
| `/ykc/talepler` | Kendi firma veya işlem izni olan şirket | Arama/durum/bekleyen iş filtreleri, sayfalama | K,T,İ,H,S; kaynak alanları B04, dönüş B06 |
| `/ykc/detay/{id}` | Kendi firma / şirket+işlem yetkisi | Firma görünümünde takip/gerekçe/geçmiş; iç rolde atama/kontrol/imza | K,T,İ,H,S; yabancı firma ve şirket erişimi engellendi |
| `/ykc/takvim` | Firma kendi randevuları; iç rolün şirket/kapsamı | Gün/ay/yıl ve mevcut filtreler | K,T,H,S,F; firma takvimine izin güncel ve kasıtlı; B11 test beklentisi |
| `/ykc/profil` | Kendi firma hesabı | Profil/şifre | K,T; kaydetme gönderilmedi |
| `/ykc/fr265/onizle/{id}` | Kendi/izinli kayıt | PDF.js ile form önizleme; form-verisi/form-pdf API | K,T,İ,H,S; PDF canvas dolu ve 2 sayfa; gerçek imza E |
| `/ykc/fr265/pdf/{id}`, `/ykc/dosya/{id}` | Belge türü + firma/şirket yetkisi | FR265/teknik ek indirme; YkcApiClient dosya yolları | K,H,S; olmayan dosya gerçek UI'da B05; bütün nihai gerçek belgeler E |
| POST `/ykc/form-yukle` | Teknik belge işlem izni | Tür/içerik doğrulama, private storage, hash | K,H/S; gerçek tarayıcı upload/DB arızası enjekte edilmedi |

**Firma akışında görülen:** Uygunsuzluk gerekçesi yeni randevu beklerken görünür; uzun açıklama native `details/summary` ile açılır. Kaynak cihaz adı/kapasitesi yeni talep ekranında gösterilmez; karşılaştırma sunucuda yapılır. Bu doğru davranış, B04'teki normal detay JSON açığını ortadan kaldırmıyor.

![Firma ekranında uyarı varken işlem devam edebilir](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/firma-kapasite-uyarisi.jpg)

![Yeni randevu beklerken uzun son gerekçe](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/firma-uzun-uygunsuzluk-gerekcesi.jpg)

### 3.4. Personel ve Yönetim

| URL / amaç | Rol/kapsam | Ana işlem ve bağlı API/servis | Durum |
|---|---|---|---|
| `/personel-panel`, `/index` | Personel, seçili şirket izinleri | Bekleyen iş bağlantıları, takvim, dashboard; AdminDashboard/PersonelPanel/Ykc API istemcileri | K,T,İ,H,S; tam, görüntüleme, atama, kontrol, sıfır yetki ayrı UI girişleri |
| `/personel-panel/profil` | Kendi hesap + ek şirket erişimi | Profil/şifre; DagitimSirketApiClient/Auth | K,T,S; çok şirket erişim koşulu incelendi |
| `/personel-panel/onay-bekleyenler`, `/onay-gecmisi` | Belge karar izni | Belge liste/geçmiş, satır ayrıntısı, onay/ret; AdminYetkiBelgesiOnayApiClient | K,T,H/S; karar kaydı tarayıcıdan gönderilmedi |
| `/personel-panel/onayla` GET/POST, `/reddet` POST | Belge karar izni | Karar endpoint'leri | K,H/S; GET değişiklik yetkisi yerine işlem yönlendirmesi, bütün UI varyantları çalıştırılmadı |
| `/personel-panel/devreyealmalar` | Devreye alma/rapor izni | Ortak tablo, ayrıntı/seçim/çıktı; AdminRaporApiClient | K,T,İ,H/S; seçim/ayrıntı, 375 px ölçüm; bütün browser indirmeleri tamamlanmadı |
| `/personel-panel/devreyealmalar/detay/{id}`, `/devreyealma-pdf/{id}`, `/devreyealma-excel/{id}`, `/devreyealmalar/pdf`, `/excel` | Aynı şirket ve izni | Tek/filtreli/seçili kayıt çıktıları | K,H/S; ayrı UI indirme turu yok |
| `/personel-panel/markalar`, `/ekle`, `/duzenle/{id}` | Marka yönetim izni | Katalog liste/form, ekle/düzenle/sil; MarkaApiClient | Liste K,T; form/yazma K,H/S |
| `/personel-panel/yetkiliservisler`, `/ekle`, `/detay/{id}`, `/duzenle/{id}` | Servis yönetim izni | Firma kayıtları, detay ve ortak düzenleme; AdminYetkiliServisApiClient | Liste K,T; formlar K,H/S; aynı seçimle düzenleme B02 |
| `/personel-panel/raporlar`, `/pdf`, `/excel` | Rapor izni | Devreye alma/belge raporu; AdminRaporApiClient | Ekran K,T,S; çıktı K/H/S |
| `/ykc/raporlar`, `/pdf`, `/excel` | YKC rapor izni; yetkili yönetici | Şirket kapsamlı kayıt, seçim, modal sekmeleri, çıktı | K,T,H,S,F; rapor izni hesabı HTTP'de; bütün modal sekmeleri her rolde ayrı UI turu yok |
| `/ykc/detay/{id}` iç görünüm; POST `/atama-yap`, `/durum-guncelle`, `/kontroller-kaydet`, `/imzaya-gonder`, `/imza-durum-sorgula` | Ayrı görüntüleme/atama/kontrol-imza izinleri | Randevu/kontrol/imza; YkcTalepService | Detay K,T; yazma H/S; gerçek harici imza E |
| `/AdminPanel`, `/index` | Genel admin / şirket admini; yetkili personel action kontrollerine tabi | Dashboard; AdminDashboardApiClient | K,T; giriş öncesi farklı admin rol atamalarıyla ayrı hesaplar; girişte rol normalizasyonu var |
| `/AdminPanel/profil` | Kendi yönetim hesabı | Profil/şifre | K,T; kaydetme gönderilmedi |
| `/AdminPanel/personeller`, `/personeller/ekle` | Kullanıcı yönetim izni, şirket sınırı | Liste/ekle/durum/arşivle; AdminKullaniciApiClient | K,T,H/S; tarayıcıda hesap açma/arşivleme yapılmadı |
| `/AdminPanel/kullanicilar`, `/ekle`, `/duzenle/{id}` | Kullanıcı yönetim izni | Rol/firma/şirket, profil/durum/arşiv; AdminKullaniciApiClient | Liste/ekle K,T; düzenle/yazma K,H/S |
| `/AdminPanel/yetkiler`, `/yetkiler/duzenle/{id}` | Yetki yönetim izni | Şirket bazlı işlem izinleri, yan panel; AdminKullaniciApiClient | K,T,İ,S; panel aç/kapat ve şirket kapsamı; gerçek grant kaydı UI'da değiştirilmedi |
| `/AdminPanel/devreyealmalar`, `/detay/{id}`, `/pdf/{id}`, `/excel/{id}` | Genel veya kendi şirket kapsamı | Ortak tablo/çıktılar; AdminRaporApiClient | Liste K,T; ayrıntı/çıktı H/S; bütün tarayıcı download'ları tamamlanmadı |
| `/AdminPanel/raporlar` | Yönetici şirket kapsamı | YKC operasyon KPI'ları + devreye alma/belge raporu | K,T,S; ortalama tamamlanma ve tekrar randevu zaten mevcut |
| `/AdminPanel/raporlar/pdf`, `/pdf-toplu`, `/excel`, `/excel-toplu`, `/operasyon/pdf`, `/operasyon/excel` | Aynı kapsam | Tek/seçili/dönem/operasyon çıktıları | K,H/S; her MVC dosya yolu tarayıcıda indirilmedi |
| `/AdminPanel/onay-bekleyenler`, `/yetki-belgesi-uyarilari` | Belge yönetim/rapor izni | Belge kararları, süre uyarıları; onay ve rapor servisleri | K,T,H/S; gerçek UI karar işlemi yapılmadı |
| `/AdminPanel/subeler`, `/subeler/duzenle/{id}` | Şube yönetim/servis kapsamı | Ortak drawer ekle/düzenle/durum/sil; AdminSubeApiClient | K,T; servis varyantında kayıt ve klavye testi; yönetim bütün POST'ları UI'da gönderilmedi |
| `/AdminPanel/yetkiliservisler`, `/ekle`, `/detay/{id}`, `/duzenle/{id}`; eski `/yetkiliservis-duzenle/{id}` | Firma yönetim izni + şirket kapsamı | Liste/form/çıktı/sil; AdminYetkiliServisApiClient | Liste K,T; get/update gerçek HTTP; diğer formlar K/H/S |
| `/AdminPanel/yetkiliservisler/pdf/{id}`, `/excel/{id}` | Aynı firma/şirket kontrolü | Firma kayıt çıktısı | K/H/S; bütün MVC dosya indirmeleri yapılmadı |
| `/Marka`, `/Marka/Ekle`, `/Marka/Duzenle/{id}`, `/Marka/Sil/{id}` | Katalog yazma için genel admin; controller rolü tek başına yeterli değil | MarkaApiClient; eski form yolları liste içi forma yönlenir | Liste K,T (SuperAdmin); diğer yollar K |

### Önemli Rol Varyantları

| Varyant | Çalıştırılan doğrulama | Sonuç |
|---|---|---|
| Giriş öncesi GenelSistemAdmin, SuperAdmin olmadan | Gerçek giriş, yönetim sayfaları | Genel yönetim çalışıyor; normal giriş uyumluluk rolünü ekleyebiliyor. |
| Giriş öncesi yalnız eski SuperAdmin | Gerçek giriş, yönetim ana sayfası ve Marka | Eski hesap normal girişle kullanılabiliyor; birincil admin rolü girişte tamamlanabiliyor. |
| Şirket admini | Gerçek giriş/listeler/rapor; başka şirket ID'si API isteği | Kendi şirket kapsamı; yabancı kayıt 403. |
| Tam yetkili çok şirketli personel | Gerçek giriş, Çorumgaz/Kargaz geçişi | Şirket değişince erişilebilir ana sayfaya gidiyor; eski YKC raporunda yetkisiz kalmıyor. |
| Yalnız görüntüleme | Gerçek ana sayfa + HTTP liste/rapor | Liste 200, rapor 403; işlem kısayolu yok; B08 metin sorunu. |
| Görüntüleme + atama | Gerçek ana sayfa + HTTP | İnceleme/randevu bağlantıları var, tamamlama yok. |
| Görüntüleme + teknik kontrol/imza | Gerçek ana sayfa + HTTP | Tamamlama bağlantısı var, atama bağlantısı yok. |
| Yalnız rapor | HTTP ayrı hesap | Liste 403, rapor 200; tarayıcı varyantı ayrıca açılmadı. |
| Sıfır işlem izni | Gerçek ana sayfa + HTTP | Profil/sade takvim; YKC liste ve rapor 403. |
| Yetkili servis | Gerçek sorgu/kayıt/geçmiş/şube, yabancı kayıt HTTP | Doğrudan devreye alma tamamlanıyor; başka firma kaydı 404. |
| Sertifikalı firma | Gerçek oluşturma/takip/randevu/önizleme, yabancı kayıt HTTP | Başka firma 404 / başka şirket 403; kendi normal JSON'unda kaynak alan sorunu B04. |

**Admin testi sınırı:** [AuthController.cs:226](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.API/Controllers/AuthController.cs:226), 236-243, normal girişte birincil/uyumluluk rollerini tamamlıyor. Dolayısıyla bu iki browser yolculuğu farklı başlangıç rol kümeleriyle giriş uyumluluğunu kanıtlar; giriş sonrasında token'ın yalnız tek admin rolü taşıdığını kanıtlamaz. Yalnız tek rol claim'i içeren token ile tüm yönetim yolları için ayrı HTTP matrisi bu çalışmada yapılmadı. Şirket TAM_YETKI ile sistem admini ayrımı ayrıca yetki/kapsam testlerinde korunuyor.

### 3.5. Menü Dışı İşlem ve API Envanteri

Tam yöntem/URL/controller/rol metadata dizini: [d3232b8_route_envanteri.json](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_route_envanteri.json).

Envanter 289 attribute-route işlem satırı (163 MVC, 126 API) ve 9 konvansiyonel action yolu içerir. **298 bağımsız sayfa veya 298 başarılı test demek değildir.** Aynı sayfanın alias ve POST işlemleri ayrı satırdır. Konvansiyonel route'un bütün eşdeğer URL'leri çoğaltılmadı. Reflection taramasındaki templatesiz Marka action'ları yanlışlıkla `/` sayfası sayılmadı; health endpoint'inin `/api/health` ve `/health` yolları kaynak koddan tamamlandı.

| API grubu | İşlev / kapsam / test sınırı |
|---|---|
| `/api/auth/*`, `/api/panel-kapsam/*`, `/api/personel-panel/yetkilerim`, `/api/dagitim-sirket/*` | Kimlik ve şirket/izin sınırları; gerçek auth HTTP testleri + component/SQL. |
| `/api/admin-panel/*` | Kullanıcı/yetki/firma/şube/belge/devreye alma/rapor yönetimi; yetki middleware ve ayrı SQL senaryoları; tüm yazma yolları UI'da tıklanmadı. |
| `/api/yetki-belgesi/*` | Firma ve karar kapsamı, yükle/sil/indir/onay/ret; sınır ve SQL testleri. |
| `/api/ys-devreyeal/*`, `/api/ys-panel/*` | Kendi firma, cihaz kaynak referansı, yetki kontrolü, doğrudan kayıt, çıktı ve şubeler; gerçek akış/H/S. |
| `/api/yetkili-servisler/*` | Anonim rehber/kayıt ile eski get/güncelle/sil yolları ayrı incelendi; firma/şirket kapsamı ve B02 eski süre farkı. |
| `/api/marka/*`, `/api/urun-kategorileri/liste`, `/api/home/ozet` | Ortak referans veriler ve public özet; B01 ortak marka mutasyonu gerçekten doğrulandı. |
| `/api/ykc/*` | Talep/rapor/atama/kontrol/imza/dosya; şirket ve firma izolasyonu, özel CRM187/doğalgaz-mobile listelerinin action izinleri H/S. Gerçek dış mobil uygulamalar E. |
| `/api/entegrasyon/imza/*` | Bekleyen liste, PDF/koordinat paketi ve imzalı sonuç; 18 sözleşme testi. Gerçek sağlayıcı/kriptografik kabul E. |
| `/api/ic-tesisat/devreye-almalar/liste`, `/health` | İç tesisat entegrasyonu ve sağlık endpoint'i koddan incelendi; dış tüketici uçtan uca E. |

## 4. Öncelikli Hatalar ve Riskler

Etiketler: **Çalıştırılarak doğrulanmış hata**, **Koddan doğrulanan sorun**, **Koşula bağlı risk / ek doğrulama gerekiyor**, **Kullanım veya ürün geliştirme önerisi**. Birinci gruptaki kayıtlar, yeni iş kuralı varsayımıyla değil mevcut davranışın kanıtıyla oluşturulmuştur.

### B01. P1 - Firma İlişkisi Kontrolü, Ortak Marka Kaydını Korumuyor

**Etiket:** Çalıştırılarak doğrulanmış hata. **Rol:** Yetkili Servis; etki diğer firma ve şirketler. **URL:** `/ys-panel/markalar`; API `/api/ys-panel/markalar/duzenle`.

**Kod:** [YetkiliServisPanelYonetimApiService.cs:184](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.API/Services/YetkiliServisPanelYonetimApiService.cs:184), özellikle 193-197.

- **Beklenen:** Kendi marka ilişkisinin bulunması, ortak marka kataloğunu değiştirme yetkisi sayılmamalı. Ortak katalog yazımı ayrı yönetim yetkisinde olmalı.
- **Mevcut:** FirmaId ile kendi ilişkisinin varlığı kontrol ediliyor; sonra `bag.Marka.MarkaAdi` ve açıklama güncelleniyor. Değişen entity firmaya ait ilişki değil, ortak marka.
- **Etki:** Başka servislerin katalog ve bağlantılı kayıt sunumları etkilenir; referans verisinin sahibi belirsizleşir.
- **Tekrar üretme:** İzole servisin bağlı olduğu marka adını kendi API'sinden değiştir -> anonim marka liste API'sini oku -> yeni ad ortak listede görünür. HTTP testinde iki adım da başarılı; test adı sonrasında geri alındı.
- **Çözüm:** Servis ekranındaki ilişki yönetimini katalog yazımından ayır. Mevcut katalog yazma politikasıyla sunucuda yetki kontrolü uygula; servis kendi kapsam/seçim bilgisini yönetebilsin, ortak adı değiştiremesin.
- **Kabul:** Kendi markasına bağlı servis bile katalog adını değiştiremez; doğrudan HTTP denemesi güvenli ret verir. Genel katalog yetkili yönetici değişikliği yapabilir; firma ilişkileri ve tarihleri korunur. Diğer firma API yanıtı değişmez.

### B02. P1 - Değişmeyen Seçimler Yetki Geçmişini ve Tarihlerini Yeniliyor

**Etiket:** Çalıştırılarak doğrulanmış hata; diğer yazma yolları koddan doğrulandı. **Rol:** Firma düzenleyen yönetici/yetkili personel ve marka seçimini kaydeden servis. **URL:** `/AdminPanel/yetkiliservisler/duzenle/{id}`, personel eşdeğeri, `/ys-panel/markalar`.

**Kod:** [AdminYetkiliServisYonetimApiService.cs:196](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.API/Services/AdminYetkiliServisYonetimApiService.cs:196), 223-236; [YetkiliServislerController.cs:396](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.API/Controllers/YetkiliServislerController.cs:396), 418-426; [YetkiliServisPanelYonetimApiService.cs:104](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.API/Services/YetkiliServisPanelYonetimApiService.cs:104).

- **Beklenen:** Firma iletişimi veya aynı seçimlerle kayıt, değişmeyen marka/kategori yetkilerinin kimliğini, ilk tarihini ve süresini değiştirmemeli.
- **Mevcut:** `RemoveRange` ardından yeni ilişkiler oluşturuluyor. Yönetim/servis yolu yeni bitişi 5 yıl, eski API 1 yıl belirliyor.
- **Etki:** Tarihçe kaybolur, yetki süresi işlem yapılan endpoint'e göre değişebilir. Bu, personel şirket erişim geçmişinin korunmasından farklı bir tablodur.
- **Tekrar üretme:** Getirilen firma verisini aynı marka/kategori ID'leriyle yönetim API'sine geri gönder -> SQL ilişki satırlarını karşılaştır. Kategori ID 1 -> 3, marka ID'leri 1-4 -> 9-12 oldu; oluşturma/bitiş zamanları yenilendi.
- **Çözüm:** Seçimleri fark üzerinden güncelle; değişmeyen ilişkilere dokunma, kaldırılanları geçmişi koruyacak şekilde pasifleştir, yeni ilişkiye kurumun onayladığı tek süre politikası uygula. Firma/kategori/marka yazımı aynı transaction sınırında olsun.
- **Kabul:** Aynı payload iki kez gönderildiğinde ilişki kimlikleri/tarihleri değişmez. Bir seçim değiştiğinde yalnız ilgili ilişkiler etkilenir; diğer firma değişmez. Eski/yeni endpoint aynı süre politikasını uygular. 1 mi 5 yıl mı olduğu kurum tarafından kararlaştırılır, inceleme bunu varsaymaz.

### B03. P1 - YKC Oluşturma Tekrar Gönderime Dayanıklı Değil

**Etiket:** Çalıştırılarak doğrulanmış hata. **Rol:** Sertifikalı Firma. **URL:** `/ykc/yeni`; API `/api/ykc/talepler/olustur`.

**Kod:** [YkcTalepService.cs:253](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.Core/Business/Services/Domain/YkcTalepService.cs:253), 269-314.

- **Beklenen:** Aynı oluşturma işleminin tekrar iletilmesi ikinci bir operasyon kaydı oluşturmamalı. İlk kaydın sonucuna dönmeli veya tekrar olarak açıklamalı reddedilmeli.
- **Mevcut:** Kaynak referansı kullanıcı/şirket/tesisat açısından doğrulanıyor; oluşturulan taleple atomik tek-kullanımlık bağ kurulmadan yeni entity ekleniyor.
- **Etki:** Çift tıklama, yanıt kaybı sonrası tekrar veya ağ tekrarında mükerrer iş ve sayılar.
- **Tekrar üretme:** Tek sorgu referansıyla aynı geçerli payload'u iki kez normal firma token'ıyla gönder. Her ikisi 200; sonuç ID'leri 43 ve 44. Yalnız izole test kayıtlarıdır.
- **Çözüm:** Oluşturma işlemine kalıcı tekrar anahtarı/kaynak tüketim bağı ve veritabanı benzersizlik güvencesi ekle; sonucunu transaction içinde sakla. Sadece düğmeyi pasifleştirmek yeterli değil. Tesisata sonsuza kadar tek talep kuralı ekleme; sonraki meşru talep engellenmemeli.
- **Kabul:** Ardışık ve eşzamanlı aynı işlemden bir talep, bir başlangıç geçmişi ve bir imza süreci oluşur. Yanıt kaybı sonrası aynı anahtarla tekrar mevcut sonuca döner. Farklı ve meşru sonraki işlem ayrı kaydedilebilir.

### B04. P1 - Normal Firma JSON'unda Kaynak Proje Alanı Kalıyor

**Etiket:** Çalıştırılarak doğrulanmış hata. **Rol:** Sertifikalı Firma. **URL:** `/ykc/detay/{id}`; API `/api/ykc/talepler/getir`.

**Kod:** [YkcFirmaSunumu.cs:5](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.Core/Business/Services/Domain/YkcFirmaSunumu.cs:5); [YkcApiController.cs:534](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.API/Controllers/YkcApiController.cs:534); liste/rapor eşlemeleri [YkcTalepService.cs:1191](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.Core/Business/Services/Domain/YkcTalepService.cs:1191).

- **Beklenen:** Personele özel kaynak proje bilgileri firma için yalnız Razor'da değil API sözleşmesinde de dışlanmalı.
- **Mevcut:** Normal detayda eski marka/kapasite temizleniyor, `ProjeNo` temizlenmiyor. Sentetik özel proje numarası firmanın kendi detay JSON'unda döndü.
- **Etki:** Kullanıcı arayüzünde görünmeyen iç bilgi ağ yanıtından okunabilir. Bu bulgu başka firmanın kaydına erişim değildir; o sınır testlerde korunuyor.
- **Tekrar üretme:** Kaynağı özel proje numarası içeren firma talebini normal firma hesabıyla getir; JSON'da `projeNo` alanını kontrol et.
- **Çözüm:** Firma için açık izinli alanlardan oluşan ayrı sunum/DTO sözleşmesi veya tek merkezli temizleme uygula; normal detay, liste, rapor ve sorgu yanıtını birlikte test et.
- **Kabul:** Firma normal yanıtlarında kaynak proje alanları yok/null; personelin izinli görünümü korunur. Başka firma/şirket ID'si yine ret verir.

**Resmi FR265 istisnası ayrıca karar gerektiriyor:** `/form-verisi` yolunda `resmiForm=true` ile eski cihaz alanları kasıtlı korunuyor; firma önizlemesinde eski marka/kapasite gerçekten görüldü. Bu, "firma API'sine kaynak bilgi hiç gelmesin" sınırı ile resmi formun mevcut içeriği arasında açıklığa kavuşturulacak bir ayrımdır. Normal JSON açığını düzeltmek için imzalı/resmi formu kendiliğinden boşaltmak doğru değildir. Kurum, hangi resmi form alanlarının firmaya açık olduğunu ayrı onaylamalı; mevcut imzalı dosyalar değiştirilmemeli.

### B05. P2 - Bulunamayan YKC Dosyası, Servis Kesintisi Gibi Gösteriliyor

**Etiket:** Çalıştırılarak doğrulanmış hata. **Rol:** YKC dosyasına erişen kullanıcı. **URL:** `/ykc/dosya/{id}` ve ortak YKC çıktı istemcisi.

**Kod:** [YkcApiClient.cs:402](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Business/Services/ApiClients/YkcApiClient.cs:402), 403-407; [ApiClientFallback.cs:23](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Business/Services/ApiClients/ApiClientFallback.cs:23).

- **Beklenen:** 404 dosya bulunamadı, 403 erişim reddi, 400 geçersiz istek ile gerçekten ulaşılmayan API ayrılmalı.
- **Mevcut:** Başarısız HTTP yanıtı genel fallback hatasına dönüşüyor. Olmayan dosya ID'sinde çalışan API 404 verdiği halde MVC "Veri servisine şu anda ulaşılamıyor" dedi.
- **Etki:** Kullanıcı gereksiz tekrar dener, BT yanlış bağlantı arızası araştırır.
- **Tekrar üretme:** Firma oturumuyla `/ykc/dosya/2147483647` aç; listeye dönüşte yanlış hata toast'ı görünür.
- **Çözüm:** Diğer çıktı istemcilerindeki yapılandırılmış hata ayrımını burada da uygula. Kullanıcıya güvenli durum mesajı ver; yabancı kaydın varlığını gereksiz ifşa etme.
- **Kabul:** 400/403/404/timeout/503 ayrı test edilir; yalnız gerçek bağlantı kesintisi kesinti mesajı verir. İzinli binary indirme bozulmaz.

![Olmayan dosya için yanlış bağlantı mesajı](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/olmayan-dosya-hata-mesaji.jpg)

### B06. P2 - Detaydan Dönüşte İş Listesi Bağlamı Kayboluyor

**Etiket:** Çalıştırılarak doğrulanmış hata. **Rol:** Özellikle personel, ayrıca filtre kullanan firma. **URL:** `/ykc/talepler?bekleyenIs=inceleme&sayfa=2` -> detay -> dönüş.

**Kod:** [Detay.cshtml:15](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Views/Ykc/Detay.cshtml:15); firma dönüşü [_FirmaTalepDetay.cshtml:56](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Views/Ykc/_FirmaTalepDetay.cshtml:56).

- **Beklenen:** İncelenen iş listesinin filtre, sayfa ve şirket bağlamına dönmek.
- **Mevcut:** Kaynak türü rapor/takvim kısmen korunuyor; talep listesine dönüş düz `/ykc/talepler`. Gerçek UI turunda `bekleyenIs` ve `sayfa=2` kayboldu.
- **Etki:** Personel her kayıttan sonra aynı listeyi yeniden bulur; bekleyen iş kısayolunun operasyonel faydası azalır.
- **Tekrar üretme:** İkinci sayfadaki inceleme bekleyen kaydı aç -> Taleplere Dön -> URL'de filtre/sayfa yok.
- **Çözüm:** Güvenli yerel dönüş bağlamı taşı; farklı şirkete geçildiğinde yeni izinleri doğrula. İşlemi tamamlayınca raporlara gidip ilgili talebi açma yönlendirmesi korunmalı; bu özel başarı akışı değiştirilmemeli.
- **Kabul:** Detaydan normal dönüş aynı filtre/sayfaya gelir; dış URL kabul edilmez; şirket değişimi eski yetkiyi açmaz; mevcut tamamlanma/rapor modal akışı testleri geçer.

### B07. P2 - Ortak Devreye Alma Tablosu Dar Ekranda Kaydırma İstiyor

**Etiket:** Çalıştırılarak doğrulanmış hata (kullanılabilirlik). **Rol:** Personel/admin/servis. **URL:** `/personel-panel/devreyealmalar`, `/AdminPanel/devreyealmalar`, `/ys-devreyeal/gecmis`.

**Kod:** [operations-directory.css:60](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/wwwroot/css/operations-directory.css:60); servis minimumu 390; ortak [_DevreyeAlmaTable.cshtml](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Views/Shared/_DevreyeAlmaTable.cshtml).

- **Beklenen:** Kullanıcının dar ekranda ana kayıt ve işlem düğmesine yatay kaydırma olmadan erişebilmesi; masaüstü yerleşiminin korunması.
- **Mevcut:** Ortak tablo minimum 960 px; servis geçmişi 900 px. 375 px personel ekranında içerik kutusu 334 px, tablo 960 px, sayfa genişliği 360 px. Sayfanın tamamı taşmıyor; tablo içinde sağdaki bilgi/işlem için kaydırma gerekiyor. 320/768/1024 + açık sidebar ölçümlerinde de minimum genişlik etkisi görüldü.
- **Etki:** Sahada bir kaydın işlem düğmesine ulaşmak için sağa-sola gezinme gerekir. Fontu daha da küçültmek okunabilirliği bozabilir.
- **Tekrar üretme:** Gerçek personel listesi 375 px -> ilk görünümde sağdaki tarih/durum/işlem görünmez; tablo ölçümü kutudan geniştir.
- **Çözüm:** Yalnız dar container için mevcut kaydın ana bilgilerini ve işlem düğmesini görünür tutan responsive sunum uygula; ikincil alanları mevcut satır detayında göster. Desktop sütunlarını, ayrı tesisat/sözleşmeyi ve tam veri erişimini koru. Veriyi `overflow:hidden` ile kesme.
- **Kabul:** 320/375/768 ve sidebar açık dar dizüstünde ana kayıt/işlem yatay kaydırmasız kullanılabilir. Uzun model/adres detaydan tam okunur. Seçim sayacı yalnız ana satırları sayar; bütün rol sütunları ve PDF/Excel kapsamı korunur.

![375 px personel tablosunda sağ sütunlar ilk görünümde yok](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/personel-devreye-alma-mobil-375.jpg)

### B08. P2 - Görüntüleme Yetkili Personelde "Yönet" İfadesi

**Etiket:** Çalıştırılarak doğrulanmış hata (metin/yetki sunumu). **Rol:** Yalnız YKC görüntüleme izni. **URL:** `/personel-panel`.

**Kod:** [Index.cshtml:52](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Views/PersonelPanel/Index.cshtml:52).

- **Beklenen:** Kart ifadesi kullanılabilir işlemi anlatmalı.
- **Mevcut:** İşlem kısayolları doğru gizlenirken kart alt metni sabit "Talep ve randevuları yönet". Gerçek yetki backend'de genişlemiyor.
- **Etki:** Kullanıcı yönetim işlemi yapabileceğini sanabilir; destek ihtiyacı doğurur.
- **Tekrar üretme:** Yalnız görüntüleme hesabıyla ana sayfayı aç; bu ifade görünür, atama/tamamlama işlemleri yoktur.
- **Çözüm:** Yeni açıklama yığını eklemek yerine izinle uyumlu kısa eylem adı kullan; salt okuma için "Cihaz değişim taleplerini görüntüle" gibi.
- **Kabul:** Okuma/atama/kontrol/rapor/sıfır yetki kombinasyonlarında metin ve bağlantılar aynı yetkiyi anlatır. Sayfa yüksekliği veya takvim değişmez.

### B09. P2 - Herkese Açık Gizlilik Sayfası Şablon Metni

**Etiket:** Çalıştırılarak doğrulanmış hata (içerik). **Rol:** Anonim başvuru sahibi. **URL:** `/Home/Privacy`.

**Kod:** [Privacy.cshtml:2](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Views/Home/Privacy.cshtml:2), 6.

- **Beklenen:** Gerçek kurum tarafından onaylanmış, Türkçe ve kayıt/giriş süreçleriyle uyumlu içerik.
- **Mevcut:** "Privacy Policy" ve "Use this page to detail your site's privacy policy." varsayılan metni gerçek sayfada duruyor.
- **Etki:** Kimlik/telefon isteyen başvuruda uygulamanın tamamlanmamış görünmesine yol açar. Bu bulgu hukuki uygunluk hükmü değildir.
- **Tekrar üretme:** Anonim `/Home/Privacy` aç.
- **Çözüm:** Kurumun onayladığı metni yerleştir; giriş/kayıt sayfasından erişim ihtiyacını kurumla netleştir. Rastgele mevzuat metni veya yeni zorunlu onay akışı uydurma.
- **Kabul:** Şablon İngilizce metin kalmaz; onaylı Türkçe içerik masaüstü/mobil okunur ve kimlik verisi ifşa etmez.

![Varsayılan gizlilik içeriği](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/d3232b8_inceleme_kanitlari/gizlilik-sablon-metni.jpg)

### B10. P2 - Şube Kaydı Başarısızsa Form Bilgisi Korunmuyor

**Etiket:** Koddan doğrulanan sorun. **Rol:** Yetkili Servis; ortak yönetim yolları için eşdeğer kontrol gerekir. **URL:** POST `/ys-panel/subeler/ekle`, `/subeler/duzenle/{id}`.

**Kod:** [YetkiliServisPanelController.cs:274](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Controllers/YetkiliServisPanelController.cs:274), 305-310.

- **Beklenen:** Sunucu iş doğrulaması başarısız olduğunda girilen alanlarla aynı panelde düzeltme yapılabilmesi.
- **Mevcut:** Başarı ve başarısızlıkta aynı liste redirect'i var; hata TempData'ya yazılıyor, gönderilen alanlarla yeniden form hazırlanıp gösterilmiyor.
- **Etki:** Uzun adres/telefon tekrar girilir. Browser'ın boş alan doğrulaması bu sunucu ret yolunu tamamen karşılamaz.
- **Tekrar üretme:** Geçerli görünümlü form için iş servisi ret cevabı döndüren izole senaryo -> redirect yolunu ve alanların taşınmamasını kontrol et. Bu arıza tarayıcıda enjekte edilmedi; kod kanıtı.
- **Çözüm:** Mevcut drawer'ı koruyarak alan hatası ve gönderilmiş modelle tekrar göster; başarıda normal listeye dönüş sürsün.
- **Kabul:** Sunucu ret/timeout testinde uygun yeniden deneme yolu ve girilen bilgiler korunur; 401/403 giriş/yetki sayfası panel içine HTML olarak basılmaz; Escape/odak çalışır.

### B11. P2 - Test Beklentisi ve Demo Kullanıcı Dokümanı Güncel Değil

**Etiket:** Çalıştırılarak doğrulanmış hata (test beklentisi); doküman farkı koddan doğrulandı. **Rol:** Bakım/test ekibi. **URL:** Firma `/ykc/takvim` akışı ve test rehberi.

**Kod:** [Verify-FormCalendar.ps1:39](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Tests/YkcRules/Verify-FormCalendar.ps1:39); [yetki-test-kullanicilari.md:14](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/yetki-test-kullanicilari.md:14); hedef sürüm [TestDataSeed.cs:20](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.API/Infrastructure/TestDataSeed.cs:20).

- **Beklenen:** Firma kendi takvimini görebilir, iç ekip/personel verisini göremez; test bu ayrımı doğrulamalı. Demo şirket açıklaması seed ile uyumlu olmalı.
- **Mevcut:** HTTP script'i firmaya 403 beklediği için 200'de durdu. Ayrıca doküman demo personeli Kargaz'da tam yetkili anlatırken hedef seed Çorumgaz tam/Kargaz rapor/Sürmeli belge onay oluşturuyor.
- **Etki:** Doğru çalışan özelliğin regresyon sanılması; başka geliştiricinin yanlış şirketle test yapması.
- **Tekrar üretme:** Güncel API ile script'i çalıştır; belirtilen check başarısız. Başarısız script sonrası testler çalışmış sayılmadı. Dokümanı commit seed'iyle karşılaştır.
- **Çözüm:** 403 beklentisini körlemesine 200'e çevirme; kendi randevusu görünür, başka firma ve iç ekip bilgisi yok kontrollerini ekle. Seed/doküman/tek test komutu uyumunu sağla.
- **Kabul:** Script güncel firma takvimini doğru doğrular; yabancı firma/şirket negatif testleri geçer. Yeni temiz test DB'sindeki şirketler rehberle aynı olur.

### Koşula Bağlı Riskler

Bu tablo gerçek üretim arızası iddiası değildir. Riskin gerçekleşme koşulu ve nasıl doğrulanacağı belirtilmiştir.

| ID / öncelik / etiket | Rol, URL ve kod | Beklenen -> mevcut / etki | Doğrulama, çözüm ve kabul |
|---|---|---|---|
| R01 / P1 / Koşula bağlı risk | Servis/firma sorgusu; `/ys-devreyeal`, `/ykc/yeni`; [OnlineCihazBilgileriClient.cs:37](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.Core/Business/Services/Online/OnlineCihazBilgileriClient.cs:37) | Canlı dış sorgu güvenli taşıma kullanmalı -> istemci http ve https kabul ediyor; istemcide ortama göre HTTPS zorunluluğu yok. Production'da HTTP ayarlanırsa kaynak veri şifresiz taşınabilir. İncelemenin loopback HTTP'si üretim kanıtı değildir. | İzole Production ayarında HTTP endpoint denemesi ve yayın ayar denetimi; canlı için HTTPS startup doğrulaması, local fixture için development istisnası. Kabul: Production HTTP reddedilir, geçerli HTTPS çalışır; gerçek sertifika/TLS kabulü kurum ortamında yapılır. |
| R02 / P2 / Koşula bağlı risk | Teknik belge yükleyen personel; `/ykc/form-yukle`; [YkcApiController.cs:742](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.API/Controllers/YkcApiController.cs:742) | Dosya/DB tutarlılığı -> fiziksel yazım sonra DB/hash çağrısı; sadece `sonuc.Basarili=false` yolunda silme var. İstisna halinde sahipsiz dosya kalabilir. Böyle bir arıza enjekte edilmedi. | Copy/hash/DB kaydı arızalarını izole testte üret; temp dosya + başarılı kayıt sonrası finalize ve başarısızlık telafisi önerilir. Kabul: başarısız yüklemede orphan kalmaz, eski/imzalı dosya değişmez; iptal/yeniden deneme güvenli. |
| R03 / P2 / Koşula bağlı risk | Dış servis kullanan roller; sorgu endpoint'leri; [OnlineCihazBilgileriClient.cs:61](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.Core/Business/Services/Online/OnlineCihazBilgileriClient.cs:61) | Kişisel verisiz teşhis -> hata response body bütünü loglanıyor. Sağlayıcı hata gövdesinde müşteri bilgisi döndürürse logda kalabilir. Gerçek sağlayıcı gövdesi görülmedi. | Sentetik kişisel alanlı 500 SOAP yanıtı ver; ham gövde yerine güvenli hata kodu/correlation ve sınırlandırılmış alanlar. Kabul: kimlik/telefon/adres/credential loga yazılmaz; BT hata kökenini yine bulabilir. |
| R04 / P2 / Koşula bağlı veri minimizasyonu kararı | Devreye alma çıktısı gören roller; tek/rapor Excel; [DevreyeAlmaExcelService.cs:11](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.Core/Business/Services/Export/DevreyeAlmaExcelService.cs:11), 23 | Standart çıktı yalnız gerekli alanları taşımalı -> T.C. kimlik no sütunu ve eski kayıttaki `MusteriTcNo` aktarılıyor. Başka firma sızıntısı doğrulanmadı; alanın gerekliliği kararlaştırılmamış. | Kurum standart raporun ihtiyaç duyduğu alanları onaylasın; gerekmiyorsa standart çıktıda çıkar/maskele, gerçekten gerekli ayrı yetkili çıktıda açıkça sınırla. Kabul: eski dolu/boş kayıt, bütün rol çıktıları ve sütun/değer hizası test edilir. |
| R05 / P3 / Ölçeklenme riski, şu an yavaşlık iddiası değil | Admin/personel devreye alma listesi; [AdminRaporApiService.cs:45](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.API/Services/AdminRaporApiService.cs:45) | Sayfa için sınırlı yükleme -> bütün eşleşmeler `ToListAsync`. 33/34 sentetik kayıtta hata yok; büyük veri yük testi yapılmadı. Ayrıca çalışma logunda EF birden çok collection include uyarısı vardı; bu N+1 kanıtı değildir. | Kullanıcının belirttiği gibi büyük veri işi sonraya bırakılabilir. Önce süre/sorgu planı/bellek ölç; gerekirse sunucu sayfalama/projection. Kabul: toplam sayı, filtre, seçili çıktı ve gizli detay satırları aynı kalır; karşılaştırmalı ölçüm kaydedilir. |
| R06 / P2 / Platform doğrulaması gerekiyor | PDF kullanıcıları; [DevreyeAlmaPdfService.cs:20](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.Core/Business/Services/Export/DevreyeAlmaPdfService.cs:20), diğer rapor PDF servisleri | Hedef ortamda aynı okunabilir PDF -> bazı üreticiler Arial'a bağlı; FR265'te font fallback var. Windows PDF testleri geçti, Linux yayın ortamı test edilmedi. "Fontlar bozuk" diye raporlanamaz. | Gerçek yayın OS/container'ında Türkçe, uzun içerik ve çok sayfa PDF üret; gerekiyorsa lisansı uygun paketli font/fallback. Kabul: eksik glif, taşma, boş sayfa yok; mevcut FR265 ve imzalı snapshot değişmez. |
| R07 / P1 / Harici kabul gerekiyor | YKC imza ve giriş/sorgu; [Onur-Mobil-Imza-Sozlesmesi.md:94](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/Docs/Onur-Mobil-Imza-Sozlesmesi.md:94) | Gerçek imza/OTP/kaynak dönüşleriyle uçtan uca kanıt -> sözleşme ve demo testleri var, gerçek sağlayıcılar kullanılmadı. PDF tür/hash kontrolü kriptografik imza geçerliliği değildir. | Yetkili test ortamında sağlayıcıyla resmi form koordinatı, imzacı/sürüm/hash, tekrar callback, gecikme, TLS ve doğrulama kararı kabul edilir. SMS gerçek test alıcısı ancak ayrıca onayla. Kabul: demo üretimde kapalı, hatalı/tekrar callback kontrollü, nihai dosya ve signer kanıtı doğru. |

**FR265 kaynak alanları:** DTO eşlemesinde tüketim noktası gibi bazı alanlar boş varsayılanlarla geliyor ([YkcTalepService.cs:1873](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.Core/Business/Services/Domain/YkcTalepService.cs:1873)). Dış servis bunları henüz sağlamıyorsa değer uydurulmamalı. Kurum hangi alanları hangi aşamada zorunlu gördüğünü ve kaynak sözleşmesini doğrulamalı. Bu inceleme yeni imza/uygunluk engeli önermiyor.

## 5. Rol Bazında Kullanım İyileştirmeleri

Bu bölüm **Kullanım veya ürün geliştirme önerisi** niteliğindedir; doğrulanmış hata B01-B11 ile karıştırılmamalı. Yeni kart veya ekran çoğaltmadan mevcut işi kolaylaştırmak önceliklidir.

| ID / öncelik / rol | Ekran ve kod dayanağı | Somut ihtiyaç, mevcut durum ve etkisi | Öneri, gözlem yolu ve kabul |
|---|---|---|---|
| U01 / P2 / Personel | Bekleyen iş listesi ve detay; B06 kodları | Kısayollar ve doğru sayılar zaten var; detaydan sonra listeyi yeniden kurmak gerekir. | Yeni "bekleyen işler" sayfası ekleme; B06 ile bağlamı koru. Kabul: aynı listede art arda 5 kayıt incelenirken filtre yeniden girilmez. |
| U02 / P2 / Firma | `/ykc/detay`; [YkcDurumSunumu.cs](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Models/YkcDurumSunumu.cs) | Aşama ve son gerekçe görünür; yeni başlayan kullanıcının kendisinden mi şirketten mi adım beklendiğini anlaması önemli. | Var olan durum ifadesini kurumun süreç adlarıyla kısa ve tutarlı tut; fazladan yardım paragrafı/kutu ekleme. Gözlem: yeni kullanıcıyla mevcut örnek durumları okut. Kabul: sorumlu taraf ve mevcut sonraki adımı kullanıcı doğru söyler; yeni durum üretilmez. |
| U03 / P2 / Servis | `/ys-devreyeal`; [Index.cshtml](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Views/DevreyeAlma/Index.cshtml) | Yetki engeli zamanında, başarılı kayıt bağlantısı doğru. Tablet kullanımında form ve sonuç/çıktı erişimini aynı kalitede tutmak gerekir. | Yeni randevu/approval adımı ekleme; B07 ve B10'u uygula. Kabul: cihaz geçişi alan karıştırmaz, form aç/kapat veriyi korur, başarı kaydı tek hareketle açılır. |
| U04 / P2 / Şirket yöneticisi | `/AdminPanel/raporlar`; [Raporlar.cshtml:113](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Views/AdminPanel/Raporlar.cshtml:113) | Ortalama tamamlanma, tekrar randevu oranı, süreç/tamamlanma dağılımı zaten mevcut; sadece Excel'e dayanılıyor demek yanlış. Yaşlı bekleyen kayıtları önce görmek ayrı bir karar ihtiyacı olabilir. | İhtiyaç onaylanırsa mevcut bekleyen listeye yaşa göre sıralama/filtre ekle; ayrı KPI kartı veya yeni rapor sayfası şart değil. Kaynak TalepTarihi/durum/geçmiş; eşik kurumca belirlenir. Kabul: şirket ve tarih kapsamı sabit, tamamlanan kayıt bekleyen yaşına girmez, boş veri "0 saat" gibi yanıltılmaz. |
| U05 / P2 / Genel admin | Şirket bağlamı, kullanıcı/yetki ekranı; [Yetkiler.cshtml](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Views/AdminPanel/Yetkiler.cshtml) | Kompakt liste ve arama artık var; 51 personelde tüm yetkileri her satıra dökmek gerekmiyor. Kurulum/kesinti etkisini kurum bazında bilmek henüz kullanıcı ekranının görevi değil. | Kompakt liste/drawer'ı koru; entegrasyon durumu için C01'i ancak işletme ihtiyacı varsa ekle. Kabul: şirket seçimi ve satır bağlamı açık, genel admin ile seçili şirket TAM_YETKI ayrı kalır. |

**Raporların güncelliği:** Bu sürüm istek anında DB'den özet alıyor; canlı push ile sürekli yenileme görülmedi. "Gerçek zamanlı" ifadesini otomatik refresh garantisi gibi kullanmamak gerekir. İşlemden sonraki yeni okumada sayaçlar güncelleniyor; SQL testleri bunu doğruladı. Sürekli ekran izleme ihtiyacı yoksa SignalR eklemek zorunlu değil.

## 6. Gerekçeli Yeni Sayfa / İşlev Önerileri

### C01. P2 - İsteğe Bağlı "Entegrasyon Durumu" Görünümü

**Etiket:** Kullanım veya ürün geliştirme önerisi. **Rol:** Genel admin; gerekirse kendi şirketiyle sınırlı şirket admini. **Önerilen URL:** Kurum kabul ederse yönetim altında tek bir sağlık/durum görünümü; mevcut bir URL'nin çalıştığı iddia edilmiyor.

**Dayanak:** [SystemHealthApiController.cs:32](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.API/Controllers/SystemHealthApiController.cs:32), 78-104. Endpoint var; DB bağlantısını gerçekten kontrol ediyor, SMS/online service için çoğunlukla ayar varlığını raporluyor. Bu, sağlayıcıya ulaşılabildiğinin veya SMS'in teslim edildiğinin kanıtı değil.

- **İhtiyaç:** BT, "hangi şirketin tesisat sorgusu/OTP/imza işlemi etkileniyor?" sorusunu kayıt ekranında tekrar deneyerek araştırmamalı.
- **Neden mevcut ekranda değil:** Operasyon ekranları kullanıcının kaydını bitirmeye odaklı. Ayrıntılı altyapı teşhisi firma/personel ekranını kalabalıklaştırır ve gereksiz teknik bilgi açabilir. İlk aşamada yeni sayfa yerine kurumun mevcut izleme aracına `/health` bağlamak daha küçük çözüm olabilir.
- **İçerik:** API/DB durumu, yapılandırma kontrolü ile gerçek son başarılı çağrının ayrı gösterimi, etkilenen şirket/servis, güvenli hata kodu, son kontrol zamanı ve correlation ID. Anahtar/parola/token/ham müşteri yanıtı gösterilmez.
- **Veri:** Mevcut health cevabı + uygulama loglarından güvenli, erişim kontrollü son çağrı özeti; gerekli şirket eşlemesi ve saklama süresi kurumla kararlaştırılır.
- **Yetki:** Genel admin tüm izinli şirketleri; şirket admini yalnız kendi şirketini görür. Public health detaylarıyla gizli ayarları açma. Personel/firma/servise yönetim teşhisi verilmez.
- **Kolaylaşan adım:** Kullanıcıdaki hata -> hangi entegrasyonun etkilenmesi -> BT teşhisi. Bu ekran gerçek SMS göndermez, tekrar imza/gaz işlemi tetiklemez.
- **Kabul / tekrar üretme:** Bir test şirketinin endpoint'ini kapat -> ayar varlığı "ulaşılabilir" diye sunulmasın; diğer şirket etkilenmiş gösterilmesin. 401/403 ve redaction testleri olsun; monitoring yoksa aynı bilgiyi mevcut BT aracında doğrula.
- **Zorunluluk:** Yeni sayfa zorunlu değil; üretim için güvenilir izleme/teşhis ve harici kabul gereklidir. B01-B04'ten sonra değerlendirilir.

**Önerilmemesi gereken gereksiz genişlemeler:** Stok sayımı, GPS saha takibi, WhatsApp Business veya yeni müşteri imza modülü bu incelemede zorunlu ihtiyaç olarak kanıtlanmadı. Uygulama içi bildirim menüsü zaten var; "bildirim sistemi yok" denemez. Gerçek push, randevu mesajı ve teslim takibi ayrı alıcı/metin/onay politikası gerektirir; rastgele yeni modül olarak eklenmemeli. Mevcut PDF üretimi ve YKC imza yapısı araştırılmadan ikinci bir belge sistemi kurulmalı denemez.

## 7. Kaldırılacak veya Sadeleştirilecek Yapılar

Bu bölüm **Kullanım veya ürün geliştirme önerisi** / bakım sadeleştirmesidir. Dosyanın kullanılmadığını yalnız adı veya eski görünümü üzerinden varsayarak silmek önerilmez.

| ID / öncelik | Kapsam, URL ve kod | Sorun/etki, öneri ve kabul |
|---|---|---|
| D01 / P2 | Firma ilişki yazımı; B02'nin üç kod yolu | Aynı işlem farklı süre ve geçmiş davranışı taşıyor. B02 düzeltmesini tek sorumlu ilişki güncelleme servisine taşı; eski API'yi önce aynı servise yönlendir. Tüketici envanteri çıkmadan endpoint silme. Kabul: tüm yollar aynı süre/ID/geçmiş testlerini geçer. |
| D02 / P2 | Çıktı istemcileri; B05 | YKC'de farklı hata ayrımı bakımda tutarsızlık üretiyor. Küçük ortak HTTP hata dönüştürücüsü, var olan istemci örüntüsüyle uyumlu olmalı; dev bir API abstraction kurulmasın. Kabul: dosya status/binary testleri tüm istemcilerde doğru. |
| D03 / P3 | Aynı tablo/şube ekranına ait eski düzenleme görünümleri; `_BranchEditor`, `_BranchSheet` ve `SubeDuzenle.cshtml` | Yeni ortak bileşenler doğru; doğrudan URL için kullanılan eski view dosyaları hâlâ işlevsel olabilir. Reachability ve route kullanımını kanıtlamadan silme. Kabul: doğrudan link, JS'siz fallback, drawer aç/kapat ve her role ait endpoint çalışır. |
| D04 / P2 | Tests ve Docs; B11 | Test script'i, fixture ve seed ayrı ayrı süreç anlatıyor. Tek çalıştırma rehberi, ortam önkoşulları ve katmanlı test raporu oluştur; eski test expectation'larını düzelt. Kabul: temiz makine/DB'de aynı adımlar çalışır, test sayıları fixture ile gerçek UI'yı karıştırmaz. |
| D05 / P3 | Büyük MVC controller/inline JS alanları; [YkcController.cs](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Controllers/YkcController.cs), [Detay.cshtml:769](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma/Views/Ykc/Detay.cshtml:769) | Akış/redirect/form taşıma bilgisi birçok yerde. Yalnız B06/B05 gibi ihtiyaç doğuran parçayı ayır; geniş refactor canlıya hazırlık şartı değil. Kabul: aynı route, izin ve iş davranışı; mevcut gezinme/iş akışı testleri geçer. |

Yetki kartlarında tekrar açıklama, aynı anda birkaç seçili/tüm çıktı düğmesi veya "özellikleri anlatan" uzun metinleri geri eklemek önerilmez. Mevcut kompakt düzen ve kullanıcı tercihleri korunmalı.

## 8. Korunacak Doğru Uygulamalar

1. **Şirket/firma izolasyonu iki katmanda var.** Yabancı firma detayları 404, yabancı şirket erişimi 403; ayrı şirket admini ve sınırlı personel hesaplarıyla gerçek HTTP'de doğrulandı. Bu örnekler bütün endpoint'ler için otomatik garanti değildir, ancak ortak görünümün tek başına veri yetkisini birleştirdiği görülmedi.
2. **Personel izinleri modül/işlem bazında.** Görüntüleme, atama, kontrol-imza, rapor ayrı. Yönetim hesabıyla gezmek yerine ayrı kombinasyonlar test edildi. Menü gizleme tek güvenlik önlemi değil; API sınırı da kontrol ediyor.
3. **YKC yetkisi yokken sade takvim var.** Randevu verisi ve bağlantıları yok; izin verilince geri geliyor. Mevcut takvim şemasını yeniden tasarlamaya gerek yok.
4. **Devreye alma doğrudan tamamlanıyor.** Servis cihazı sorguluyor, yetkisini öğreniyor ve kaydediyor. Personel onayı yeni bir süreç olarak eklenmemeli.
5. **Devreye alma kaynak referansı ve tekrar koruması güçlü.** Sunucu kaynak alanlarını client'tan kabul etmek yerine referans üzerinden tamamlıyor; süresi dolmuş, başkasına ait ve tüketilmiş referanslar SQL testlerinde reddedildi. YKC oluşturmadaki B03 ile karıştırılmamalı.
6. **Beşli teknik kontrol dönemi ve tarihçe korunuyor.** SQL senaryosu 11. kontrole/üçüncü döneme kadar ilerledi; dönem yeniden planlaması ikinci beşliyi çoğaltmadı, resmi form slotları 1-5 kaldı. Önceki kayıtlar silinmedi.
7. **Randevu 30 dakikalık kuralı mevcut.** 00/30 slot doğrulaması ve aynı ekip/personel için aralık kuralı iş kurallarında test edildi. Ekip atama akışı [YkcTalepService.cs:344](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.Core/Business/Services/Domain/YkcTalepService.cs:344) gibi SQL transaction/UPDLOCK sınırları kullanıyor. RowVersion görmemek tek başına "eşzamanlılık yok" kanıtı değildir; yüksek yükte deadlock/yeniden deneme ayrıca ölçülmeli.
8. **Kapasite karşılaştırması uyarıdır.** Yeni > eski ise tadilat uyarısı; eşit/düşükte bu uyarı yok. Marka/baca farkı ayrı değerlendirilir. Uyarı varken sentetik talep oluşturuldu. Kaynak baca null/eksik olduğunda çökme veya sahte fark oluşmadı; müdürden gelecek veri uydurulmadı.
9. **Belge geçerliliği ayrılıyor.** Geçerli eski belge varken bekleyen yenileme kaydı yanlış engel oluşturmadı; gelecekte başlayan/süresi biten belge koşulları SQL/iş kuralı testlerinde ayrı.
10. **İmzalı nihai belge snapshot/hash yaklaşımı korunmalı.** Eşzamanlı imza sorgularında tek nihai PDF ve geçmiş olayı test edildi. Önizlemeyi güncellemek, geçmiş imzalı PDF'yi yeniden üretme yetkisi değildir. Önizlemede kişi/tarih bulunması kişinin imzalamış sayılması demek değil; imza işaretleri ayrı tutuluyor.
11. **Ortak bileşenler yararlı.** `_DevreyeAlmaTable`, `_BranchEditor`, `_BranchSheet` ve yetki listesi ortaklaştırması rol varyantlarındaki görsel ayrışmayı azaltıyor. B07/B10 ortak bileşen sınırında düzeltilebilir; ayrı rol kopyaları üretmek gereksiz.
12. **Excel gerçek veri türleri kullanıyor.** Tarih ve kapasite native türler; tesisat/sözleşme gibi baştaki sıfırı önemli numaralar metin olarak korunuyor. Formül benzeri serbest metin güvenli yazılıyor. Tek bir "bütün değerleri string yap" yaklaşımına dönülmemeli.
13. **Rapor sayaç/filtre tutarlılığı düzeltilmiş.** Belge durum/tarih/şirket sayaçları ve çıktıları için yeni SQL kontrolleri geçti. "Onay bekleyen 2, onaylanan 3" eski bulgusu bu sürümde tekrar doğrulanmadı; açık hata gibi listelenmedi.
14. **5000 çıktı sınırı sessiz kesme değil.** YKC API fazla kaydı kullanıcıya açıklayarak reddediyor ([YkcApiController.cs:329](C:/Users/byildiz/source/repos/YetkiliServisGazAcma/YetkiliServisGazAcma.API/Controllers/YkcApiController.cs:329)). Büyük veri geliştirmesi ertelenebilir; mevcut davranış mükerrer talep veya eksik çıktı hatasıyla karıştırılmamalı.
15. **Takvimde 25 sınırı otomatik kayıp kanıtı değil.** Sayfalı yakın randevu listesi ile ay/gün verisinin ayrı alınması korunuyor; SQL/gezinme testleri mevcut. Eski "yalnız 25 randevu var" iddiasını bu sürüm için doğrulanmış hata saymadım.
16. **Injection kontrolünde somut açık saptanmadı.** İncelenen Razor görünümlerinde `Html.Raw` kullanımı bulunmadı; SQL kilit sorguları parametreli `FromSqlInterpolated` veya parametreli işlemler. Dinamik kullanıcı girdisiyle birleştirilmiş SQL bulunmaması bütün uygulama için tam penetrasyon testi sertifikası değildir.
17. **MVC'nin API'ye bağımlılığı görünür.** Testte DB fallback kapalıydı; API hatası gizlice web DB yazımına dönmedi. Şirket değişimi ve oturum sonlanması mevcut testlerde korundu.

## 9. Çalıştırılan Testler ve Doğrulanamayan Alanlar

### 9.1. Yeniden Çalıştırılan Testler

Commit sahibinin beyanı kopyalanmadı; aşağıdakiler hedef sürümün izole kopyasında yeniden çalıştırıldı. Gruplar arasında ortak test/prologue olabilir; sayılar birleştirilerek bağımsız kapsam yüzdesi verilmez.

| Katman | Test / sonuç | Gerçekte neyi kanıtlar? |
|---|---|---|
| Derleme | `dotnet build YetkiliServisGazAcma.slnx --artifacts-path ../artifacts -p:NuGetAudit=false -m:1 -nr:false`: 0 hata/0 uyarı | Bu ortamda derlenebilirlik; paket güvenlik denetimi kapalı olduğundan zafiyet taraması başarısı değildir. Çalışma anındaki EF uyarıları ayrıdır. |
| İş kuralı / belge | YkcRules: 149 başarılı | Kapasite, randevu slotları, beşli dönem, null baca, çıktı türleri/PDF içerik senaryoları; gerçek dış servis değil. |
| MVC gezinme | WebNavigation: 152 başarılı | Controller doğrudan çağrısı/mock HTTP, redirect/filtre/izin sunumu ve dosya hata örnekleri; gerçek middleware zinciri değil. |
| Oturum bileşenleri | WebSession: 27 başarılı | Token/cookie/rol uyumu yardımcıları; bütün gerçek browser session akışı değil. |
| İmza sözleşmesi | MobilImza: 18 başarılı | Şirket anahtarı/kapsam, paket/queue/hash sözleşmeleri; gerçek e-imza doğrulaması değil. |
| SQL iş akışı | YkcWorkflow: 205 başarılı; iç servis kapsam/public kayıt grubu 36 başarılı | Gerçek LocalDB üzerinden dönem/geçmiş/işlem/yarış ve hesap kapsamları; birçok controller doğrudan çağrılır. |
| SQL devreye alma | `--commissioning-only`: 49 başarılı | Belge/marka/kategori/ref tüketimi, tekrar kayıt, eski sözleşme numarası tamamlama. |
| SQL rapor/dashboard | `--panel-reports-only`: 60 başarılı | Şirket/tarih/durum/grup/sayaç/liste tutarlılığı ve bekleyen iş filtreleri. |
| SQL şema | `--schema-only`: 7 başarılı | İzole eski şema yükseltme ve ikinci çalışma idempotency; gerçek DB'ye migration uygulanmadı. |
| HTTP/middleware | Verify-ApiBoundary: 76 başarılı | Çalışan API'ye gerçek normal token ve izin sınırı istekleri. |
| HTTP yeni talep sınırı | Verify-NewRequest: 9 başarılı | Kaynak sorgu/redaction, uygunsuz referans ve yetkisiz rol; başarılı yeni iş kaydı oluşturmayı tek başına kapsamaz. |
| HTTP/MVC oturum | Verify-WebAuth: 7 başarılı | Süresi dolan oturumun girişe dönmesi/cookie temizliği; yalnız test hesabının stamp'i değişti. |
| Tarayıcı fixture | BrowserForms: 123 başarılı, 0 başarısız | Production JS/CSS kullanan HTML fixture'da aç/kapat, hızlı cihaz geçişi, form, seçim ve drawer regresyonları. Gerçek rol sayfalarına eşdeğer değil. |
| Ek gerçek HTTP senaryosu | 33 gözlem: 30 beklenen, 3 sapma | B03, normal JSON kaynak alanı ve resmi form istisnası gözlendi. Resmi form sapması kurum kararı notuyla raporlandı; üç ayrı bağımsız güvenlik açığı sayılmadı. |
| Ek HTTP + SQL | Aynı firma seçimleri ve ortak marka düzenleme | B01 ve B02 gerçekten tekrar üretildi. Ortak marka sentetik değeri test sonrası geri alındı. |
| Gerçek tarayıcı yolculukları | Bölüm 3'te belirtilen sayfa/işlemler, ayrı rollerle normal giriş | Servis kayıt tamamlama; firma uyarılı talep; gerekçe açma; şirket değişimi; şube ekleme; drawer Escape/odak; rol listeleri. Resmi bir tüm-düğmeler test sayısı uydurulmadı. |

**Başarısız kalan mevcut test:** Verify-FormCalendar, firma takvimine 403 bekleyen check'te durdu. Bu test "geçti" diye raporlanmadı; sonraki adımları çalışmış sayılmadı. Güncel uygulamanın firma takvimi 200 olması tek başına erişim açığı değil; kendi veri kapsamı korunuyor. B11 güncellemesi gerekir.

**Çalıştırılmayan script'ler:** Verify-AuthApi ayrı SMS açık/kapalı API ortamları gerektiriyor; Verify-DashboardCalendar sabit Eylül test kayıtlarını bekliyor. Bunların yerine geçen bazı kurallar başka gruplarda test edilse de script'lerin kendisi başarılı sayılmadı.

### 9.2. Senaryo Kanıtı ve Sınırlar

| Senaryo | Yapılan | Yapılmayan / kalan kabul |
|---|---|---|
| Az/çok kayıt, uzun içerik | 51 personel, 33/34 commissioning kaydı, 40+ talep; uzun model/adres/isim/gerekçe | Binlerce kayıt performans testi; mevcut hız için olumsuz ölçüm iddiası yok. |
| Boş/yetkisiz durum | Sıfır izinli personel gerçek ekranı; boş listeler SQL/gezinme; başka şirket/firma HTTP | Sıfırdan yeni servis ilk kurulumunun bütün UI adımları tek yolculukta tamamlanmadı. |
| Eksik/hatalı/ref süresi | Geçersiz ref HTTP; süresi dolmuş/başka kullanıcının ref'i SQL; istemci fixture doğrulaması | Bütün kombinasyonlar gerçek formda tek tek gönderilmedi. |
| Oturum süresi/yetki kaldırma | HTTP/MVC expiry, session ve SQL yetki kaldırma; drawer auth-redirect kodu/fixture | Her sayfa açıkken gerçek browser oturumunu sona erdirip her düğmeye basma yapılmadı. |
| Dış servis hatası | Yerel SOAP fixture 503/uyuşmayan kaynak; yapılandırılmış başarısız cevap | Gerçek sağlayıcı kesintisi, gerçek SMS teslimi ve canlı imza E. |
| Tekrar/eşzamanlılık | B03 aynı istek gerçek HTTP; commissioning tüketim/DB kontrolleri; concurrent imza polling SQL | Ağ kesilmesi/yoğun üretim trafiği/deadlock yük testi; her işleme RowVersion şartı varsayılmadı. |
| PDF/Excel | Gerçek API binary yanıtları; PDF/Excel üretim testleri; FR265 gerçek browser canvas/screenshot | Excel download olayını tarayıcı aracı yakalarken takıldı. Bu araç sorunu uygulama hatası sayılmadı; bütün MVC download'ları ve seçili çıktı uçtan uca başarılı ilan edilmedi. |
| Responsive | 320/375/768/1024 ve masaüstü; B07 gerçek DOM ölçümü + 375 px screenshot | Her sayfanın her role ait bütün breakpoint kombinasyonu taranmadı; yüzde 200 yakınlaştırma uygulanamadı/doğrulanmadı. |
| Klavye/erişilebilirlik | Gerçek Enter ile gerekçe açma; drawer Escape/odak dönüşü; fixture klavye kontrolleri | Tam ekran okuyucu/kontrast/WCAG denetimi, touch cihaz ve OS klavye matrisi yapılmadı. |
| Depolama/yayın | Private storage/hash akışı ve dosya erişim kontrolleri kod/test | Gerçek disk ACL, yedek/geri yükleme tatbikatı, Linux fonts, proxy/TLS ve sağlayıcı credential kurulumları E. |

### 9.3. Kanıtların Yeri

- Kalıcı sentetik ekran görüntüleri: `Docs/d3232b8_inceleme_kanitlari/`.
- Ayrıntılı geçici test logları ve izole kaynak: `C:/Users/byildiz/AppData/Local/Temp/ysga-review-d3232b8-20261006-d92b5640/`. `test-*.log`, `http-*.log`, `http-review-results.json`, `rights-before.json`, `rights-after.json`, `shared-brand-review.json` bu çalışmaya aittir. Temp klasör kalıcı arşiv garantisi vermez; önemli sonuçlar bu belgede yer alır.
- İnceleme sırasında açılmış test sunucuları ve tarayıcı sekmeleri iş bitiminde kapatılır; kullanıcının kendi sunucusu/sekmeleri kapatılmaz. İzole DB ve temp test çıktıları, kullanıcı dosyalarını silmeme sınırı nedeniyle otomatik toplu temizlemeye alınmaz.

## 10. Uygulama Sırası, Bağımlılıklar ve Kabul Testleri

**Bu sıra uygulanabilir iş listesidir; bu incelemede düzeltme yapılmadı.** Önce dört önemli veri/yetki sorunu, sonra günlük akış, ardından yayın kabulü ve ihtiyaç varsa ürün geliştirmesi.

| Sıra | İş / sorumlu alan | Bağımlılık | Tamamlanmış sayılacağı kabul |
|---|---|---|---|
| 1 | B01 ortak katalog yazma sınırı - API/servis | Katalog sahipliği politikası mevcut genel yönetimle uyumlu netleştirilir. | Kendi markasına bağlı servis global kaydı değiştiremez; yetkili katalog admini değiştirebilir; diğer firma verisi sabit. |
| 2 | B02 ilişki geçmişi - domain/DB/API | Geçerlilik süresi kurumca tekleştirilir; eski API tüketicileri bulunur. | Değişmeyen payload ilişki ID/tarihini korur; ekle/kaldır geçmişi korunur; eski/yeni endpoint eşdeğer. |
| 3 | B03 YKC tekrar koruması - domain/DB | İşlem anahtarı ve mevcut sonucu döndürme sözleşmesi belirlenir. | Ardışık/parallel/retry bir talep oluşturur; başlangıç kayıtları tek; sonraki meşru talep engellenmez. |
| 4 | B04 firma sunum sözleşmesi - API/DTO | Normal ekran vs resmi FR265 izinli alanları kurumca ayrılır. | Normal firma JSON'unda proje/iç alan yok; list/detail/report/query aynı politika; resmi/imzalı form keyfi değişmez. |
| 5 | B05 dosya hata ayrımı - MVC istemci | Mevcut çıktı istemcisi örüntüsü kullanılır; response body güvenli parse edilir. | 400/403/404/503/timeout + izinli binary; hata farklılaşır, kaynak/kimlik sızmaz. |
| 6 | B06 liste dönüşü + B08 kısa rol metni - MVC/View | Yerel URL güvenliği ve şirket değişimi davranışı korunur. | Aynı filtre/sayfa korunur; salt okuma "yönet" demez; tamamlanma -> rapor/modal akışı bozulmaz. |
| 7 | B07 responsive ortak tablo - UI | Masaüstü sütunları/ayrı tesisat-sözleşme/çıktı şeması korunur. | 320/375/768/dar dizüstünde ana kayıt+işlem erişilebilir; bilgi kesilmez; tüm rol/uzun içerik/seçim/klavye testleri. |
| 8 | B10 şube hata dönüşü - ortak drawer/MVC | Form modelini başarı/ret yollarında taşıma sözleşmesi. | Ret/timeout girdiyi korur; auth hatası güvenli yönlenir; Escape/focus/race testleri geçer. |
| 9 | B09 içerik + B11 test/doküman uyumu | Kurum onaylı metin; firma takvimi kapsamının güncel kararı. | Şablon yok; temiz seed rehberle aynı; güncel takvim testi kendi/yabancı kapsamını doğrular. |
| 10 | R01/R02/R03/R06/R07 yayın kabulü | Hedef sunucu, onaylı servis test hesapları, depolama/backup/TLS ayarları. | HTTPS kuralı, veri log redaction, file/DB telafisi, hedef OS PDF, gerçek sağlayıcı/hash/signer/retry ve geri yükleme tatbikatı. |
| 11 | R04 standart Excel veri minimizasyonu | Kurumun alan gerekliliği onayı. | Her rol/eski kayıt/başta sıfır/sütun hizası; gerekli olmayan kimlik aktarılmaz. |
| 12 | U04 / C01 / R05 isteğe bağlı geliştirme | Gerçek işletme ihtiyaç ve ölçümleri; kritik işler kapalı. | Mevcut ekrana fayda ölçülür; gereksiz sayfa/kart olmaz; şirket izolasyonu korunur; büyük veri değişikliği ölçümle doğrulanır. |

### Yönetici Gözüyle Canlı Kullanım Öncesi Görmek İstediğim Temel Düzeltmeler

1. Bir servis başka firmaların ortak referans verisini değiştiremesin; firma düzenleme yetki tarihçesini bozmasın.
2. Aynı talep tekrar iletildiğinde ikinci iş oluşmasın; firma API'si personele özel proje bilgisini taşımasın.
3. Personel başladığı iş listesine geri dönebilsin; dosya hatasında gerçek nedeni görsün; dar ekranda ana kayda ve işlem düğmesine ulaşabilsin.
4. Testler güncel süreçle uyuşsun; gerçek dış servis, imza, hedef ortam PDF/TLS/depolama ve geri yükleme için ayrı kabul kaydı olsun.

Takvim, genel tasarım, doğrudan devreye alma, beşli kontrol geçmişi, uyarıların engel olmaması ve rol ayrımı korunmalı. Daha fazla modül eklemeden önce mevcut işlemlerin güvenilirliği ve günlük kullanım sürtünmesi giderilmeli.
