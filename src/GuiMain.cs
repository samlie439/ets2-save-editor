using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace Ets2Save
{
    public sealed class MainForm : Form
    {
        // ------------------------------------------------------------------
        // data
        List<SaveSlot> _slots = new List<SaveSlot>();
        SaveEditor _editor;
        FreightMarket _market;
        List<FreightOffer> _filtered = new List<FreightOffer>();
        int _truckIndex = -1;
        bool _loading;

        readonly Dictionary<int, float> _pendingFuel = new Dictionary<int, float>();
        readonly Dictionary<int, float> _pendingWear = new Dictionary<int, float>();

        // ------------------------------------------------------------------
        // right panel
        SearchCombo cmbRoot;
        SearchCombo cmbProfile;
        SearchCombo cmbSave;
        Label lblInfo;
        Label lblStatus = new Label();
        PictureBox picPortrait;

        // ------------------------------------------------------------------
        // profile tab
        Label lblLevel;
        Label lblLegend;
        Label lblXp;
        CheckBox[,] _skillButtons = new CheckBox[6, 6];
        readonly string[] _skillFields = { "adr", "long_dist", "heavy", "fragile", "urgent", "mechanical" };
        readonly string[] _skillNames = { "危险品", "长途运输", "贵重货物", "易碎货物", "及时运输", "燃油效能" };
        CheckBox chkFatigue;
        CheckBox chkDebt;
        Panel pnlColors;
        List<Button> _colorButtons = new List<Button>();

        // ------------------------------------------------------------------
        // company tab
        TextBox txtCompanyName;
        Label lblNameCount;
        TextBox txtMoney;
        SearchCombo cmbHq;
        ListBox lstGarages;
        Label lblGarageCount;
        readonly List<SiiBlock> _garageBlocks = new List<SiiBlock>();

        // ------------------------------------------------------------------
        // truck tab
        SearchCombo cmbTruck;
        ProgressBar[] _bars = new ProgressBar[5];
        Label[] _barValues = new Label[5];
        Panel pnlFuelFill;
        Label lblFuel;
        TextBox txtPlate;
        Button btnTruckFuel;

        // ------------------------------------------------------------------
        // trailer tab
        SearchCombo cmbTrailer;
        ProgressBar[] _tBars = new ProgressBar[4];
        Panel pnlTFuel;

        // ------------------------------------------------------------------
        // freight tab
        SearchCombo cmbFreightCountry;
        SearchCombo cmbFreightCompany;
        SearchCombo cmbSourceCity;
        SearchCombo cmbSourceCompany;
        SearchCombo cmbDestCity;
        SearchCombo cmbDestCompany;
        SearchCombo cmbCargo;
        SearchCombo cmbUrgency;
        SearchCombo cmbTrailerDef;
        SearchCombo cmbTrailerVariant;
        ListBox lstCargoMarket;
        CheckBox chkRandom;
        Label lblDistance;
        ListView lvOffers;

        // ------------------------------------------------------------------
        // level table
        static readonly int[] LevelSteps = new int[]
        {
            200, 500, 700, 900, 1000, 1100, 1300, 1600, 1700, 2100, 2300, 2600, 2700, 2900, 3000,
            3100, 3400, 3700, 4000, 4300, 4600, 4700, 4900, 5200, 5700, 5900, 6000, 6200, 6600, 6800
        };
        static readonly int[] LevelLimits = { 0, 5, 10, 15, 20, 25, 30, 40, 50, 100 };
        static readonly string[] LevelTitles =
        { "Newbie", "Enthusiast", "Workhorse", "Entrepeneur", "Master", "Instructor", "Elite", "King of the Road", "Legend", "Divine Champion" };


        // ==================================================================
        public MainForm()
        {
            Text = "ETS2 SE Tool [1.0.0]";
            ClientSize = new Size(860, 640);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            AutoScaleMode = AutoScaleMode.None;
            MaximizeBox = false;
            HelpButton = false;
            Font = new Font("Microsoft YaHei UI", 9f);
            BackColor = UiTheme.PageBack;

            BuildMenu();
            BuildRightPanel();
            BackColor = UiTheme.Bg;
            BuildTabs();
            LoadSlots();
            Shown += delegate
            {
                UiTheme.Modernize(this);
                StylePrimary();
                tabs.ItemSize = new Size(112, 34);
                tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabs.DrawItem += DrawModernTab;
                tabs.Invalidate();
            };
        }

        // ------------------------------------------------------------------
        Button Btn(string text, int x, int y, int w, int h, EventHandler click)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x, y, w, h);
            b.FlatStyle = FlatStyle.System;
            if (click != null) b.Click += click;
            return b;
        }

        Button BtnD(string text, int x, int y, int w, int h, EventHandler click)
        {
            Button b = Btn(text, x, y, w, h, null);
            b.Enabled = false;
            return b;
        }

        Label Lbl(string text, int x, int y, int w, int h)
        {
            Label l = new Label();
            l.Text = text;
            l.SetBounds(x, y, w, h);
            return l;
        }

        CheckBox Chk(string text, int x, int y, int w, int h)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.SetBounds(x, y, w, h);
            return c;
        }

        UiTheme.CardBox Group(string text, int x, int y, int w, int h)
        {
            UiTheme.CardBox g = new UiTheme.CardBox();
            g.Text = text;
            g.SetBounds(x, y, w, h);
            return g;
        }

        // ==================================================================
        void BuildMenu()
        {
            MenuStrip menu = new MenuStrip();
            menu.Font = Font;
            menu.RenderMode = ToolStripRenderMode.System;

            ToolStripMenuItem prog = new ToolStripMenuItem("程序");
            prog.DropDownItems.Add("保存修改", null, OnSave);
            prog.DropDownItems.Add("重新扫描存档", null, delegate { LoadSlots(); });
            prog.DropDownItems.Add(new ToolStripSeparator());
            prog.DropDownItems.Add("备份全部存档", null, delegate { BackupAll(); });
            prog.DropDownItems.Add("还原备份…", null, delegate { RestoreBackup(); });
            prog.DropDownItems.Add("导出可读文本", null, delegate { ExportText(); });
            ToolStripMenuItem fmt = new ToolStripMenuItem("保存格式");
            ToolStripMenuItem fmtBin = new ToolStripMenuItem("二进制 BSII（推荐）", null, delegate { _asText = false; });
            ToolStripMenuItem fmtTxt = new ToolStripMenuItem("文本 SiiNunit", null, delegate { _asText = true; });
            fmt.DropDownItems.Add(fmtBin);
            fmt.DropDownItems.Add(fmtTxt);
            prog.DropDownItems.Add(fmt);
            prog.DropDownItems.Add(new ToolStripSeparator());
            prog.DropDownItems.Add("退出", null, delegate { Close(); });

            ToolStripMenuItem lang = new ToolStripMenuItem("语言");
            string[] langs = { "简体中文", "English", "繁體中文", "日本語" };
            string[] codes = { "zh", "en", "tw", "ja" };
            for (int i = 0; i < langs.Length; i++)
            {
                string code = codes[i];
                ToolStripMenuItem item = new ToolStripMenuItem(langs[i]);
                item.Checked = i == 0;
                item.Click += delegate
                {
                    foreach (ToolStripMenuItem other in lang.DropDownItems)
                        other.Checked = (other.Text == langs[Array.IndexOf(codes, code)]);
                    ApplyLanguage(code);
                };
                lang.DropDownItems.Add(item);
            }

            ToolStripMenuItem help = new ToolStripMenuItem("帮助");
            help.DropDownItems.Add("使用说明", null, delegate { OpenHelp(); });
            help.DropDownItems.Add("关于", null, delegate { ShowAbout(); });

            menu.Items.Add(prog);
            menu.Items.Add(lang);
            menu.Items.Add(help);
            MainMenuStrip = menu;
            Controls.Add(menu);
        }

        bool _asText;

        // ==================================================================
        void BuildRightPanel()
        {
            Button ets2 = Btn("ETS 2", 612, 44, 204, 48, delegate { });
            ets2.Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
            ets2.TextAlign = ContentAlignment.MiddleRight;
            ets2.Image = UiTheme.TruckLogo(40, 30);
            ets2.ImageAlign = ContentAlignment.MiddleLeft;
            ets2.FlatStyle = FlatStyle.Flat;
            ets2.BackColor = Color.White;
            ets2.FlatAppearance.BorderColor = UiTheme.Accent;
            ets2.FlatAppearance.BorderSize = 2;
            Controls.Add(ets2);

            Controls.Add(BtnD("添加自定义文件夹", 612, 100, 204, 26, null));

            GroupBox saves = Group("档案与存档", 612, 136, 204, 446);
            Controls.Add(saves);

            picPortrait = new PictureBox();
            picPortrait.SetBounds(10, 20, 112, 92);
            picPortrait.Image = UiTheme.Portrait(112, 92);
            picPortrait.SizeMode = PictureBoxSizeMode.StretchImage;
            saves.Controls.Add(picPortrait);

            CheckBox backup = Chk("备份", 142, 44, 58, 20);
            backup.Checked = true;
            backup.Enabled = false;
            saves.Controls.Add(backup);

            cmbRoot = new SearchCombo();
            cmbRoot.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbRoot.SetBounds(10, 122, 168, 22);
            saves.Controls.Add(cmbRoot);
            Button refresh = Btn("", 182, 122, 18, 22, delegate { LoadSlots(); });
            refresh.Image = UiTheme.TabIcon(6, 12);
            saves.Controls.Add(refresh);

            cmbProfile = new SearchCombo();
            cmbProfile.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbProfile.SetBounds(10, 148, 168, 22);
            cmbProfile.SelectedIndexChanged += delegate { FillSaves(); };
            saves.Controls.Add(cmbProfile);
            saves.Controls.Add(BtnD("", 182, 148, 18, 22, null));

            cmbSave = new SearchCombo();
            cmbSave.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSave.SetBounds(10, 176, 168, 22);
            cmbSave.SelectedIndexChanged += delegate { UpdateInfo(); };
            saves.Controls.Add(cmbSave);
            saves.Controls.Add(BtnD("", 182, 176, 18, 22, null));

            saves.Controls.Add(Btn("打开文件夹", 10, 208, 184, 26, delegate { OpenFolder(); }));

            Button load = Btn("加载存档", 10, 244, 184, 42, delegate { LoadSelected(); });
            load.Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
            saves.Controls.Add(load);

            Button unload = Btn("Unload", 106, 290, 88, 24, delegate { CloseSave(); });
            unload.Enabled = false;
            unload.Tag = "unload";
            saves.Controls.Add(unload);

            lblStatus = Lbl("", 10, 396, 184, 40);
            lblStatus.ForeColor = Color.FromArgb(0, 80, 150);
            saves.Controls.Add(lblStatus);

            Button save = Btn("保存", 10, 330, 184, 58, OnSave);
            save.Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
            saves.Controls.Add(save);


        }

        HScrollBar statusBarScroll;

        // ==================================================================
        TabControl tabs;

        void BuildTabs()
        {
            tabs = new TabControl();
            tabs.SetBounds(6, 26, 600, 584);
            tabs.Font = new Font("Microsoft YaHei UI", 9f);
            tabs.ImageList = new ImageList();
            tabs.ImageList.ImageSize = new Size(16, 16);
            tabs.ImageList.ColorDepth = ColorDepth.Depth32Bit;
            for (int i = 0; i < 7; i++) tabs.ImageList.Images.Add(UiTheme.TabIcon(i, 16));
            Controls.Add(tabs);

            statusBarScroll = new HScrollBar();
            statusBarScroll.SetBounds(612, 580, 204, 16);
            statusBarScroll.Enabled = false;
            Controls.Add(statusBarScroll);

            tabs.TabPages.Add(BuildProfilePage());
            tabs.TabPages.Add(BuildCompanyPage());
            tabs.TabPages.Add(BuildTruckPage());
            tabs.TabPages.Add(BuildTrailerPage());
            tabs.TabPages.Add(BuildFreightPage());
            tabs.TabPages.Add(BuildCargoMarketPage());
            tabs.TabPages.Add(BuildConvoyPage());

            for (int i = 0; i < 7; i++) tabs.TabPages[i].ImageIndex = i;
        }

        // ==================================================================
        TabPage BuildProfilePage()
        {
            TabPage page = new TabPage("档案");
            page.BackColor = UiTheme.PageBack;

            GroupBox level = Group("玩家级别", 8, 4, 560, 96);
            level.BackColor = UiTheme.Surface;
            page.Controls.Add(level);

            level.Controls.Add(Btn("-10", 12, 22, 40, 26, delegate { ChangeLevel(-10); }));
            level.Controls.Add(Btn("-1", 56, 22, 40, 26, delegate { ChangeLevel(-1); }));
            level.Controls.Add(Btn("+1", 462, 22, 40, 26, delegate { ChangeLevel(1); }));
            level.Controls.Add(Btn("+10", 506, 22, 40, 26, delegate { ChangeLevel(10); }));

            lblLevel = Lbl("1", 210, 12, 70, 46);
            lblLevel.Font = new Font("Microsoft YaHei UI", 22f, FontStyle.Bold);
            lblLevel.TextAlign = ContentAlignment.MiddleCenter;
            level.Controls.Add(lblLevel);

            lblLegend = Lbl("Newbie", 284, 22, 150, 26);
            lblLegend.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            lblLegend.ForeColor = Color.Firebrick;
            level.Controls.Add(lblLegend);

            lblXp = Lbl("0 / 0", 150, 58, 262, 20);
            lblXp.TextAlign = ContentAlignment.MiddleCenter;
            level.Controls.Add(lblXp);

            level.Controls.Add(Btn("<< 最小", 12, 54, 76, 26, delegate { SetLevel(0); }));
            level.Controls.Add(Btn("最大 >>", 464, 54, 82, 26, delegate { SetLevel(150); }));

            GroupBox skills = Group("技能", 8, 106, 560, 356);
            page.Controls.Add(skills);

            for (int i = 0; i < 6; i++)
            {
                PictureBox icon = new PictureBox();
                icon.SetBounds(14, 16 + i * 56, 48, 48);
                icon.Image = UiTheme.SkillIcon(i, 48);
                icon.SizeMode = PictureBoxSizeMode.StretchImage;
                skills.Controls.Add(icon);

                Label name = Lbl(_skillNames[i], 70, 28 + i * 56, 86, 22);
                name.Font = new Font("Microsoft YaHei UI", 9f);
                skills.Controls.Add(name);

                for (int j = 0; j < 6; j++)
                {
                    CheckBox cb = new CheckBox();
                    cb.Appearance = Appearance.Button;
                    cb.FlatStyle = FlatStyle.Flat;
                    cb.FlatAppearance.BorderColor = Color.FromArgb(120, 90, 40);
                    cb.BackgroundImageLayout = ImageLayout.Stretch;
                    cb.SetBounds(160 + j * 64, 20 + i * 56, 44, 38);
                    cb.Text = "";
                    cb.Tag = i * 10 + j;
                    cb.BackgroundImage = UiTheme.Gradient(Color.FromArgb(235, 235, 235), UiTheme.GreyDark, 38, 38);
                    cb.Click += OnSkillClick;
                    skills.Controls.Add(cb);
                    _skillButtons[i, j] = cb;
                }
            }

            chkFatigue = Chk("清除疲劳", 0, 0, 10, 10);
            chkDebt = Chk("清除债务与透支", 0, 0, 10, 10);

            return page;
        }

        // ==================================================================
        TabPage BuildCompanyPage()
        {
            TabPage page = new TabPage("公司");
            page.BackColor = UiTheme.PageBack;

            PictureBox logo = new PictureBox();
            logo.SetBounds(10, 8, 112, 92);
            logo.Image = UiTheme.TabIcon(1, 96);
            logo.SizeMode = PictureBoxSizeMode.CenterImage;
            logo.BorderStyle = BorderStyle.FixedSingle;
            logo.BackColor = Color.White;
            page.Controls.Add(logo);

            page.Controls.Add(Lbl("公司名称", 132, 16, 70, 20));
            txtCompanyName = new TextBox();
            txtCompanyName.SetBounds(206, 14, 272, 24);
            txtCompanyName.ReadOnly = true;
            page.Controls.Add(txtCompanyName);

            lblNameCount = Lbl("0 / 20", 482, 18, 60, 20);
            page.Controls.Add(lblNameCount);

            page.Controls.Add(Lbl("游戏资金", 132, 50, 70, 20));
            txtMoney = new TextBox();
            txtMoney.SetBounds(206, 48, 272, 24);
            txtMoney.TextAlign = HorizontalAlignment.Right;
            page.Controls.Add(txtMoney);

            page.Controls.Add(Lbl("总部城市", 132, 84, 70, 20));
            cmbHq = new SearchCombo();
            cmbHq.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbHq.SetBounds(206, 82, 272, 24);
            page.Controls.Add(cmbHq);

            page.Controls.Add(BtnD("Garages", 8, 106, 76, 26, null));
            page.Controls.Add(BtnD("Drivers", 88, 106, 76, 26, null));
            page.Controls.Add(BtnD("Visited cities", 168, 106, 100, 26, null));

            page.Controls.Add(Lbl("车库", 8, 140, 40, 20));
            lblGarageCount = Lbl("0 / 0", 520, 140, 70, 20);
            lblGarageCount.TextAlign = ContentAlignment.MiddleRight;
            page.Controls.Add(lblGarageCount);

            lstGarages = new ListBox();
            lstGarages.SetBounds(8, 162, 578, 280);
            lstGarages.DrawMode = DrawMode.OwnerDrawFixed;
            lstGarages.ItemHeight = 34;
            lstGarages.IntegralHeight = false;
            lstGarages.DrawItem += DrawGarageItem;
            page.Controls.Add(lstGarages);

            page.Controls.Add(Btn("购买", 8, 450, 108, 40, delegate { ChangeGarage(0); }));
            page.Controls.Add(Btn("升级", 120, 450, 66, 40, delegate { ChangeGarage(1); }));
            page.Controls.Add(Btn("降级", 190, 450, 66, 40, delegate { ChangeGarage(-1); }));
            page.Controls.Add(Btn("出售", 260, 450, 72, 40, delegate { ChangeGarage(9); }));
            page.Controls.Add(BtnD("Select All", 340, 450, 108, 40, null));
            page.Controls.Add(BtnD("Unselect All", 452, 450, 124, 40, null));

            return page;
        }

        void StylePrimary()
        {
            foreach (Button b in new Button[] { FindButton("加载存档"), FindButton("保存") })
            {
                if (b == null) continue;
                b.BackColor = UiTheme.Accent;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderColor = UiTheme.Accent;
                b.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            }
        }

        Button FindButton(string text)
        {
            foreach (Control c in Controls)
            {
                foreach (Control inner in c.Controls)
                {
                    Button b = inner as Button;
                    if (b != null && b.Text == text) return b;
                }
            }
            return null;
        }

        void DrawModernTab(object sender, DrawItemEventArgs e)
        {
            TabControl tab = (TabControl)sender;
            bool selected = e.Index == tab.SelectedIndex;
            using (SolidBrush back = new SolidBrush(selected ? UiTheme.Surface : UiTheme.Bg))
                e.Graphics.FillRectangle(back, e.Bounds);
            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(UiTheme.Accent))
                    e.Graphics.FillRectangle(accent, e.Bounds.Left + 10, e.Bounds.Bottom - 3, e.Bounds.Width - 20, 3);
            }
            string text = tab.TabPages[e.Index].Text;
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                using (SolidBrush ink = new SolidBrush(selected ? UiTheme.Accent : UiTheme.Muted))
                    e.Graphics.DrawString(text, new Font("Microsoft YaHei UI", 9.5f, selected ? FontStyle.Bold : FontStyle.Regular), ink, e.Bounds, sf);
            }
        }

        void ChangeGarage(int mode)
        {
            if (_editor == null) { MessageBox.Show("请先加载存档。", Text); return; }
            int index = lstGarages.SelectedIndex;
            if (index < 0 || index >= _garageBlocks.Count) { MessageBox.Show("请先在列表里选择一个车库。", Text); return; }
            SiiBlock garage = _garageBlocks[index];
            long status = Convert.ToInt64(garage.Get("status") ?? 0L, CultureInfo.InvariantCulture);
            long next;
            if (mode == 0) next = 1;
            else if (mode == 1) next = Math.Min(2, status + 1);
            else if (mode == -1) next = Math.Max(0, status - 1);
            else
            {
                if (MessageBox.Show("确定出售该车库？里面的车辆不会被删除，但需要你自行处理。", Text,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                next = 0;
            }
            if (next == status) return;
            _editor.File.SetInt(garage, "status", next);
            FillCompanyPage();
            if (index < lstGarages.Items.Count) lstGarages.SelectedIndex = index;
            lblStatus.Text = "车库状态已修改（未保存）";
        }

        void RepairTruckAll()
        {
            if (_editor == null || _truckIndex < 0) { MessageBox.Show("请先选择卡车。", Text); return; }
            TruckInfo t = _editor.Trucks[_truckIndex];
            foreach (string field in new string[] { "engine_wear", "transmission_wear", "chassis_wear", "cabin_wear",
                                                    "engine_wear_unfixable", "transmission_wear_unfixable",
                                                    "cabin_wear_unfixable", "chassis_wear_unfixable" })
            {
                SiiValue v = t.Block.GetValue(field);
                if (v != null && v.Type == SiiType.Single) _editor.File.SetFloat(t.Block, field, 0f);
            }
            SiiValue wheels = t.Block.GetValue("wheels_wear");
            if (wheels != null && wheels.Type == SiiType.SingleArray)
                for (int i = 0; i < wheels.Count; i++) _editor.File.SetFloatArrayItem(t.Block, "wheels_wear", i, 0f);
            t.EngineWear = 0f; t.TransmissionWear = 0f; t.ChassisWear = 0f; t.CabinWear = 0f;
            if (t.WheelsWear != null) for (int i = 0; i < t.WheelsWear.Length; i++) t.WheelsWear[i] = 0f;
            OnTruckChanged(null, EventArgs.Empty);
            lblStatus.Text = "已修复全部部件（未保存）";
        }

        void FillTruckFuel()
        {
            if (_editor == null || _truckIndex < 0) { MessageBox.Show("请先选择卡车。", Text); return; }
            TruckInfo t = _editor.Trucks[_truckIndex];
            if (t.Block.GetValue("fuel_relative") == null) { MessageBox.Show("这辆车没有油量字段。", Text); return; }
            _editor.File.SetFloat(t.Block, "fuel_relative", 1f);
            SetFuel(1f);
            lblStatus.Text = "已加满油（未保存）";
        }

        void DrawGarageItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            string text = lstGarages.Items[e.Index].ToString();
            string[] parts = text.Split('|');
            using (Bitmap icon = UiTheme.GarageIcon(24))
                e.Graphics.DrawImage(icon, e.Bounds.Left + 4, e.Bounds.Top + 5, 24, 24);
            using (Font bold = new Font(Font, FontStyle.Bold))
                e.Graphics.DrawString(parts[0], bold, Brushes.Black, e.Bounds.Left + 34, e.Bounds.Top + 2);
            if (parts.Length > 1)
                e.Graphics.DrawString(parts[1], Font, Brushes.DimGray, e.Bounds.Left + 34, e.Bounds.Top + 18);
            e.DrawFocusRectangle();
        }

        // ==================================================================
        TabPage BuildTruckPage()
        {
            TabPage page = new TabPage("卡车");
            page.BackColor = UiTheme.PageBack;

            page.Controls.Add(Lbl("卡车", 8, 6, 40, 20));
            cmbTruck = new SearchCombo();
            cmbTruck.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbTruck.SetBounds(8, 24, 578, 24);
            cmbTruck.SelectedIndexChanged += OnTruckChanged;
            page.Controls.Add(cmbTruck);

            page.Controls.Add(BtnD("玩家当前卡车", 8, 54, 150, 28, null));
            page.Controls.Add(BtnD("将该卡车设为玩家当前卡车", 162, 54, 186, 28, null));
            page.Controls.Add(Btn("修复全部", 352, 54, 110, 28, delegate { RepairTruckAll(); }));
            page.Controls.Add(Btn("加满油", 466, 54, 120, 28, delegate { FillTruckFuel(); }));

            GroupBox details = Group("详细数据", 8, 88, 330, 344);
            page.Controls.Add(details);

            string[] names = { "发动机", "变速箱", "底盘", "驾驶舱", "车轮" };
            for (int i = 0; i < 5; i++)
            {
                string[] iconNames = { "engine", "transmission", "chassis", "cabin", "tyres" };
                PictureBox part = new PictureBox();
                part.SetBounds(6, 20 + i * 38, 24, 24);
                part.SizeMode = PictureBoxSizeMode.Zoom;
                part.Image = UiAssets.Get(iconNames[i]);
                details.Controls.Add(part);

                details.Controls.Add(Lbl(names[i], 34, 24 + i * 38, 56, 20));

                ProgressBar bar = new ProgressBar();
                bar.SetBounds(92, 22 + i * 38, 182, 18);
                bar.Minimum = 0; bar.Maximum = 100;
                details.Controls.Add(bar);
                _bars[i] = bar;

                Label value = Lbl("0%", 92, 40 + i * 38, 182, 14);
                value.TextAlign = ContentAlignment.MiddleRight;
                value.ForeColor = Color.DimGray;
                details.Controls.Add(value);
                _barValues[i] = value;

                int index = i;
                Button repair = Btn("修", 280, 21 + i * 38, 28, 22, delegate { RepairPart(index); });
                repair.Font = new Font("Microsoft YaHei UI", 7.5f);
                details.Controls.Add(repair);
            }

            GroupBox fuel = Group("燃料", 346, 88, 130, 344);
            page.Controls.Add(fuel);

            Panel fuelBack = new Panel();
            fuelBack.SetBounds(40, 22, 50, 290);
            fuelBack.BackColor = Color.Gainsboro;
            fuelBack.BorderStyle = BorderStyle.FixedSingle;
            fuel.Controls.Add(fuelBack);

            pnlFuelFill = new Panel();
            pnlFuelFill.SetBounds(2, 286, 44, 0);
            pnlFuelFill.BackColor = Color.FromArgb(120, 170, 60);
            fuelBack.Controls.Add(pnlFuelFill);

            lblFuel = Lbl("0%", 40, 314, 50, 18);
            lblFuel.TextAlign = ContentAlignment.MiddleCenter;
            fuel.Controls.Add(lblFuel);

            page.Controls.Add(Lbl("车牌", 8, 440, 40, 20));
            txtPlate = new TextBox();
            txtPlate.SetBounds(52, 438, 240, 24);
            txtPlate.ReadOnly = true;
            page.Controls.Add(txtPlate);

            GroupBox share = Group("分享卡车设置", 8, 470, 578, 74);
            page.Controls.Add(share);
            string[] btns = { "复制喷漆设置", "复制卡车数据", "复制所有卡车设置", "粘贴喷漆设置", "粘贴卡车数据", "粘贴所有卡车设置" };
            for (int i = 0; i < 6; i++)
            {
                Button b = Btn(btns[i], 8 + (i % 3) * 190, 16 + (i / 3) * 28, 182, 26, null);
                b.Enabled = false;
                share.Controls.Add(b);
            }

            return page;
        }

        void numFuelQuickFill() { }

        // ==================================================================
        TabPage BuildTrailerPage()
        {
            TabPage page = new TabPage("挂车");
            page.BackColor = UiTheme.PageBack;

            page.Controls.Add(Lbl("Trailer", 8, 6, 60, 20));
            cmbTrailer = new SearchCombo();
            cmbTrailer.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbTrailer.SetBounds(8, 24, 578, 24);
            cmbTrailer.Items.Add("- NONE -");
            cmbTrailer.SelectedIndex = 0;
            cmbTrailer.Enabled = false;
            page.Controls.Add(cmbTrailer);

            page.Controls.Add(BtnD("玩家当前挂车", 8, 54, 150, 28, null));
            page.Controls.Add(BtnD("将该挂车设为玩家当前挂车", 162, 54, 186, 28, null));
            page.Controls.Add(BtnD("", 352, 54, 30, 28, null));
            page.Controls.Add(BtnD("", 386, 54, 30, 28, null));

            GroupBox details = Group("详细数据", 8, 88, 330, 300);
            page.Controls.Add(details);
            string[] names = { "货物", "车身", "底盘", "车轮" };
            for (int i = 0; i < 4; i++)
            {
                details.Controls.Add(Lbl(names[i], 8, 26 + i * 44, 60, 20));
                ProgressBar bar = new ProgressBar();
                bar.SetBounds(70, 26 + i * 44, 208, 18);
                details.Controls.Add(bar);
                _tBars[i] = bar;
                details.Controls.Add(BtnD("", 282, 26 + i * 44, 24, 18, null));
            }

            GroupBox fuel = Group("燃料", 346, 88, 130, 300);
            page.Controls.Add(fuel);
            pnlTFuel = new Panel();
            pnlTFuel.SetBounds(40, 22, 50, 246);
            pnlTFuel.BackColor = Color.Gainsboro;
            pnlTFuel.BorderStyle = BorderStyle.FixedSingle;
            fuel.Controls.Add(pnlTFuel);

            page.Controls.Add(Lbl("车牌", 8, 396, 40, 20));
            TextBox plate = new TextBox();
            plate.SetBounds(52, 394, 240, 24);
            plate.ReadOnly = true;
            page.Controls.Add(plate);

            GroupBox share = Group("分享挂车设置", 8, 426, 578, 92);
            page.Controls.Add(share);
            string[] btns = { "Copy Paint Settings", "Copy Truck Details", "Copy All Truck Settings",
                              "Paste Paint Settings", "Paste Truck Details", "Paste All Truck Settings" };
            for (int i = 0; i < 6; i++)
            {
                Button b = Btn(btns[i], 8 + (i % 3) * 190, 16 + (i / 3) * 28, 182, 26, null);
                b.Enabled = false;
                share.Controls.Add(b);
            }

            return page;
        }

        // ==================================================================
        TabPage BuildFreightPage()
        {
            TabPage page = new TabPage("货运市场");
            page.BackColor = UiTheme.PageBack;

            GroupBox filter = Group("过滤", 8, 4, 578, 100);
            page.Controls.Add(filter);

            filter.Controls.Add(Lbl("国家或地区", 8, 18, 76, 20));
            cmbFreightCountry = Combo(88, 16, 160);
            cmbFreightCountry.Items.Add("All");
            cmbFreightCountry.SelectedIndex = 0;
            filter.Controls.Add(cmbFreightCountry);

            filter.Controls.Add(Lbl("公司", 260, 18, 40, 20));
            cmbFreightCompany = Combo(300, 16, 160);
            filter.Controls.Add(cmbFreightCompany);
            chkRandom = Chk("随机", 470, 18, 60, 20);
            filter.Controls.Add(chkRandom);

            filter.Controls.Add(Lbl("起点", 8, 46, 40, 20));
            cmbSourceCity = Combo(48, 44, 200);
            filter.Controls.Add(cmbSourceCity);
            cmbSourceCompany = Combo(300, 44, 160);
            filter.Controls.Add(cmbSourceCompany);

            filter.Controls.Add(Lbl("终点", 8, 74, 40, 20));
            cmbDestCity = Combo(48, 72, 200);
            filter.Controls.Add(cmbDestCity);
            cmbDestCompany = Combo(300, 72, 160);
            filter.Controls.Add(cmbDestCompany);

            page.Controls.Add(Lbl("货物", 8, 112, 40, 20));
            cmbCargo = Combo(48, 110, 200);
            cmbCargo.SelectedIndexChanged += delegate { OnCargoChanged(); };
            page.Controls.Add(cmbCargo);

            page.Controls.Add(Lbl("加急", 258, 112, 40, 20));
            cmbUrgency = Combo(300, 110, 110);
            cmbUrgency.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbUrgency.Items.AddRange(new object[] { "0 Standard", "1 加急", "2 特急" });
            cmbUrgency.SelectedIndex = 0;
            page.Controls.Add(cmbUrgency);

            page.Controls.Add(Lbl("挂车", 8, 140, 40, 20));
            cmbTrailerDef = Combo(48, 138, 300);
            page.Controls.Add(cmbTrailerDef);
            cmbTrailerVariant = Combo(360, 138, 130);
            page.Controls.Add(cmbTrailerVariant);

            page.Controls.Add(Btn("添加任务到列表", 8, 164, 300, 28, delegate { AddFreightOffer(); }));
            page.Controls.Add(Btn("随机生成货物", 314, 164, 130, 28, delegate { RandomCargo(); }));

            page.Controls.Add(Lbl("路线里程:", 8, 196, 60, 20));
            lblDistance = Lbl("-", 70, 196, 200, 20);
            page.Controls.Add(lblDistance);
            page.Controls.Add(Btn("清空列表", 380, 194, 120, 26, delegate { RefreshOffers(); }));

            lvOffers = new ListView();
            lvOffers.SetBounds(8, 220, 578, 348);
            lvOffers.View = View.Details;
            lvOffers.FullRowSelect = true;
            lvOffers.HideSelection = false;
            lvOffers.VirtualMode = true;
            lvOffers.Columns.Add("来源公司", 140);
            lvOffers.Columns.Add("货物", 140);
            lvOffers.Columns.Add("目的地", 150);
            lvOffers.Columns.Add("距离", 52, HorizontalAlignment.Right);
            lvOffers.Columns.Add("紧急", 36, HorizontalAlignment.Center);
            lvOffers.Columns.Add("剩余", 50, HorizontalAlignment.Right);
            lvOffers.RetrieveVirtualItem += OnRetrieveOffer;
            lvOffers.SelectedIndexChanged += OnOfferSelected;
            lvOffers.DoubleClick += delegate { ApplySelectedOffer(); };
            page.Controls.Add(lvOffers);

            return page;
        }

        SearchCombo Combo(int x, int y, int w)
        {
            SearchCombo c = new SearchCombo();
            c.SetBounds(x, y, w, 24);
            c.DropDownStyle = ComboBoxStyle.DropDown;
            return c;
        }

        // ==================================================================
        TabPage BuildCargoMarketPage()
        {
            TabPage page = new TabPage("货物市场");
            page.BackColor = UiTheme.PageBack;

            page.Controls.Add(Lbl("起点", 8, 10, 40, 20));
            SearchCombo city = Combo(48, 8, 180);
            city.Items.Add("All");
            city.SelectedIndex = 0;
            page.Controls.Add(city);

            page.Controls.Add(Lbl("公司", 240, 10, 40, 20));
            SearchCombo comp = Combo(280, 8, 160);
            comp.Items.Add("All");
            comp.SelectedIndex = 0;
            page.Controls.Add(comp);

            page.Controls.Add(Btn("刷新列表", 8, 42, 180, 28, delegate { FillCargoMarketPage(); }));
            page.Controls.Add(Btn("查看自有挂车", 280, 42, 180, 28, delegate { ShowTrailers(); }));
            page.Controls.Add(BtnD("随机货物列表", 8, 76, 180, 28, null));
            page.Controls.Add(BtnD("随机货物列表", 280, 76, 180, 28, null));

            lstCargoMarket = new ListBox();
            lstCargoMarket.SetBounds(8, 116, 560, 236);
            page.Controls.Add(lstCargoMarket);

            Label info = Lbl("货物市场任务来自 job_info（需要自有挂车）。修改后用右侧“保存”写入。", 8, 364, 560, 40);
            info.ForeColor = Color.DimGray;
            page.Controls.Add(info);

            return page;
        }

        void ShowTrailers()
        {
            if (_editor == null) { MessageBox.Show("请先加载存档。", Text); return; }
            object trailers = _editor.Player.Get("trailers");
            string[] list = trailers as string[];
            MessageBox.Show(list == null || list.Length == 0
                ? "当前存档没有自有挂车。\n要去商店购买挂车后才能接货物市场任务。"
                : "自有挂车数量：" + list.Length, Text);
        }

        // ==================================================================
        TabPage BuildConvoyPage()
        {
            TabPage page = new TabPage("联机工具");
            page.BackColor = UiTheme.PageBack;

            page.Controls.Add(Btn("复制车辆位置", 8, 8, 280, 30, delegate { CopyTruckPosition(); }));
            page.Controls.Add(Btn("粘贴车辆位置", 298, 8, 280, 30, delegate { PasteTruckPosition(); }));
            page.Controls.Add(Btn("复制导航路线", 8, 42, 280, 30, delegate { CopyGpsRoute(); }));
            page.Controls.Add(Btn("粘贴导航路线", 298, 42, 280, 30, delegate { PasteGpsRoute(); }));

            Button big1 = BtnD("从多个存档中复制车辆位置", 8, 68, 280, 30, null);
            Button big2 = BtnD("根据多个车辆位置创建多个存档", 298, 68, 280, 30, null);
            page.Controls.Add(big1);
            page.Controls.Add(big2);

            Label info = Lbl("说明：复制/粘贴的是“我的卡车”位置（坐标+四元数）和 GPS 路径点。\n粘贴前请先在游戏里把车停稳，粘贴后重新载入存档即可。", 8, 120, 560, 60);
            info.ForeColor = Color.DimGray;
            page.Controls.Add(info);

            return page;
        }

        void CopyTruckPosition()
        {
            if (_editor == null) { MessageBox.Show("请先加载存档。", Text); return; }
            float[] v = _editor.Player.Get("my_truck_placement") as float[];
            if (v == null) { MessageBox.Show("这个存档没有车辆位置数据。", Text); return; }
            Clipboard.SetText(string.Join(", ", ToText(v)));
            lblStatus.Text = "车辆位置已复制到剪贴板";
        }

        void PasteTruckPosition()
        {
            if (_editor == null) { MessageBox.Show("请先加载存档。", Text); return; }
            float[] v = ParseFloats(Clipboard.GetText(), 7);
            if (v == null) { MessageBox.Show("剪贴板内容不是 7 个坐标数值。", Text); return; }
            try
            {
                _editor.File.SetVec8(_editor.Player, "my_truck_placement", v);
                lblStatus.Text = "车辆位置已粘贴（未保存）";
            }
            catch (Exception ex) { ShowError("粘贴失败", ex); }
        }

        void CopyGpsRoute()
        {
            if (_editor == null) { MessageBox.Show("请先加载存档。", Text); return; }
            string[] ahead = _editor.Player.Get("stored_gps_ahead_waypoints") as string[];
            string[] behind = _editor.Player.Get("stored_gps_behind_waypoints") as string[];
            string text = string.Join(Environment.NewLine, ahead ?? new string[0])
                + Environment.NewLine + "---" + Environment.NewLine
                + string.Join(Environment.NewLine, behind ?? new string[0]);
            Clipboard.SetText(text);
            lblStatus.Text = "导航路线已复制到剪贴板";
        }

        void PasteGpsRoute()
        {
            if (_editor == null) { MessageBox.Show("请先加载存档。", Text); return; }
            string[] parts = (Clipboard.GetText() ?? "").Replace("\r", "").Split(new string[] { "\n---\n" }, StringSplitOptions.None);
            if (parts.Length != 2) { MessageBox.Show("剪贴板格式不正确（应为 去程点 --- 回程点）。", Text); return; }
            string[] ahead = SplitIds(parts[0]);
            string[] behind = SplitIds(parts[1]);
            try
            {
                _editor.File.SetIdArray(_editor.Player, "stored_gps_ahead_waypoints", ahead);
                _editor.File.SetIdArray(_editor.Player, "stored_gps_behind_waypoints", behind);
                lblStatus.Text = "导航路线已粘贴（未保存）";
            }
            catch (Exception ex) { ShowError("粘贴失败", ex); }
        }

        static string[] SplitIds(string block)
        {
            string[] raw = block.Replace("\r", "").Split('\n');
            List<string> ids = new List<string>();
            foreach (string line in raw)
            {
                string t = line.Trim();
                if (t.Length > 0) ids.Add(t);
            }
            return ids.ToArray();
        }

        static float[] ParseFloats(string text, int count)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string[] parts = text.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != count) return null;
            float[] result = new float[count];
            for (int i = 0; i < count; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i])) return null;
            return result;
        }


        // ==================================================================
        // slots
        // ==================================================================
        void LoadSlots()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _slots = SaveLibrary.Discover();

                cmbRoot.BeginUpdate();
                cmbRoot.Items.Clear();
                cmbRoot.Items.Add("profiles");
                cmbRoot.SelectedIndex = 0;
                cmbRoot.EndUpdate();

                cmbProfile.BeginUpdate();
                cmbProfile.Items.Clear();
                string last = null;
                foreach (SaveSlot slot in _slots)
                {
                    if (slot.ProfileName != last)
                    {
                        cmbProfile.Items.Add(slot.ProfileName);
                        last = slot.ProfileName;
                    }
                }
                cmbProfile.EndUpdate();
                if (cmbProfile.Items.Count > 0) cmbProfile.SelectedIndex = 0;
                lblStatus.Text = string.Format("找到 {0} 个存档", _slots.Count);
            }
            catch (Exception ex)
            {
                ShowError("扫描存档失败", ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        void FillSaves()
        {
            if (cmbProfile.SelectedIndex < 0) return;
            string profile = cmbProfile.SelectedItem.ToString();
            cmbSave.BeginUpdate();
            cmbSave.Items.Clear();
            foreach (SaveSlot slot in _slots)
            {
                if (slot.ProfileName != profile) continue;
                cmbSave.Items.Add(slot);
            }
            cmbSave.EndUpdate();
            if (cmbSave.Items.Count > 0) cmbSave.SelectedIndex = 0;
        }

        void UpdateInfo()
        {
            SaveSlot slot = cmbSave.SelectedItem as SaveSlot;
            if (slot == null) { lblInfo = null; return; }
            if (lblInfo != null) lblInfo.Text = slot.Name;
            lblStatus.Text = string.Format("{0}\n{1:yyyy-MM-dd HH:mm}", slot.Name, slot.Modified);
        }

        void LoadSelected()
        {
            SaveSlot slot = cmbSave.SelectedItem as SaveSlot;
            if (slot == null) { MessageBox.Show("请先选择存档。", Text); return; }
            try
            {
                Cursor = Cursors.WaitCursor;
                _editor = SaveEditor.Load(slot.GamePath);
                _market = FreightMarket.Load(_editor.File);
                _pendingFuel.Clear();
                _pendingWear.Clear();
                _truckIndex = -1;

                FillProfilePage();
                FillCompanyPage();
                FillTruckPage();
                FillTrailerPage();
                FillFreightPage();
                FillCargoMarketPage();

                lblStatus.Text = string.Format("已加载 {0}：{1} 辆卡车", slot.Name, _editor.Trucks.Count);
                tabs.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _editor = null;
                _market = null;
                ShowError("打开存档失败（游戏是否在运行？）", ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        void CloseSave()
        {
            _editor = null;
            _market = null;
            lblStatus.Text = "已卸载";
        }

        // ==================================================================
        // profile page
        // ==================================================================
        void FillProfilePage()
        {
            _loading = true;
            UpdateLevelLabels();
            FillSkills();
            if (pnlColors != null) FillColors();
            chkFatigue.Checked = false;
            chkDebt.Checked = false;
            _loading = false;
        }

        int LevelFromXp(long xp)
        {
            long threshold = 0;
            int level = 0;
            foreach (int step in LevelSteps)
            {
                threshold += step;
                if (xp < threshold) return level;
                level++;
            }
            int last = LevelSteps[LevelSteps.Length - 1];
            while (true)
            {
                threshold += last;
                if (xp < threshold) return level;
                level++;
                if (level > 400) return level;
            }
        }

        long XpForLevel(int level)
        {
            if (level <= 0) return 0;
            long total = 0;
            for (int i = 0; i < level; i++)
                total += i < LevelSteps.Length ? LevelSteps[i] : LevelSteps[LevelSteps.Length - 1];
            return total;
        }

        long NextLevelXp(long xp)
        {
            long threshold = 0;
            foreach (int step in LevelSteps)
            {
                threshold += step;
                if (xp < threshold) return threshold;
            }
            int last = LevelSteps[LevelSteps.Length - 1];
            while (true)
            {
                threshold += last;
                if (xp < threshold) return threshold;
            }
        }

        void UpdateLevelLabels()
        {
            long xp = _editor == null ? 0 : _editor.Experience;
            int level = LevelFromXp(xp);
            lblLevel.Text = level.ToString();
            string title = LevelTitles[0];
            for (int i = LevelTitles.Length - 1; i >= 0; i--)
                if (level >= LevelLimits[i]) { title = LevelTitles[i]; break; }
            lblLegend.Text = title;
            long next = NextLevelXp(xp);
            lblXp.Text = string.Format("{0} / {1}", xp, next);
        }

        void ChangeLevel(int delta)
        {
            if (_editor == null) { MessageBox.Show("请先加载存档。", Text); return; }
            SetLevel(LevelFromXp(_editor.Experience) + delta);
        }

        void SetLevel(int level)
        {
            if (_editor == null) { MessageBox.Show("请先加载存档。", Text); return; }
            if (level < 0) level = 0;
            if (level > 150) level = 150;
            long xp = XpForLevel(level);
            if (xp > uint.MaxValue) xp = uint.MaxValue;
            _editor.Experience = xp;
            UpdateLevelLabels();
        }

        void FillSkills()
        {
            SiiBlock economy = _editor.File.FindBlock("economy");
            for (int i = 0; i < 6; i++)
            {
                int value = Convert.ToInt32(economy.Get(_skillFields[i]) ?? 0L, CultureInfo.InvariantCulture);
                for (int j = 0; j < 6; j++)
                {
                    bool on = i == 0 ? (value & (1 << j)) != 0 : value >= j + 1;
                    _skillButtons[i, j].Checked = on;
                    _skillButtons[i, j].BackColor = on ? Color.FromArgb(176, 128, 32) : Color.FromArgb(236, 238, 241);
                    _skillButtons[i, j].FlatAppearance.BorderColor = on ? Color.FromArgb(140, 100, 24) : Color.FromArgb(210, 214, 220);
                    _skillButtons[i, j].BackgroundImage = UiTheme.Gradient(
                        on ? Color.FromArgb(214, 176, 96) : Color.FromArgb(240, 242, 245),
                        on ? Color.FromArgb(150, 104, 30) : Color.FromArgb(214, 218, 225), 38, 36);
                    _skillButtons[i, j].BackgroundImageLayout = ImageLayout.Stretch;
                }
            }
        }

        void OnSkillClick(object sender, EventArgs e)
        {
            if (_loading || _editor == null) return;
            CheckBox cb = (CheckBox)sender;
            int tag = (int)cb.Tag;
            int skill = tag / 10;
            int level = tag % 10 + 1;
            long value = skill == 0 ? (1L << level) - 1 : level;
            _editor.File.SetInt(_editor.File.FindBlock("economy"), _skillFields[skill], value);
            FillSkills();
        }

        void FillColors()
        {
            pnlColors.Controls.Clear();
            _colorButtons.Clear();
            SiiBlock economy = _editor.File.FindBlock("economy");
            SiiValue v = economy == null ? null : economy.GetValue("user_colors");
            uint[] colors = v == null ? new uint[0] : (v.Data as uint[]);
            if (colors == null)
            {
                long[] raw = v == null ? null : v.Data as long[];
                if (raw != null)
                {
                    colors = new uint[raw.Length];
                    for (int i = 0; i < raw.Length; i++) colors[i] = unchecked((uint)raw[i]);
                }
                else colors = new uint[0];
            }
            for (int i = 0; i < colors.Length && i < 8; i++)
            {
                Button b = new Button();
                b.SetBounds(4 + (i % 4) * 34, 4 + (i / 4) * 34, 30, 30);
                b.BackColor = colors[i] == 0 ? Color.White : Color.FromArgb(unchecked((int)colors[i]));
                UiTheme.StyleSwatch(b);
                b.Tag = i;
                b.Click += delegate { PickColor((int)b.Tag); };
                pnlColors.Controls.Add(b);
                _colorButtons.Add(b);
            }
        }

        void PickColor(int index)
        {
            if (_editor == null) return;
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.FullOpen = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                SiiValue v = _editor.File.FindBlock("economy").GetValue("user_colors");
                if (v == null) return;
                long[] raw = v.Data as long[];
                if (raw == null || index >= raw.Length) return;
                raw[index] = unchecked((uint)dialog.Color.ToArgb());
                v.Data = raw;
                v.Dirty = true;
                _editor.File.FindBlock("economy").Dirty = true;
                if (pnlColors != null) FillColors();
            }
        }

        void AddColorSlot()
        {
            if (_editor == null) return;
            SiiValue v = _editor.File.FindBlock("economy").GetValue("user_colors");
            if (v == null) return;
            long[] raw = v.Data as long[];
            if (raw == null) return;
            Array.Resize(ref raw, raw.Length + 1);
            v.Data = raw;
            v.Count = raw.Length;
            v.Dirty = true;
            _editor.File.FindBlock("economy").Dirty = true;
            if (pnlColors != null) FillColors();
        }

        // ==================================================================
        // company page
        // ==================================================================
        void FillCompanyPage()
        {
            lblInfo = lblStatus;
            SaveSlot slot = cmbSave.SelectedItem as SaveSlot;
            txtCompanyName.Text = slot == null ? "" : slot.ProfileName;
            lblNameCount.Text = string.Format("{0} / 20", txtCompanyName.Text.Length);

            txtMoney.Text = string.Format("€ {0:N0}", _editor.Money);

            cmbHq.Items.Clear();
            object hq = _editor.Player.Get("hq_city");
            string city = hq as string;
            if (!string.IsNullOrEmpty(city))
            {
                city = city.Replace('_', ' ');
                city = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(city);
                cmbHq.Items.Add(city);
            }
            if (cmbHq.Items.Count > 0) cmbHq.SelectedIndex = 0;

            lstGarages.BeginUpdate();
            lstGarages.Items.Clear();
            _garageBlocks.Clear();
            List<SiiBlock> garages = _editor.File.FindBlocks("garage");
            int owned = 0;
            for (int i = 0; i < garages.Count; i++)
            {
                SiiBlock g = garages[i];
                long status = Convert.ToInt64(g.Get("status") ?? 0L, CultureInfo.InvariantCulture);
                if (status > 0) owned++;
                string name = g.Ident.StartsWith("garage.", StringComparison.Ordinal)
                    ? g.Ident.Substring(7) : g.Ident;
                name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.Replace('_', ' '));
                lstGarages.Items.Add(name + "|" + (status > 0 ? "已购买" : "Not owned"));
                _garageBlocks.Add(g);
            }
            lstGarages.EndUpdate();
            if (lstGarages.Items.Count > 0 && lstGarages.SelectedIndex < 0) lstGarages.SelectedIndex = 0;
            lblGarageCount.Text = string.Format("{0} / {1}", owned, garages.Count);
        }

        bool SaveCompanyMoney()
        {
            if (_editor == null) return false;
            string text = txtMoney.Text.Replace("€", "").Replace(".", "").Replace(",", "").Replace(" ", "").Replace("-", "").Trim();
            long value;
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return false;
            _editor.Money = value;
            txtMoney.Text = string.Format("€ {0:N0}", value);
            return true;
        }

        // ==================================================================
        // truck page
        // ==================================================================
        void FillTruckPage()
        {
            _loading = true;
            cmbTruck.Items.Clear();
            foreach (TruckInfo t in _editor.Trucks)
                cmbTruck.Items.Add(string.Format("{0}  ·  {1:N0} km", TruckInfo.CleanPlate(t.Plate), t.Odometer));
            if (cmbTruck.Items.Count > 0) cmbTruck.SelectedIndex = 0;
            _loading = false;
            OnTruckChanged(null, EventArgs.Empty);
        }

        void OnTruckChanged(object sender, EventArgs e)
        {
            if (_editor == null) return;
            int index = cmbTruck.SelectedIndex;
            if (index < 0 || index >= _editor.Trucks.Count) return;
            _truckIndex = index;
            TruckInfo t = _editor.Trucks[index];

            float[] values = { t.EngineWear, t.TransmissionWear, t.ChassisWear, t.CabinWear,
                               t.WheelsWear == null || t.WheelsWear.Length == 0 ? 0f : t.WheelsWear[0] };
            for (int i = 0; i < 5; i++)
            {
                int pct = (int)Math.Round(values[i] * 100);
                if (pct < 0) pct = 0;
                if (pct > 100) pct = 100;
                _bars[i].Value = pct;
                _barValues[i].Text = pct + "%";
            }
            SetFuel(t.Fuel);
            txtPlate.Text = TruckInfo.CleanPlate(t.Plate);
        }

        void SetFuel(float fuel)
        {
            if (fuel < 0) fuel = 0;
            if (fuel > 1) fuel = 1;
            int height = (int)Math.Round(286 * fuel);
            pnlFuelFill.SetBounds(2, 288 - height, 44, height);
            lblFuel.Text = string.Format("{0:P0}", fuel);
        }

        void RepairPart(int index)
        {
            if (_editor == null || _truckIndex < 0) return;
            TruckInfo t = _editor.Trucks[_truckIndex];
            string[] fields = { "engine_wear", "transmission_wear", "chassis_wear", "cabin_wear" };
            if (index == 0) t.EngineWear = 0f;
            if (index == 1) t.TransmissionWear = 0f;
            if (index == 2) t.ChassisWear = 0f;
            if (index == 3) t.CabinWear = 0f;
            if (index < 4) _editor.File.SetFloat(t.Block, fields[index], 0f);
            else
            {
                SiiValue wheels = t.Block.GetValue("wheels_wear");
                if (wheels != null && wheels.Type == SiiType.SingleArray)
                    for (int i = 0; i < wheels.Count; i++)
                        _editor.File.SetFloatArrayItem(t.Block, "wheels_wear", i, 0f);
            }
            OnTruckChanged(null, EventArgs.Empty);
        }

        void FillTrailerPage()
        {
            object trailers = _editor.Player.Get("trailers");
            string[] list = trailers as string[];
            int count = list == null ? 0 : list.Length;
            cmbTrailer.Items.Clear();
            cmbTrailer.Items.Add(count == 0 ? "- NONE -" : string.Format("{0} 辆挂车", count));
            cmbTrailer.SelectedIndex = 0;
            for (int i = 0; i < 4; i++) _tBars[i].Value = 0;
        }

        // ==================================================================
        // freight page
        // ==================================================================
        void FillFreightPage()
        {
            _loading = true;

            cmbFreightCompany.Items.Clear();
            cmbFreightCompany.Items.Add("All");
            foreach (string s in _market.SourceCompanies) cmbFreightCompany.Items.Add(FreightOffer.ShortCompany(s));
            cmbFreightCompany.SelectedIndex = 0;

            cmbSourceCity.Items.Clear();
            cmbSourceCity.Items.Add("All");
            cmbSourceCompany.Items.Clear();
            cmbSourceCompany.Items.Add("All");
            foreach (string sc in _market.SourceCompanies) cmbSourceCompany.Items.Add(FreightOffer.ShortCompany(sc));
            cmbSourceCompany.SelectedIndex = 0;

            cmbDestCity.Items.Clear();
            cmbDestCity.Items.Add("All");
            cmbDestCompany.Items.Clear();
            cmbDestCompany.Items.Add("All");

            HashSet<string> sourceCities = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> destCities = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> destCompanies = new HashSet<string>(StringComparer.Ordinal);
            foreach (string s in _market.SourceCompanies)
            {
                string shortName = FreightOffer.ShortCompany(s);
                int dot = shortName.IndexOf('.');
                if (dot > 0) sourceCities.Add(shortName.Substring(dot + 1));
            }
            foreach (string t in _market.Targets)
            {
                int dot = t.IndexOf('.');
                if (dot > 0)
                {
                    destCompanies.Add(t.Substring(0, dot));
                    destCities.Add(t.Substring(dot + 1));
                }
            }
            foreach (string c in sourceCities) cmbSourceCity.Items.Add(c);
            foreach (string c in destCities) cmbDestCity.Items.Add(c);
            foreach (string c in destCompanies) cmbDestCompany.Items.Add(c);
            cmbSourceCity.SelectedIndex = 0;
            cmbDestCity.SelectedIndex = 0;
            cmbDestCompany.SelectedIndex = 0;

            cmbCargo.Items.Clear();
            foreach (string s in _market.Cargos) cmbCargo.Items.Add(s);
            if (cmbCargo.Items.Count > 0) cmbCargo.SelectedIndex = 0;

            _loading = false;
            OnCargoChanged();
            RefreshOffers();
        }

        void OnCargoChanged()
        {
            if (_loading || _market == null) return;
            SiiBlock template = _market.FindTemplate(cmbCargo.Text);
            cmbTrailerDef.Items.Clear();
            cmbTrailerVariant.Items.Clear();
            if (template != null)
            {
                object def = template.Get("trailer_definition");
                object variant = template.Get("trailer_variant");
                if (def is string) cmbTrailerDef.Items.Add(def);
                if (variant is string) cmbTrailerVariant.Items.Add(variant);
            }
            if (cmbTrailerDef.Items.Count > 0) cmbTrailerDef.SelectedIndex = 0;
            if (cmbTrailerVariant.Items.Count > 0) cmbTrailerVariant.SelectedIndex = 0;
        }

        void RandomCargo()
        {
            if (_market == null || _market.Cargos.Count == 0) return;
            cmbCargo.SelectedIndex = new Random().Next(_market.Cargos.Count);
            OnCargoChanged();
        }

        void RefreshOffers()
        {
            if (_market == null) return;
            _filtered = new List<FreightOffer>();
            string company = cmbFreightCompany.SelectedIndex > 0 ? cmbFreightCompany.SelectedItem.ToString() : null;
            if (company == null && cmbSourceCompany.SelectedIndex > 0) company = cmbSourceCompany.SelectedItem.ToString();
            string cargo = string.IsNullOrEmpty(cmbCargo.Text) ? null : cmbCargo.Text;
            string sourceCity = cmbSourceCity.SelectedIndex > 0 ? cmbSourceCity.SelectedItem.ToString() : null;
            string destCompany = cmbDestCompany.SelectedIndex > 0 ? cmbDestCompany.SelectedItem.ToString() : null;

            long totalDistance = 0;
            for (int i = 0; i < _market.Offers.Count; i++)
            {
                FreightOffer o = _market.Offers[i];
                if (string.IsNullOrEmpty(o.Cargo) || string.IsNullOrEmpty(o.Target)) continue;
                if (company != null && o.SourceLabel != company) continue;
                if (cargo != null && o.Cargo != cargo) continue;
                if (sourceCity != null && !o.SourceLabel.EndsWith("." + sourceCity, StringComparison.Ordinal)) continue;
                if (destCompany != null && !o.TargetLabel.StartsWith(destCompany + ".", StringComparison.Ordinal)) continue;
                _filtered.Add(o);
                totalDistance += o.Distance;
            }
            lvOffers.VirtualListSize = _filtered.Count;
            lvOffers.Invalidate();
            lblDistance.Text = string.Format("{0} km  ({1} 条)", totalDistance, _filtered.Count);
        }

        void OnRetrieveOffer(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= _filtered.Count) return;
            FreightOffer o = _filtered[e.ItemIndex];
            long left = o.Expiry - _market.GameTime;
            if (left < 0) left = 0;
            e.Item = new ListViewItem(new string[]
            {
                o.SourceLabel, o.Cargo, o.TargetLabel,
                o.Distance.ToString(CultureInfo.InvariantCulture),
                o.Urgency.ToString(CultureInfo.InvariantCulture),
                left.ToString(CultureInfo.InvariantCulture)
            });
        }

        FreightOffer SelectedOffer()
        {
            if (lvOffers.SelectedIndices.Count == 0) return null;
            int index = lvOffers.SelectedIndices[0];
            if (index < 0 || index >= _filtered.Count) return null;
            return _filtered[index];
        }

        void OnOfferSelected(object sender, EventArgs e)
        {
            FreightOffer o = SelectedOffer();
            if (o == null) return;
            _loading = true;
            cmbCargo.Text = o.Cargo;
            cmbSourceCity.Text = o.SourceLabel.Substring(o.SourceLabel.IndexOf('.') + 1);
            cmbDestCity.Text = o.TargetLabel.Substring(o.TargetLabel.IndexOf('.') + 1);
            cmbUrgency.SelectedIndex = o.Urgency >= 0 && o.Urgency <= 2 ? o.Urgency : 0;
            _loading = false;
        }

        void AddFreightOffer()
        {
            if (_market == null) { MessageBox.Show("请先加载存档。", Text); return; }
            try
            {
                FreightOffer selected = SelectedOffer();
                string source = selected != null ? selected.Source : null;
                if (source == null && cmbFreightCompany.SelectedIndex > 0)
                    source = FindCompanyByLabel(cmbFreightCompany.SelectedItem.ToString());
                if (source == null)
                {
                    MessageBox.Show("请先在“过滤 → 公司”里选择来源公司，或在列表里选中一条报价。", Text);
                    return;
                }
                string cargo = cmbCargo.Text;
                if (string.IsNullOrEmpty(cargo)) { MessageBox.Show("请选择货物。", Text); return; }
                string target = BuildTarget(cmbDestCity.Text);
                if (target == null) { MessageBox.Show("请选择目的地城市。", Text); return; }

                int distance = selected != null ? selected.Distance : 500;
                int urgency = cmbUrgency.SelectedIndex;
                FreightOffer added = _market.Add(source, cargo, target, distance, urgency, 48 * 60,
                    selected != null ? selected.Units : 0, 1f);
                RefreshOffers();
                lblStatus.Text = "已添加报价：" + added.Cargo;
            }
            catch (Exception ex)
            {
                ShowError("添加报价失败", ex);
            }
        }

        void ApplySelectedOffer()
        {
            FreightOffer o = SelectedOffer();
            if (o == null || _market == null) return;
            try
            {
                string target = BuildTarget(cmbDestCity.Text);
                if (target == null) target = o.Target;
                _market.Apply(o, cmbCargo.Text, target, o.Distance, cmbUrgency.SelectedIndex, 48 * 60, o.Units, o.Fill);
                RefreshOffers();
                lblStatus.Text = "报价已更新（未保存）";
            }
            catch (Exception ex)
            {
                ShowError("修改报价失败", ex);
            }
        }

        string BuildTarget(string city)
        {
            if (string.IsNullOrEmpty(city) || city == "All") return null;
            foreach (string t in _market.Targets)
                if (t.EndsWith("." + city, StringComparison.Ordinal)) return t;
            return null;
        }

        string FindCompanyByLabel(string label)
        {
            foreach (string s in _market.SourceCompanies)
                if (FreightOffer.ShortCompany(s) == label) return s;
            return null;
        }

        // ==================================================================
        // save / tools
        // ==================================================================
        void OnSave(object sender, EventArgs e)
        {
            if (_editor == null) { MessageBox.Show("请先加载存档。", Text); return; }
            if (Process.GetProcessesByName("eurotrucks2").Length > 0)
            {
                if (MessageBox.Show("检测到游戏正在运行，退出时可能覆盖修改。仍要保存吗？",
                        Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }
            try
            {
                Cursor = Cursors.WaitCursor;
                if (!SaveCompanyMoney()) { MessageBox.Show("金钱格式不正确。", Text); return; }
                if (chkFatigue.Checked) _editor.ClearFatigue();
                if (chkDebt.Checked) _editor.ClearDebt();

                string backup = _editor.Save(_asText);
                lblStatus.Text = "已保存。备份：" + Path.GetFileName(backup);
                MessageBox.Show("修改已保存。\n进游戏载入该存档，确认后再手动存档。", Text);
            }
            catch (Exception ex)
            {
                ShowError("保存失败", ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        void OpenFolder()
        {
            if (_editor == null) { MessageBox.Show("请先加载存档。", Text); return; }
            Process.Start("explorer.exe", "/select,\"" + _editor.SourcePath + "\"");
        }

        void BackupAll()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                string root = Path.Combine(SaveLibrary.DocumentsRoot, "存档备份");
                List<string> written = SaveLibrary.BackupAll(root, 30);
                MessageBox.Show("已备份 " + written.Count + " 个存档到：\n" + root, Text);
            }
            catch (Exception ex)
            {
                ShowError("备份失败", ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        void RestoreBackup()
        {
            if (_editor == null) { MessageBox.Show("请先加载存档。", Text); return; }
            string dir = Path.Combine(Path.GetDirectoryName(_editor.SourcePath), "editor_backups");
            if (!Directory.Exists(dir)) { MessageBox.Show("还没有备份。", Text); return; }
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.InitialDirectory = dir;
                dialog.Filter = "备份存档 (*.sii)|*.sii";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (MessageBox.Show("用备份覆盖当前存档？", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                File.Copy(dialog.FileName, _editor.SourcePath, true);
                LoadSelected();
            }
        }

        void ExportText()
        {
            if (_editor == null) { MessageBox.Show("请先加载存档。", Text); return; }
            string target = _editor.SourcePath + ".文本预览.sii";
            File.WriteAllText(target, _editor.File.ToText(), new System.Text.UTF8Encoding(false));
            Process.Start("explorer.exe", "/select,\"" + target + "\"");
        }

        readonly Dictionary<Control, string> _originalText = new Dictionary<Control, string>();
        string _language = "zh";

        static readonly Dictionary<string, string[]> Phrases = new Dictionary<string, string[]>
        {
            { "档案", new[] { "Profile", "檔案", "プロフィール" } },
            { "公司", new[] { "Company", "公司", "会社" } },
            { "卡车", new[] { "Truck", "卡車", "トラック" } },
            { "挂车", new[] { "Trailer", "拖車", "トレーラー" } },
            { "货运市场", new[] { "Freight Market", "貨運市場", "貨物市場" } },
            { "货物市场", new[] { "Cargo Market", "貨物市場", "カーゴ市場" } },
            { "联机工具", new[] { "Convoy Tools", "連線工具", "コンボイツール" } },
            { "程序", new[] { "Program", "程式", "プログラム" } },
            { "语言", new[] { "Language", "語言", "言語" } },
            { "帮助", new[] { "Help", "說明", "ヘルプ" } },
            { "保存修改", new[] { "Save Changes", "儲存修改", "変更を保存" } },
            { "重新扫描存档", new[] { "Rescan Saves", "重新掃描存檔", "セーブを再走査" } },
            { "备份全部存档", new[] { "Backup All Saves", "備份全部存檔", "全セーブをバックアップ" } },
            { "还原备份…", new[] { "Restore Backup...", "還原備份…", "バックアップを復元…" } },
            { "导出可读文本", new[] { "Export Text", "匯出文字", "テキストを出力" } },
            { "保存格式", new[] { "Save Format", "儲存格式", "保存形式" } },
            { "退出", new[] { "Exit", "結束", "終了" } },
            { "使用说明", new[] { "Manual", "使用說明", "説明書" } },
            { "关于", new[] { "About", "關於", "バージョン情報" } },
            { "档案与存档", new[] { "Profiles And Saves", "設定檔與存檔", "プロファイルとセーブ" } },
            { "添加自定义文件夹", new[] { "Add Custom Folder", "新增自訂資料夾", "カスタムフォルダを追加" } },
            { "备份", new[] { "Backups", "備份", "バックアップ" } },
            { "解密", new[] { "Decrypt", "解密", "復号" } },
            { "打开文件夹", new[] { "Open Folder", "開啟資料夾", "フォルダを開く" } },
            { "加载存档", new[] { "Load", "載入存檔", "ロード" } },
            { "保存", new[] { "Save", "儲存", "保存" } },
            { "玩家级别", new[] { "Player Level", "玩家等級", "プレイヤーレベル" } },
            { "技能", new[] { "Skills", "技能", "スキル" } },
            { "状态", new[] { "State", "狀態", "状態" } },
            { "清除疲劳", new[] { "Clear Fatigue", "清除疲勞", "疲労を解除" } },
            { "清除债务与透支", new[] { "Clear Debt", "清除債務與透支", "借金を解除" } },
            { "危险品", new[] { "ADR", "危險品", "危険物" } },
            { "长途运输", new[] { "Long Distance", "長途運輸", "長距離輸送" } },
            { "贵重货物", new[] { "High Value", "貴重貨物", "高価な貨物" } },
            { "易碎货物", new[] { "Fragile", "易碎貨物", "壊れ物" } },
            { "及时运输", new[] { "Just-In-Time", "及時運輸", "時間指定" } },
            { "燃油效能", new[] { "Ecodriving", "燃油效能", "エコドライブ" } },
            { "公司名称", new[] { "Company Name", "公司名稱", "会社名" } },
            { "游戏资金", new[] { "Money", "遊戲資金", "資金" } },
            { "总部城市", new[] { "HQ City", "總部城市", "本拠地" } },
            { "车库", new[] { "Garages", "車庫", "ガレージ" } },
            { "购买", new[] { "Buy", "購買", "購入" } },
            { "升级", new[] { "Upgrade", "升級", "アップグレード" } },
            { "降级", new[] { "Downgrade", "降級", "ダウングレード" } },
            { "出售", new[] { "Sell", "出售", "売却" } },
            { "详细数据", new[] { "Details", "詳細資料", "詳細データ" } },
            { "发动机", new[] { "Engine", "引擎", "エンジン" } },
            { "变速箱", new[] { "Transmission", "變速箱", "トランスミッション" } },
            { "底盘", new[] { "Chassis", "底盤", "シャシー" } },
            { "驾驶舱", new[] { "Cabin", "駕駛艙", "キャビン" } },
            { "车轮", new[] { "Wheels", "車輪", "ホイール" } },
            { "燃料", new[] { "Fuel", "燃料", "燃料" } },
            { "车牌", new[] { "Plate", "車牌", "ナンバー" } },
            { "分享卡车设置", new[] { "Share Truck Settings", "分享卡車設定", "トラック設定を共有" } },
            { "分享挂车设置", new[] { "Share Trailer Settings", "分享拖車設定", "トレーラー設定を共有" } },
            { "过滤", new[] { "Filter", "過濾", "フィルター" } },
            { "起点", new[] { "Source", "起點", "出発地" } },
            { "终点", new[] { "Destination", "終點", "目的地" } },
            { "货物", new[] { "Cargo", "貨物", "貨物" } },
            { "加急", new[] { "Urgency", "急件", "緊急度" } },
            { "添加任务到列表", new[] { "Add Job To List", "新增任務到列表", "リストに追加" } },
            { "随机生成货物", new[] { "Random Cargo", "隨機貨物", "ランダム貨物" } },
            { "清空列表", new[] { "Clear List", "清空列表", "リストを消去" } },
            { "刷新列表", new[] { "Refresh", "重新整理", "更新" } },
            { "查看自有挂车", new[] { "My Trailers", "查看自有拖車", "所有トレーラー" } },
            { "复制车辆位置", new[] { "Copy Truck Position", "複製車輛位置", "車両位置をコピー" } },
            { "粘贴车辆位置", new[] { "Paste Truck Position", "貼上車輛位置", "車両位置を貼り付け" } },
            { "复制导航路线", new[] { "Copy GPS Route", "複製導航路線", "ルートをコピー" } },
            { "粘贴导航路线", new[] { "Paste GPS Route", "貼上導航路線", "ルートを貼り付け" } }
        };

        void ApplyLanguage(string code)
        {
            _language = code;
            TranslateControls(this, code);
        }

        void TranslateControls(Control root, string code)
        {
            foreach (Control child in root.Controls)
            {
                string original;
                if (!_originalText.TryGetValue(child, out original))
                {
                    original = child.Text;
                    _originalText[child] = original;
                }
                string[] translations;
                if (!string.IsNullOrEmpty(original) && Phrases.TryGetValue(original, out translations))
                {
                    int index = code == "en" ? 0 : code == "tw" ? 1 : code == "ja" ? 2 : -1;
                    child.Text = index < 0 ? original : translations[index];
                }
                if (child is MenuStrip)
                {
                    TranslateMenu((MenuStrip)child, code);
                }
                TranslateControls(child, code);
            }
        }

        void TranslateMenu(MenuStrip menu, string code)
        {
            int index = code == "en" ? 0 : code == "tw" ? 1 : code == "ja" ? 2 : -1;
            foreach (ToolStripMenuItem top in menu.Items)
            {
                string[] t;
                string orig;
                if (!_menuText.TryGetValue(top, out orig)) { orig = top.Text; _menuText[top] = orig; }
                if (Phrases.TryGetValue(orig, out t)) top.Text = index < 0 ? orig : t[index];
                foreach (ToolStripItem sub in top.DropDownItems)
                {
                    ToolStripMenuItem item = sub as ToolStripMenuItem;
                    if (item == null) continue;
                    string o;
                    if (!_menuText.TryGetValue(item, out o)) { o = item.Text; _menuText[item] = o; }
                    if (Phrases.TryGetValue(o, out t)) item.Text = index < 0 ? o : t[index];
                }
            }
        }

        readonly Dictionary<ToolStripItem, string> _menuText = new Dictionary<ToolStripItem, string>();

        void OpenHelp()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "使用说明.txt");
            if (File.Exists(path)) Process.Start(path);
        }

        void ShowAbout()
        {
            MessageBox.Show("ETS2 存档修改器 1.0\n界面参考 TS SE Tool\n\n直接解析二进制存档，只改写目标字段。", Text);
        }

        void FillCargoMarketPage()
        {
            if (lstCargoMarket == null) return;
            lstCargoMarket.Items.Clear();
            if (_editor == null) return;
            List<SiiBlock> jobs = _editor.File.FindBlocks("job_info");
            for (int i = 0; i < jobs.Count; i++)
            {
                SiiBlock job = jobs[i];
                object cargo = job.Get("cargo");
                string cargoText = cargo as string;
                if (string.IsNullOrEmpty(cargoText) || cargoText == "null") continue;
                object target = job.Get("target_company");
                string targetText = target as string;
                object distance = job.Get("planned_distance_km");
                lstCargoMarket.Items.Add(string.Format("{0}  →  {1}   {2} km", cargoText, targetText, distance));
            }
            if (lstCargoMarket.Items.Count == 0)
                lstCargoMarket.Items.Add("（当前存档没有货物市场报价：需要自有挂车并接取货物市场任务）");
        }

        void ConvoyAction(string label)
        {
            if (_editor == null) { MessageBox.Show("请先加载存档。", Text); return; }
            try
            {
                if (label.StartsWith("复制车辆位置"))
                {
                    object placement = _editor.Player.Get("my_truck_placement");
                    float[] v = placement as float[];
                    string text = v == null ? "(none)" : string.Join(", ", ToText(v));
                    Clipboard.SetText(text);
                    lblStatus.Text = "车辆位置已复制到剪贴板";
                    return;
                }
                if (label.StartsWith("复制导航路线"))
                {
                    object gps = _editor.Player.Get("stored_gps_waypoints_history");
                    string[] ids = gps as string[];
                    Clipboard.SetText(ids == null ? "(none)" : string.Join(Environment.NewLine, ids));
                    lblStatus.Text = "导航路线已复制到剪贴板";
                    return;
                }
                MessageBox.Show("写入位置/路线需要重编码四元数，当前版本暂不支持。", Text);
            }
            catch (Exception ex)
            {
                ShowError("操作失败", ex);
            }
        }

        static string[] ToText(float[] values)
        {
            string[] result = new string[values.Length];
            for (int i = 0; i < values.Length; i++) result[i] = values[i].ToString("0.######", CultureInfo.InvariantCulture);
            return result;
        }

        void ShowError(string title, Exception ex)
        {
            MessageBox.Show(title + "：\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static class GuiMain
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
