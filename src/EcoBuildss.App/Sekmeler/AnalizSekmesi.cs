namespace EcoBuildss.UI
{
    /// <summary>
    /// Teklif ekranındaki projenin derin analizi: ekonomik yalıtım kalınlığı (optimizasyon + fiyat regresyonu),
    /// bütçe kısıtlı seçim ve Pareto, duyarlılık (ya şöyle olursa) ve Monte Carlo fiyat belirsizliği.
    /// </summary>
    public sealed class AnalizSekmesi : UserControl
    {
        readonly Durum d;
        readonly Func<Girdi?> girdi; readonly Func<Sonuc?> sonuc;
        readonly SacilimGrafik egri = new() { XBaslik = "Yalıtım kalınlığı (cm)", YBaslik = "Ömür boyu maliyet (TL)" };
        readonly SacilimGrafik pareto = new() { XBaslik = "İlk yatırım (TL)", YBaslik = "Yıllık ısıtma (kWh)" };
        readonly Label optMetin = Ui.Aciklama("", 520), butceMetin = Ui.Aciklama("", 520), mcMetin = Ui.Aciklama("", 520);
        readonly NumericUpDown butce = new() { Minimum = 0, Maximum = 1_000_000_000, Increment = 10_000, ThousandsSeparator = true, Width = 140, TextAlign = HorizontalAlignment.Right };
        readonly ComboBox amac = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        readonly ComboBox aile = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        readonly DilimGrafik gaz = new() { Height = 58, Dock = DockStyle.Top }, malzeme = new() { Height = 58, Dock = DockStyle.Top },
            artis = new() { Height = 58, Dock = DockStyle.Top }, sure = new() { Height = 58, Dock = DockStyle.Top };
        readonly CubukGrafik mc = new() { Dock = DockStyle.Fill, EtiketGenisligi = 230 };
        string sonKod = "";

        public AnalizSekmesi(Durum durum, Func<Girdi?> g, Func<Sonuc?> s)
        {
            d = durum; girdi = g; sonuc = s;
            amac.Items.AddRange(new object[] { "En az yıllık ısıtma", "En az karbon", "En düşük ömür boyu maliyet" }); amac.SelectedIndex = 0;
            foreach (var m in FiyatModeli.Hepsi(d.V)) aile.Items.Add(m.Aile);
            if (aile.Items.Count > 0) aile.SelectedIndex = 0;

            var izgara = Ui.Izgara2(2, 50, 50);
            izgara.RowCount = 2; izgara.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); izgara.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            var optUst = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34 };
            optUst.Controls.Add(Ui.Etiket("Yalıtım ailesi")); optUst.Controls.Add(aile);
            var opt = new Panel(); opt.Controls.Add(egri); egri.Dock = DockStyle.Fill; opt.Controls.Add(optMetin); optMetin.Dock = DockStyle.Bottom; opt.Controls.Add(optUst);
            izgara.Controls.Add(Ui.Kart("Ekonomik yalıtım kalınlığı", opt, "Altın oran araması · ek santimetrenin fiyatı levha fiyatlarından regresyonla"), 0, 0);

            var butUst = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34 };
            butUst.Controls.Add(Ui.Etiket("Bütçe (TL)")); butUst.Controls.Add(butce); butUst.Controls.Add(Ui.Etiket("Amaç")); butUst.Controls.Add(amac);
            var but = new Panel(); but.Controls.Add(pareto); pareto.Dock = DockStyle.Fill; but.Controls.Add(butceMetin); butceMetin.Dock = DockStyle.Bottom; but.Controls.Add(butUst);
            izgara.Controls.Add(Ui.Kart("Bütçe kısıtlı seçim ve Pareto cephesi", but, "Bütçeye sığan TS 825 uyumlu paketler içinde amaca göre en iyisi"), 1, 0);

            var duy = new Panel { AutoScroll = true };
            foreach (var (c, ad) in new[] { (sure, "Analiz süresi (yıl)"), (artis, "Enerji fiyatının reel yıllık artışı (%)"), (malzeme, "Malzeme fiyatı değişimi (%)"), (gaz, "Enerji fiyatı değişimi (%)") })
            { duy.Controls.Add(c); duy.Controls.Add(new Label { Text = ad, Dock = DockStyle.Top, Height = 20, Font = Ui.Kalin }); }
            izgara.Controls.Add(Ui.Kart("Duyarlılık: öneri hangi aralıkta değişmiyor?", duy, "Her renk bir önerilen paket; dikey çizgi bugünkü değer"), 0, 1);

            var mcp = new Panel(); mcp.Controls.Add(mc); mcp.Controls.Add(mcMetin); mcMetin.Dock = DockStyle.Top;
            var mcUst = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36 }; mcUst.Controls.Add(Ui.Dugme("1.000 senaryo çalıştır", (_, __) => MonteCarlo(), true)); mcp.Controls.Add(mcUst);
            izgara.Controls.Add(Ui.Kart("Monte Carlo: fiyat belirsizliği", mcp, "Kaynakların verdiği fiyat aralıklarında rastgele fiyatlar (düzgün dağılım, sabit tohum)"), 1, 1);

            Controls.Add(izgara);
            aile.SelectedIndexChanged += (_, __) => Optimum();
            amac.SelectedIndexChanged += (_, __) => Butce();
            butce.ValueChanged += (_, __) => Butce();
            VisibleChanged += (_, __) => { if (Visible) Yenile(); };
        }

        void Yenile()
        {
            var g = girdi(); var r = sonuc();
            if (g == null || r == null || r.Siralama.Count == 0) return;
            string kod = $"{g.Il?.Ad}|{g.Bina}|{g.CamOrani}|{g.Mevcut}|{g.Yakit}|{g.Yil}|{g.MaliyetEnerji}|{g.Kalibrasyon}|{r.Siralama[0].Kod}";
            if (kod == sonKod) return;
            sonKod = kod;
            butce.Value = Math.Min(butce.Maximum, (decimal)Math.Round(r.Siralama[0].IlkYatirim / 10000) * 10000);
            Optimum(); Butce(); Duyarlilik();
            mcMetin.Text = "Çalıştırmak için düğmeye basın."; mc.Cubuklar.Clear(); mc.Invalidate();
        }

        void Optimum()
        {
            var g = girdi(); var r = sonuc();
            if (g == null || r == null || aile.SelectedItem == null) return;
            try
            {
                var model = FiyatModeli.Hepsi(d.V).First(m => m.Aile == (string)aile.SelectedItem);
                var o = r.Siralama[0];
                var k = Optimizasyon.OptimumKalinlik(d.V, g, model, o.Duvar, o.Cam);
                egri.Temizle(); egri.SeriAd = new[] { "Ömür boyu maliyet" };
                egri.Cizgiler.Add(new SacilimGrafik.Cizgi { Noktalar = k.Egri.Select(p => (p.cm, p.lcc)).ToList(), Seri = 0 });
                egri.Dikeyler.Add((k.OptimumCm, "optimum", 1));
                if (k.AltSinirCm > 0) egri.Dikeyler.Add((k.AltSinirCm, "TS 825 alt sınırı", 2));
                egri.Invalidate();
                optMetin.Text = $"Ekonomik kalınlık {Ui.Sayi(k.KisitsizCm, "0.0")} cm, TS 825 için en az {Ui.Sayi(k.AltSinirCm, "0.0")} cm → seçilecek {Ui.Sayi(k.OptimumCm, "0.0")} cm" +
                                (k.KisitBaglayici ? " (yönetmelik belirleyici)." : ".") +
                                $" Fiyat modeli: her ek cm {Ui.Tl(model.EgimCm)}/m² (R² {Ui.Sayi(model.LevhaEgimiKullaniliyor ? model.LevhaR2 : model.R2, "0.000")})." +
                                (k.EnIyiUrun != null ? $" Piyasada satılanlardan en iyisi: {k.EnIyiUrun.KisaAd}." : "");
            }
            catch (Exception e) { optMetin.Text = "Hesaplanamadı: " + e.Message; }
        }

        void Butce()
        {
            var r = sonuc(); if (r == null || r.Siralama.Count == 0) return;
            var a = amac.SelectedIndex switch { 1 => ButceAmaci.Karbon, 2 => ButceAmaci.OmurBoyuMaliyet, _ => ButceAmaci.Isitma };
            var b = Optimizasyon.Butce(r.Siralama, (double)butce.Value, a);
            pareto.Temizle(); pareto.SeriAd = new[] { "Paketler", "Pareto cephesi", "Seçim" };
            var cephe = new HashSet<string>(b.Pareto.Select(s => s.Kod));
            foreach (var s in r.Siralama) pareto.Noktalar.Add(new SacilimGrafik.Nokta { X = s.IlkYatirim, Y = s.YillikIsitmaKWh, Seri = cephe.Contains(s.Kod) ? 1 : 0, Soluk = !cephe.Contains(s.Kod) });
            pareto.Cizgiler.Add(new SacilimGrafik.Cizgi { Noktalar = b.Pareto.OrderBy(s => s.IlkYatirim).Select(s => (s.IlkYatirim, s.YillikIsitmaKWh)).ToList(), Seri = 1, Kesikli = true });
            if (b.Secim != null) pareto.Noktalar.Add(new SacilimGrafik.Nokta { X = b.Secim.IlkYatirim, Y = b.Secim.YillikIsitmaKWh, Seri = 2, Vurgu = true, Etiket = "seçim" });
            pareto.Dikeyler.Add(((double)butce.Value, "bütçe", 2));
            pareto.Invalidate();
            butceMetin.Text = b.Secim == null ? "Bu bütçeye sığan TS 825 uyumlu paket yok." :
                $"Seçim: {TeklifRaporu.PaketAdi(b.Secim)} · {Ui.Tl(b.Secim.IlkYatirim)} · {Ui.Sayi(b.Secim.YillikIsitmaKWh)} kWh/yıl." +
                (b.SonrakiAdim != null ? $" Bütçe {Ui.Tl(b.SonrakiAdim.IlkYatirim - (double)butce.Value)} artarsa: {TeklifRaporu.PaketAdi(b.SonrakiAdim)}." : "");
        }

        void Duyarlilik()
        {
            var g = girdi(); var r = sonuc(); if (g == null || r == null) return;
            var renk = new Dictionary<string, int>();
            void Ciz(DilimGrafik c, List<Dilim> l, double mevcut, Func<double, string> bicim)
            {
                c.Dilimler.Clear(); c.Isaretler.Clear(); c.Bas = l.First().Bas; c.Son = l.Last().Son; c.Mevcut = mevcut; c.Bicim = bicim;
                foreach (var x in l)
                {
                    if (!renk.ContainsKey(x.Oneri.Kod)) renk[x.Oneri.Kod] = renk.Count;
                    c.Dilimler.Add((x.Bas, x.Son, renk[x.Oneri.Kod], TeklifRaporu.PaketAdi(x.Oneri)));
                }
                c.Invalidate();
            }
            try
            {
                Func<double, string> yuzde = x => (x > 0 ? "+" : "") + x.ToString("0", Ui.TR) + "%";
                Ciz(gaz, Core.Duyarlilik.GazFiyati(d.V, g), 0, yuzde);
                Ciz(malzeme, Core.Duyarlilik.MalzemeFiyati(d.V, g), 0, yuzde);
                Ciz(artis, Core.Duyarlilik.EnerjiArtisi(d.V, g), 100 * g.EnerjiArtisi, x => x.ToString("0.#", Ui.TR) + "%");
                Ciz(sure, Core.Duyarlilik.AnalizSuresi(d.V, g), g.Yil, x => x.ToString("0", Ui.TR) + " yıl");
            }
            catch (Exception e) { d.Hata("Duyarlılık hesaplanamadı: " + e.Message, e); }
        }

        void MonteCarlo()
        {
            var g = girdi(); if (g == null) return;
            Cursor = Cursors.WaitCursor;
            try
            {
                var b = Belirsizlik.Calistir(d.V, g);
                mc.Cubuklar.Clear();
                foreach (var (s, oran) in b.Birinciler.Take(6)) mc.Cubuklar.Add((TeklifRaporu.PaketAdi(s), 100 * oran, s.Kod == b.OneriKod ? 0 : 1));
                mc.Bicim = v => "%" + v.ToString("0.0", Ui.TR);
                mc.Invalidate();
                mcMetin.Text = $"{b.Deneme} senaryoda bugünkü öneri %{100 * b.OneriBirinciOrani:0.0} oranında birinci. Önerinin ömür boyu maliyeti %90 olasılıkla " +
                               $"{Ui.Tl(b.LccP5)} – {Ui.Tl(b.LccP95)} (ortanca {Ui.Tl(b.LccP50)}). Fiyat aralığı bilinen {b.AraliklıMalzeme} malzeme değişkendi.";
            }
            catch (Exception e) { mcMetin.Text = "Çalıştırılamadı: " + e.Message; }
            finally { Cursor = Cursors.Default; }
        }
    }
}
