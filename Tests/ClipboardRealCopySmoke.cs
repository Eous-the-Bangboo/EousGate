using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Interop;
using EousGate;
using EousGate.Infrastructure;

internal static class ClipboardRealCopySmoke
{
    // Explicit opt-in: requires a plain-text clipboard and restores its text.
    // Substitutes only browser launch, never the clipboard reader or notifications.
    internal static int Run()
    {
        var result = 1;
        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var backup = new DataObject();
            var original = Clipboard.GetDataObject();
            if (original is not null)
                foreach (var format in original.GetFormats(false))
                {
                    var data = original.GetData(format, false);
                    if (data is not null) backup.SetData(format, data, false);
                }
            var textFormats = new[] { DataFormats.Text, DataFormats.UnicodeText, DataFormats.OemText, DataFormats.StringFormat, DataFormats.Locale };
            if (backup.GetFormats(false).Any(format => !textFormats.Contains(format)))
            {
                Console.Error.WriteLine("Skipped without changing clipboard: real-copy test requires a plain-text clipboard so restoration can be guaranteed.");
                app.Shutdown();
                return;
            }
            var originalText = original?.GetData(DataFormats.UnicodeText, true) as string
                ?? original?.GetData(DataFormats.Text, true) as string;
            if (backup.GetFormats(false).Length > 0 && originalText is null)
            {
                Console.Error.WriteLine("Skipped without changing clipboard: original text could not be preserved.");
                app.Shutdown();
                return;
            }
            const string sample = "重复复制验证 https://example.com/eous-copy-test https://example.org/eous-copy-test https://example.com/eous-copy-test";
            var settings = new UserSettings { ClipboardAutoOpenEnabled = true, ClipboardAutoOpenCopyCount = 3 };
            var copies = 0;
            var opens = 0;
            var prompts = 0;
            var sequences = new List<uint>();
            Exception? failure = null;
            var frame = new DispatcherFrame();
            var actions = new ClipboardLinkActions(() => settings, new WebLinkOpener(_ => opens++), (_, _) => prompts++, () => { });
            using var monitor = new ClipboardMonitor(text =>
            {
                if (text != sample) return;
                copies++;
                sequences.Add(GetClipboardSequenceNumber());
                actions.HandleCopy(text);
            });
            var editor = new TextBox { Text = sample };
            var window = new Window { Title = "EousAssistant · 真实复制测试", Content = editor, Width = 650, Height = 180, ShowActivated = false };
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            var step = 0;
            try
            {
                window.Show();
                editor.SelectAll();
                if (!monitor.SetEnabled(true)) throw new InvalidOperationException("Could not register clipboard listener");
                timer.Tick += (_, _) =>
                {
                    try
                    {
                        if (step++ < 4) editor.Copy();
                        else
                        {
                            timer.Stop();
                            if (copies != 4 || opens != 2 || prompts != 2)
                                throw new InvalidOperationException($"Expected 4 real copies, 2 unique URL launches, 2 prompts; got {copies}, {opens}, {prompts}. Sequences: {string.Join(',', sequences)}");
                            Console.WriteLine($"PASS real WPF TextBox.Copy → Windows clipboard → production ClipboardMonitor reader → repeated-copy actions: copies={copies}, launches={opens}, prompts={prompts}, distinct sequences={sequences.Distinct().Count()}.");
                            result = 0;
                            frame.Continue = false;
                        }
                    }
                    catch (Exception exception) { failure = exception; frame.Continue = false; }
                };
                timer.Start();
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                timer.Stop();
                monitor.SetEnabled(false);
                Exception? restoreError = null;
                // Do not replace anything the user copied during the test.
                var stillOwnsContent = sequences.Count > 0 && GetClipboardSequenceNumber() == sequences[^1];
                for (var attempt = 0; stillOwnsContent && attempt < 20; attempt++)
                {
                    try
                    {
                        stillOwnsContent = RestoreTextClipboard(new WindowInteropHelper(window).Handle, originalText, sequences[^1]);
                        restoreError = null;
                        break;
                    }
                    catch (COMException exception) { restoreError = exception; Thread.Sleep(100); }
                }
                if (restoreError is not null) { result = 1; failure = restoreError; }
                else Console.WriteLine(stillOwnsContent ? "Original plain-text clipboard content restored before shutdown." : "Clipboard changed independently; preserved the newer content.");
                window.Close();
                app.Shutdown();
            }
            if (failure is not null) Console.Error.WriteLine(failure.Message);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    private static bool RestoreTextClipboard(IntPtr owner, string? text, uint expectedSequence)
    {
        if (!OpenClipboard(owner)) throw new COMException("Clipboard is busy during restoration");
        IntPtr memory = IntPtr.Zero;
        try
        {
            if (GetClipboardSequenceNumber() != expectedSequence) return false;
            if (text is not null)
            {
                memory = GlobalAlloc(0x42, (UIntPtr)((text.Length + 1) * 2));
                if (memory == IntPtr.Zero) throw new OutOfMemoryException();
                var pointer = GlobalLock(memory);
                if (pointer == IntPtr.Zero) throw new COMException("Could not lock restore memory");
                try { Marshal.Copy((text + '\0').ToCharArray(), 0, pointer, text.Length + 1); }
                finally { GlobalUnlock(memory); }
            }
            if (!EmptyClipboard()) throw new COMException("Could not restore clipboard");
            if (memory != IntPtr.Zero)
            {
                if (SetClipboardData(13, memory) == IntPtr.Zero) throw new COMException("Could not restore text");
                memory = IntPtr.Zero; // Windows owns the restored data.
            }
            return true;
        }
        finally { if (memory != IntPtr.Zero) GlobalFree(memory); CloseClipboard(); }
    }
    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr memory);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr memory);
}
