using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

// Isolated executable for process-control tests. Never reads clipboard, records audio or shows UI.
var key = "Local\\EousAssistant.ExternalFixture." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AppContext.BaseDirectory)))[..20];
if (args.Contains("--exit"))
{
    using var signal = EventWaitHandle.OpenExisting(key); signal.Set(); return;
}
using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, key);
if (args.FirstOrDefault(a => a.StartsWith("--handoff=", StringComparison.Ordinal)) is { } handoff)
{
    using var child = Process.Start(new ProcessStartInfo(handoff[10..]) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { "--window" } });
    return;
}
if (args.Contains("--window"))
{
    CloseWindowFixture.Run(stop); return;
}
if (args.Contains("--spawn-helper"))
{
    using var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { "--wait" } });
}
stop.WaitOne(TimeSpan.FromSeconds(90)); // Safety timeout if the test host is interrupted.

