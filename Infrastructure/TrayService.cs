using System.Drawing;
using System.Diagnostics;
using Forms = System.Windows.Forms;

namespace EousGate.Infrastructure;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _pause;
    private readonly Action _pauseAction;
    private readonly Action _resumeAction;
    private readonly Icon _appIcon;
    private bool _paused;

    public TrayService(Action pause, Action resume, Action settings, Action exit)
    {
        _pauseAction = pause; _resumeAction = resume;
        _pause = new Forms.ToolStripMenuItem();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_pause);
        menu.Items.Add(new Forms.ToolStripMenuItem("设置", null, (_, _) => settings()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("退出", null, (_, _) => exit()));
        _appIcon = LoadApplicationIcon();
        _icon = new Forms.NotifyIcon { Icon = _appIcon, Text = "EousGate", Visible = true, ContextMenuStrip = menu };
        SetPaused(false);
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        _pause.Text = paused ? "恢复提示与边缘唤出" : "暂停提示与边缘唤出";
        _pause.Click -= OnPauseClick;
        _pause.Click += OnPauseClick;
    }

    private void OnPauseClick(object? sender, EventArgs e)
    {
        if (_paused) _resumeAction(); else _pauseAction();
    }

    public void Dispose() { _icon.Visible = false; _icon.Dispose(); _appIcon.Dispose(); }

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
