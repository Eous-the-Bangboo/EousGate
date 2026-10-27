# A 原生 Fluent · Figma 精修

范围：仅设计 A。B 的实现与视觉保持现状，不写入 B 页面。三张可编辑稿：助手首页、面板外观设置、单列打开面板；附三者必需的控件状态。

Figma 文件：https://www.figma.com/design/TcXTuFPrFZUWRognzfx1Rf

## 发现与设计边界

- 已检查当前 WPF 代码与 Code Connect 文件；仓库没有 Code Connect 映射。
- 新建文件没有已有屏幕、组件、局部变量和文字样式。
- 已发现可用库，并搜索 Simple Design System 的 Button、Checkbox、Input、颜色变量和正文样式。
- 通用库按钮为 40px、Inter 字体、Web Variant/Size API；Windows 产品现有控件使用 Segoe UI Variable / Microsoft YaHei UI、34–36px 控件与 Panel 语义资源。为保留源控件语义与状态，必要组件使用本地 WPF 对应设计组件；不强套 Web 控件。
- Figma 云端仅提供 Noto Sans SC 的可用中文字体，稿件标记为预览替代字体；最终 WPF 仍使用产品字体，字体度量不能宣称完全一致。
- 使用当前 A 色板：PanelBackground #F4F6FA、PanelForeground #172033、PanelMutedForeground #526075、PanelBorder #D5DCE6、PanelAccent #2563EB、PanelAccentSoft #E8F0FF、PanelElevatedSurface #FFFFFF。
- 图标使用源程序 Segoe Fluent Icons 字形的矢量轮廓与仓库现有 Logo；不导入整页截图、不重画第三方软件商标。
- 设置页沿用最新范围：仅 A/B 皮肤、主题与动效；不恢复已移除的旧材质和布局选项。打开面板为临时单列面板。

验收：文字、图标、组件实例及自动布局可编辑；必要状态与语义变量绑定；三张稿全部检查结构、正常比例截图、重叠与越界。源代码实施另行依据验证后的设计参数推进，保持 B 不动。

## 最终设计

- [A 总览](https://www.figma.com/design/TcXTuFPrFZUWRognzfx1Rf?node-id=7-2)
- [助手首页](https://www.figma.com/design/TcXTuFPrFZUWRognzfx1Rf?node-id=7-7)：1040 × 750，六项工具、两组状态、四种批量操作同屏展示。
- [外观设置](https://www.figma.com/design/TcXTuFPrFZUWRognzfx1Rf?node-id=7-297)：1180 × 800，导航、参数、预览与固定操作区。
- [打开面板](https://www.figma.com/design/TcXTuFPrFZUWRognzfx1Rf?node-id=7-212)：404 × 405，临时单列四选项，避免普通窗口控制键。
- [本地总览](preview.png)。

最终读回：138 个 Frame、129 个 Text、110 个 Instance、54 个 Vector；无整页或局部截图填充，无零尺寸文字、无文字超出直接父容器。正常比例截图检查后，修复首页名称行高度、Logo 比例与焦点环，再检查修复后总览。

所有自由文字使用已标注的 Noto Sans SC 预览字体；这是与 Windows 原字体之间的已知差异，不宣称像素级字体一致。37 个变量、6 个文字样式与本地控件族仅覆盖 A 所需范围。B 没有新增页面、变量或组件；应用源码未在本轮修改。

## A 动效

已给原设计添加 39 条手动关键帧轨道，没有重建、拆散或栅格化静态设计；B 保持不动。

| 对象 | 动作 | 时长／节奏 |
| --- | --- | --- |
| 打开面板 | 渐显并从左侧平移 12 px 归位 | 180–220 ms |
| 四个软件选项 | 渐显并平移 4 px 归位 | 每项 140 ms，间隔 30 ms |
| 当前软件选项 | 白色向浅蓝色选中表面过渡 | 140 ms |
| 助手标题与工具行 | 渐显，向上归位 4–8 px | 140–160 ms，工具行间隔 30 ms |
| 设置分区和预览 | 渐显并向上归位 6 px | 200 ms，分区间隔 40 ms |

采用缓出曲线 `cubic-bezier(.2,0,0,1)`；保留原 2 秒时间线，0.46 秒后全部稳定，不增加循环或弹跳。静态截图仍显示原来的静止设计，动画在 Figma 时间线中查看。

Figma 不允许通过 API 向实例内部子图层写动画，因此软件选项轨道写在 A 打开面板主组件；已读回确认独立面板和设置预览均继承其轨道。记录见 `motion-state.json`。本机仅有不含 H.264 解码器的 Playwright 精简 FFmpeg，未执行视频导出或逐帧视觉验收；关键帧及实例继承已验证。

本轮仅为设计动效，尚未转为 WPF 动画。实施时应使用用户动效时长设置，并尊重 Windows 关闭动画／减少动态效果的偏好。

## WPF 实现
- 已实施到 EousAssistant beta.4：首页、外观设置、打开面板及一次入场／错峰／滑入滑出。
- 继续使用原生 Windows 标题栏、本地 Fluent 字体／软件真实图标；不使用 Figma 临时资源 URL。
- 参考设计时长按现有 80–600 ms 配置缩放；设计板导出的循环播放在产品中转换为一次入场。
- B 首页与设置 100% / 125% / 150% 及最小尺寸与实施前截图 SHA-256 一致。
- 具体实现、自动验证与边界见 ../../../releases/v0.2.0-beta.4.md 和当日日志。
