# Toplantı İstekleri ve Bağlantı Sınırları

Bu not mevcut yerel değişikliklerin durumunu gösterir; canlı entegrasyon kabul raporu değildir. Commit/push yapılmadı. Dış servisler gelmediği için mevcut ekranlar kapatılmadı.

## Bugünkü Çalışma Biçimi

- Giriş: mevcut yerel hesaplar ve mevcut SMS doğrulaması korunuyor. Harici firma kimlik servisi varsayılan olarak kapalı. Adres/JSON gelince adapter bağlanacak.
- Tesisat: mevcut online SOAP sorgusu çalışıyor. İstemci yalnızca kullanıcıya ait, 20 dakika geçerli cihaz sorgu referansını gönderiyor. Eski cihaz, müşteri, şirket ve tesisat bilgisi API belleğindeki sorgu kaydından alınır.
- Randevu: canlı personel listesi yokken 187 Acil veya Mühendis birimine yönlendirme yapılabilir. Bölge adından sahte çalışan/ekip oluşturulmaz. Gerçek personel atanmış sayılmaz.
- İmza: yalnızca Development + Demo ayarında mevcut demo akışı çalışır. Onur'un uygulamasına gerçek gönderim yapılmaz. Production demo sağlayıcıyı etkinleştirmez.
- Nihai belge: yeni demo sonuçları PDF olur ve demo olarak işaretlenir. Eski Word kayıtları indirme sırasında değiştirilmez. Yetkili personel eski demo belgesini açık bir POST işlemiyle PDF olarak yenileyebilir.
- MVC/API iletişimi: salt-okunur YKC çağrıları geçici bağlantı, 408, 429 ve 5xx cevaplarında sınırlı olarak yeniden denenir. Yazma çağrıları çift kayıt oluşturmamak için otomatik tekrarlanmaz. Servis yine kullanılamazsa teknik exception metni yerine kullanıcıya kısa bir erişim uyarısı gösterilir.

## İsteklerin Durumu

| No | İstek | Yerel durum / kalan iş |
|---|---|---|
| 1 | Giriş yerleşimi | Açıklamalı sol panel ve tanıtım görseli kaldırıldı. Kurumsal logo, kullanıcı türü sekmeleri ve giriş formu tek merkezî kartta toplandı. Geliştirme hesapları seçildiğinde ilgili alanlar doldurulur; mavi/yeşil kurumsal renkler korunur. |
| 2 | Yeni talep | Üç ince adımlı, tek sayfalık operasyon düzeni var. Online servisten alınan tesisat/proje cihazı ile firmanın girdiği yeni cihaz alanları görsel olarak ayrıldı; teknik açıklama paragrafları kaldırıldı. |
| 3 | Talep listesi üst alanı | Başlık ve küçük sayaçlar tek kompakt satırda. Filtreler açılır/kapanır. Sunucu sayfalaması korunuyor. |
| 4 | Detay kullanılabilirliği | Rol ayrımı var. Durum, beklenen işlem açıklaması ve tek ana buton ilk odakta; karşılaştırma, kayıt bilgileri ve geçmiş açılır alanlarda. Tamamlanan kayıtta aktif işlem görünümü yok, belge indirme ikincil komuttur. Rapor/takvim bağlamı geri dönüşlerde korunur. |
| 5 | Form adı | Uygulama kabuğunda “Formu Önizle”, “Cihaz Değişim Formu” ve “Talep” gibi şirketten bağımsız adlar kullanılıyor. Resmî belgenin içindeki tam FR265 adı korunuyor. Şirket başına gerçek belge şablonu seçimi henüz yok. |
| 6 | Müşteri/tesisat/sözleşme/abone | Liste ve filtre alanları mevcut. API filtreleri ve sayfalama korunuyor. |
| 7 | İmzalı PDF | Yeni demo final PDF, gerçek provider sonucu için PDF içerik başlangıcı/MIME/uzantı kontrolü. Mevcut demo PDF çizimi Word dönüştürmesi değildir; şablonla birebirlik tamamlandı denilemez. |
| 8 | Menü grupları | Ortak sidebar Aksa referansındaki gruplama mantığıyla açılır alt menülere dönüştürüldü. Cihaz değişimi ile yetkili servis/devreye alma işleri ayrıldı; firma yalnızca kendi menülerini görür. |
| 9 | Yetki belgesi depolama | Dosya App_Data altında şirket/firma/yıl grubunda; DB'de metadata ve depolama anahtarı. İki yetki belgesi indeksi 08.09.2026 tarihinde yerel geliştirme DB'sinde uygulandı ve doğrulandı. |
| 10 | Harici giriş/telefon/sertifika | Interface ve başarısızlığı açık döndüren yapı hazır, gerçek adapter yok. Mevcut giriş açık. Harici doğrulamanın API token akışıyla ortak uygulanması bağlantı kabul testinin parçası olmalı. |
| 11 | Daire/abone/adres/cihaz servisi | Mevcut SOAP'tan gelen abone/adres/cihaz kullanılıyor. Daire, sertifika ve bina alanları veri gelene kadar boş; uydurulmuyor. |
| 12 | İmza listesi ve callback | Mevcut gönder/sorgula akışı korunuyor. Onur için servis hesabı, imzalanacaklar listesi ve callback sözleşmesi henüz uygulanmış/canlı değildir; birlikte netleştirilecek. |
| 13 | Şirket/bölge güvenli ekip seçimi | Kompakt dropdown, sunucuda şirket/il/bölge eşlemesi. Boş liste kabul edilir; birim yönlendirmesi yapılabilir. Şimdiki bölge değeri SOAP'tan gelen saha bölgesi değil, mevcut il/şirket eşlemesidir. |
| 14 | İmzalı dosya adı/URL | Onur'un gerçek URL/kimlik doğrulama sözleşmesi bekleniyor. Henüz dış URL indiren callback yok. |
| 15 | Firmadan eski cihaz detayını gizle | Firma sorgu/liste/detay cevaplarında eski marka/kapasite gizlenir. Kullanıcı onayına göre resmi form verisi bunları korur. |
| 16 | Marka uyuşmazlığı | Personel karşılaştırmasında uyarı gösterilir, tek başına talebi engellemez. |
| 17 | Randevu SMS ve e-posta | Randevu ekranı çalışır. Otomatik randevu SMS/e-posta gönderimi henüz uygulanmadı. Alıcı telefonları, metinler, gönderici ve e-posta servisi netleşmeli. Gönderilmiş gibi kayıt/mesaj üretilmiyor. |
| 18 | Firma kendi kayıtları | Sunucu firma kapsamı korunuyor. Firma takvim ve iç entegrasyon listelerini kullanamaz. |
| 19a | Saat çakışması | Aynı personel veya aynı şirket/bölge/birim için aynı dakika engellenir. AsgariAralikDakika varsayılan 0; kesin ziyaret süresi varsayılmadı. Serializable işlem kullanılır. Eşzamanlı DB stres testi henüz yapılmadı. |
| 19b | Gidememe/yeniden planlama | İmza başlamadan mevcut randevu güncellenebilir. İmza sonrası yeniden planlama/değişiklik iş kuralı ayrıca netleşmeli. |
| 20 | Tip/baca/kapasite uyuşmazlığı | Uyarı verilir; eski veri sunucudan gelir, yeni veri doğrulanır. Uyuşmazlık tek başına oluşturmayı durdurmaz. Uygunluk kararını personel verir. |
| 21 | Ana panel takvimi | İç rollerde bugünkü randevular, ayrıca tarih/il/bölge/personel filtreli ajanda ve sunucu sayfalaması eklendi. Firma ana ekranı da görev bağlantıları, süreç özeti ve son talepleri kompakt şekilde gösterecek biçimde düzenlendi. |
| 22 | Canlı ekip listesi | Bekleniyor. Şimdilik YkcPlanlama:Ekipler yapılandırmasından okunabilir; DB tabanlı ekip yönetimi veya canlı servis adapterı hazır sayılmaz. |

## Onur ve Müdürle Netleştirilecek Sözleşme

1. İmza uygulamasına hazır PDF mi, Word mü gönderilecek? Word kabul ediliyorsa dönüşümü o uygulama mı yapacak? Bu kesinleşmeden LibreOffice kurulmadı ve yeni dönüştürücü bağımlılığı eklenmedi.
2. PDF bizden istenirse şirket bazlı şablonlar nereden alınacak, Word/PDF sürümü ve dönüşüm hizmeti kimde olacak?
3. İmzalanacak belge ID/sürüm/hash, dosya adı, base64 veya erişim URL'si ve imzacı kimlikleri için örnek JSON.
4. İmza koordinatlarının sayfa numarası başlangıcı, birimi, başlangıç köşesi ve dikdörtgen ölçüleri. Koordinatlar tahmin edilmemeli.
5. Callback kimlik doğrulaması, tekrar bildirimlerde tek kayıt, kısmi/final/red durumları ve URL erişim süresi. Yeni callback kimliksiz açılmamalı; URL indirme için izinli hostlar belirlenmeli.
6. Harici login: kullanıcı/şifre doğrulaması, dönen telefon/sertifika/firma/şirket kimliği, OTP sonrası kullanılacak tek kullanımlık doğrulama referansı, süre ve hata cevapları.
7. Bölge/ekip: değişmeyen şirket, il, bölge, ekip ve personel ID'leri; pasiflik ve şirket yetkisi bilgisi.
8. Randevu: dakika aralığı, bir ekibin paralel kapasitesi, firma/abone alıcıları, SMS/mail metinleri, ertelenen randevunun bildirim kuralı.

## Teknik Notlar

- Sorgu referansı API belleğindedir. API yeniden başlarsa yeniden sorgu gerekir. Birden fazla API sunucusuna geçmeden dağıtık, kullanıcı kapsamlı snapshot deposu gerekir.
- Kullanıcının yanlış gönderdiği eski veriyi kabul etmek yerine kaynağı sunucudan kullanıyoruz; boş kaynakta manuel/fake eski cihaza düşülmez.
- Dosya indirme veriyi değiştirmez. PDF kontrolü dosya türü kontrolüdür; kriptografik imza doğrulaması yerine geçmez.
- Mevcut demo imza gerçeği taklit eden test davranışıdır; Onur bağlantısının test edildiği anlamına gelmez.
- İndeks scriptleri: `DatabaseScripts/2026-09-04_yetki_belgesi_depolama_indeksleri.sql` ve `DatabaseScripts/2026-09-07_ykc_randevu_takvim_indeksi.sql`. Üç indeks yerel geliştirme veritabanında uygulandı; migration eklenmedi ve veri satırları değiştirilmedi.
- Bağımsız kural kontrolleri: `dotnet run --project Tests/YkcRules/YkcRules.csproj --no-launch-profile`. Snapshot kapsamı, eski veri manipülasyonu, resmi form/firma görünümü, baştaki sıfırların normalleştirilmesi, kapasite ve dakika sınırları dahil 27 kontrol; veritabanına dokunmaz.
- Yeni talep, başlangıç kontrol satırları, imza süreci ve ilk geçmiş kaydı tek SaveChanges işleminde kaydedilir. Randevu güncellemesinde mevcut yapılandırılmış ekip seçimi sunucudan geri getirilir.

## Son Ekran Kontrolleri

- Güncel giriş ekranı 1440x900 masaüstü ve 390x844 mobil görünümde gerçek tarayıcıda açıldı. Logo, rol sekmeleri ve giriş formu görünür; yatay taşma yok. Tanıtım/kombi/manzara görseli kullanılmıyor.
- Yetkili servis kayıt ekranı 1440x900 masaüstünde tek görünümde, 390x844 mobilde tek sütunlu akışta açıldı. Her iki görünümde de yatay taşma, kesilen kontrol veya JavaScript hatası görülmedi.
- Yerel API Swagger belgesi çalışan süreçten 200 döndü; 20 YKC yolu üretildi. Örnek YKC liste/özet uçlarında gerçek 200 DTO şemaları, yetkili servis cihaz devreye alma geçmişinde doğru response DTO'su doğrulandı.
- Önceki turda sertifikalı firma oturumunda tesisat sorgusu, cihaz seçimi, talep listesi, mevcut talep detayı ve resmî form önizlemesi gerçek tarayıcıda açıldı. Yeni talep veya randevu kaydı oluşturulmadı.
- Firma ekranlarında eski cihaz marka/kapasitesi gizli, resmî formda görünür. Liste son işlem sütunu masaüstünde görünür; mobil görünümde yatay sayfa taşması görülmedi.
- En son doğrulamada kimlikli ekranlara geçiş SMS doğrulama adımında kaldı. Kod kullanıcı onayı olmadan girilmediği için personel ve firma sayfalarının son CSS değişiklikleri için tam canlı rol turu tamamlandı denilmiyor.
- Baştan sona yeni talep oluşturma, yeni imza/PDF alma ve eşzamanlı randevu yazma testi bu tur yapılmadı. Gerçek Onur/kimlik/bildirim servisi kabul testleri bağlantılar geldikten sonra yapılmalı.

## Giriş Görsel Kararı

Giriş ekranında tanıtım görseli kullanılmıyor. Aksa ekranı yalnızca merkezî yerleşim ve kullanıcı türü sekmeleri için referans alındı; marka, metin veya görsel unsurları kopyalanmadı.
