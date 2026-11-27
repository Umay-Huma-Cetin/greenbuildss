# EcoBuildss

Yalıtım ve inşaat firmaları için karar destek sistemi. Üç gerçek işi çözer:

1. **Apartman mantolaması.** Bu işler kat malikleri toplantısında kazanılır ya da kaybedilir. Toplantıda herkes aynı üç şeyi sorar: daire başına ne ödeyeceğim, faturam ne kadar düşecek, kaç yılda döner. Sahada 10 dakikada bu üç sorunun cevabı ve toplantıda dağıtılacak tek sayfa çıkar. Bir dairenin gerçek doğal gaz faturası girilirse tasarruf o faturadan hesaplanır.
2. **Yeni bina.** Ruhsattan önce TS 825 ve yangın yönetmeliğine uyan duvar–yalıtım–cam paketlerinden en uygun maliyetlisi; keşif, teklif ve Excel çıktısıyla.
3. **Tekliflerim.** Verilen tekliflerin takibi: bekleyen işler, kazanma oranı, kazanılan iş. Sonuçlar işaretlendikçe sistem firmanın kendi verisinden kazanma olasılığını öğrenir.

Geliştiren: **Umay Hüma Çetin**

![Apartman mantolaması](docs/ekran/02_apartman_mantolamasi.png)

## Ekranlar

| Ekran | Kullanıcıya sağladığı | Arkadaki yöntem |
|---|---|---|
| **Ana sayfa** | Üç işten birini seçme, bekleyen teklif sayısı | — |
| **Apartman mantolaması** | Daire başına tutar (KDV dahil), taksit, ısıtma faturasındaki düşüş, geri ödeme süresi, toplantı sayfası (yazdır / PDF) | TS 825 derece-gün modeli, gerçek faturadan tasarruf, iskontolu geri ödeme, anüite, Kat Mülkiyeti Kanunu md. 42 |
| **Yeni bina** | Üç paket, bina çizimi ve ölçekli duvar kesiti, malzemeleri tek tek değiştirme, keşif ve teklif, ayrıntılı analiz | AHP + TOPSIS, ömür boyu maliyet, kural tabanı (TS 825, yangın yönetmeliği md. 27), optimizasyon, Monte Carlo |
| **Tekliflerim** | Bekleyen / kazanılan işler, kazanma oranı, her teklifin kazanma olasılığı | Sınıflandırma (kalite eşiğini geçerse) |
| **Veri ve modeller** | Firma bilgileri, gerçek veri yükleme, modellerin doğrulaması | Regresyon (kalibrasyon), sınıflandırma, kümeleme |

| | |
|---|---|
| ![Ana sayfa](docs/ekran/01_ana_sayfa.png) | ![Toplantı sayfası](docs/ekran/03_toplanti_sayfasi.png) |
| ![Yeni bina](docs/ekran/04_yeni_bina.png) | ![Tekliflerim](docs/ekran/05_tekliflerim.png) |

## Güvenlik ve işlem günlüğü

**Kullanıcılar ve roller**
- Program oturum açılmadan açılmaz. İlk çalıştırmada firmanın yönetici hesabı oluşturulur; diğer kullanıcıları yönetici ekler.
- **Yönetici** her şeyi yapar. **Satış** teklif hazırlar, kaydeder ve sonucunu işaretler. Satış rolü teklif silemez, veri yükleyemez, kullanıcı ve ayar değiştiremez.
- Yetki yalnız arayüzde değil, veritabanı katmanında da denetlenir. Yetkisiz istek reddedilir ve günlüğe yazılır.
- Sistemde en az bir etkin yönetici kalması zorunludur.

**Parolalar ve oturum**
- Parolalar PBKDF2-HMAC-SHA256 ile saklanır: 600.000 tekrar, kullanıcıya özel tuz. Parolanın kendisi hiçbir yerde tutulmaz.
- Parola kuralı: en az 8 karakter, harf ve rakam içermeli, kullanıcı adını içermemeli.
- Yöneticinin verdiği geçici parola ilk girişte değiştirilmek zorundadır.
- 5 hatalı denemede hesap 15 dakika kilitlenir. Kullanıcı adının var olup olmadığı giriş ekranında belli edilmez.
- 15 dakika işlem yapılmazsa oturum kilitlenir.

**İşlem günlüğü** (`islem_gunlugu` tablosu)
- Şu işlemler kim yaptıysa onun adıyla, tarih ve seviyesiyle (Bilgi, Uyarı, Hata, Güvenlik) yazılır:
  - program açılışı ve kapanışı
  - oturum açma ve kapama, başarısız giriş, hesap kilitlenmesi
  - teklif kaydı, silme, sonuç işaretleme, Excel'e aktarma, yazdırma
  - veri yükleme ve silme
  - ayar, kullanıcı ve parola değişiklikleri
  - yedek alma ve geri yükleme
  - hatalar
- Her kayıt bir öncekinin SHA-256 özetini içerir. Kayıtlar böylece zincirlenir: biri sonradan değiştirilir ya da silinirse **Güvenlik ve yedek** sekmesindeki denetim bunu bulur.
- Veritabanına yazılamayan hatalar ayrıca `%AppData%\EcoBuildss\hata.log` dosyasına yazılır.

**Yedek**
- Program her gün ilk açılışta otomatik yedek alır ve son 14 günün yedeğini saklar.
- Yönetici istediği an yedek alabilir ve yedekten geri yükleyebilir. Geri yüklemeden önce mevcut durum ayrıca yedeklenir.
- Yedekler SQLite'ın çevrimiçi yedekleme arayüzüyle alınır, yani tutarlıdır.

**Sınır:** Veritabanı dosyası şifreli değildir; Windows'un kendi SQLite'ı şifrelemeyi desteklemez. Dosya, kullanıcının kendi `%AppData%` klasöründe Windows dosya izinleriyle korunur. Günlükteki değişiklikler zincir sayesinde fark edilir.

![Güvenlik ve yedek](docs/ekran/08_guvenlik_ve_yedek.png)

## Kurulum

Gerekenler: Windows 10/11 ve Visual Studio 2022 (.NET 8, “.NET masaüstü geliştirme” iş yükü).

1. `EcoBuildss.sln` dosyasını açın.
2. **F5** tuşuna basın. Başlangıç projesi `EcoBuildss.App`'tir.
3. İlk açılışta yönetici hesabınızı oluşturun.

Hiçbir NuGet paketi gerekmez. SQLite'a Windows'un kendi `winsqlite3.dll` dosyasıyla bağlanılır. `nuget.config` paket kaynaklarını kapatır, bu yüzden proje internetsiz de derlenir. Veritabanı `%AppData%\EcoBuildss\ecobuildss.db` dosyasıdır.

## Gerçek veriyle kullanım

İlk açılışta veri yoktur. Teklif ekranı yine çalışır; enerji TS 825 modeliyle hesaplanır, kabul tahmini kapalıdır.

1. **Veri ve modeller → Veri yükleme → Boş şablon kaydet.** Tüketim kayıtları (faturalar) ve teklif geçmişi (CRM) için şablonu alın. Sütunlar [docs/VERI_SOZLUGU.md](docs/VERI_SOZLUGU.md) dosyasında açıklanmıştır.
2. Şablonu Excel'de doldurup CSV olarak kaydedin. Türkçe Excel'in `;` ayırıcısı ve `1.234,5` sayı biçimi de okunur.
3. **Kendi dosyamı içe aktar.** Denetim raporu her hatalı satırın satır numarasını, sütununu ve nedenini gösterir. Gerçek veri yüklenince örnek veri silinir, ikisi karışmaz.
4. **Modeller ve doğrulama** sekmesinde sonuçlara bakın. Bir model ancak kalite eşiğini geçerse teklif ekranında kullanılır:
   - **Kalibrasyon:** en az 20 kayıt gerekir. Görülmemiş kayıtlarda (5 katlı çapraz doğrulama) hatayı düşürmüyorsa uygulanmaz.
   - **Kabul tahmini:** en az 100 teklif ve her sonuçtan en az 20 kayıt gerekir. Modelin seçimde hiç kullanılmamış %25'lik test kümesinde şu üç koşulu sağlaması gerekir:
     - F1 ≥ 0,87
     - ROC-AUC ≥ 0,80
     - "her teklife kabul de" tahmininden belirgin iyi olmak

     Sağlamazsa teklif ekranında **gösterilmez**.
5. Uygulamada verilen her teklifi kaydedin, sonucu belli olunca **Tekliflerim** ekranında Kabul/Red işaretleyin. Model bu kayıtlarla kendini yeniden kurar.

Gerçek veri yokken denemek için **Veri yükleme → Örnek (sentetik) veriyi yükle** düğmesi vardır. Örnek veri kullanıldığı sürece sol altta uyarı görünür.

## Modeller ve doğrulama

Aşağıdaki sonuçlar `veri/ornek` klasöründeki **sentetik** veriyle alınmıştır. Bu verideki binalar gerçek il iklimi ve gerçek malzeme fiyatlarıyla hesaplanmıştır. Kullanıcı ve müşteri davranışı ise `SentetikUretici.cs` içinde açıkça yazılı varsayımlarla üretilmiştir: kullanıcılar modelin %85'i kadar ısıtır, binadan binaya %18 sapma vardır, müşteri kararına gürültü eklenmiştir.

Sentetik veride F1 bu varsayımlara bağlıdır. Gerçek veride başarı, verinin kendisine bağlıdır ve önceden bilinemez. Bu yüzden kalite eşiği uygulamanın içindedir.

| Model | Ne çözer | Doğrulama | Sonuç (örnek veri) |
|---|---|---|---|
| Kalibrasyon regresyonu | TS 825 modeli gerçek faturadan ne kadar sapıyor? Sıcak su baz yükü ne kadar? | 5 katlı çapraz doğrulama, ASHRAE Guideline 14 | β₁ = 0,814 (%95 GA 0,783–0,844); CV(RMSE) %23,6 → %18,8; NMBE %0,08 |
| Teklif kabul tahmini | Bu teklif kabul edilir mi? Hangi koşul kabulü artırır? | Tabakalı 5 katlı çapraz doğrulama + ayrı test kümesi | Lojistik regresyon. Çapraz doğrulama F1 0,896 ± 0,014. Test: F1 0,915, AUC 0,964 ("hep kabul" tahmininin F1'i 0,718) |
| İl kümeleme | Pazar hangi iklim bölgelerine ayrılıyor? Bölgeye standart paket ne olmalı? | Davies–Bouldin, siluet | k = 2 (DB 0,594, siluet 0,570). Gerçek MGM verisi |
| Müşteri segmentleri | Hangi müşteri grubu hangi koşulda kabul ediyor? | Davies–Bouldin, siluet | k = 4 (siluet 0,32) |

Modellerin doğru çalıştığı da ayrıca sınanmıştır:
- **Etiketler rastgele karıştırıldığında** (veride sinyal yokken) kabul modeli kalite eşiğinden **geçemez** (AUC ≈ 0,5). Eşiğin sahte başarıyı yakaladığını gösterir.
- **Python ile bağımsız tekrar:** `araclar/dogrulama/bagimsiz_dogrulama.py` aynı dosyalardan scikit-learn ile yeniden hesaplar. Regresyon katsayıları ve il kümeleri birebir aynı çıkar. Kabul modeli farklı bir rastgele bölmede aynı düzeyde kalır (lojistik regresyon test F1 0,906).

## Testler

```
dotnet run --project tests/EcoBuildss.Tests
python araclar/dogrulama/bagimsiz_dogrulama.py          # isteğe bağlı: pandas, scikit-learn
python araclar/dogrulama/bagimsiz_dogrulama.py <klasör> # kendi gerçek veriniz için
```

81 test vardır ve şunları kapsar:
- metrikler (F1, AUC, CV(RMSE))
- regresyon ve gerçek levha fiyatları
- enerji motoru ve yangın yönetmeliği kuralı
- hatalı CSV satırlarının reddi
- uçtan uca örnek veri akışı
- az veride ve sinyalsiz veride kalite eşiğinin modeli reddetmesi
- apartman hesabı (kapsam, faturadan tasarruf, taksit)
- SQLite kaydı (parametreli sorgu)
- güvenlik: parola özeti, kilitleme, roller, geçici parola, günlük zinciri ve değişiklik tespiti, yedek ve geri yükleme

## Klasörler

```
src/EcoBuildss.Core     Hesap çekirdeği (arayüzden bağımsız, test edilebilir)
  Referans/             Kaynak gösterimli referans verinin okunması
  Enerji/               TS 825 derece-gün enerji modeli, ömür boyu maliyet, AHP + TOPSIS, yangın kuralı
  Karar/                Optimizasyon, Pareto, duyarlılık, Monte Carlo, KPI
  Ogrenme/              Regresyon, lojistik regresyon, karar ağacı, rastgele orman, k-ortalamalar, metrikler (dış kütüphane yok)
  Veri/                 CSV şemaları ve içe aktarma denetimi
  Uygulamalar/          Kalibrasyon, kabul tahmini, segmentasyon
  Sentetik/             Örnek veri üreteci (yalnız deneme için)
  Depo/                 SQLite (winsqlite3) ve veritabanı
src/EcoBuildss.App      Windows Forms arayüzü
tests/EcoBuildss.Tests  Birim ve bütünleşik testler
veri/referans           İklim (MGM), TS 825:2024, yangın yönetmeliği, malzeme fiyatları, tarifeler; her satırda kaynak
veri/ornek              Sentetik örnek veri (gerçek değildir)
araclar/dogrulama       Python ile bağımsız doğrulama
docs/                   Veri sözlüğü, ekran görüntüleri
```

## Sınırlılıklar

- Enerji modeli yıllık derece-gün yöntemidir. Güneş kazancı ve ısıl kütle ayrı hesaplanmaz. Kalibrasyon bu sapmayı gerçek tüketimle toplu olarak düzeltir.
- Bina dikdörtgen plan olarak girilir. Dikdörtgen olmayan planlar için çekirdekte `Bina.Alanlarla(...)` vardır.
- Referans fiyatlar Kasım 2026 tarihlidir. Malzeme ve tarife güncellemesi `veri/referans` dosyalarından yapılır; her satırda kaynak ve tarih bulunur.

## Lisans

[LICENSE](LICENSE)
