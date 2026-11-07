# EcoBuildss – Test ve Doğrulama Raporu (sürüm 3)

Tarih: 7 Kasım 2026 · Kapsam: hesap çekirdeği (Core), optimizasyon, veritabanı (Depo), arayüz (UI), veri dosyaları (Veri)

## 1. Bağımsız ikinci uygulama ile karşılaştırma

Enerji modeli (TS 825 derece-gün), ömür boyu maliyet, TS 825 kontrolü, AHP, TOPSIS, paket seçimi, fiyat regresyonu,
optimum kalınlık ve bütçe modeli Python ile C# koduna bakılmadan, yalnızca CSV dosyaları ve formüllerden yeniden yazıldı.

Senaryolar: **15 bina geometrisi** (12 ENB formu + 3 özel ölçü: 4×4 m 1 kat, 12×15 m 3 kat, 30×20 m 5 kat)
× 4 pencere oranı × 3 öncelik × TS 825 filtresi açık/kapalı × 3 analiz süresi (10/20/35 yıl) × 2 doğalgaz tarifesi
× 3 pencere doğraması (PVC 5 odacıklı, PVC 3 odacıklı, yalıtım köprülü alüminyum) = **6.480 senaryo**. Python pencere U'sunu
TS 825:2024 Ek A.3 çizelgesinden (ts825_pencere.csv) kendisi okur.

| Karşılaştırılan çıktı | Sonuç |
|---|---|
| Değerlendirilen seçenek sayısı | 6.480 / 6.480 aynı |
| Önerilen paket (TOPSIS 1.) | 6.480 / 6.480 aynı |
| Ekonomik paket | 6.480 / 6.480 aynı |
| Yüksek performans paketi | 6.480 / 6.480 aynı |
| Bütçe modeli (bütçe = önerinin yatırımı; 3 amaç) | 19.440 / 19.440 aynı |
| İlk yatırım, yıllık ısıtma (en büyük fark) | 0 |
| Ömür boyu maliyet (en büyük fark) | 0 TL (reel iskonto oranı artık her iki tarafta faiz ve enflasyondan hesaplanıyor) |
| Ekonomik optimum kalınlık: C# altın oran araması ↔ Python kapalı form | en büyük fark 2 × 10⁻⁶ cm |
| TS 825 alt sınırı (kalınlık) | fark 0 |

Tip 1, %25 pencere, 20 yıl için elle izlenebilir ara değerler (Python): kullanım alanı 220,5 m², pencere 55,125 m²,
opak duvar 238,875 m², çatı U 0,2312, zemin U 0,2488, havalandırma HV 203,74 W/K, PWF 12,6754, pencere U (PVC 5 odacıklı) Low-E + argon 1,7 ve üçlü Low-E 1,2, uygun seçenek 42 / 60,
uygulanmış fiyat doğruları EPS 473,68 + 49,737·cm, Taş yünü 621,71 + 74,342·cm; levha serisi eğimleri EPS 31,2117, taş yünü 71,7203 TL/m² her cm;
yangın yönetmeliği şeridi 63 m² (EPS 10 cm'de 25.200 TL); Pareto cephesi 8 nokta (448.380 → 469.121 → 496.614 → 517.355 → 546.788 → 655.637 → 680.170 → 789.019 TL). Hepsi C# ile aynı.

**Bulgu:** Taş yününde 6.480 senaryonun hepsinde, EPS'de 3.240'ında ekonomik optimum kalınlık TS 825 alt sınırının
altında kalıyor; yönetmelik bağlayıcı. EPS'de ekonomik optimum sınırı geçiyor: doğalgaz Kademe-2 iken 20 ve 35 yılda,
Kademe-1 iken 35 yılda (EPS'nin ek santimetresi 12 noktalı levha serisine göre 31,2 TL/m²; ucuz olduğu için uzun ufukta
yönetmeliğin istediğinden kalın EPS kendini ödüyor).
Aynı nedenle Tip 1'de TS 825'e uygun en ucuz pakete göre yapılan ek yatırımların hiçbiri iskontolu olarak geri dönmüyor.

### Yakıt, soğutma, yenileme ve enerji artışı

4 il (Malatya, Antalya, Erzurum, Şanlıurfa) × 5 ısıtma (doğalgaz, LPG, kömür, elektrikli ısıtıcı, ısı pompası) × 2 tarife kademesi ×
soğutma var/yok × yeni bina/yenileme × reel enerji artışı %0/%3 = **320 senaryo** Python'da (yeni_dogrula.py) yakitlar.csv ve
parametreler.csv'den bağımsız hesaplandı: uygun seçenek sayısı ve önerilen paket 320 / 320 aynı; ömür boyu maliyet en büyük fark
5 × 10⁻¹⁰ TL, yıllık enerji gideri ve CO₂ fark 0, yenilemede mevcut binanın yıllık gideri fark 0. Yangın yönetmeliği şeridi bedeli
6.480 senaryoluk karşılaştırmada da Python'da ayrıca hesaplanıyor (ilk yatırım en büyük fark 1 × 10⁻¹⁰ TL).

### İl iklimi ve TS 825 bölgeleri

81 il × 6 TS 825 bölgesi = **486 senaryo** (Tip 1, %25 pencere, PVC 5 odacıklı, 20 yıl, Dengeli; çatı katmanı bölgeye göre) Python'da bağımsız hesaplandı:
uygun seçenek sayısı ve önerilen paket 486 / 486 aynı; ekonomik optimum kalınlık (EPS ve taş yünü, taş yününde levha
serisinin eğimiyle) en büyük fark 1 × 10⁻⁶ cm; TS 825 alt sınırı fark 0.

### Sınıflandırma – scikit-learn ile karşılaştırma

81 il (özellikler: ısıtma DG, soğutma DG, TS 825 bölgesi; etiket: karar motorunun ilin kendi verisiyle önerdiği paket)
scikit-learn 1.9.1 `DecisionTreeClassifier(criterion="gini")` ve `LeaveOneOut` ile yeniden çalıştırıldı (dogrulama/sinif_dogrula.py).

| Derinlik | LOOCV (C#) | LOOCV (scikit-learn, 20 tohum) |
|---|---|---|
| 1 | %74,1 | %74,1 – %75,3 |
| 2 | %88,9 | %88,9 – %91,4 |
| 3 (seçilen) | %97,5 | %97,5 – %100 |
| 4–6 | %97,5 | %97,5 – %100 |

Fark yalnız eşit kazançlı bölünmelerden: ısıtma DG ≤ 3.064,5 ile bölge ≤ 4,5 aynı Gini azalmasını verir. scikit-learn
eşitlikte özellikleri rastgele sırayla dener; C# her zaman ilk özelliği seçer (tekrarlanabilir, iyimser olmayan uç).
50 tohumun 15'inde scikit-learn C# ağacının birebir aynısını kurar; o ağaçta eğitim doğruluğu %100 ve 81 ilin LOOCV
tahmini C# ile aynı (81 / 81). Taban model (hep en sık paket) %40,7.

### Kümeleme – scikit-learn ile karşılaştırma

Aynı standartlaştırılmış veriyle (81 il, ısıtma ve soğutma derece-günü, z-skoru) scikit-learn 1.9.1 `KMeans`
(k-means++, 200 başlangıç), `silhouette_score` ve `davies_bouldin_score` kullanıldı.

| k | WCSS (C#) | WCSS (scikit-learn) | Siluet (C#) | Siluet (scikit-learn) | Ayarlanmış Rand indeksi |
|---|---|---|---|---|---|
| 2 | 70,730 | 70,730 | 0,570 | 0,570 | 1,00 |
| 3 | 38,680 | 38,680 | 0,507 | 0,507 | 1,00 |
| 4 | 25,192 | 25,192 | 0,497 | 0,497 | 1,00 |
| 5 | 19,215 | 19,215 | 0,491 | 0,491 | 1,00 |
| 6 | 13,801 | 13,801 | 0,524 | 0,524 | 1,00 |
| 7 | 10,941 | 10,941 | 0,479 | 0,479 | 1,00 |
| 8 | 9,290 | 9,290 | 0,470 | 0,470 | 1,00 |

Davies–Bouldin endeksi (C# ve scikit-learn aynı): **k = 2: 0,594** · 3: 0,693 · 4: 0,637 · 5: 0,679 · 6: 0,613 · 7: 0,648 ·
8: 0,659. Uygulama k'yı bu endeksin en düşük olduğu değerden seçer (k = 2); siluet de en yüksek k = 2'dedir. k ≥ 3 arasında
iki ölçüt de k = 6'yı seçer. 78 ilde DB en düşük k = 6 idi (0,576); Çankırı, Karabük ve Şırnak eklenince sıra değişti, seçim
kuralı değiştirilmedi. Standart paket için k = 6 önerilir (README, Kümeleme).

Bütün k değerlerinde küme ataması scikit-learn ile birebir aynı (ARI = 1). İlk denemede 50 başlangıçla k = 8'de yerel
en küçüğe düşüldüğü görüldü (WCSS 8,39); başlangıç sayısı 200'e çıkarıldı.

## 2. Birim testleri (EcoBuildss.Testler)

`cd EcoBuildss.Testler && dotnet run` → **77 test geçti, 0 test kaldı.**

| Grup | Testler |
|---|---|
| Veri yönetimi (13) | bütün CSV'lerde boş hücre yok · 81 il merkezinin bölgesi kaynaklı ve iklim tablosunda · bölge ortalaması ve ortancası derece-günle artar · her k için her kümede bölgesi bilinen il var · 12 bina tipi, yüzey = duvar + 2 × çatı, kompaktlık = 6·V^(2/3)/A · malzeme sayıları ve kalınlık serileri (10 mantolama, 2 kaplamalı cam, 2 çatı katmanı) · hesaplanan fiyatlar: 12/15 cm = 10 cm uygulanmış fiyat + levha farkı, çift kat çatı = 2 × 10 cm şilte; regresyona girmez · TS 825:2024 Ek A.3 pencere U çizelgesi (9 doğrama, 26 değer; PVC 5 odacıklı: Low-E çift 1,7, üçlü 1,2; alüminyumda üçlü cam yok, enerji ve kontrol cam değil pencere U'su ile) · fiyat, λ / U ve kaynak dolu · 19 parametre var ve kaynaklı, türetilmiş ya da tekrarlanan parametre yok · türetilen değerler kodda: 1 Sm³ = 10,647 kWh, 0,20196 kgCO₂/kWh, reel iskonto %4,8008 · CSV okuyucu · özel ölçü geometrisi (10 × 20 m, 2 kat) · kat sayısı |
| Enerji modeli (8) | duvar U formülü (60 birleşim) · çatı ve zemin U · H bileşenleri · 0,024 · DG · H ve kazan verimi · Tip 1 geometrisi · kalınlık arttıkça ısıtma azalır · pencere oranı 0 · pencere alanı duvarı aşınca sınırlama |
| Ekonomi (6) | PWF formülü ve r = 0 durumu · ömür boyu maliyet ve kalem toplamı · iskontolu geri ödemenin bugünkü değer eşitliği · bulgu: uygun paketlerde ek yatırım geri dönmüyor · kontrol değerleri (517.355 / 832.167 TL, 21.404 kWh; 448.380 TL (25.200 TL yangın şeridi dahil) ve 789.019 TL) |
| AHP ve TOPSIS (9) | üç ön ayarın ağırlıkları · tutarsız yargıda CR > 0,10 · 17³ kaydırıcı birleşimi · TS 825 filtresi dört eleman (42 / 60) · puan 0–1 ve azalan sıra · her kriterde geçilen seçenek 1. olamaz · 15 geometri × 4 oran × 3 öncelik × 2 filtrede sonlu sonuç |
| Optimizasyon ve regresyon (9) | EPS uygulanmış fiyat regresyonu elle hesapla aynı, kullanılan doğru ağırlık merkezinden geçer · en küçük kareler koşulu · optimum = kapalı form (2 aile × 3 duvar × 2 cam × 3 süre) · alt sınırda U = 0,35, kısıt ihlali yok, eğrinin en küçüğü optimuma yakın · kontrol: 7,49 cm < 8,45 cm, en iyi ürün EPS 10 cm · en uygun satılık ürün = tam sayım · Pareto cephesi (3 amaç) · bütçe modeli = tam sayım (3 amaç × 21 bütçe) · bütçe yetmezse sonuç yok, sınırsızsa Yüksek performans |
| İl, bölge ve kümeleme (9) | k veriden: Davies–Bouldin en düşük k = 2 (0,594), siluet en yüksek k = 2 (0,570); k ≥ 3 arasında ikisi de k = 6 (0,613 / 0,524) · 81 il, Malatya 2425/374, tablo sırası, bölge kaynağı · 6 bölge ve 4. bölge sınırları · ısıtma derece-günle orantılı · Antalya (1. bölge) 52 seçenek; Erzurum (6. bölge) 20 seçenek, hepsi 10 cm ve üstü yalıtım ve çift kat çatıyla; çatı katmanı bölge sınırını sağlayanların en ucuzu · her il tek kümede, kümeler sıralı, merkez = ortalama · her il kendi merkezine en yakın · siluet aralığı, k taraması, tekrarlanabilirlik, Malatya–Elazığ aynı küme |
| Sınıflandırma (3) | Gini formülü, orta nokta eşiği, saf düğümde durma, eşitlikte ilk özellik ve ada göre ilk sınıf · etiket = karar motorunun 6 ildeki önerisi, 81 il ve 4 sınıf (33 / 7 / 28 / 13), taban %40,7 · LOOCV derinlik 1: %74,1, 2: %88,9, 3–6: %97,5 → 3; eğitim %100; ağaç 2.054 / 3.064,5 / 2.248 DG; yanlışlar Gaziantep ve Kastamonu; matris toplamı 81, köşegen 79; derinlik değiştirmede etiket aynı; Malatya kuralı "2.248 < Isıtma DG ≤ 3.065" |
| Levha regresyonu (2) | taş yünü 8 kalınlık (tek ürün): b = 71,72, R² = 0,9996, %95 aralığı ±1,49; uygulanmış eğim 74,34 · EPS 12 kalınlık (tek ürün): b = 31,21, R² = 0,9992, %95 aralığı ±0,60; optimizasyonda bu eğim kullanılır |
| Kullanım kolaylığı (2) | "Neden bu paket?" gerekçesi önerilen, ekonomik, yüksek performans ve özel seçim için doğru ölçütü ve sırayı söyler · Excel dökümü: 8 parçanın hepsi geçerli XML, genel toplam sayı olarak, XML kaçışı, 60 + 1 satır, sütun adları (A, Z, AA, ZZ) |
| Duyarlılık (1) | dilimler sıralı, ardışık dilimlerin önerisi farklı |
| Yeni modüller (8) | yakıt: LPG/kömür TL/kWh, ısıl değer, yakıt = ısıtma ÷ verim, ısı pompası dirençliden 3,8 kat az · soğutma: 0,024 × SDG × H ÷ 4,6, gider ve CO₂'ye eklenir · yenileme: 20 seçenek, U = 1/(1/1,78 + d/λ), tasarruf, NBD, geri dönüşün bugünkü değer eşitliği · yangın: 35 m binada EPS çıkar (30 birleşim), 28 m'de kalır, şerit 63 m² ve bedeli · reel enerji artışı: PWF = Σ((1+e)/(1+r))^t · duvar ağırlığı · Monte Carlo: aralık okuma, tekrarlanabilirlik, oran toplamı 1, öneri %66,5 · fiyat yaşı |
| Veritabanı (7) | CSV'den oluşturma ve satır sayıları (10 tablo, ts825_pencere, yakitlar, yangin_siniflari dahil) · okunan veri CSV ile birebir aynı (iller, bölgeler, levhalar dahil) · karar sonucu iki kaynakta aynı · teklif kaydet / listele / sil (il, bölge ve doğrama dahil) · CSV değişince yeniden yükleme, teklifler korunur · önceki sürümün veritabanı açılınca il/bölge/doğrama sütunları eklenir · işlem günlüğü: veri yüklemesi kendiliğinden yazılır, Bilgi/Uyarı/Hata kayıtları tarih, kullanıcı ve bilgisayarla saklanır, geçersiz tür reddedilir, veritabanı yoksa uygulama durmaz |

## 3. Arayüz stres testi

Uygulama sanal ekranda açıldı ve kod üzerinden kullanıcı gibi sürüldü; tüm istisnalar yakalandı.

- 13 bina seçeneği (12 form + özel ölçü) × 2 yön × 4 pencere oranı × 3 öncelik = **312 birleşim**, her birinde tüm sekmeler güncellendi.
- Özel ölçü uç değerleri: 4×4 m 1 kat, 80×80 m 1 kat (pencere alanı duvarı aşıyor → uyarı), 80×80 m 10 kat, 4×80 m 10 kat.
- 5 ısıtma × 2 proje türü × soğutma var/yok (20 birleşim).
- 9 pencere doğraması × 4 pencere oranı (alüminyum doğramada uygun seçenek yok uyarısı ve nedeni: pencere U 2,80 > 1,80; üçlü cam kartı gizlenir).
- Analiz süresi 1 ve 50 yıl, TS 825 filtresi açık/kapalı.
- Teklif listesindeki illerin hepsi, 6 bölge sırayla seçildi (Bölgesel analiz sekmesi açıkken); küme sayısı 2–8 değiştirildi.
- Sınıflandırma sekmesi açıkken il, öncelik, ısıtma ve proje türü değiştirildi; ağaç derinliği 1–6 (hata 0).
- Excel'e aktarma arayüzden çalıştırıldı; dosya openpyxl ile açıldı (3 sayfa, tutarlar sayı, 60 seçenek). İlk açılış rehberi
  ilk açılışta bir kez göründü, sonraki açılışta görünmedi.
- Optimizasyon: bütçe 1.000 TL (uygun seçenek yok mesajı) ve 2.000.000 TL, üç amaç, iki yalıtım ailesi.
- Veritabanı: iki teklif kaydı, listeleme, kaydedilmiş özel ölçülü teklifin geri yüklenmesi (en, boy, kat, yön, pencere
  oranı, süre, tarife ve malzemeler aynı geldi), silme.
- Altı sekmenin hepsi açılıp ekran görüntüsü alındı; teklif belgesi A4 sayfaya çizdirilip kontrol edildi.

- Harita, Karşılaştır ve Duyarlılık (Monte Carlo dahil) sekmeleri; yenileme + kömür + klima senaryosunda teklif, Excel ve karşılaştırma.

**Sonuç: 0 hata.** 457 senaryo yaklaşık 42 saniyede hesaplandı.

## 4. Veri denetimi

| Bulgu | Yöntem | Yapılan |
|---|---|---|
| ENB2012'nin 768 satırından yalnız 12 bina geometrisi kullanılıyordu; yön, pencere dağılımı, ısıtma ve soğutma yükü sütunları hesaba girmiyordu | Kod taraması | Veri 12 satırlık `bina_tipleri.csv`'ye indirildi (her geometrinin 64 kayıtta aynı olduğu doğrulandı); ham dosya veri paketinde `kaynak_ham_veri` klasöründe, uygulamaya yüklenmez |
| Tip 2–6'da çatı alanı × yükseklik (857,5 ve 1.029 m³), makaledeki ortak hacimle (771,75 m³) çelişiyor | Kompaktlık bağıntısı 6·V^(2/3)/A ile kontrol | Kompaktlık değeriyle tutarlı olan 771,75 m³ kullanıldı; kullanım alanı = hacim ÷ 3,5 m (Tip 1 ve 7–12 değişmedi) |
| EPS'de ek santimetre maliyeti yalnız 3 uygulanmış fiyata (aralık ortaları) dayanıyordu | Örneklem büyüklüğü (n = 3, serbestlik derecesi 1) | Aynı satıcıdan tek ürünün 12 kalınlıklı levha serisi eklendi (Expert EPS Beyaz, 4–15 cm, Kasım 2026); eğim 49,74 → 31,21 TL/m², R² 0,9992, en büyük Cook uzaklığı 0,31 |
| Levha dosyasında yoğunluk sütunu yalnız taş yünü için biliniyordu (EPS ürün föyünde yoğunluk yayımlanmamış) | Boş hücre taraması | Sütun hesaba girmiyordu; çıkarıldı. Taş yünü yoğunluğu ürün adında (150 kg/m³) |
| Taş yünü levha serisinde 3 cm satırı etkili gözlem (Cook uzaklığı 1,17 > 4/n) | Standartlaştırılmış artık ve Cook uzaklığı | Ürün farklı çıktı (F 150; diğer 8 satır F 150 Pro); çıkarıldı. R² 0,998 → 0,9996, eğim 70,38 → 71,72 TL/m² |
| İllerde ısıtma derece-günü 4 aykırı (Ağrı, Erzurum, Kars, Ardahan), soğutmada 1 (Şanlıurfa) | Çeyrekler arası açıklık (1,5 × IQR), z-skoru (en büyük 3,1) | Ölçüm hatası değil, gerçek iklim uçları: tablo sıralı, değerler aylık toplamlarla tutarlı. Tutuldu; kümelemede soğuk iller ayrı küme oluşturur |
| MGM tablosunda Nevşehir ve Ağrı satırları kaymış | Aylık değerlerin toplamı yıllık değerle karşılaştırıldı | 2894 ve 4517 aylık toplamla doğrulandı |
| MGM tablosunda (Şensoy vd.) Çankırı, Karabük ve Şırnak yok | Liste karşılaştırması | MGM gün-derece sayfasından 1991–2005 yıllık değerleri okunup ortalaması alındı (Çankırı 2.825 / 116, Karabük 2.431 / 94, Şırnak 2.172 / 576); yöntem farkı (ısıtmada ortanca +%1,5, soğutmada ortalama +60 gün-derece, 49 ortak ilde ölçüldü) `kaynak` sütununda yazılı |
| 60 ilde il merkezinin TS 825 bölgesi boştu | Boş hücre taraması | Kaynağı bulunan il sayısı 10'dan 32'ye, sonra tesisat.org il listesi ve iki akademik kaynakla 66'ya, İZODER il föyleriyle 77'ye, son taramada 81'e çıkarıldı (Afyonkarahisar, Çankırı, Karabük, Şırnak: 4. bölge, İZODER föyleri). İki bağımsız akademik kaynakla çapraz kontrol: Gül (2026, DÜMF 17(3), Tablo 6) ve Başarır vd. (2026, ICONARP 14(1), Tablo 2) resmî bölgeleri, veride zaten olan 24 ilin hepsinde bizim değerle aynı (çelişki yok). Bu kaynaklardan 7 il eklendi: Giresun 3, Muğla 3, Siirt 2, Bartın 4, Tunceli 4, Niğde 4, Elazığ 4. Bölgesi kaynakla belli olmayan tek il (Afyonkarahisar) teklif ekranından çıkarıldı: veri eksikken teklif üretilmez. 78 ilin iklim verisi tam olduğu için kümelemede kalır; k = 2…8'in hepsinde her kümede bölgesi bilinen en az bir il vardır (testle doğrulanır) |
| Çatı şiltesinin karbonu boştu (yoğunluk yayımlanmamış) | Boş hücre taraması | Çatı ve zemin katmanı her seçenekte aynı olduğundan karbonları hesaba hiç girmiyordu; bu katmanlar karbon sütunları olmadan ayrı `cati_zemin.csv` dosyasına alındı. Zemindeki kullanılmayan karbon değeri de çıkarıldı |
| `U_W_m2K` sütunu duvarlarda yüzey dirençleriyle, yalıtımda dirençsiz hesaplanmıştı | Sütun formüllerinin yeniden hesaplanması | Tek tanım: camda kaynak U değeri, diğerlerinde λ ÷ d. Bu sütun bilgi amaçlıdır; duvar U'su hesapta katmanlardan bulunur |
| Camda birim ağırlık kalınlık × yoğunluktan farklı | Aynı | Hata değil: ağırlık yalnız cam katmanlarından (2 × 4 mm, üçlü camda 3 × 4 mm); açıklama eklendi |
| M5 ve M6 parametreleri hiçbir hesapta kullanılmıyordu | Kod taraması | Çıkarıldı (hacim artık `bina_tipleri.csv`'de) |
| Aynı bilgi iki dosyada tekrarlanıyordu: Malatya derece-günü (K1, K2) iller.csv'de, Malatya bölgesi (T1) il_bolgeleri.csv'de, 4. bölge sınırları (T2–T5) ts825_bolgeler.csv'de de vardı; E4, E5, E9, I3 ise ham değerlerden türetilmiş kopyalardı | Dosyalar arası karşılaştırma | 11 tekrar/türetilmiş parametre çıkarıldı (30 → 19). Türetilen değerler (Sm³ başına kWh, TL/kWh, kgCO₂/kWh, reel iskonto) kodda ham değerden hesaplanır; test bunların veride tekrar etmediğini doğrular |

**Sonuç:** 8 veri dosyasının hiçbirinde boş hücre yok; bu, birim testiyle her derlemede doğrulanır. Veritabanında da referans tabloların bütün sütunları NOT NULL'dur.


## 5. Sürüm notları

### 7 Kasım 2026 – sürüm 3

| Değişiklik | Neden |
|---|---|
| **Fiyat belirsizliği (Monte Carlo):** kaynak aralıkları içinde 1.000 çekiliş, önerinin birinci kalma oranı | Fiyatlar aralık ortası; önerinin bu belirsizliğe dayanıklılığı ölçülmüyordu |
| **Soğutma:** klima varsa soğutma derece-günüyle elektrik gideri ve CO₂ | Soğutma derece-günü veride vardı ama kullanılmıyordu; sıcak illerde yaz gideri kararı etkiler |
| **Yakıt türü:** LPG, kömür, elektrikli ısıtıcı, ısı pompası (kaynaklı fiyat, ısıl değer, verim, emisyon) | Model herkesin doğalgaz kullandığını kabul ediyordu |
| **Mevcut bina (yenileme):** mevcut duvara mantolama; tasarruf, geri dönüş, net bugünkü değer | Mantolama işlerinin çoğu mevcut binada |
| **Yangın yönetmeliği:** 28,50 m üstünde EPS çıkar; altında zeminden 1,5 m A1 şerit bedeli eklenir | Uygulama yönetmeliğe aykırı paket önerebiliyordu; EPS'nin gerçek maliyeti eksikti |
| **Enerji fiyatı reel artışı** (ayar ve duyarlılık), **duvar ağırlığı** (bilgi), **fiyat yaşı** uyarısı | Uzun ufuk ve deprem açısından ek bilgi; eski fiyatın görünür olması |
| **Harita, Karşılaştır sekmeleri; Malzeme yönetimi penceresi** | İller arası farkı bir bakışta görmek; iki senaryoyu yan yana koymak; veriyi uygulamadan kaynaklı ekleyip düzenlemek |
| **Kaynağı bulunamadığı için yapılmayanlar:** cam g kontrolü, yoğuşma (Glaser), gerçek tüketimle karşılaştırma, endeksle fiyat güncelleme | Veride kaynaksız değer olmaması kuralı (README'da ne gerektiği yazılı) |

### 5 Kasım 2026 – sürüm 2

| Değişiklik | Neden |
|---|---|
| **Yeni enerji modeli:** TS 825 derece-gün yöntemi, Malatya 2425 derece-gün (MGM) | Eski model Atina iklimli ENB2012 simülasyonunu ölçekliyordu; artık Malatya'nın gerçek iklim verisi kullanılıyor |
| **Eklendi:** Çatı ve zemin yalıtımı (İzocam cam yünü 10 cm, XPS 2 × 5 cm), dört elemanlı TS 825 kontrolü | "TS 825'e uygun" artık tüm bina kabuğu için geçerli |
| **Eklendi:** Havalandırma ısı kaybı, kazan verimi (%86) | TS 825 hesap yöntemi ve AB 813/2013 |
| **Eklendi:** Ömür boyu maliyet (bugünkü değer), iskontolu geri ödeme | TCMB faizi ve TÜİK enflasyonundan reel iskonto %4,80 |
| **Eklendi:** Mantolama fiyatları uygulanmış sistem fiyatı (işçilik dahil), KDV %20 satırları | Teklifte işçilik ve KDV soruları |
| **Eklendi:** Özel ölçü (en × boy × kat) | Kullanıcının kendi binası |
| **Eklendi:** Optimizasyon sekmesi (optimum kalınlık, bütçe modeli, Pareto) ve fiyat–kalınlık regresyonu | Hocanın şartı; ikisi de karara doğrudan girdi verir |
| **Eklendi:** SQLite veritabanı ve Kayıtlı teklifler sekmesi | Verinin veritabanında tutulması, tekliflerin saklanması |
| **Eklendi:** 78 ilin MGM derece-günü, il ve TS 825 bölgesi seçimi, 6 bölgenin U sınırları | Uygulama Malatya dışındaki işlerde de kullanılabilsin |
| **Eklendi:** Kümeleme (Bölgesel analiz sekmesi) | İllerin iklim grupları ve her grup için standart paket ve optimum kalınlık |
| **Veri temizliği:** 768 satırlık ENB2012 → 12 satırlık bina tipleri; tip 2–6 hacmi düzeltildi; farklı ürün olan 3 cm levha çıkarıldı; kullanılmayan M5, M6 çıkarıldı; U sütunu tek tanıma getirildi; 8 ilin TS 825 bölgesi eklendi | Hocanın veri incelemesi: eksik, aykırı ve kullanılmayan veri |
| **Güçlendirildi:** Regresyon, taş yünü için 8 kalınlıklı levha fiyat serisi | 3 noktalık modelin zayıflığı; ek cm maliyeti daha geniş veriden |
| **Çıkarıldı:** ENB2012 ısıtma yükü regresyonu ve karar ağacı | Yeni enerji modeli Malatya'ya göre hesaplıyor; Atina simülasyonu üzerine kurulu bu iki model karara bilgi vermiyordu |
| **Çıkarıldı:** Pencere yerleşimi seçimi | Derece-gün yönteminde sonucu değiştirmiyor (güneş kazancı hesaba katılmıyor) |
| **Güçlendirildi (6 Kasım):** EPS regresyonu 12 kalınlıklı levha serisiyle; küme sayısı Davies–Bouldin endeksiyle seçiliyor | Hocanın sorabileceği iki zayıf nokta: EPS'de n = 3, k = 6'nın gerekçesi |
| **Eklendi (7 Kasım): Sınıflandırma sekmesi** — karar ağacı (CART, Gini), etiket karar motorunun il önerisi, derinlik LOOCV ile; ağaç çizimi, seçili ilin kuralı, karmaşıklık matrisi; 3 yeni test, scikit-learn karşılaştırması | Dersin sınıflandırma haftası; karar motorunun sahada kullanılabilir kurallara indirilmesi |
| **Veri taraması (7 Kasım):** Afyonkarahisar, Çankırı, Karabük, Şırnak'ın TS 825 bölgesi İZODER föylerinden (hepsi 4. bölge; Ek A.3 çizelgesi 13 föyde aynı); Çankırı, Karabük, Şırnak'ın derece-günü MGM gün-derece sayfasından → 81 il. EL1 yazım hatası düzeltildi (2,919065 → 2,918965). Üçlü camın adındaki "4+12+4+12+4" kaynakta yok → addan çıkarıldı (Up 12/16 mm'nin büyüğü; sonuç değişmedi). Bütün fiyat kaynakları yeniden okundu: tuğla (Eylül), gazbeton (Nisan), bims (Ekim), XPS (1.300 TL/paket), cam, mantolama, BOTAŞ ve EPDK değişmemiş; taş yünü levha sayfası Kasım 2026'ya güncellenmiş, fiyatlar aynı. Kümeleme 81 ilde k = 2 (DB ve siluet); harita ve bölgesel analiz 81 il | Hocanın "kesinlik" ölçütü: bütün iller kaynaklı veriyle |
| **Kesinlik (6 Kasım, gece):** pencere U'su artık TS 825:2024 Ek A.3'ten cam + doğrama değeri (kullanıcı doğramayı seçer); kaplamasız standart cam çıkarıldı (TS 825:2024 kaplamalı cam ister). 12/15 cm mantolama ve çift kat çatı, yayımlanmış değerlerden hesaplanan fiyatla tutuldu; hesap her satırda yazılı ve raporda açıklanır | Pencere kontrolü standardın kendi tanımıyla; yayımlanmamış fiyatlar hesapla ve açık formülle |
| **Veri (6 Kasım, üçüncü tarama):** İZODER il föyleriyle 11 il daha eklendi (Amasya, Bilecik, Karaman, Kırıkkale, Kırklareli, Kırşehir, Nevşehir, Uşak 4; Sakarya, Sinop, Zonguldak 3; Sinop'ta merkez 3, Boyabat 4). Föylerin ilk sayfası ve metni veri paketinde (kaynak_ham_veri/izoder_il_foyleri). (66 → 77 il; tek eksik Afyonkarahisar) | Eksik veri |
| **Veri (6 Kasım, ikinci tarama):** İki bağımsız akademik kaynakla çapraz kontrol: Gül (2026, DÜMF 17(3), Tablo 6) ve Başarır vd. (2026, ICONARP 14(1), Tablo 2) resmî bölgeleri, veride zaten olan 24 ilin hepsinde bizim değerle aynı (çelişki yok). Bu kaynaklardan 7 il eklendi: Giresun 3, Muğla 3, Siirt 2, Bartın 4, Tunceli 4, Niğde 4, Elazığ 4. (59 → 66 il) | Eksik veri |
| **Veri (6 Kasım, akşam):** 27 ilin il merkezi TS 825:2024 bölgesi eklendi (tesisat.org il listesi; 32 → 59 il). Van için kaynaklar ayrışıyor (İZODER Van föyü 6, tesisat.org 5); il föyü birincil kaynak olduğu için 6 tutuldu. BOTAŞ tarifesinin 31 Ekim 2026'da hâlâ 4 Nisan 2026 tarifesi olduğu doğrulandı. | Hocanın veri incelemesi: eksik veri |
| **Sadeleştirildi (6 Kasım, akşam):** ekrandaki açıklama metinleri kaldırıldı (il ve bina bilgi satırları, öncelik açıklaması, kapsam metni, sekme giriş paragrafları, küme ve duyarlılık dipnotları); yalnız uyarılar ve sonuçlar kaldı, açıklamalar "?" işaretlerinde ve Hesaplama yöntemi penceresinde | Kalabalık metin kafa karıştırıyordu |
| **Arayüz (6 Kasım, akşam):** düz, yeşil alt çizgili sekmeler; okunaklı "Hesaplama yöntemi" ve "Veri kaynakları" pencereleri; kısayollar (Ctrl+S, Ctrl+P, Ctrl+E, F1); boş kayıt listesinde yönlendirme; gösterge kartları satırı doldurur | Kullanılabilirlik |
| **Eklendi (6 Kasım):** ilk açılış rehberi, terim açıklamaları (?), "Neden bu paket?", Excel'e aktarma; uygulamanın adı EcoBuildss | Kullanılabilirlik ve anlaşılırlık |
| **Değişti:** XPS ve cam yünü cephe seçeneklerinden çıktı | Cephe mantolamasında 2026 uygulanmış fiyatı bulunan aileler EPS ve taş yünü; XPS zemine, cam yünü çatıya taşındı |

## 6. Sınırlar

- Bu ortamda Windows bulunmadığı için arayüz Mono WinForms ile, veritabanı Mono.Data.Sqlite sürücüsüyle test edildi.
  Windows'ta uygulama Microsoft.Data.Sqlite kullanır (aynı SQL ve ADO.NET arabirimi). Yazdırma penceresi Windows'a özgüdür.
- Güneş kazancı ve iç kazançlar hesaba katılmaz (TS 825'in tam aylık yöntemi yerine yıllık derece-gün yaklaşımı);
  sonuçlar seçenekleri karşılaştırmak içindir.
- Mantolama dışındaki kalemlerde işçilik ve nakliye yoktur: Çevre, Şehircilik ve İklim Değişikliği Bakanlığı'nın 2026 rayiç
  belgesine bu ortamdan erişilemedi. Bu işçilikler her seçenekte aynı birim fiyatla eklenseydi sıralama değişmezdi;
  malzemeye göre farklı olmaları durumunda etkisi bu sürümde ölçülemiyor.
- Fiyatlar 2026 piyasa kaynaklarından alınmıştır (kaynaklar `Veri/malzemeler.csv` içinde).
- 81 ilin hepsi var. Çankırı, Karabük ve Şırnak'ın derece-günü MGM'nin kendi sayfasından (1991–2005 yıllık değerlerinin ortalaması)
  alındı; diğer 78 il Şensoy vd. (1975–2005). İki yöntem 49 ortak ilde ısıtmada ortanca %1,5 farklı (−%6,0…+%10,8). Bu üç il için
  bu fark kadar belirsizlik vardır; `kaynak` sütununda yazılıdır.
- 12 ve 15 cm mantolama ve çift kat çatı fiyatı yayımlanmış değildir, yayımlanmış değerlerden hesaplanır: 12/15 cm = aynı ailenin
  10 cm uygulanmış fiyatı + aynı ürünün levha fiyat farkı; çift kat çatı = 10 cm şilte fiyatının iki katı. Bu beş satır veride
  "Hesaplanan:" ile işaretlidir. ÇŞB 2026 resmî birim fiyatlarında (poz 15.341.1001–1004 ve 15.341.3001–3004) mantolama fiyatı
  kalınlıkla doğrusal artar (EPS'de her cm 21,66 TL; taş yününde 52,5–53,6 TL): işçilik ve aksesuar kalınlıktan bağımsızdır, hesabın dayanağı budur.
- Low-E çift ve üçlü camın ara boşluğu kaynakta yazmadığından pencere U'su Ek A.3'teki 12 ve 16 mm değerlerinin büyüğüdür (en elverişsiz).
  Camların g değeri kaynakta yayımlanmadığından TS 825:2024'ün g sınırı denetlenmez; kontrol U sınırlarıyla yapılır.
- EPS levha serisi (Kasım 2026) ile uygulanmış EPS fiyatları (Şubat 2026, aralık ortaları) farklı aylardan; uygulanmış
  fiyatlar kullanılan doğrunun ağırlık merkezini belirler, eğim levha serisinden gelir. Kullanılan doğrunun 3 uygulanmış
  fiyata uyumu R² 0,86'dır (taş yününde 0,99): EPS uygulanmış fiyat aralıkları kalınlıkla levhadan daha hızlı artıyor.
- 81 ilde Davies–Bouldin ve siluet k = 2'yi seçer; uygulama bu k ile açılır. k = 2'de ikinci küme 2.–6. bölgeden 62 il içerir ve
  standart paketi (EPS 12 cm) 5.–6. bölgede TS 825'i sağlamaz. Standart paket için k = 6 (k ≥ 3 arasında iki ölçütün seçtiği) kullanılmalıdır;
  teklif her zaman ilin kendi bölgesiyle hesaplanır.
