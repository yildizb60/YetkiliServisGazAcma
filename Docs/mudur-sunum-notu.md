# Yetkili Servis Gaz Acma Sistemi - Sunum Notu

## Kisa Ozet

Bu sistem, yetkili servislerin cihaz devreye alma surecini dijital ortamdan takip etmek, yetki belgesi sureclerini onaylamak ve dagitim sirketi/personel/yetkili servis rollerini tek panelde yonetmek icin hazirlanmistir.

## Roller ve Giris Bilgileri

| Rol | Kullanici | Sifre | Ne Yapar? |
| --- | --- | --- | --- |
| Genel Sistem Admini | test.geneladmin@demo.com | Demo123! | Tum sirketleri, kullanicilari, yetkili servisleri, devreye alma kayitlarini ve raporlari gorur. |
| Sirket Admini | test.sirketadmin@demo.com | Demo123! | Kendi sirket kapsamindaki servisleri, personelleri, onaylari ve raporlari yonetir. |
| Personel | test.personel@demo.com | Demo123! | Yetkili oldugu sirketi secer; yetki belgesi onaylarini, servisleri, devreye alma kayitlarini ve raporlari takip eder. |
| Yetkili Servis | test.servis@demo.com | Demo123! | Kendi yetki belgesini yukler, cihaz devreye alma surecini baslatir, islem gecmisini ve raporlarini gorur. |

## Sunumda Anlatilacak Akis

1. Giris ekranindan rol bazli demo kullanicilariyla sisteme giris yapilir.
2. Genel Sistem Admini panelinde toplam firmalar, bekleyen yetki belgeleri, devreye alma kayitlari ve raporlar gosterilir.
3. Personel panelinde sirket secimi yapilir; personel sadece yetkili oldugu sirketin onay ve raporlarini gorur.
4. Yetkili servis panelinde servis kendi yetki belgesini takip eder ve cihaz devreye alma surecini baslatir.
5. Cihaz devreye alma ekraninda tesisat ve sozlesme no ile servis sorgusu yapilir; gelen cihaz bilgileriyle islem kaydi tamamlanir.
6. Rapor ekranlarinda devreye alma ve yetki belgesi durumlari tarih araligina gore takip edilir.

## Teknik Durumun Basit Anlatimi

- Web panel ve API ayrik calisacak sekilde ilerletiliyor.
- Swagger sadece API projesinde yer aliyor.
- Web tarafinda bazi ekranlar artik veriyi dogrudan veritabanindan degil API uzerinden aliyor.
- Yetki belgesi adlandirmasi uygulama ve veritabani tarafinda standart hale getirildi.
- SMS servisi su an gosterim icin kapali tutuluyor; once ana servis ve panel akisi gosterilebilir.

## Bugunku On Yuz Test Sonucu

Test edilen ana ekranlarda 500/404/exception hatasi gorulmedi:

- Admin: Dashboard, Kullanicilar, Yetki Belgesi Onaylari, Devreye Almalar, Yetkili Servisler, Subeler, Bitis Uyarilari, Raporlar
- Sirket Admini: Dashboard, Kullanicilar, Yetkili Servisler, Raporlar
- Personel: Sirket secimi, Dashboard, Yetki Belgesi Onaylari, Devreye Almalar, Raporlar, Yetkili Servisler
- Yetkili Servis: Dashboard, Yetki Belgem, Cihaz Devreye Al, Islem Gecmisim, Raporlarim, Subeler, Markalar

## Dikkat Notu

Local testte API'nin Development ortaminda calismasi gerekiyor. API Production ortaminda acilirsa HTTP adresi HTTPS'e yonlenir ve Web panel API cevabi alamaz. Bu durumda Admin dashboard hata verir. Test sirasinda API ve Web Development ortaminda yeniden baslatildi ve dashboard API cevabi duzgun alindi.

## Sunumda Soylenebilecek Kapanis Cumlesi

Sistem su anda rol bazli giris, yetki belgesi onayi, yetkili servis yonetimi, cihaz devreye alma ve raporlama akisini tek panelde gosterebilecek durumdadir. API-Web ayrimi icin ana yapi kuruldu; kalan isler ekran ekran API'ye tasinmaya devam edecektir.
