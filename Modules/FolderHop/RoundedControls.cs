using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Button = System.Windows.Forms.Button;
using Panel = System.Windows.Forms.Panel;
using TextBox = System.Windows.Forms.TextBox;
using Point = System.Drawing.Point;

namespace EousGate.FolderHop;

internal static class RoundedGeometry
{
    internal static GraphicsPath Path(Rectangle bounds, float scale = 1)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min((int)Math.Round(24 * scale), Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure(); return path;
    }
}

internal sealed class RoundedButton : Button
{
    private bool _hover;
    internal RoundedButton() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = RoundedGeometry.Path(new Rectangle(0, 0, Width - 1, Height - 1), DeviceDpi / 96f);
        var fill = _hover && Enabled ? FlatAppearance.MouseOverBackColor : BackColor;
        using var background = new SolidBrush(fill);
        using var border = new Pen(Focused && ShowFocusCues ? Theme.Blue : FlatAppearance.BorderColor, Focused && ShowFocusCues ? 2 : 1);
        e.Graphics.FillPath(background, shape); e.Graphics.DrawPath(border, shape);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class RoundedInput : Panel
{
    private readonly TextBox _input;
    internal RoundedInput(TextBox input)
    {
        _input = input; DoubleBuffered = true; Padding = new Padding(10, 6, 10, 6);
        input.BackColor = Theme.Surface; input.ForeColor = Theme.Ink; input.Dock = DockStyle.None; input.BorderStyle = BorderStyle.None; Controls.Add(input);
        input.GotFocus += (_, _) => Invalidate(); input.LostFocus += (_, _) => Invalidate();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e); var inset = LogicalToDeviceUnits(10);
        _input.Location = new Point(inset, Math.Max(2, (Height - _input.PreferredHeight) / 2));
        _input.Width = Math.Max(1, Width - inset * 2);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Background); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = RoundedGeometry.Path(new Rectangle(0, 0, Width - 1, Height - 1), DeviceDpi / 96f);
        using var fill = new SolidBrush(Theme.Surface); using var pen = new Pen(_input.Focused ? Theme.Blue : Theme.Line);
        e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(pen, shape);
    }
}
