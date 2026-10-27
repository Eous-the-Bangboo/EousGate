using System.Globalization;
using System.Windows.Data;

namespace EousGate;

// Keep lifecycle and process policy in settings; the overview explains each tool's job.
public sealed class ToolDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is ToolDescriptor tool ? tool.Id switch
    {
        "file-hop" => "拖到屏幕边缘，选择软件打开",
        "web-hop" => "复制网页链接，点击提示打开",
        "folder-hop" => "在打开和保存对话框快速跳转",
        "external-ditto" => "剪贴板管理",
        "external-everything" => "文件搜索",
        "external-sayall" => "语音输入",
        _ => tool.Description
    } : "";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
