using EousGate;
using EousGate.Infrastructure;
using System.Diagnostics;

internal static class LauncherProcessSmoke
{
    internal static int Run(string directory)
    {
        try { Verify(directory).GetAwaiter().GetResult(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static async Task Verify(string directory)
    {
        var root = Path.Combine(Path.GetTempPath(), "Eous-handoff-" + Guid.NewGuid());
        var version = Path.Combine(root, "1.0.0.1"); var other = Path.Combine(root, "games");
        foreach (var folder in new[] { root, version, other })
        {
            Directory.CreateDirectory(folder);
            foreach (var file in Directory.GetFiles(directory)) File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        }
        var launcher = Path.Combine(root, "launcher.exe"); var target = Path.Combine(version, "HYP.exe"); var excluded = Path.Combine(other, "HYP.exe");
        var fixtureName = "EousAssistant.ExternalFixture.exe";
        File.Copy(Path.Combine(root, fixtureName), launcher); File.Copy(Path.Combine(version, fixtureName), target); File.Copy(Path.Combine(other, fixtureName), excluded);
        var def = LauncherProcessTests.Definition(launcher) with { RawStartArguments = "--handoff=\"" + target + "\"" };
        var controller = new WindowsExternalProgramController((definition, path) => LauncherProcessTargets.Resolve(definition, path, _ => "miHoYo"));
        var tool = new ExternalProgramTool(def, controller, () => new() { ExecutablePath = launcher }, () => { });
        using var peer = Process.Start(new ProcessStartInfo(excluded) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { "--window" } })!;
        try
        {
            await tool.StartAsync();
            for (var i = 0; i < 30 && controller.Inspect(def, launcher).Processes.All(p => p.ExecutablePath != target); i++) await Task.Delay(100);
            await tool.RefreshAsync();
            var snapshot = controller.Inspect(def, launcher);
            Check(tool.State == ToolState.Running && snapshot.Processes.Any(p => p.ExecutablePath == target) && snapshot.Processes.All(p => p.ExecutablePath != excluded), "handoff target must stay running after bootstrap exits, excluding games");
            var ids = snapshot.Processes.Select(p => p.Id).Order().ToArray();
            await tool.StartAsync();
            Check(controller.Inspect(def, launcher).Processes.Select(p => p.Id).Order().SequenceEqual(ids), "handoff start must remain idempotent");
            var authorizationCalls = 0;
            var denied = new WindowsExternalProgramController(_ => 5, (_, request) => { authorizationCalls++; Check(request.Processes.Any(p => p.ExecutablePath == target), "authorization must retain pinned target identity"); return Task.CompletedTask; },
                (definition, path) => LauncherProcessTargets.Resolve(definition, path, _ => "miHoYo"));
            if (!FileDropPermissionGuard.IsProcessElevated())
            { await denied.StopAsync(def, snapshot); Check(authorizationCalls == 1, "only actual access denial requests narrow authorization"); }
            await tool.StopAsync();
            Check(tool.State == ToolState.Stopped && !peer.HasExited, "closing the handoff process must preserve excluded sibling/game processes");
            // The same narrow worker code can close a fixture at equal permissions, without requesting UAC.
            var workerDef = LauncherProcessTests.Definition(excluded);
            var peerSnapshot = new WindowsExternalProgramController().Inspect(workerDef, excluded);
            Check(peerSnapshot.Processes.Count == 1, "worker fixture must be observed");
            Check(ElevatedExternalStop.ExecuteWorker(ElevatedExternalStop.Encode(workerDef, peerSnapshot)) == 0, "stop worker must close only revalidated fixture identities");
            await peer.WaitForExitAsync();
            Console.WriteLine("PASS launcher handoff: bootstrap exits, target stays observed, no duplicate start, pinned close target, excluded game path, authorization dispatch and actual narrow worker at equal permissions; only fixtures controlled.");
        }
        finally
        {
            var cleanup = def with { ForceTerminateOnTimeout = true };
            await controller.StopAsync(cleanup, controller.Inspect(def, launcher));
            var peerDef = LauncherProcessTests.Definition(excluded) with { ForceTerminateOnTimeout = true };
            var basic = new WindowsExternalProgramController(); await basic.StopAsync(peerDef, basic.Inspect(peerDef, excluded));
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
