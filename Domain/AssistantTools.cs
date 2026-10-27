using System.ComponentModel;

namespace EousGate;

public enum ToolKind { BuiltIn, External }
[Flags]
public enum ToolCapabilities { None = 0, Start = 1, Stop = 2, Settings = 4 }
public enum ToolState { Stopped, Starting, Running, Stopping, Failed }
public sealed record ToolDescriptor(string Id, string EnglishName, string ChineseName,
    string Description, string Glyph, ToolKind Kind = ToolKind.BuiltIn,
    ToolCapabilities Capabilities = ToolCapabilities.Start | ToolCapabilities.Stop | ToolCapabilities.Settings,
    string? IconPath = null);

// External process adapters can implement the same contract without changing the control panel.
public interface IAssistantTool : INotifyPropertyChanged
{
    ToolDescriptor Descriptor { get; }
    ToolState State { get; }
    string? Error { get; }
    bool Selected { get; set; }
    string StateText { get; }
    bool CanStart { get; }
    bool CanStop { get; }
    bool CanOpenSettings { get; }
    Task StartAsync();
    Task StopAsync();
    void OpenSettings();
}
