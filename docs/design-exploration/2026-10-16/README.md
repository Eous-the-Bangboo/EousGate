# EousGate / Eous 助手 UI 方案探索

本轮为视觉方向探索，交付概念渲染图，不修改应用实现。以当前 WPF 设置中心、打开面板和正在整合的 EousAssistant 入口为背景。

## 使用的技能

- `ui-ux-pro-max`：已读取并查询桌面设置设计系统与 WPF 可访问性建议。首次查询误匹配 AI 界面，已缩小为 desktop settings utility；仅采纳匹配的简约布局方向。网站 Hero/CTA、远程字体与本产品无关，未采用。
- `imagegen`：使用内置图像生成工具，每个方向单独生成一张。

## 案例与可借鉴部分

- [PowerToys 设置中心](https://learn.microsoft.com/en-us/windows/powertoys/general)：设置分组、导航、系统主题适配。
- [Raycast for Windows](https://www.raycast.com/windows)：紧凑的命令列表、图标对齐、选择反馈。
- [Files](https://files.community/)：桌面工具的内容分区、主题与材质表达。
- [Fluent 2 材质](https://fluent2.microsoft.design/material)：Mica 用于窗口背景，Acrylic 用于短暂出现的浮层。Mica 并非透明玻璃。

上述借鉴是设计分析，不代表复制对应产品界面或引入其功能。

## 三个方向

| 方案 | 视觉 | 结构 | 取舍 |
| --- | --- | --- | --- |
| A 原生 Fluent | 冷白、灰、克制蓝色 | 导航 + 设置与预览双列 | 最贴近 Windows，建议作为默认方向 |
| B 深色效率 | 炭黑、薄荷色选中态 | 紧凑设置行 + 预览检查区 | 扫描效率高，适合偏爱深色的用户 |
| C 暖白工作室 | 暖白、墨色、灰绿 | 大预览画布 + 右侧参数检查区 | 更直观，布局改造范围较大 |

三套沿用现有功能边界：本地文件打开、不显示文件名和路径、四个软件选项、明确选中反馈，设置底部保留恢复默认/帮助/取消/应用/保存顺序。概念图不代表交互已实现或已完成 DPI、键盘、高对比度测试。

## 生成记录

完整提示词见 `prompts.json`。输出使用内置 `image_gen`，不是 CLI/API 脚本。图中文字与图标为生成式概念表现，具体实现仍以实际 WPF 资源、产品术语和可访问性规范为准。

## 输出与检查

- [A 原生 Fluent](A-native-fluent.png)
- [B 深色效率](B-focus-dark.png)
- [C 暖白工作室](C-quiet-studio.png)

已逐张检查布局、主要中文、四个软件选项、选中状态和设置底部操作。C 初稿把打开面板误画成标准窗口，已通过一次定向图像编辑移除独立打开面板的窗口控制键，缩小文件示意图，并统一标题为“打开面板”。编辑时其余布局、配色、图标、参数和设置操作保持不变。

推荐以 A 作为默认设计基础；B 作为深色视觉参考；C 适合优先突出配置预览的方向。图中不同标识仅为概念表现，不代表已确定品牌重设计。未修改应用源码，因此本轮不涉及构建或运行测试。

## 后续 A/B 实现

用户随后选定 A/B，已在 v0.2.0-beta.3 实现可切换皮肤。上述“未修改源码”仅指最初概念探索。实际渲染位于 [A](implemented/skin-a-settings-1.png)、[B](implemented/skin-b-settings-1.png)，实现范围与验证见 [发布记录](../../releases/v0.2.0-beta.3.md)。
