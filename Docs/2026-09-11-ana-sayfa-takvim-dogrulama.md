# Ana Sayfa ve Takvim Kontrolü - 11 Eylül 2026

## Bu Turda Değişenler

- Sertifikalı firma ana sayfası: açıklayıcı talep sayıları ve sütunları belirli son talepler tablosu. İç operasyon takvimi kaldırıldı; firmanın kontrol randevusu kendi talebinde görünmeye devam ediyor.
- Personel ana sayfası: yetkiye bağlı işlem kartları, ay/gün takvimi, abone adı, personel/ekip, il ve bölge filtreleri. Randevu listesi sınırlı yükseklikte; fazla kayıt için tam takvim bağlantısı var.
- Bekleyen belge listesi gerçek bekleyenler API sonucundan alınıyor. Önceden son belgeler listesindeki onaylanmış kayıtlar da bekleyen gibi gösterilebiliyordu.
- Talep ve rapor özetlerindeki "İncelenen" yerine "İnceleme / atama bekleyen talep", "Randevu / İşlem" yerine "Kontrol sürecindeki talep" kullanıldı.
- Beğenilen firma talep detayının yerleşimi korundu; tekrar eden inceleme/kontrol açıklamaları kaldırıldı.
- Şirket illüstrasyonu giriş kartındaki şirket seçim formunun arkasına taşındı. Giriş fotoğrafı ve mevcut yerleşim değiştirilmedi.
- Başarı bildirimi üstten 12 pikselde, 9 saniye görünür; fare veya klavye odağında süre durur.
- Personel profili: solda hesap bilgileri, sağda kişisel bilgiler, şirket/yetkiler ve güvenlik sekmeleri. "Profilim" başlığı içerikte tekrarlanmıyor.
- Menü alt başlıklarında cihaz değişim talepleri, randevuları ve raporları açıkça adlandırıldı.

## Veri ve Yetki

Web tarafındaki yeni veri çağrıları ApiClient üzerinden yapılıyor. Takvim ve dashboard API çağrıları seçili şirketi taşıyor; API şirketin varlığını, kullanıcının şirket ilişkisini ve ilgili işlem yetkisini doğruluyor. Şirket üyeliği tek başına YKC erişimi sağlamıyor. Firma eski cihaz bilgilerini dashboard yanıtında almıyor.

Filtrelerin boş olması seçilen dönemdeki yetkili kayıtları gösterir; bütün şirketlere erişim anlamına gelmez. Genel yönetici açıkça şirket seçtiğinde sonuçlar o şirketle sınırlandırılır. Gerçek ekip listesi yerine yeni ekip adı üretilmedi.

## Doğrulama

- API ve MVC derlemeleri: 0 hata, 0 uyarı.
- `Verify-DashboardCalendar.ps1`: 30 kontrol geçti. Anonim/rol/şirket/yetki reddi, boş ve birleşik filtreler, gün/ay toplamları, firma veri gizleme ve özet toplamları denetlendi.
- `Tests/YkcRules`: 30 kontrol geçti. Bu çalıştırmada NuGet güvenlik verisine erişilemediği için NU1900 uyarısı vardı; test hatası yoktu. Bu, bağımlılık güvenlik taramasının tamamlandığı anlamına gelmez.
- Gerçek tarayıcı: firma ve personel ana sayfaları, 33 numaralı firma detayı, talep listesi, rapor özeti, personel profili, girişte şirket seçimi ve günlük takvim kontrol edildi.
- 1280x720, 768x1024 ve 390x844 boyutlarında ilgili ana sayfa yerleşimleri incelendi. Tablet kartları ikiye iki; mobil geniş tablo yalnız kendi alanında kayıyor.
- Ana sayfada 9 Eylül seçildiğinde iki randevu geldi. Ekip ve il filtresi birlikte çalıştı. Tam takvimde olmayan abone ile sıfır sonuç geldi; temizleme filtreleri kaldırdı.
- Profilde mevcut değerler değiştirilmeden kaydetme denendi; üst bildirim ve kapanması doğrulandı. Şifre değiştirilmedi.

## Sınırlar

Bu kontrol bütün projenin entegrasyon testinin tamamlandığı anlamına gelmez. Mevcut demo personel, üç şirkete bağlı olsa da yalnız birinde YKC görüntüleme yetkisine sahip; diğer iki şirkette erişim reddi doğrulandı. Birden fazla şirkette YKC yetkili personelin detay/düzenleme akışları için ayrıca uçtan uca test gerekir. Gerçek SMS, ekip ve Onur imza servisleri bu çalışmada test edilmedi.

Yalnızca bu bilgisayardaki 127.0.0.1:55222 önizlemesi kullanıldı. Dışarıya yayın, migration veya iş kaydı ekleme/silme yapılmadı. 7161'deki kullanıcı uygulaması durdurulmadı.

## Son İstek Sonrası Ek Kontrol

- Personel ana sayfasında bağımsız tarih satırı, kartların "Kayıtları Gör" alt satırları ve yinelenen belge/devreye alma akış listeleri kaldırıldı. Yetkili işlem kartları ve günlük kontrol takvimi korundu.
- Takvim filtresi kapalı başlıyor; uygulanan filtre varsa açık kalıyor. 1280x720 ekranında varsayılan ana sayfa yüksekliği 720 piksel, sayfa kaydırması gerekmiyor. Küçük ekranlarda doğal dikey kaydırma korunuyor.
- 8 Eylül'deki dolu gün, olmayan aboneyle boş sonuç ve filtre temizleme gerçek tarayıcıda denendi. Kartların tablet/mobil yerleşimi iki sütuna getirildi.
- Onay geçmişi, cihaz markaları, yetkili servisler ve devreye alma listelerinde kayıt başlığı ve özet filtrelerden önce geliyor. Onay geçmişinde kapalı filtre satırı 36 piksel.
- YKC rapor ve personel devreye alma detay pencereleri ortak 940 piksel genişlik, bölüm renkleri ve sabit alt işlem alanı kullanıyor. Masaüstü ve 390x844 mobil pencereler denetlendi; yatay taşma yok, alt işlem düğmeleri görünür.
- YKC renk değişkenlerinin personel detay penceresinde eksik kalması düzeltildi; "Tamamlandı" yeşil gösteriliyor.
- Sertifikalı firma hesabıyla `/ykc/detay/36` açıldı: işlem düğmelerinin yanındaki "İnceleme Bekleniyor" yok, dağıtım şirketinin incelemesini belirten durum mesajı duruyor.
- 16 eski doğrulama derlemesi klasörü (yaklaşık 1,6 GB) silindi. Kaynaklar, belge arşivleri, testler ve toplantı notları korunuyor.
- Onur için ayrı API sözleşmesi ve test sonuçları `Onur-Mobil-Imza-Sozlesmesi.md` dosyasında. Bu ek çalışmada yalnız geçici entegrasyon test kayıtları oluşturulup temizlendi; kullanıcı iş kayıtları değiştirilmedi.

## Personel Detayı ve Kartların Son Düzenlemesi

- Personel talep detayında firma görünümüyle aynı kayıt başlığı ve bilgi gruplaması kullanıldı. Süreç şeridi kaldırıldı; güncel personel işlemi sağda, tesisat/cihaz bilgileri solda. Firma ve geçmiş sekmeleri sayfayı aşağı kaydırmıyor. Personel form uçları, yetki ve durum koşulları korunuyor.
- Tarayıcıda #39 yeni talep, #23 randevulu talep ve #22 tamamlanmış talep kontrol edildi. #39 ve ana sayfa 1280x720'de kaydırmadan sığıyor. #23'ün randevu düzenleme alanı açıkken de kaydet düğmesi görünür. Gerçek talebin durumu bu kontrollerde değiştirilmedi.
- Kartlara onay/red, kontroldeki/tamamlanan talep, toplam kayıt/son işlem ve aktif/pasif servis bilgileri eklendi. Servis sayısı genel firma toplamından değil, servis listesinin yetkili API sonucundan geliyor. API hatasında olmayan sayı üretilmiyor.
- YKC rapor, personel devreye alma ve yetkili servis geçmişi pencerelerine erişilebilir bilgi sekmeleri eklendi. Masaüstünde ve 390x844 mobilde test edilen içeriklerde pencere gövdesinin `scrollHeight` ve `clientHeight` değerleri eşit; uzun cihaz adı olan servis kaydı da denendi. Çok küçük ekran/yüksek yakınlaştırma veya olağan dışı uzun içerikte erişimi korumak için güvenli taşma davranışı kaldırılmadı.
- Ortak sekmelerde yön tuşu desteği, modal odağının pencere içinde tutulması ve yeni kayıt açılınca ilk sekmeye dönme davranışı mevcut. Sekme değiştirme ve yeniden açılış tarayıcıda denendi.
- MVC derlemesi 0 hata/0 uyarı. Takvim/yetki API kontrolleri 30/30, mevcut YKC iş kuralı kontrolleri 30/30 geçti. Bunlar bütün projenin entegrasyon testinin tamamlandığı anlamına gelmez.
