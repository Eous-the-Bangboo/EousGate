using EousGate;
using EousGate.Infrastructure;

internal static class FileDropPermissionSmoke
{
    internal static int Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new EousGate.App(false);
            app.InitializeComponent();
            try
            {
                var elevated = FileDropPermissionGuard.IsProcessElevated();
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                    Check(elevated == new System.Security.Principal.WindowsPrincipal(identity)
                        .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator),
                        "production elevation probe must match the actual current Windows token");
                Console.WriteLine($"PASS real current-process elevation probe: elevated={elevated}.");
                FileDropPermissionGuard.EnsureSupported(false);
                // Inject elevation; never request UAC, touch a user's running tools or take the legacy marker.
                app.InitializeAssistant(Path.Combine(Path.GetTempPath(), "EousAssistant-permission-" + Guid.NewGuid()),
                    tray: false, showPanel: false, includeExternalTools: false, fileDropElevationProbe: () => true);
                var file = app.Tools.Tools.Single(t => t.Descriptor.Id == "file-hop");
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    file.StartAsync().GetAwaiter().GetResult();
                    Check(file.State == ToolState.Failed && file.Error!.Contains("管理员")
                        && file.Error.Contains("普通双击"), "elevated file-hop must expose an actionable failure");
                    Check(app.Windows.Count == 0, "permission failure must not create any edge or overlay windows");
                    Check(app.Tools.Tools.Where(t => t != file).All(t => t.State == ToolState.Stopped && t.CanStart),
                        "permission failure must leave other tools independently available");
                    file.StopAsync().GetAwaiter().GetResult();
                    Check(file.State == ToolState.Stopped, "failed start must stop cleanly");
                }
                Console.WriteLine("PASS file-drop permission: standard mode allowed; elevated host start fails clearly, cleans up, retries and preserves other tool states.");
            }
            catch (Exception ex) { failure = ex; }
            finally { app.Shutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(15))) { Console.Error.WriteLine("Permission check timed out"); return 1; }
        if (failure is null) return 0;
        Console.Error.WriteLine(failure);
        return 1;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
