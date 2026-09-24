using System.Windows;
using EousGate.Infrastructure;

namespace EousGate;

public partial class CustomAppWindow : Window
{
    public CustomAppDefinition? Definition { get; private set; }

    public CustomAppWindow(string executablePath, string displayName, string? arguments = null, string? skinId = null)
    {
        InitializeComponent();
        MicaWindowHelper.Enable(this, skinId);
        ExecutablePath.Text = executablePath;
        DisplayName.Text = displayName;
        Arguments.Text = arguments ?? "";
    }

    private void AddClick(object sender, RoutedEventArgs e)
    {
        var displayName = DisplayName.Text.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            System.Windows.MessageBox.Show("请输入显示名称。", "添加打开方式", MessageBoxButton.OK, MessageBoxImage.Information);
            DisplayName.Focus();
            return;
        }

        Definition = new CustomAppDefinition
        {
            Id = ExecutablePath.Text,
            DisplayName = displayName,
            ExecutablePath = ExecutablePath.Text,
            Arguments = string.IsNullOrWhiteSpace(Arguments.Text) ? null : Arguments.Text.Trim()
        };
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
