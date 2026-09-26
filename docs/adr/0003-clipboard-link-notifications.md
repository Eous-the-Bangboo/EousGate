# 使用 Windows 剪贴板通知显示网页链接提示

状态：已接受，2026-09-27。

## 决定

- Infrastructure 用隐藏的消息窗口注册 `AddClipboardFormatListener`，接收 `WM_CLIPBOARDUPDATE`。每次通知触发读取，不按文本去重，因此重新复制同样内容会再次提示。
- 使用原生 Unicode 文本读取；剪贴板占用时每 80 ms 重试，最多 6 次。新通知替换未完成读取；不轮询、不保存历史。
- Domain 负责提取 HTTP/HTTPS/www 链接、校验协议和计算可暂停的倒计时。文本限制为 262144 字符，每次最多显示 20 个不同链接，避免超大输入阻塞界面。
- Presentation 使用独立可复用的非激活窗口；新通知重置 2–60 秒倒计时，默认 5 秒，鼠标悬停或键盘焦点在内部时暂停。关闭按钮、Esc、窗口关闭命令均收起窗口；应用退出才释放。
- 只有用户点击时才把 URL 作为目标传给 Shell/default browser；不拼接命令、不探测网址可达性、不记录网址。多个链接逐项可选。
- 开关、时长、左右侧写入既有用户设置。旧配置默认启用；暂停和退出会停止监听。

## 验证边界

自动化覆盖提取、倒计时、设置兼容和浏览器分派。显式 `--clipboard-smoke` 使用实际 Windows 通知消息和模拟文本读取器，验证占用重试、重复更新、窗口计时、点击处理、设置应用，并渲染浅色、深色与设置页。

当前工具环境的剪贴板写入/格式备份不可靠，因此完整真实复制到默认浏览器的链路仍需桌面手工复测；测试不会再修改真实剪贴板。

参考：[AddClipboardFormatListener](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-addclipboardformatlistener)、[WM_CLIPBOARDUPDATE](https://learn.microsoft.com/en-us/windows/win32/dataxchg/wm-clipboardupdate)。
