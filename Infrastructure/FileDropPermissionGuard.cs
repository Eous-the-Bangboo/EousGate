using System.Security.Principal;

namespace EousGate.Infrastructure;

internal static class FileDropPermissionGuard
{
    internal static bool IsProcessElevated()
    {
        // IsInRole duplicates a primary token before CheckTokenMembership; Query alone cannot do that.
        using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.Duplicate);
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    internal static void EnsureSupported(bool elevated)
    {
        if (elevated)
            throw new InvalidOperationException("助手正以管理员身份运行，Windows 会阻止普通资源管理器拖入文件。请从托盘退出助手，再普通双击 EousAssistant.exe 启动，勿选“以管理员身份运行”；如仍自动提权，请取消快捷方式或兼容性设置中的管理员运行选项。外部程序需要权限时可单独授权。");
    }
}
