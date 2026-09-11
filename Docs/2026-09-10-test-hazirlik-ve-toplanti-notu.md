# Test Hazırlığı ve Müdür Görüşmesi

Tarih: 10.09.2026. Kaynak: mevcut yerel kod, yerel API kontrolleri ve gerçek tarayıcı incelemesi.
Bu belge üretim veya entegrasyon kabul onayı değildir. Eski “13 tamam / 5 hazır / 5 bekliyor” sayımı esas alınmadı.
Hiçbir harici sunucuya yayın yapılmadı. Önizleme yalnızca bu bilgisayardaki `127.0.0.1:55222` adresinde çalıştırıldı.

## Son Beş İsteğin Karşılığı

1. Girişte ortak üst başlık eklendi. Masaüstünde görsel genişliğin 3/12'sini, form 5/12'sini kullanır; görsel ile form arasında 2/12 boşluk vardır. Renkler projeye aittir. “Online İşlemler” veya Aksa sosyal hesapları eklenmedi.
2. Tüm rollerin ortak sidebar logo alanında yalnızca mevcut şirket logosu kaldı. Hesap, rol ve şirket bilgileri sağ üst alandadır.
3. Sertifikalı firma detayının ayrı, salt-okunur görünümü oluşturuldu. Personelin gelecek adımları, sağlayıcı ismi ve tekrar eden “Önizlemeye Hazır” alanları kaldırıldı. Kontrol randevusunun dağıtım şirketinin atadığı ekibe ait olduğu belirtilir. Tesisat, yeni cihaz, firma ve geçmiş görünür bölümlerdedir.
4. Talep listesindeki tekrar başlık kaldırıldı. Müşteri ve cihaz türü ayrı sütunlar; tesisat/sözleşme/abone numaraları iki noktalı etiketler; marka ve kapasite ayrı alanlardır. API eski birleşik metinleri uyumluluk için korur, yeni tablo tipli alanları kullanır.
5. Çok şirket seçimi AJAX ile mevcut giriş formunun yerine açılır. Tarayıcıda URL `/giris` olarak kaldı; form ve şirket seçiminin sol/üst konumu ve genişliği aynı ölçüldü (1440 piksel ekranda x=600, y=108, genişlik=600). Şirket seçeneklerinde eklenmiş logo yok; bina illüstrasyonu başlık arka planındadır. JavaScript kapalıysa sunucunun ayrı sayfa geri dönüşü korunur.

## Müdürün Maddeleri

“Yerelde mevcut” gerçek dış servisle uçtan uca kabul edildiği anlamına gelmez. 19a/19b ayrı satırlar olduğundan toplam 23 madde vardır.

| Madde | Durum | Yapılan / Kalan |
|---|---|---|
| 1 Giriş yerleşimi | Yerelde mevcut | Üst başlık, dar görsel, giriş türü sekmeleri ve yerinde şirket seçimi. Rol tanıtımı yapan sol metin yok. |
| 2 Yeni talep açıklamaları ve kullanım | Yerelde mevcut | Teknik paragraflar kaldırıldı; sorgu, kaynak cihaz seçimi ve yeni cihaz girişi sırası korundu. Başarılı sorgu olmadan uydurma kaynakla kayıt açılmaz. |
| 3 Liste istatistik yüksekliği | Yerelde mevcut | Büyük kartlar yerine kompakt sayaç şeridi ve sunucu sayfalaması. |
| 4 Talep detayı | Yerelde mevcut; kabul kapsamı sınırlı | Firma için yeni/randevulu/tamamlanmış kayıtlar tarayıcıda incelendi. İç personel işlem yetkileri korunur. Bütün durum geçişlerinin uçtan uca testi henüz tamamlanmadı. |
| 5 Form adı | Yerelde mevcut | Komut “Formu Önizle”. Resmi şablon başlığı korunur; şirket başına farklı şablon seçimi henüz yok. |
| 6 Müşteri ve numara filtreleri | Yerelde mevcut | Müşteri, tesisat, sözleşme, abone filtreleri; müşteri ve firma ayrı, cihaz alanları etiketli. |
| 7 Nihai PDF | Yerelde mevcut; gerçek imza bekliyor | Mevcut Word şablonundan PDF hazırlanır. Önizleme aynı PDF'yi gösterir; tamamlanmış kayıtta indirilen dosyayla SHA-256 eşleşti. Yeni demo PDF açıkça demo işaretlidir. Genel amaçlı/piksel birebir Word dönüştürücü olduğu iddia edilmez. |
| 8 Menü isimleri | Yerelde mevcut | Paylaşılan menüde yetki belgesi, cihaz devreye alma ve cihaz değişimi işleri ayrıdır. Bu tur personel ve firma menüleri görüldü; her rolün her ekranı yeniden denenmedi. |
| 9 Belge depolama ve indeks | Kısmen | Yeni dosyalar özel App_Data alanında, DB'de dosya metadata/anahtarı ve indeksler var. Eski fiziksel wwwroot dosyaları halen mevcut; erişim engeli var, doğrulanmış taşıma ayrıca gerekli. |
| 10 Harici hesap/telefon/sertifika | Servis ve uygulama bekliyor | Mevcut yerel giriş korunur. Harici kimlik arayüzü var; gerçek adapter, kullanıcı oluşturma/gönderme sözleşmesi ve OTP sonrası çağrı kabul testi yok. |
| 11 Daire/bina/abone/cihaz | Servis bekliyor | Mevcut SOAP abone/adres/cihaz alanları kullanılır. Daire ve bina alanları DTO'da/formda desteklenir, mevcut SOAP eşlemesi bunları doldurmuyor. Veri uydurulmaz. |
| 12 İmzalanacaklar listesi ve callback | Uygulanmadı; sözleşme bekliyor | Mevcut gönder/sorgula uçları Onur'un istediği servis hesabı/listesi/callback/koordinat sözleşmesinin yerine geçmez. |
| 13 Bölge/ekip eşleme | Kısmen | Sunucuda şirket yetkisi ve kompakt seçim var. Gerçek saha bölgesi/master ekip servisi yok; mevcut bölge il/şirket eşlemesidir. Birime yönlendirme gerçek personel ataması gibi sunulmaz. |
| 14 İmzalı dosya adı ve URL | Uygulanmadı; sözleşme bekliyor | Onur'un bildirim biçimi, izinli dosya adresleri ve kimlik doğrulaması henüz bağlanmadı. |
| 15 Firmadan kaynak cihazı gizleme | Yerelde mevcut | Ekran/API listesinde eski cihaz gizli. Yeni tipli DTO'da da kaynak alanı temizlenir. Kullanıcının kararı gereği resmi formda kaynak cihaz korunur. |
| 16 Marka uyuşmazlığı | Yerelde mevcut | Uyarı verir; tek başına talebi engellemez. Kaynak cihaz kullanıcı tarafından değiştirilemez. |
| 17 Randevu SMS ve e-posta | Uygulanmadı | Randevu kaydı çalışır fakat abone/firma için otomatik randevu SMS + e-posta gönderimi yok. Bu yalnızca bir ayar anahtarının eksik olması değildir. |
| 18 Firma kendi kayıtları | Yerelde mevcut | Sunucu firma/şirket sınırı uygular; firma iç takvime erişemez. Tüm çapraz şirket/hesap kombinasyonları ayrıca test edilmeli. |
| 19a Dakika çakışması | Yerelde mevcut; yük testi eksik | Aynı görevli veya ilgili şirket/bölge/birimde aynı dakika engellenir. AsgariAralikDakika=0 varsayılan; ziyaret süresi tahmin edilmedi. Eşzamanlı DB yazma stres testi eksik. |
| 19b Gidememe/yeniden planlama | Kısmen | İmza öncesi mevcut randevu güncellenebilir. Erteleme/iptal bildirimleri ve imza sonrası kural netleşmeli. |
| 20 Tip/baca/kapasite uyumu | Yerelde mevcut | Uyumsuzluk uyarıdır; kaynak doğrulama ve zorunlu yeni cihaz alan kontrolleri ayrı tutulur. Uygunluk kararını yetkili kontrol personeli verir. |
| 21 Günlük takvim | Yerelde mevcut | İlk açılış bugün; gün/hafta, tarih/şehir/bölge/personel/müşteri/tesisat filtreleri. Saat, müşteri, adres, ekip ayrı hizalanır. Liste 25 kayıtlık sayfalıdır, gün toplamları sadece yüklü sayfadan hesaplanmaz. |
| 22 Gerçek ekip/personel verisi | Servis bekliyor | Yapılandırmadan ekip okuma var. Canlı master data adapterı veya ekip yönetimi tamamlandı denilemez. |

## Veri Nereden Geliyor?

- Web arayüzü (MVC) işlem API'sine gider; API iş kuralları ve SQL veritabanını kullanır. Tesisat sorgusu ayrıca mevcut SOAP servisine gider.
- `OnlineCihazBilgileriClient.cs` SOAP yanıtından `cariad` (müşteri), `carikod`, `sozlesmeno`, `sayacno`, `adres` ve cihazın `projeno` alanlarını okur.
- `216195` proje ve `2600172341` sayaç değerlerinin kod yolu bu servis alanlarından kayıt/snapshot'a gelir; ekranda metin sabiti olarak yazılmıyor. Bu, dış servisin döndürdüğü verinin gerçek saha kaydı olduğunu bağımsız doğruladığımız anlamına gelmez.
- Yeni marka/tip/kapasite firma girdisidir. Mevcut örnek kayıtlardaki `gsdfg`, `tryeryt` gibi metinler kayıtlı geliştirme verileridir; arayüz bunları gerçek bir marka adına çevirmedi, kayıtlar silinmedi.
- Sorgu referansı kullanıcıya/tesisata bağlı, 20 dakika geçerli API belleği kaydıdır. API yeniden başlarsa yeniden sorgu gerekir. Çoklu API sunucusunda ortak snapshot deposu planlanmalıdır.
- Bugünkü randevu yoksa takvim bunu söyler; eski güne ait kayıtlar bugünmüş gibi gösterilmez.

## PDF ve İmza

PDF düzeni `YkcFr265PdfService` içinde dolu Word şablonunun tabloları, satırları ve sayfa yapısı üzerinden oluşturulur. Resmi şablonun değişmesi durumunda iki sayfa yeniden görsel olarak doğrulanmalı.

Tarayıcının gömülü PDF eklentisi boş kaldığı için Mozilla PDF.js 6.3.289 yerel dosya olarak eklendi. Aynı yetkili PDF URL'sini açar; sonraki/önceki sayfa, yakınlaştırma ve indirme kontrolleri vardır. Belge harici CDN veya dönüştürme servisine gönderilmez. Lisans ve paket bütünlük bilgisi `wwwroot/lib/pdfjs/README.md` içindedir. Kaynak: [Mozilla PDF.js örnekleri](https://mozilla.github.io/pdf.js/examples/).

Gerçek imza bağlantısı yokken demo başarı gerçek imza kabulü değildir. İmzalı PDF'nin dosya türünü kontrol etmek kriptografik imzayı doğrulamaz. Onur entegrasyonunda imzacı, sertifika, zaman, belge sürümü/hash ve tekrar bildirim politikası kesinleşmeli.

## Müdür Sorarsa

**“Test için her şey hazır mı?”** Yerel ekranlar ve temel kurallar çalışıyor; harici kimlik, Onur imza, randevu bildirimleri ve gerçek ekip verisi bağlanmadan uçtan uca kabul tamam denilemez.

**“Genel admin Swagger'da neden 403 alıyor?”** Genel admin her iş rolünü taklit etmez. `/api/ys-devreyeal/gecmis` yetkili servis hesabının kendi geçmişidir. Yönetim kayıtları için yönetim uçları kullanılmalı. Rol ve şirket/firmaya erişim ayrıca kontrol edilir.

**“401, 403 ve boş cevap arasındaki fark?”** 401 oturum/token yok veya geçersiz; 403 oturum var ama rol/işlem/şirket yetkisi yok; 200 ve boş liste o filtre ve yetkide kayıt bulunmadığını gösterir. Kodları gizleyerek 200 döndürmek doğru değil. Swagger'ın örnek `string` değerleri gerçek filtre olarak gider; filtresiz denemede örnek değerleri kaldırıp `{}` gönderilir.

**“Tesisat bilgilerini firma değiştirebilir mi?”** Kaynak cihaz ve tesisatın doğrulanmış sorgu kaydı API tarafında esas alınır. Arayüzde readonly olması tek koruma değildir. Firma yalnızca yeni cihaz bilgilerini girer.

**“Uyumsuz cihazda neden devam ediyor?”** Talep alınması ile uygunluk kararı farklı işlemlerdir. Uyumsuzluk görünür uyarı olur; saha kontrolünün uygun/uygun değil kararı yetkili personeldedir.

**“Randevu kimin?”** Abonenin tesisatına dağıtım şirketinin atadığı kontrol ekibi gider. Sertifikalı firma ekibi görevlendirmez ve kontrol sonucunu kendisi girmez; kendi talebinin sonucunu takip eder.

**“Beş kontrolün hepsi yapılacak mı?”** Hayır. Formdaki 1-5 alanlar tekrar kontrol sırasıdır. Uygun sonuçta imza aşamasına ilerlenir; uygun değilse eksiklik giderildikten sonra sonraki kontrol kullanılır.

**“Belgeler DB içinde mi?”** Yeni belge dosyaları özel depodadır; veritabanı metadata, dosya anahtarı ve ilişkileri tutar. İndirme yetkili API üzerinden olur. Eski açık kök altı dosyalar için taşıma işi ayrıca kapatılmalıdır.

**“Migration kullanıyor musunuz?”** Hayır; kurum düzenine uygun SQL scriptleri var. Test ortamına kurulurken uygulanmış script/indeks kontrol listesi tutulmalı. Bu tur DB şeması değiştirilmedi.

**“Senden hangi servisler bekleniyor?”** Harici kimlik/sertifika/telefon ve OTP sonrası doğrulama, tesisat daire/bina alanları, şirket-bölge-ekip/personel ID listesi; Onur'dan imza liste/callback JSON'u, dosya ID/sürümü, dosya URL'si/base64, imzacı listesi ve sayfa bazlı koordinatlar. Ayrıca randevu alıcı/metin ve mail gönderici bilgisi.

**“Hata mesajları neden input yanında?”** Kullanıcının düzelteceği alan hataları kaybolmamalı; alan yanında kalır. Başarılı kaydetme/silme gibi işlem sonuçları ortak üst toast'tır. İndirme bildirimi sadece “başlatıldı” der, dosyanın kullanıcının diskine kesin kaydedildiğini iddia etmez.

## Doğrulama Kanıtı ve Sınırlar

- MVC ve API derlemeleri: 0 hata, 0 derleyici uyarısı.
- `Tests/YkcRules`: 30 kural/form kontrolü geçti. Paket güvenlik danışma kaynağına erişimde NU1900 uyarısı vardı; bağımlılık güvenliği tam tarandı denilemez.
- `Verify-FormCalendar.ps1`: 16 salt-okunur yerel API kontrolü geçti. Firma kaynak alan gizliliği, rol reddi, aynı PDF byte/hash, takvim gün toplamları/filtre/sayfalama ve dashboard müşteri-tarih alanları kontrol edildi.
- Tarayıcı: giriş üst başlığı, rol sekmesi, aynı yerde şirket seçimi; personel ve firma sidebar; firma listesi ve #31 yeni / #23 randevulu / #25 tamamlanmış detay. Mobil firma detayında yatay sayfa taşması yok.
- Takvim: bugün boş görünüm ve 9 Eylül'deki 09:00 / 09:36 ziyaretleri incelendi. Gerçek kayıt kullanıldı, yeni randevu oluşturulmadı.
- PDF: ilk ve ikinci sayfa, %75 yakınlaştırma ve mobil genişliğe sığdırma tarayıcıda görüntülendi. Önceki gömülü eklenti boşluğu giderildi; önizleme ile final indirme aynı byte dizisini kullanır. Mobilde erişilebilir metnin görünür olması giderildi.
- Mevcut eski yetki belgesi dosyaları MVC ve API'de anonim istekte 404 döndü. PDF.js karakter haritası/font/wasm dosyaları 200 döndü; ek dosya türleri yalnızca kütüphane klasöründe sunulur.
- Önceki çalışmada yalnızca #25 demo nihai formu açık yenileme işlemiyle yeni PDF tasarımına geçirildi; gerçek imzalı belgeler değiştirilmedi. Bu son doğrulama turunda yeni talep, randevu veya kullanıcı kaydı açılmadı.
- Doğrulama süreçlerinde SMS gönderimi geçici süreç ayarıyla kapalıydı; uygulama kaynak ayarı değiştirilmedi. SMS/OTP uçtan uca test edilmiş sayılmaz.
- Tüm rollerin tüm formları, eşzamanlı DB yazmaları, harici servis kesintileri ve gerçek imza kabulü için eksiksiz test raporu yok. “Her açıdan sorunsuz” garantisi verilmez.
- Yalnızca `_verify` altındaki artık kullanılmayan 16 üretilmiş klasör temizlendi (yaklaşık 1.76 GB). Kullanıcı belgeleri, eski yüklemeler, kaynak ve SQL scriptleri silinmedi.

## Teste Geçmeden Kapatılacaklar

1. Yukarıdaki 10, 12, 14, 17 ve 22 bağlantıları; 11 ve 13 için gerçek alan/ID eşlemeleri.
2. Şirket şablonları, imza koordinatları ve gerçek imzalı PDF örneğinin onayı.
3. Eski dosyaların erişim + özel depoya taşıma ve geri alma kontrolü.
4. Aynı dakikada eşzamanlı atama, çapraz firma/şirket yetkileri ve bütün durum geçişleri için entegrasyon testleri.
5. Ortam kurulumunda SQL scriptleri, erişim anahtarları, izinler, log ve yedekleme kontrolü. Kullanıcı onayı olmadan herhangi bir sunucuya yayın yapılmamalı.

## 11 Eylül: Web / API Kimlik Ayrımı Güncellendi

Önceki denetimde açık olan MVC Identity/SQL bağlantısı bu tur kaldırıldı. Web başlangıcında AppDbContext, Identity EF store ve SMS domain servisi kaydı yok; MVC JWT imzalamıyor.

- Giriş, SMS devamı, şifre sıfırlama, mevcut kullanıcı, profil ve şifre değiştirme `AuthApiClient` üzerinden `/api/auth/*` uçlarına gider. Identity ve SMS kod tablosu yalnız API tarafında kullanılır.
- `ApiKullaniciOturumu` API'nin verdiği tokenı sunucu session'ında tutar; tarayıcıya HttpOnly oturum çerezi gider. Her MVC isteğinde mevcut kullanıcı API'den doğrulanır (aynı istek içinde önbellekli).
- API profil hedefini token kimliğinden belirler. Kullanıcı ID, rol, kullanıcı tipi ve şirket alanları profil isteğiyle değiştirilemez. Kullanıcı DTO'sunda Identity hash/stamp alanları yoktur.
- SMS bekleme işlemi süreli, korumalı bir referanstır; kullanıcı, amaç ve tek SMS kaydıyla bağlantılıdır. Kod tüketimi koşullu SQL update ile bir kez yapılır; yanlış deneme sınırı ve şifre değişikliği sonrası token reddi test edildi.
- Seed yardımcıları API projesine taşındı; yalnız Development ve açık seed bayrağıyla çalışır. MVC'nin şehir listesi yalnız yapılandırmayı okuyan sınıfa ayrıldı; şehir/şirket DB işlemi API domain servisinde kaldı.
- Şema veya migration değişmedi. Var olan demo personelin aynı şifresiyle reset/değişim yapılarak stamp yenilendi; gerçek hesap veya iş kaydı değiştirilmedi, gerçek SMS gönderilmedi.

### Bu Turun Kontrolleri

- API ve MVC son derlemeleri: 0 hata, 0 derleyici uyarısı.
- `Verify-AuthApi.ps1`: 29 kontrol geçti. Kimlik, profil yetkisi, yanlış/tekrar SMS, amaç ayrımı, parola kuralları ve token iptali.
- `Verify-WebAuth.ps1`: 6 kontrol geçti. CSRF reddi, şirket seçimi, profil, süresi dolan oturumun çerez temizliği ve yönlendirme döngüsü olmadan girişe dönüş.
- `Verify-ApiBoundary.ps1`: 13 rol/şirket kontrolü geçti.
- Mevcut `YkcRules`: 30 kontrol geçti. NuGet güvenlik danışma kaynağı erişilemediği için NU1900 uyarısı var; bağımlılık güvenlik taraması tamamlandı denilmez.
- Web, `ConnectionStrings:DefaultConnection=disabled` ve `Jwt:Key=disabled` ile localhost üzerinde çalıştırıldı. Personel girişi, aynı giriş alanında şirket seçimi, profil okuma/kaydetme ve yetkili servis girişi gerçek tarayıcıda doğrulandı. Ayrı Development önizlemesinde SMS kodu ekranı ve SMS sonrası şirket seçimi doğrulandı.
- Bir önceki çalışma notundaki “Identity hâlâ MVC'de” ve “SMS akışı denenmedi” ifadeleri artık bu yerel kimlik değişikliği için geçerli değildir. Gerçek SMS sağlayıcısı ve dış kimlik servisi kabul testi hâlâ bekler.

### Kurulum Notları

- API ve MVC birlikte yeniden derlenip başlatılmalı; eski API derlemesi yeni auth sözleşmesini karşılamaz. Eski oturumlarla devam edilmez, yeniden giriş yapılır.
- SQL bağlantısı, JWT imza anahtarı, SMS ve SertifikaliFirmaKimlik ayarları API tarafındadır. MVC için ApiIntegration:BaseUrl yeterlidir; canlıda HTTPS kullanılmalı.
- API erişimi yokken MVC veritabanına geri dönmez. Yerel demo hesapları eksik dış kimlik servisine rağmen kullanılabilir; harici doğrulama zorunlu açılmışsa başarısız kontrol atlanmaz.
- Session şu anda sunucu belleğindedir; web yeniden başlarsa yeniden giriş gerekir. Birden fazla web/API örneğine çıkmadan ortak session deposu, Data Protection anahtarları ve hız sınırı politikası kurum altyapısına göre yapılandırılmalı.
- Gerçek kimlik/sertifika servisi, Onur imza listesi/bildirimi, randevu SMS/e-postası ve master ekip bağlantıları ayrı açık işlerdir. Bu tur tamamlandı denilen, webin kimlik işlemlerindeki doğrudan SQL bağımlılığının ayrılmasıdır.

Yazdırılabilir kısa özet: `output/pdf/Mudur-Gorusmesi-Kisa-Not.pdf` (tek A4 sayfası).
