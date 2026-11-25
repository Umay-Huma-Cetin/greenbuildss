# Veri sözlüğü

Bu dosya `dotnet run --project tests/EcoBuildss.Tests -- --sozluk docs/VERI_SOZLUGU.md` ile şemalardan üretilir; elle düzenlemeyin.

## Tüketim kayıtları (`tuketim_kayitlari.csv`)

Her satır bir binanın bir yıllık ısıtma yakıtı tüketimi (faturalardan). Enerji modelinin gerçek tüketime göre kalibrasyonunda kullanılır.

| Sütun | Açıklama | Biçim | Zorunlu | Örnek |
|---|---|---|---|---|
| `kayit_id` | Benzersiz kayıt kodu | metin | evet | T-0001 |
| `il` | Binanın ili | 81 il adından biri | evet | Malatya |
| `fatura_yili` | Tüketimin ait olduğu yıl | tam sayı, 2000 – 2100 | evet | 2025 |
| `kullanim_alani_m2` | Isıtılan net kullanım alanı | sayı, 20 – 200000 m² | evet | 220 |
| `hacim_m3` | Isıtılan brüt hacim | sayı, 50 – 1000000 m³ | evet | 770 |
| `dis_duvar_alani_m2` | Pencereler hariç opak dış duvar alanı | sayı, 10 – 200000 m² | evet | 250 |
| `pencere_alani_m2` | Toplam pencere alanı | sayı, 0 – 100000 m² | evet | 45 |
| `cati_alani_m2` | Isı kaybeden çatı/tavan alanı | sayı, 0 – 100000 m² | evet | 110 |
| `zemin_alani_m2` | Zemine oturan döşeme alanı | sayı, 0 – 100000 m² | evet | 110 |
| `duvar_U` | Dış duvar U değeri (proje ya da enerji kimlik belgesi) | sayı, 0.1 – 4 W/m²K | evet | 0.45 |
| `pencere_U` | Pencere U değeri | sayı, 0.5 – 6 W/m²K | evet | 1.8 |
| `cati_U` | Çatı U değeri | sayı, 0.1 – 4 W/m²K | evet | 0.35 |
| `zemin_U` | Döşeme U değeri | sayı, 0.1 – 4 W/m²K | evet | 0.6 |
| `yakit` | DG = doğal gaz; diğerleri yakitlar.csv kodları | DG / LPG / KOMUR / ELEK / ISIPOMPASI | evet | DG |
| `yillik_tuketim_kWh` | Yıllık yakıt tüketimi (doğal gazda Sm³ × fatura ısıl değeri ÷ 860) | sayı, 100 – 1000000000 kWh | evet | 32500 |
| `sicak_su_dahil` | Tüketime sıcak su ve pişirme dahil mi (boşsa Evet) | Evet / Hayır | hayır | Evet |
| `isitma_derece_gun` | O yılın gerçekleşen ısıtma derece-günü (boşsa ilin uzun dönem MGM değeri) | sayı, 0 – 8000 °C·gün | hayır |  |

## Teklif geçmişi (`teklif_gecmisi.csv`)

Her satır firmanın verdiği bir teklif ve sonucu (CRM kaydı). Kabul tahmini ve müşteri segmentasyonunda kullanılır.

| Sütun | Açıklama | Biçim | Zorunlu | Örnek |
|---|---|---|---|---|
| `teklif_id` | Benzersiz teklif kodu | metin | evet | TK-2025-001 |
| `tarih` | Teklif tarihi | tarih (yyyy-aa-gg ya da gg.aa.yyyy) | evet | 2025-03-14 |
| `il` | Projenin ili | 81 il adından biri | evet | Malatya |
| `musteri_tipi` |  | Bireysel / Site yönetimi / Müteahhit / Kurum | evet | Site yönetimi |
| `proje_turu` |  | Yeni bina / Yenileme | evet | Yenileme |
| `kullanim_alani_m2` |  | sayı, 20 – 200000 m² | evet | 1450 |
| `paket` |  | Ekonomik / Önerilen / Yüksek performans / Özel | evet | Önerilen |
| `oncelik` | Müşterinin belirttiği öncelik | Bütçe / Dengeli / Enerji ve karbon | evet | Dengeli |
| `teklif_TL_m2` | Teklif tutarı ÷ kullanım alanı (KDV hariç) | sayı, 50 – 50000 TL/m² | evet | 2350 |
| `geri_odeme_yil` | İskontolu geri ödeme süresi (boş = analiz süresinde geri ödemiyor) | sayı, 0 – 100 yıl | hayır | 7.9 |
| `indirim_orani` | Liste fiyatına uygulanan indirim (0,05 = %5) | sayı, 0 – 0.6 | evet | 0.05 |
| `yanit_suresi_gun` | Talepten teklife geçen süre | sayı, 0 – 365 gün | evet | 3 |
| `rakip_teklif` | Müşteri başka firmadan da teklif aldı mı | Evet / Hayır | evet | Evet |
| `sonuc` | Teklifin sonucu | Kabul / Red | evet | Kabul |
