using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Ets2Save
{
    /// <summary>Small drawn artwork so the tool does not need external image files.</summary>
    public static class UiTheme
    {
        public static readonly Color PageBack = Color.FromArgb(240, 240, 240);
        public static readonly Color LevelBack = Color.FromArgb(255, 255, 225);
        public static readonly Color Gold = Color.FromArgb(176, 128, 32);
        public static readonly Color GoldLight = Color.FromArgb(226, 190, 110);
        public static readonly Color GreyLight = Color.FromArgb(214, 214, 214);
        public static readonly Color GreyDark = Color.FromArgb(160, 160, 160);
        public static readonly Color Accent = Color.FromArgb(140, 184, 242);
        public static readonly Color AccentDeep = Color.FromArgb(96, 150, 226);
        public static readonly Color Bg = Color.FromArgb(242, 243, 245);
        public static readonly Color Surface = Color.White;
        public static readonly Color Border = Color.FromArgb(222, 226, 232);
        public static readonly Color Ink = Color.FromArgb(31, 35, 40);
        public static readonly Color Muted = Color.FromArgb(110, 118, 129);

        public static Bitmap Gradient(Color top, Color bottom, int width, int height)
        {
            Bitmap bmp = new Bitmap(Math.Max(1, width), Math.Max(1, height));
            using (Graphics g = Graphics.FromImage(bmp))
            using (LinearGradientBrush brush = new LinearGradientBrush(
                new Rectangle(0, 0, bmp.Width, bmp.Height), top, bottom, LinearGradientMode.Vertical))
            {
                g.FillRectangle(brush, 0, 0, bmp.Width, bmp.Height);
                using (Pen p = new Pen(Color.FromArgb(120, 90, 40)))
                    g.DrawRectangle(p, 0, 0, bmp.Width - 1, bmp.Height - 1);
            }
            return bmp;
        }

        public static Bitmap TabIcon(int kind, int size)
        {
            string[] assetNames = { null, null, "truck", "trailer", "cargo", "cargo", null };
            if (kind >= 0 && kind < assetNames.Length && assetNames[kind] != null)
            {
                Image art = UiAssets.Get(assetNames[kind], size, size);
                if (art != null) return new Bitmap(art);
            }
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float s = size;
                switch (kind)
                {
                    case 0: // profile
                        g.FillEllipse(Brushes.SteelBlue, s * 0.32f, s * 0.10f, s * 0.36f, s * 0.36f);
                        g.FillEllipse(Brushes.SteelBlue, s * 0.16f, s * 0.52f, s * 0.68f, s * 0.60f);
                        break;
                    case 1: // company
                        g.FillRectangle(Brushes.SandyBrown, s * 0.15f, s * 0.28f, s * 0.70f, s * 0.62f);
                        g.FillRectangle(Brushes.Peru, s * 0.10f, s * 0.18f, s * 0.80f, s * 0.14f);
                        for (int i = 0; i < 3; i++)
                            g.FillRectangle(Brushes.LightYellow, s * (0.24f + i * 0.20f), s * 0.40f, s * 0.12f, s * 0.12f);
                        break;
                    case 2: // truck
                        g.FillRectangle(Brushes.IndianRed, s * 0.10f, s * 0.36f, s * 0.55f, s * 0.34f);
                        g.FillRectangle(Brushes.Firebrick, s * 0.62f, s * 0.26f, s * 0.28f, s * 0.44f);
                        g.FillEllipse(Brushes.Black, s * 0.18f, s * 0.66f, s * 0.18f, s * 0.18f);
                        g.FillEllipse(Brushes.Black, s * 0.66f, s * 0.66f, s * 0.18f, s * 0.18f);
                        break;
                    case 3: // trailer
                        g.FillRectangle(Brushes.Silver, s * 0.08f, s * 0.32f, s * 0.70f, s * 0.32f);
                        g.DrawRectangle(Pens.DimGray, s * 0.08f, s * 0.32f, s * 0.70f, s * 0.32f);
                        g.FillEllipse(Brushes.Black, s * 0.20f, s * 0.62f, s * 0.16f, s * 0.16f);
                        g.FillEllipse(Brushes.Black, s * 0.56f, s * 0.62f, s * 0.16f, s * 0.16f);
                        break;
                    case 4: // freight
                        g.FillRectangle(Brushes.Peru, s * 0.18f, s * 0.30f, s * 0.58f, s * 0.46f);
                        g.DrawLine(Pens.SaddleBrown, s * 0.18f, s * 0.44f, s * 0.76f, s * 0.44f);
                        g.DrawLine(Pens.SaddleBrown, s * 0.47f, s * 0.30f, s * 0.47f, s * 0.76f);
                        break;
                    case 5: // cargo
                        g.FillRectangle(Brushes.DarkOrange, s * 0.12f, s * 0.48f, s * 0.34f, s * 0.30f);
                        g.FillRectangle(Brushes.Orange, s * 0.48f, s * 0.36f, s * 0.36f, s * 0.42f);
                        g.DrawRectangle(Pens.SaddleBrown, s * 0.12f, s * 0.48f, s * 0.34f, s * 0.30f);
                        g.DrawRectangle(Pens.SaddleBrown, s * 0.48f, s * 0.36f, s * 0.36f, s * 0.42f);
                        break;
                    default: // convoy
                        g.DrawEllipse(Pens.RoyalBlue, s * 0.10f, s * 0.34f, s * 0.32f, s * 0.32f);
                        g.DrawEllipse(Pens.RoyalBlue, s * 0.56f, s * 0.34f, s * 0.32f, s * 0.32f);
                        g.DrawLine(Pens.RoyalBlue, s * 0.42f, s * 0.50f, s * 0.58f, s * 0.50f);
                        break;
                }
            }
            return bmp;
        }

        public static Bitmap SkillIcon(int index, int size)
        {
            Color[] colors =
            {
                Color.FromArgb(214, 84, 40), Color.FromArgb(60, 110, 170), Color.FromArgb(196, 156, 40),
                Color.FromArgb(150, 60, 60), Color.FromArgb(60, 140, 80), Color.FromArgb(70, 140, 140)
            };
            string[] files = { "skill_adr", "skill_distance", "skill_heavy", "skill_fragile", "skill_jit", "skill_mechanical" };
            Image art = UiAssets.Get(files[index]);
            if (art != null)
            {
                Bitmap tile = new Bitmap(size, size);
                using (Graphics g = Graphics.FromImage(tile))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    int half = art.Width / 2;
                    g.DrawImage(art, new Rectangle(0, 0, size, size), new Rectangle(0, 0, half, art.Height), GraphicsUnit.Pixel);
                }
                return tile;
            }
            string[] glyphs = { "危", "远", "贵", "碎", "急", "油" };
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                Rectangle rect = new Rectangle(1, 1, size - 3, size - 3);
                using (LinearGradientBrush brush = new LinearGradientBrush(rect,
                    Lighten(colors[index]), colors[index], LinearGradientMode.ForwardDiagonal))
                using (Pen pen = new Pen(Color.FromArgb(90, 90, 90)))
                {
                    g.FillEllipse(brush, rect);
                    g.DrawEllipse(pen, rect);
                }
                using (Font font = new Font("Microsoft YaHei UI", size * 0.40f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(glyphs[index], font, Brushes.White, new RectangleF(0, 0, size, size), sf);
                }
            }
            return bmp;
        }

        public static Bitmap Portrait(int width, int height)
        {
            Image art = UiAssets.Get("autosave", width, height);
            if (art != null) return new Bitmap(art);
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(200, 200, 200));
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(226, 226, 226)))
                    g.FillRectangle(bg, 0, 0, width, height);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush body = new SolidBrush(Color.FromArgb(120, 120, 120)))
                {
                    g.FillEllipse(body, width * 0.33f, height * 0.16f, width * 0.34f, height * 0.34f);
                    g.FillEllipse(body, width * 0.16f, height * 0.52f, width * 0.68f, height * 0.62f);
                }
                using (Pen p = new Pen(SystemColors.ControlDark))
                    g.DrawRectangle(p, 0, 0, width - 1, height - 1);
            }
            return bmp;
        }

        public static Bitmap GarageIcon(int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float s = size;
                g.FillRectangle(Brushes.SeaGreen, s * 0.14f, s * 0.40f, s * 0.72f, s * 0.50f);
                g.FillPolygon(Brushes.DarkGreen, new PointF[]
                {
                    new PointF(s * 0.08f, s * 0.40f), new PointF(s * 0.50f, s * 0.12f), new PointF(s * 0.92f, s * 0.40f)
                });
                g.FillRectangle(Brushes.WhiteSmoke, s * 0.28f, s * 0.56f, s * 0.44f, s * 0.34f);
            }
            return bmp;
        }

        public static Bitmap TruckLogo(int width, int height)
        {
            Image art = UiAssets.Get("game_n", width, height);
            if (art != null) return new Bitmap(art);
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(232, 236, 244));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int s = height;
                using (SolidBrush cab = new SolidBrush(Color.FromArgb(60, 90, 150)))
                    g.FillRectangle(cab, width * 0.08f, height * 0.42f, width * 0.46f, height * 0.30f);
                using (SolidBrush box = new SolidBrush(Color.FromArgb(120, 140, 180)))
                    g.FillRectangle(box, width * 0.56f, height * 0.30f, width * 0.34f, height * 0.42f);
                using (SolidBrush wheel = new SolidBrush(Color.FromArgb(50, 50, 50)))
                {
                    g.FillEllipse(wheel, width * 0.14f, height * 0.66f, s * 0.15f, s * 0.15f);
                    g.FillEllipse(wheel, width * 0.66f, height * 0.66f, s * 0.15f, s * 0.15f);
                }
            }
            return bmp;
        }

        static Color Lighten(Color color)
        {
            return Color.FromArgb(255, Math.Min(255, color.R + 55), Math.Min(255, color.G + 55), Math.Min(255, color.B + 55));
        }

        public static void StyleFlat(Button button)
        {
            button.FlatStyle = FlatStyle.System;
        }

        public static void StyleGold(CheckBox box)
        {
            box.Appearance = Appearance.Button;
            box.FlatStyle = FlatStyle.Flat;
            box.FlatAppearance.BorderColor = Color.FromArgb(120, 90, 40);
            box.BackgroundImageLayout = ImageLayout.Stretch;
        }

        public static GraphicsPath Rounded(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        static readonly string[] PrimaryNames = { "加载存档", "保存", "购买", "修复全部", "加满油", "生成所选货运", "添加任务到列表" };

        public static void Modernize(Control root)
        {
            foreach (Control c in root.Controls)
            {
                Button b = c as Button;
                if (b != null)
                {
                    StyleButton(b, Array.IndexOf(PrimaryNames, b.Text) >= 0 || b.BackColor == Accent);
                }
                if (c is TextBox) WrapField(c);
                if (c is ComboBox) WrapField(c);
                if (c is ListBox)
                {
                    ListBox lb = (ListBox)c;
                    lb.BorderStyle = BorderStyle.None;
                    lb.BackColor = Surface;
                }
                if (c is Label)
                {
                    Label l = (Label)c;
                    if (l.ForeColor == Color.DimGray || l.ForeColor == Color.Black)
                        l.ForeColor = Muted;
                }
                Modernize(c);
            }
        }

        static readonly string[] ChipNames = { "-10", "-1", "+1", "+10", "<< 最小", "最大 >>" };

        public static void StyleButton(Button b, bool primary)
        {
            if (Array.IndexOf(ChipNames, b.Text) >= 0)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = Color.FromArgb(205, 211, 220);
                b.BackColor = Color.FromArgb(237, 240, 244);
                b.ForeColor = Color.FromArgb(60, 68, 80);
                b.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
                b.Cursor = Cursors.Hand;
                b.Resize += delegate { RoundRegion(b, 7); };
                RoundRegion(b, 7);
                return;
            }
            if (b.Text == "修")
            {
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = Color.FromArgb(198, 219, 245);
                b.BackColor = Color.FromArgb(240, 246, 255);
                b.ForeColor = AccentDeep;
                b.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold);
                b.Cursor = Cursors.Hand;
                b.Resize += delegate { RoundRegion(b, 6); };
                RoundRegion(b, 6);
                return;
            }
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(214, 219, 227);
            b.BackColor = primary ? Accent : Color.FromArgb(247, 248, 250);
            b.ForeColor = primary ? Color.White : Color.FromArgb(75, 82, 92);
            b.Font = new Font("Microsoft YaHei UI", primary ? 9.5f : 9f, primary ? FontStyle.Bold : FontStyle.Regular);
            b.Cursor = Cursors.Hand;
            b.Resize += delegate { RoundRegion(b, 9); };
            RoundRegion(b, 9);
        }

        public static void StyleSwatch(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(205, 211, 220);
            b.Cursor = Cursors.Hand;
            b.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold);
            b.Resize += delegate { RoundRegion(b, 7); };
            RoundRegion(b, 7);
        }

        public static void RoundRegion(Control c, int radius)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            using (GraphicsPath path = Rounded(new Rectangle(0, 0, c.Width, c.Height), radius))
                c.Region = new Region(path);
        }


        public sealed class FieldHost : Panel
        {
            public FieldHost()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Bg;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = Rounded(rect, 8))
                using (SolidBrush fill = new SolidBrush(Surface))
                using (Pen pen = new Pen(Border))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        public static void WrapField(Control field)
        {
            Control parent = field.Parent;
            if (parent == null || parent is FieldHost) return;
            Rectangle bounds = field.Bounds;
            FieldHost host = new FieldHost();
            host.Bounds = new Rectangle(bounds.X - 8, bounds.Y - 4, bounds.Width + 16, bounds.Height + 9);
            parent.Controls.Add(host);
            host.BringToFront();
            parent.Controls.Remove(field);
            field.Bounds = new Rectangle(8, 4, bounds.Width, bounds.Height);
            host.Controls.Add(field);
            TextBox tb = field as TextBox;
            if (tb != null) tb.BorderStyle = BorderStyle.None;
            ComboBox cb = field as ComboBox;
            if (cb != null) { cb.FlatStyle = FlatStyle.Flat; cb.BackColor = Surface; }
            NumericUpDown num = field as NumericUpDown;
            if (num != null) num.BorderStyle = BorderStyle.None;
        }

        public sealed class CardBox : GroupBox
        {
            public CardBox()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Surface;
                ForeColor = Ink;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 10, Width - 5, Height - 13);
                using (GraphicsPath shadow = Rounded(new Rectangle(rect.X + 2, rect.Y + 3, rect.Width, rect.Height), 12))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(18, 0, 0, 0)))
                    e.Graphics.FillPath(sb, shadow);
                using (GraphicsPath path = Rounded(rect, 12))
                using (SolidBrush fill = new SolidBrush(Surface))
                using (Pen pen = new Pen(Border))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(pen, path);
                }
                using (Font title = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold))
                    e.Graphics.DrawString(Text, title, new SolidBrush(Ink), 16, 0);
            }
        }
}
}
