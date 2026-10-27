namespace EousGate.Infrastructure;

internal sealed class LegacyInstanceGuard : IDisposable
{
    private readonly Mutex _marker;
    internal LegacyInstanceGuard(string name, string displayName)
    {
        _marker = new Mutex(false, name, out var created);
        if (!created) { _marker.Dispose(); throw new InvalidOperationException($"请先从托盘退出旧版 {displayName}，再启动此模块。"); }
    }
    public void Dispose() => _marker.Dispose();
}
