using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EcoBuildss.Core;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Teklif paketi kartı (Ekonomik / Önerilen / Yüksek performans).
    /// Sektördeki "iyi – daha iyi – en iyi" teklif sunumunu izler.
    /// </summary>
    public sealed class PaketKarti : Control
    {
        static readonly Color Yesil = Color.FromArgb(31, 111, 74);
        public string Baslik = "", Rozet = "";
        public Secenek? Secenek;
        public double YillikGider, GeriOdeme;
        public bool GeriOdemeVar, Vurgulu, EkYatirim, Tasarruf;   // Ekonomik pakete göre ek yatırım / yakıt tasarrufu var mı
        public int AnalizSuresi = 20;   // Vurgulu = önerilen paket (yeşil başlık)
        bool secili, uzerinde;
        public event EventHandler? Secildi;
        /// <summary>Son çizimde içeriğin sığması için gereken yükseklik (dar pencerelerde satırlar kırılınca artar).</summary>
        public int IstenenYukseklik { get; private set; } = 178;
        public event EventHandler? YukseklikDegisti;

        public bool Secili { get => secili; set { secili = value; Invalidate(); } }

        public PaketKarti()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            BackColor = Color.White; Cursor = Cursors.Hand; TabStop = true;
            Height = 178; Margin = new Padding(0, 0, 12, 0);
        }

        protected override void OnMouseEnter(EventArgs e) { uzerinde = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { uzerinde = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Focus(); Secildi?.Invoke(this, EventArgs.Empty); base.OnClick(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode is Keys.Enter or Keys.Space) Secildi?.Invoke(this, EventArgs.Empty); base.OnKeyDown(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Cizim.Hazirla(g);
            var tr = Ortak.TR;
            var s = Secenek;
            int w = Width - 24;   // iç genişlik
            using var tek = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
            using var fBas = new Font("Segoe UI Semibold", 11f);
            using var fRoz = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            using var fBuyuk = new Font("Segoe UI Semibold", Width < 200 ? 15f : 18f);
            using var f = new Font("Segoe UI", 9f);
            using var fk = new Font("Segoe UI", 8.5f);
            using var metin = new SolidBrush(Cizim.Metin);
            using var soluk = new SolidBrush(Cizim.Soluk);
            using var turuncu = new SolidBrush(Color.FromArgb(180, 84, 26));
            void Satir(string t, Font ft, Brush b, float y, float h = 20) => g.DrawString(t, ft, b, new RectangleF(12, y, w, h), tek);

            // başlık bandı: rozet sığmıyorsa bandın altına ikinci satır olarak yazılır
            var basGen = g.MeasureString(Baslik, fBas).Width;
            var rozGen = Rozet.Length > 0 ? g.MeasureString(Rozet, fRoz).Width + 8 : 0;
            bool rozetSagda = rozGen == 0 || basGen + rozGen + 30 <= Width;
            int bantY = rozetSagda ? 34 : 52;
            using (var b = new SolidBrush(Vurgulu ? Yesil : Color.FromArgb(236, 239, 235))) g.FillRectangle(b, 0, 0, Width, bantY);
            using (var bb = new SolidBrush(Vurgulu ? Color.White : Cizim.Metin)) Satir(Baslik, fBas, bb, 7, 24);
            if (rozGen > 0)
            {
                var r = rozetSagda ? new RectangleF(Width - rozGen - 10, 9, rozGen, 17) : new RectangleF(12, 31, rozGen, 16);
                using (var rb = new SolidBrush(Vurgulu ? Color.FromArgb(60, 255, 255, 255) : Color.White)) g.FillRectangle(rb, r);
                Doku.TextOrta(g, Rozet, fRoz, Vurgulu ? Color.White : Yesil, r);
            }
            float y = bantY + 6;
            if (s == null)
            {
                Satir("Uygun seçenek yok", f, soluk, y);
            }
            else
            {
                // malzeme satırı gerekirse iki satıra kırılır
                g.DrawString($"{s.Yalitim.KisaAd} · {s.Cam.KisaAd} · {s.DuvarAdi}", f, soluk, new RectangleF(12, y, w, 34));
                bool ikiSatir = g.MeasureString($"{s.Yalitim.KisaAd} · {s.Cam.KisaAd} · {s.DuvarAdi}", f, w).Height > 20;
                y += ikiSatir ? 34 : 18;
                Satir(s.IlkYatirim.ToString("N0", tr) + " TL", fBuyuk, metin, y, 34); y += fBuyuk.Size > 16 ? 32 : 27;
                Satir("İlk yatırım (KDV hariç)", fk, soluk, y); y += 22;
                Satir($"Yıllık enerji  {YillikGider.ToString("N0", tr)} TL", f, metin, y); y += 19;
                if (!EkYatirim) Satir("Ek yatırım gerektirmez", f, metin, y);
                else if (!Tasarruf) Satir("Yakıt tasarrufu sağlamaz", f, turuncu, y);
                else if (GeriOdemeVar && GeriOdeme <= AnalizSuresi) Satir($"Geri dönüş {GeriOdeme.ToString("N1", tr)} yıl", f, metin, y);
                else Satir($"{AnalizSuresi} yılda geri dönmüyor", f, turuncu, y);
                y += 19;
                using var uy = new SolidBrush(s.Ts825Uygun ? Yesil : Color.FromArgb(180, 84, 26));
                Satir(s.Ts825Uygun ? "✓ TS 825 uygun" : "✕ TS 825 uygun değil", f, uy, y);
                int ist = (int)y + 26;
                if (ist != IstenenYukseklik) { IstenenYukseklik = ist; BeginInvoke(new Action(() => YukseklikDegisti?.Invoke(this, EventArgs.Empty))); }
            }
            var cer = new Rectangle(0, 0, Width - 1, Height - 1);
            if (secili) { using var p = new Pen(Yesil, 3f) { Alignment = PenAlignment.Inset }; g.DrawRectangle(p, cer); }
            else { using var p = new Pen(uzerinde ? Color.FromArgb(140, 170, 152) : Color.FromArgb(220, 224, 219)); g.DrawRectangle(p, cer); }
            if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(4, 4, Width - 9, Height - 9));
        }
    }
}
