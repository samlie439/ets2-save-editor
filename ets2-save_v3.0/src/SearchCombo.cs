using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Ets2Save
{
    /// <summary>Modern rounded combo box with a searchable drop-down panel.</summary>
    public sealed class SearchCombo : Control
    {
        public readonly List<object> Items = new List<object>();
        public event EventHandler SelectedIndexChanged;

        int _index = -1;
        string _text = "";
        bool _open;
        ComboBoxStyle _style = ComboBoxStyle.DropDownList;

        public ComboBoxStyle DropDownStyle
        {
            get { return _style; }
            set { _style = value; }
        }

        public string Placeholder = "搜索并选择";

        public void BeginUpdate() { }
        public void EndUpdate() { Invalidate(); }

        public SearchCombo()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            ForeColor = Color.FromArgb(31, 35, 40);
            Font = new Font("Microsoft YaHei UI", 9.5f);
            Height = 32;
        }

        public int SelectedIndex
        {
            get { return _index; }
            set
            {
                _index = value;
                _text = value >= 0 && value < Items.Count ? Convert.ToString(Items[value]) : "";
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get { return _index >= 0 && _index < Items.Count ? Items[_index] : null; }
        }

        public override string Text
        {
            get { return _text; }
            set { _text = value ?? ""; Invalidate(); }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (GraphicsPath path = UiTheme.Rounded(new Rectangle(0, 0, Width, Height), 8))
                Region = new Region(path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = UiTheme.Rounded(rect, 8))
            using (SolidBrush fill = new SolidBrush(Enabled ? Color.White : Color.FromArgb(246, 247, 249)))
            using (Pen pen = new Pen(_open ? UiTheme.Accent : UiTheme.Border, _open ? 1.6f : 1f))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
            string shown = string.IsNullOrEmpty(_text) ? Placeholder : _text;
            Color color = string.IsNullOrEmpty(_text) ? Color.FromArgb(160, 166, 176) : ForeColor;
            using (SolidBrush text = new SolidBrush(color))
                e.Graphics.DrawString(shown, Font, text, new RectangleF(12, 0, Width - 40, Height), MidLeft());
            using (Pen chevron = new Pen(Color.FromArgb(120, 128, 140), 1.6f))
            {
                int cx = Width - 18, cy = Height / 2;
                e.Graphics.DrawLines(chevron, new Point[] {
                    new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
            }
        }

        static StringFormat MidLeft()
        {
            StringFormat f = new StringFormat();
            f.LineAlignment = StringAlignment.Center;
            f.FormatFlags = StringFormatFlags.NoWrap;
            f.Trimming = StringTrimming.EllipsisCharacter;
            return f;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled) return;
            if (!_open) OpenPopup();
        }

        void OpenPopup()
        {
            _open = true;
            Invalidate();
            ComboPopup popup = new ComboPopup(this);
            popup.FormClosed += delegate { _open = false; Invalidate(); };
            popup.Show(this);
        }

        internal void Choose(object item)
        {
            int idx = Items.IndexOf(item);
            if (idx >= 0) SelectedIndex = idx;
        }
    }

    internal sealed class ComboPopup : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        readonly SearchCombo _owner;
        readonly TextBox _search;
        readonly ListBox _list;

        public ComboPopup(SearchCombo owner)
        {
            _owner = owner;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.White;
            Size = new Size(owner.Width, 240);
            DoubleBuffered = true;

            Panel host = new Panel();
            host.SetBounds(8, 8, owner.Width - 16, 34);
            host.BackColor = Color.White;
            host.Paint += delegate (object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, host.Width - 1, host.Height - 1);
                using (GraphicsPath path = UiTheme.Rounded(r, 8))
                using (Pen pen = new Pen(UiTheme.Border))
                    e.Graphics.DrawPath(pen, path);
            };
            Controls.Add(host);

            _search = new TextBox();
            _search.BorderStyle = BorderStyle.None;
            _search.SetBounds(12, 8, owner.Width - 44, 20);
            _search.TextChanged += delegate { Filter(); };
            host.Controls.Add(_search);

            _list = new ListBox();
            _list.BorderStyle = BorderStyle.None;
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = 30;
            _list.SetBounds(8, 50, owner.Width - 16, 182);
            _list.DrawItem += DrawItem;
            _list.DoubleClick += delegate { Accept(); };
            _list.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) Accept(); };
            Controls.Add(_list);

            KeyPreview = true;
            KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };

            Fill("");
            Load += delegate
            {
                SendMessage(_search.Handle, 0x1501, (IntPtr)1, "搜索…");
                _search.Focus();
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = UiTheme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 12))
            using (Pen pen = new Pen(UiTheme.Border))
                e.Graphics.DrawPath(pen, path);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (GraphicsPath path = UiTheme.Rounded(new Rectangle(0, 0, Width, Height), 12))
                Region = new Region(path);
        }

        void DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush back = new SolidBrush(selected ? Color.FromArgb(232, 241, 253) : Color.White))
                e.Graphics.FillRectangle(back, e.Bounds);
            string text = Convert.ToString(_list.Items[e.Index]);
            using (SolidBrush ink = new SolidBrush(selected ? UiTheme.AccentDeep : UiTheme.Ink))
            using (Font font = new Font("Microsoft YaHei UI", 9.5f))
                e.Graphics.DrawString(text, font, ink, e.Bounds.Left + 12, e.Bounds.Top + 6);
        }

        void Fill(string filter)
        {
            _list.Items.Clear();
            foreach (object item in _owner.Items)
            {
                string text = Convert.ToString(item);
                if (string.IsNullOrEmpty(filter) || text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    _list.Items.Add(item);
            }
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        }

        void Filter() { Fill(_search.Text); }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle screen = Screen.FromControl(_owner).WorkingArea;
            Point p = _owner.PointToScreen(new Point(0, _owner.Height + 4));
            if (p.Y + Height > screen.Bottom) p.Y = _owner.PointToScreen(new Point(0, -Height - 4)).Y;
            Location = p;
        }

        void Accept()
        {
            if (_list.SelectedItem == null) return;
            object item = _list.SelectedItem;
            Close();
            _owner.Choose(item);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Close();
        }
    }
}
