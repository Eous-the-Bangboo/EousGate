# 03 · 原画 Eous / 圆角工具清单

用户已选第02轮03方向，并修正：保留统一圆角，去掉软胶玩具画风，使用原本Eous；图标也换Eous；角色不能作为头像放在界面顶部。

## 当前结果

设计北极星：六项工具状态一眼看清，Eous作为侧栏工作区伙伴，操作保持安静明确。

- 保留A工具清单结构，03暖白与鼠尾草绿配色；橙色来自Eous围巾，用于主操作及修复入口。
- 所有可见控件、窗口、抽屉和弹层共用12px圆角；表单勾选也采用同一圆角。
- 使用原始透明全身Eous，置于侧栏导航与底部设置之间，共用侧栏背景，不设头像框、姓名或个人资料区。高度不足时收起插画，优先保留操作。
- 窗口图标及favicon使用现有Eous头像图标；工具领域图标保持文件、链接、目录等不同符号。
- 独立启停使用46×26滑动开关；处理中阻止重复请求且保留键盘焦点，实际状态确认后更新aria-checked。缺路径时开关不可用，邻近“设置路径”可修复。
- 完整延伸到文件、网页、目录、外部程序、通用设置、关于及三类临时浮层；常规设置降为灰绿，异常修复保留强调。

入口：[prototype.html](prototype.html)，本机预览 http://127.0.0.1:8744/prototype.html 。HTML自包含图片、样式及Alpine脚本，可离线打开。

## 素材

- `assets/eous-original-full.png`逐字节复制自用户原始`C:/Users/Eous/Documents/Codex/2026-10-16/cha/work/eous-reference.png`，RGBA透明全身原画；没有重绘、调色或更换画风。
- `assets/eous-icon.png`复制自项目`Assets/EousPortrait-sage.png`，用于窗口及标签页标识。
- `verify-selected.cjs`检查嵌入素材与文件SHA-256一致，原始全身图与源文件一致。
- 没有使用第02轮任何生成的抽象、哥特、软胶图片。打开软件列表使用通用应用窗口符号与软件全名；原生实施时可按EXE提取真实图标。

## 交互与动效

继承完整A原型的草稿、应用设置、刷新读回、保存失败保留输入、外部程序路径校验、目录收藏与导入等流程。仅在浏览器独立localStorage键`eous-assistant-design-03-v1`保存，不读取真实工具配置，不控制实际进程。

动效：六行首次进入6px渐入；开关滑块180ms移动20px，处理中状态转动；抽屉280ms从右侧36px位移进入；浮层短渐入及操作toast。支持系统减少动态效果。

证据工具缺少ffmpeg，因此只留动作三帧与动效探测，没有完整视频。初次取证抽屉流末帧与静态终态不一致，另行重录抽屉；静态以终态截图为准，不把探测或三帧当作完整视频验收。

## 验证

主Agent实际运行：

- `node --check app.js`通过。
- `node verify-ui.cjs`：12项异步主流程连续3轮及1项等效200%检查，共37项通过。
- `node verify-extended.cjs`：5组边界通过，含保存失败回滚与重试、损坏导入保留记录等。
- `node verify-selected.cjs`：7组专项连续3轮通过，覆盖开关键盘/忙碌状态、缺路径修复、10状态12px圆角、目录滚动末项与选中关系、原图一致性、图标、深色及等效200%工作区。
- 主Agent查看最终首屏、设置、浮层、深色及放大工作区实际截图。最终10状态无控制台错误、横向溢出、图片失败，lint均为空。

报告：`output/playwright/verification.json`、`extended-verification.json`、`selected-verification.json`。最终截图：`shots/final/`；深色与等效200%：`shots/dark.png`、`shots/compact-200.png`；动效：`evidence-final/`与`evidence-drawer-retry/`。

## 独立评审

一轮隔离上下文视觉评审，初评8/10；修改后独立复核，没有重新打分。原画侧栏融入及列表骨架获保留。已处理常规设置权重、目录路径截断、字母占位素材和重复计数；复核无新增必改项。完整记录见`review-independent.md`。

独立评审只确认静态视觉；目录滚动、选中关系和动效未独立验证，前两项由主Agent实际脚本核验。动效没有完整视频独立验收。

## 范围与维护

当前为可操作设计原型，尚未移植WPF、不生成EXE、不修改真实工具。原有A原型与第02轮对比页保留。

源文件：`shell.html`、`styles.css`、`app.js`、`parts/`、`vendor/`及`assets/`。编辑后运行`python build.py`，生成自包含`prototype.html`。预览运行`python -m http.server 8744 --bind 127.0.0.1`。
