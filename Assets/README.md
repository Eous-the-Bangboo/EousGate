# Eous 角色图

用户提供的来源目录：`C:\Users\Eous\Documents\Codex\2026-10-16\cha\outputs`。

- `EousPortrait-sage.png`：来自 `eous-icon-03-sage.png`，等比缩小为 256 × 256，供浅色主题使用。
- `EousPortrait-charcoal.png`：来自 `eous-icon-01-charcoal.png`，等比缩小为 256 × 256，供深色主题和 B 皮肤使用。
- `EousGate.ico`：同一绿底原图生成，包含 16、20、24、32、40、48、64、128、256 像素帧，供 EXE、窗口和托盘使用。

转换使用 Pillow 12.3.0、LANCZOS 重采样，保留原始构图、比例与底色。PNG 和 ICO 均内嵌到程序集，运行不依赖来源目录。`EousGate.svg` 为历史矢量标识，当前应用图标不再引用它。

`EousCharacter.png`用于beta.15侧栏完整伙伴，来源为用户原始`C:/Users/Eous/Documents/Codex/2026-10-16/cha/work/eous-reference.png`。源文件扩展名虽为PNG，实际编码为带透明通道的WebP；为避免WPF解码黑底，仅无损转码为真正RGBA PNG，核验解码后的RGBA像素逐字节一致。未重绘或使用软胶风生成图，资源内嵌。
