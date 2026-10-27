#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace EousGate.FolderHop
{
    internal static class Theme
    {
        internal static Color Background = Color.FromArgb(248,250,252), Ink = Color.FromArgb(15,23,42), Muted = Color.FromArgb(71,85,105), Blue = Color.FromArgb(37,99,235), Line = Color.FromArgb(218,226,237);
        internal static Color Surface=Color.White, Selection=Color.FromArgb(232,240,255), AccentText=Color.White;
        internal static void SyncFromHost()
        {
            Color Read(string key, Color fallback)
            {
                if(System.Windows.Application.Current?.TryFindResource(key) is System.Windows.Media.SolidColorBrush brush)
                {
                    var c=brush.Color; var baseColor=System.Drawing.SystemColors.Window;
                    return Color.FromArgb(255, (c.R*c.A+baseColor.R*(255-c.A))/255,
                        (c.G*c.A+baseColor.G*(255-c.A))/255, (c.B*c.A+baseColor.B*(255-c.A))/255);
                }
                return fallback;
            }
            Background=Read("PanelBackground",Background); Ink=Read("PanelForeground",Ink);
            Muted=Read("PanelMutedForeground",Muted); Blue=Read("PanelAccent",Blue); Line=Read("PanelBorder",Line);
            Surface=Read("PanelElevatedSurface",Surface); Selection=Read("PanelSelection",Selection);
            AccentText=Read("PanelOnAccent", System.Drawing.SystemColors.HighlightText);
        }
        internal static Icon MakeIcon()
        {
            using var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/EousAssistant;component/Assets/EousGate.ico")).Stream;
            using var icon = new Icon(stream);
            return (Icon)icon.Clone();
        }
        internal static Button Button(string text, bool primary)
        {
            var button = new RoundedButton { Text=text, FlatStyle=FlatStyle.Flat, AutoSize=false, Height=36, Width=116, Cursor=Cursors.Hand, Margin=new Padding(0,0,8,0), BackColor=primary ? Blue : Theme.Surface, ForeColor=primary ? AccentText : Ink };
            button.FlatAppearance.BorderColor = primary ? Blue : Line;
            button.FlatAppearance.MouseOverBackColor = primary ? Theme.Blue : Theme.Selection;
            return button;
        }
    }

    internal sealed class PickerForm : Form
    {
        private readonly bool _showWithoutActivation;
        protected override bool ShowWithoutActivation => _showWithoutActivation;
        protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle=(p.ExStyle|0x80)&~0x40000; return p; } }
        readonly HistoryStore store;
        internal readonly TextBox Search = new TextBox();
        internal readonly ListBox Rows = new ListBox();
        readonly Label context = new Label(), detail = new Label(), counter = new Label(), empty = new Label();
        readonly Button go, favorite;
        readonly Button[] tabs = new Button[3];
        int tab;
        internal IntPtr Target;
        internal List<string> Current = new List<string>();
        internal event Action<FolderEntry> Choose;
        internal event Action Changed;
        internal PickerForm(HistoryStore history, bool showWithoutActivation = false)
        {
            _showWithoutActivation = showWithoutActivation;
            SuspendLayout();
            Theme.SyncFromHost(); store=history; Text="EousFolderHop · 文件夹快跳"; Icon=Theme.MakeIcon();
            Font=new Font("Microsoft YaHei UI",9.5f); BackColor=Theme.Background; ForeColor=Theme.Ink;
            ClientSize=new Size(650,550); MinimumSize=new Size(560,450); StartPosition=FormStartPosition.CenterScreen;
            ShowInTaskbar=false; KeyPreview=true; AutoScaleDimensions=new SizeF(96,96); AutoScaleMode=AutoScaleMode.Dpi;
            var layout = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=7, Padding=new Padding(20) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,55)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
            Controls.Add(layout);
            var title = new Label { Text="文件夹快跳", Font=new Font(Font.FontFamily,18,FontStyle.Bold), Dock=DockStyle.Fill, AutoSize=false, Margin=Padding.Empty };
            layout.Controls.Add(title,0,0);
            context.Dock=DockStyle.Fill; context.ForeColor=Theme.Muted; context.AutoEllipsis=true; context.TextAlign=ContentAlignment.TopLeft; context.Margin=Padding.Empty;
            layout.Controls.Add(context,0,1);
            var searchPanel=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=2, Margin=Padding.Empty };
            searchPanel.RowStyles.Add(new RowStyle(SizeType.Absolute,21)); searchPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            searchPanel.Controls.Add(new Label { Text="搜索文件夹名称或路径", Dock=DockStyle.Fill, Font=new Font(Font.FontFamily,9.5f), ForeColor=Theme.Muted, Margin=Padding.Empty },0,0);
            Search.Dock=DockStyle.Fill; Search.AccessibleName="搜索文件夹名称或路径"; Search.Margin=new Padding(0,0,0,4);
            searchPanel.Controls.Add(new RoundedInput(Search) { Dock=DockStyle.Fill, Margin=new Padding(0,0,0,4) },0,1); layout.Controls.Add(searchPanel,0,2);
            var tabBar=new FlowLayoutPanel { Dock=DockStyle.Fill, Margin=Padding.Empty, Padding=new Padding(0,4,0,4), WrapContents=false };
            string[] names={"最近访问","已收藏","当前打开"};
            for(int i=0;i<3;i++) { int chosen=i; tabs[i]=Theme.Button(names[i],false); tabs[i].Width=100; tabs[i].Height=31; tabs[i].Click+=delegate { tab=chosen; RefreshRows(); }; tabBar.Controls.Add(tabs[i]); }
            counter.AutoSize=true; counter.ForeColor=Theme.Muted; counter.Margin=new Padding(6,7,0,0); tabBar.Controls.Add(counter); layout.Controls.Add(tabBar,0,3);
            var listHost=new Panel { Dock=DockStyle.Fill, BackColor=Theme.Surface, Margin=Padding.Empty, Padding=new Padding(8), BorderStyle=BorderStyle.None };
            Rows.Dock=DockStyle.Fill; Rows.BorderStyle=BorderStyle.None; Rows.DrawMode=DrawMode.OwnerDrawFixed; Rows.ItemHeight=62; Rows.IntegralHeight=false; Rows.BackColor=Theme.Surface;
            Rows.AccessibleName="文件夹列表"; Rows.DrawItem+=DrawRow; Rows.SelectedIndexChanged+=delegate { UpdateSelection(); };
            Rows.DoubleClick+=delegate { SelectPath(); };
            Rows.KeyDown+=delegate(object s,KeyEventArgs e) { if(e.KeyCode==Keys.Enter) { e.Handled=true; SelectPath(); } };
            var menu=new ContextMenuStrip(); menu.Items.Add("收藏／取消收藏",null,delegate { ToggleFavorite(); });
            menu.Items.Add("复制完整路径",null,delegate { var row=Selected; if(row!=null) { try { Clipboard.SetText(row.Path); } catch { } } });
            menu.Items.Add("从记录中移除",null,delegate { var row=Selected; if(row!=null) { store.Remove(row); RefreshRows(); OnChanged(); } }); Rows.ContextMenuStrip=menu;
            Rows.MouseDown+=delegate(object s,MouseEventArgs e) { if(e.Button==MouseButtons.Right) { int n=Rows.IndexFromPoint(e.Location); if(n>=0) Rows.SelectedIndex=n; } };
            listHost.Controls.Add(Rows); empty.Dock=DockStyle.Fill; empty.TextAlign=ContentAlignment.MiddleCenter; empty.ForeColor=Theme.Muted; empty.Padding=new Padding(20); empty.Visible=false; listHost.Controls.Add(empty);
            layout.Controls.Add(listHost,0,4);
            detail.Dock=DockStyle.Fill; detail.AutoEllipsis=true; detail.TextAlign=ContentAlignment.MiddleLeft; detail.ForeColor=Theme.Muted; detail.Margin=Padding.Empty;
            layout.Controls.Add(detail,0,5);
            var actions=new FlowLayoutPanel { Dock=DockStyle.Fill, Margin=Padding.Empty, WrapContents=false };
            go=Theme.Button("打开文件夹",true); go.Width=190; go.Click+=delegate { SelectPath(); };
            favorite=Theme.Button("收藏",false); favorite.Width=112; favorite.Click+=delegate { ToggleFavorite(); };
            var add=Theme.Button("添加收藏…",false); add.Width=118; add.Click+=delegate
            {
                using(var dialog=new FolderBrowserDialog { Description="选择要收藏的文件夹", ShowNewFolderButton=false })
                    if(dialog.ShowDialog(this)==DialogResult.OK) { var row=store.Touch(dialog.SelectedPath); row.Favorite=true; RefreshRows(); OnChanged(); }
            };
            actions.Controls.Add(go); actions.Controls.Add(favorite); actions.Controls.Add(add); layout.Controls.Add(actions,0,6);
            Search.TextChanged+=delegate { RefreshRows(); };
            Search.KeyDown+=delegate(object s,KeyEventArgs e)
            {
                if(e.KeyCode==Keys.Down && Rows.Items.Count>0) { Rows.Focus(); Rows.SelectedIndex=Math.Max(0,Rows.SelectedIndex); e.Handled=true; }
                if(e.KeyCode==Keys.Enter) { e.Handled=true; e.SuppressKeyPress=true; SelectPath(); }
            };
            KeyDown+=delegate(object s,KeyEventArgs e)
            {
                if(e.KeyCode==Keys.Escape) { Hide(); e.Handled=true; }
                if(e.Control && e.KeyCode==Keys.F) { Search.Focus(); Search.SelectAll(); e.Handled=true; }
                if(e.Control && e.KeyCode==Keys.D) { ToggleFavorite(); e.Handled=true; }
            };
            FormClosing+=delegate(object s,FormClosingEventArgs e) { if(e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; Hide(); } };
            RefreshRows();
            ResumeLayout(false);
        }
        internal void RefreshTheme()
        {
            Theme.SyncFromHost();
            void Paint(Control c)
            {
                c.BackColor=c is Form?Theme.Background:Theme.Surface; c.ForeColor=Theme.Ink;
                foreach(Control child in c.Controls)Paint(child);
            }
            Paint(this); RefreshRows(); Invalidate(true);
        }
        void OnChanged() { if(Changed!=null) Changed(); }
        internal FolderEntry Selected { get { return Rows.SelectedItem as FolderEntry; } }
        internal void ShowFor(IntPtr target,List<string> current)
        {
            Target=Native.IsFileDialog(target)?target:IntPtr.Zero; Current=current;
            context.Text=Target==IntPtr.Zero?"Ctrl+Alt+J 随时唤起 · 关闭窗口后仍保留访问历史":"正在为「"+Native.Title(Target)+"」选择目录";
            go.Text=Target==IntPtr.Zero?"在资源管理器中打开":"跳转到此文件夹";
            Search.Clear(); RefreshRows();
            if(Target!=IntPtr.Zero)
            {
                Native.Rect r; Native.GetWindowRect(Target,out r); var area=Screen.FromHandle(Target).WorkingArea;
                Location=new Point(Math.Max(area.Left,Math.Min(r.Left+(r.Right-r.Left-Width)/2,area.Right-Width)),Math.Max(area.Top,Math.Min(r.Top+64,area.Bottom-Height)));
            }
            if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;
            Show(); Activate(); Search.Focus();
        }
        internal void RefreshRows()
        {
            string selected=Selected==null?null:Selected.Path;
            Rows.BeginUpdate(); Rows.Items.Clear();
            foreach(var row in store.Query(tab,Search.Text,Current)) Rows.Items.Add(row);
            if(Rows.Items.Count>0) { int n=0; if(selected!=null) { for(int i=0;i<Rows.Items.Count;i++) if(((FolderEntry)Rows.Items[i]).Path==selected) n=i; } Rows.SelectedIndex=n; }
            Rows.EndUpdate();
            counter.Text=Rows.Items.Count+" 个目录";
            for(int i=0;i<3;i++) { tabs[i].BackColor=i==tab?Theme.Selection:Theme.Surface; tabs[i].ForeColor=Theme.Ink; }
            empty.Visible=Rows.Items.Count==0; if(empty.Visible) empty.BringToFront();
            empty.Text=Search.Text.Length>0?"没有找到匹配的目录\r\n试试文件夹名称或路径中的几个字。":tab==1?"还没有收藏\r\n选择目录后点击“收藏”，或使用“添加收藏”。":tab==2?"当前没有检测到资源管理器目录。":"从现在开始记录\r\n请用系统资源管理器浏览文件夹，再回来查看。";
            UpdateSelection();
        }
        void UpdateSelection()
        {
            var row=Selected; detail.Text=row==null?"Enter 打开／跳转 · Ctrl+D 收藏 · Esc 收起":row.Path;
            go.Enabled=row!=null; favorite.Enabled=row!=null; favorite.Text=row!=null&&row.Favorite?"取消收藏":"收藏";
        }
        void ToggleFavorite() { var row=Selected; if(row==null)return; store.ToggleFavorite(row); RefreshRows(); OnChanged(); }
        void SelectPath() { var row=Selected; if(row!=null && Choose!=null) Choose(row); }
        internal void Status(string text) { detail.Text=text; }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e); Rows.ItemHeight=LogicalToDeviceUnits(62);
        }
        void DrawRow(object sender,DrawItemEventArgs e)
        {
            if(e.Index<0)return; var row=(FolderEntry)Rows.Items[e.Index]; bool selected=(e.State&DrawItemState.Selected)!=0;
            using(var background=new SolidBrush(Theme.Surface)) e.Graphics.FillRectangle(background,e.Bounds); e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var shape=RoundedGeometry.Path(new Rectangle(e.Bounds.X+2,e.Bounds.Y+1,e.Bounds.Width-5,e.Bounds.Height-2),Rows.DeviceDpi/96f))
            using(var b=new SolidBrush(selected?Theme.Selection:Theme.Surface)) e.Graphics.FillPath(b,shape);
            int Px(int v)=>(int)Math.Round(v*Rows.DeviceDpi/96d);
            int y=e.Bounds.Top; using(var b=new SolidBrush(Color.FromArgb(231,170,50))) { e.Graphics.FillRectangle(b,Px(12),y+Px(18),Px(12),Px(5)); e.Graphics.FillRectangle(b,Px(12),y+Px(22),Px(25),Px(17)); }
            var titleRect=new Rectangle(Px(49),y+Px(9),e.Bounds.Width-Px(110),Px(23)); var pathRect=new Rectangle(Px(49),y+Px(33),e.Bounds.Width-Px(65),Px(20));
            TextRenderer.DrawText(e.Graphics,row.Name,Font,titleRect,Theme.Ink,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine);
            using(var small=new Font(Font.FontFamily,9.5f)) TextRenderer.DrawText(e.Graphics,row.Path,small,pathRect,Theme.Muted,TextFormatFlags.PathEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine);
            if(row.Favorite) TextRenderer.DrawText(e.Graphics,"收藏",Font,new Rectangle(e.Bounds.Right-Px(52),y+Px(10),Px(45),Px(24)),Theme.Blue,TextFormatFlags.SingleLine);
            if((e.State&DrawItemState.Focus)!=0) ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(e.Bounds.Left+2,e.Bounds.Top+2,e.Bounds.Width-4,e.Bounds.Height-4),Theme.Blue,selected?Theme.Selection:Theme.Surface);
        }
    }

    internal sealed class DialogButton : Form
    {
        internal IntPtr Target;
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=0x08000000|0x80; return p; } }
        protected override void WndProc(ref Message message)
        {
            if(message.Msg==0x21) { message.Result=new IntPtr(3); return; } // MA_NOACTIVATE: clicks still reach the button.
            base.WndProc(ref message);
        }
        internal DialogButton(Action<IntPtr> open)
        {
            AutoScaleMode=AutoScaleMode.Dpi; FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true; ClientSize=new Size(142,36); BackColor=Theme.Blue;
            Font=new Font("Microsoft YaHei UI",9f); var button=Theme.Button("最近文件夹  ▾",true); button.Dock=DockStyle.Fill; button.AccessibleName="选择最近访问的文件夹";
            button.Click+=delegate { open(Target); }; Controls.Add(button);
        }
        internal void Follow(IntPtr dialog)
        {
            Target=dialog; Native.Rect r; if(!Native.GetWindowRect(dialog,out r)) { Hide(); return; }
            var area=Screen.FromHandle(dialog).WorkingArea;
            int x=r.Right-Width-24, y=r.Bottom-Height-140;
            foreach(var child in Native.Children(dialog))
            {
                if(Native.Class(child)!="SHELLDLL_DefView" || !Native.IsWindowVisible(child))continue;
                Native.Rect view;
                if(Native.GetWindowRect(child,out view) && view.Right-view.Left>Width+32 && view.Bottom-view.Top>Height+24)
                { x=view.Right-Width-12; y=view.Bottom-Height-12; break; }
            }
            Location=new Point(Math.Max(area.Left,Math.Min(x,area.Right-Width)),Math.Max(area.Top,Math.Min(y,area.Bottom-Height)));
            if(!Visible)Show();
        }
    }

    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        bool disposed;
        internal Action Pressed; internal bool Registered;
        internal HotkeyWindow() { CreateHandle(new CreateParams { Caption="FolderHop.Message" }); Registered=Native.RegisterHotKey(Handle,1,0x4003,0x4A); }
        protected override void WndProc(ref Message m) { if(m.Msg==0x0312 && Pressed!=null)Pressed(); base.WndProc(ref m); }
        public void Dispose() { if(disposed)return; disposed=true; Native.UnregisterHotKey(Handle,1); DestroyHandle(); }
    }

}
