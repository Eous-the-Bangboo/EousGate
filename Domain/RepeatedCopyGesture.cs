using System.Security.Cryptography;
using System.Text;

namespace EousGate;

/// <summary>Recognizes one deliberate copy gesture without keeping clipboard content.</summary>
public sealed class RepeatedCopyGesture
{
    public static readonly TimeSpan MaxInterval = TimeSpan.FromSeconds(3);
    public static int NormalizeCount(int count) => Math.Clamp(count, 2, 10);
    private byte[]? _fingerprint;
    private TimeSpan _lastCopy;
    private int _count;
    private int _required;
    public bool HasTriggered { get; private set; }

    public bool Observe(string text, int requiredCopies, TimeSpan now)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > ClipboardLinks.MaxTextLength)
        {
            Reset();
            return false;
        }
        var required = NormalizeCount(requiredCopies);
        var fingerprint = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        if (_fingerprint is null || !_fingerprint.AsSpan().SequenceEqual(fingerprint)
            || required != _required || now < _lastCopy || now - _lastCopy > MaxInterval)
        {
            _count = 1;
            HasTriggered = false;
        }
        else if (_count < required) _count++;
        _fingerprint = fingerprint;
        _lastCopy = now;
        _required = required;
        if (HasTriggered || _count < required) return false;
        HasTriggered = true;
        return true;
    }

    public void Reset()
    {
        _fingerprint = null;
        _count = _required = 0;
        _lastCopy = default;
        HasTriggered = false;
    }
}
