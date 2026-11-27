"""
EcoBuildss — bağımsız doğrulama (Python + scikit-learn).

C# çekirdeğinin ürettiği sonuçlar, aynı veri dosyalarından tamamen ayrı bir kodla yeniden hesaplanır:
  1) Kalibrasyon regresyonu (EKK katsayıları)       → C# ile birebir aynı olmalı
  2) İl kümelemesi (k-means, Davies–Bouldin, siluet) → C# ile aynı k ve aynı ölçüler
  3) Teklif kabul sınıflandırması                    → farklı kod, farklı rastgele bölme: başarı aynı düzeyde olmalı

Kullanım:  python araclar/dogrulama/bagimsiz_dogrulama.py <veri_klasoru>
Klasörde tuketim_kayitlari.csv ve teklif_gecmisi.csv bulunmalı (gerçek veriniz ya da
`dotnet run --project tests/EcoBuildss.Tests -- --ornek-yaz <klasör>` ile yazılan test verisi).
"""
import sys, pathlib
import numpy as np, pandas as pd
from sklearn.linear_model import LinearRegression, LogisticRegression
from sklearn.ensemble import RandomForestClassifier
from sklearn.cluster import KMeans
from sklearn.metrics import davies_bouldin_score, silhouette_score, f1_score, roc_auc_score
from sklearn.model_selection import StratifiedKFold, cross_val_predict, train_test_split
from sklearn.preprocessing import StandardScaler
from sklearn.pipeline import make_pipeline

KOK = pathlib.Path(__file__).resolve().parents[2]
if len(sys.argv) < 2:
    sys.exit("Kullanım: python araclar/dogrulama/bagimsiz_dogrulama.py <veri_klasoru>")
VERI = pathlib.Path(sys.argv[1])
REF = KOK / "veri" / "referans"

p = pd.read_csv(REF / "parametreler.csv").set_index("id")["deger"].astype(float)
iller = pd.read_csv(REF / "iller.csv")
yakit = pd.read_csv(REF / "yakitlar.csv").set_index("id")

# ---------------- 1) Kalibrasyon ----------------
t = pd.read_csv(VERI / "tuketim_kayitlari.csv")
t = t.merge(iller[["il", "isitma_derece_gun"]].rename(columns={"isitma_derece_gun": "dg_uzun"}), on="il")
dg = t["isitma_derece_gun"].fillna(t["dg_uzun"])
H = (t.duvar_U * t.dis_duvar_alani_m2 + t.pencere_U * t.pencere_alani_m2 + p["F1"] * t.cati_U * t.cati_alani_m2
     + p["F2"] * t.zemin_U * t.zemin_alani_m2 + p["V3"] * p["V1"] * p["V2"] * t.hacim_m3)
verim = t.yakit.map(lambda y: p["H1"] if y == "DG" else yakit.loc[y, "verim"])
model = 0.024 * dg * H / verim
su = np.where(t.sicak_su_dahil.fillna("Evet") == "Hayır", 0, t.kullanim_alani_m2)
X = np.column_stack([model, su]); y = t.yillik_tuketim_kWh.values
lr = LinearRegression().fit(X, y)
print(f"Kalibrasyon  β0 {lr.intercept_:.3f}  β1 {lr.coef_[0]:.6f}  β2 {lr.coef_[1]:.4f}  R² {lr.score(X, y):.6f}  (n={len(y)})")

# ---------------- 2) İl kümelemesi ----------------
Z = StandardScaler().fit_transform(iller[["isitma_derece_gun", "sogutma_derece_gun"]])
for k in range(2, 9):
    km = KMeans(k, n_init=50, random_state=42).fit(Z)
    print(f"İller k={k}  DB {davies_bouldin_score(Z, km.labels_):.3f}  siluet {silhouette_score(Z, km.labels_):.3f}")

# ---------------- 3) Teklif kabul sınıflandırması ----------------
k = pd.read_csv(VERI / "teklif_gecmisi.csv").merge(iller[["il", "isitma_derece_gun"]], on="il")
uyum = (((k.oncelik == "Bütçe") & (k.paket == "Ekonomik")) | ((k.oncelik == "Dengeli") & (k.paket == "Önerilen"))
        | ((k.oncelik == "Enerji ve karbon") & (k.paket == "Yüksek performans"))).astype(float)
# Fiyat endeksi: aynı proje türündeki tekliflerin medyan m² fiyatına oran (C# eğitim kümesinden hesaplar; burada bölmeden sonra)
F = np.column_stack([
    k.teklif_TL_m2, k.geri_odeme_yil.fillna(40).clip(upper=40), k.indirim_orani, k.yanit_suresi_gun, (k.rakip_teklif == "Evet"),
    (k.proje_turu == "Yenileme"), np.log(k.kullanim_alani_m2), uyum, (k.paket == "Ekonomik"), (k.paket == "Yüksek performans"),
    (k.paket == "Özel"), (k.musteri_tipi == "Site yönetimi"), (k.musteri_tipi == "Müteahhit"), (k.musteri_tipi == "Kurum"), k.isitma_derece_gun,
]).astype(float)
Y = (k.sonuc == "Kabul").astype(int).values
yen = (k.proje_turu == "Yenileme").values
Ftr, Fte, Ytr, Yte, ytr_, yte_ = train_test_split(F, Y, yen, test_size=0.25, stratify=Y, random_state=0)   # C#'tan farklı bölme
med = {True: np.median(Ftr[ytr_, 0]), False: np.median(Ftr[~ytr_, 0])}   # yalnız eğitim kümesinden
for A, Yen in ((Ftr, ytr_), (Fte, yte_)):
    A[:, 0] = A[:, 0] / np.where(Yen, med[True], med[False])
cv = StratifiedKFold(5, shuffle=True, random_state=0)
for ad, m in [("Lojistik regresyon", make_pipeline(StandardScaler(), LogisticRegression(C=1.0, max_iter=1000))),
              ("Rastgele orman", RandomForestClassifier(300, max_depth=8, min_samples_leaf=3, random_state=0))]:
    oof = cross_val_predict(m, Ftr, Ytr, cv=cv, method="predict_proba")[:, 1]
    m.fit(Ftr, Ytr); pt = m.predict_proba(Fte)[:, 1]
    print(f"{ad:20s} CV F1 {f1_score(Ytr, oof >= .5):.3f}  CV AUC {roc_auc_score(Ytr, oof):.3f}  |  TEST F1 {f1_score(Yte, pt >= .5):.3f}  AUC {roc_auc_score(Yte, pt):.3f}")
print(f"'Hep kabul' tahmininin test F1'i: {f1_score(Yte, np.ones_like(Yte)):.3f}")
