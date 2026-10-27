using System.Drawing;
using System.Diagnostics;
using Forms = System.Windows.Forms;

namespace EousGate.Infrastructure;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Icon _appIcon;
    private readonly Forms.ContextMenuStrip _menu;
    public TrayService(Action showPanel, Action exit)
    {
        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add(new Forms.ToolStripMenuItem("打开 Eous 助手", null, (_, _) => showPanel()));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(new Forms.ToolStripMenuItem("退出助手", null, (_, _) => exit()));
        _appIcon = LoadApplicationIcon();
        _icon = new Forms.NotifyIcon { Icon = _appIcon, Text = "EousAssistant · Eous 助手", Visible = true, ContextMenuStrip = _menu };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) showPanel(); };
    }
    public void Dispose() { _icon.Visible = false; _icon.Dispose(); _menu.Dispose(); _appIcon.Dispose(); }

    private static Icon LoadApplicationIcon()
    {
        var executable = Process.GetCurrentProcess().MainModule?.FileName ?? Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executable))
        {
            var icon = Icon.ExtractAssociatedIcon(executable);
            if (icon is not null) return icon;
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
