# v0.2.0-beta.15.4 · 2026-10-20

- 米哈游启动器由根目录launcher.exe接力到版本目录HYP.exe后，继续显示真实运行状态并可选择停止；识别范围限制在同安装位置、同发布者的版本目录，不按进程树结束游戏。
- 显式停止遇到访问拒绝时，可单独授权短时关闭工作器，助手仍以普通权限运行；默认只请求正常关闭，彻底退出托盘程序仍需用户勾选超时强制结束。
- 修复助手自带文件选择框中，文件夹快跳按钮抢前台、反复出现/消失；按钮使用独立STA消息循环，点击派发回宿主，停止时释放线程。
- 新增真实模态对话框两轮显示/点击/清理、启动器接力和定向关闭夹具回归，并验证发布EXE的工作器入口。

# v0.2.0-beta.15.3 · 2026-10-20

- 修复beta15.1引入的文件快开启动“拒绝访问”：管理员状态查询需复制进程令牌，补足Query之外的Duplicate访问权限。
- 默认权限回归增加生产检测的真实调用和当前令牌比对，覆盖此前模拟测试遗漏的路径；隔离宿主验证文件快开完整启停。

# v0.2.0-beta.15.2 · 2026-10-20

- 修复文件设置“触发与面板／打开方式”页签背景及边框被裁切，按内容自然排布并保留间距。
- 接收条改为三侧独立输入起点、终点百分比，默认0–100，支持小数；宽度单独设置，旧长度和偏移按实际覆盖位置换算。
- 非法范围保留草稿、拒绝保存并就近提示；跨设置页保存时跳回完整文件页签组。新增范围几何、持久化迁移、真实HWND位置更新与页签裁切回归。

# v0.2.0-beta.15.1 · 2026-10-20

- 修复网页提示关闭按钮继承 12px 横向内边距，导致 32px 按钮中的“×”被裁切。
- 文件快开启动前检测管理员运行：明确提示 Windows 拖放权限隔离及普通启动方法，失败回弹并释放资源，避免显示运行中但拖入被拒绝。
- 新增权限宿主回归；剪贴板提示检查关闭符号内容边界并输出浅深色 100/125/150/200% 离屏图。

# v0.2.0-beta.15 · 2026-10-20

- 按用户确认的03方向从新原型落实原生界面：原画Eous融入侧栏、六行工具清单、统一12px控件与暖白/鼠尾草绿。
- 设置改为右侧抽屉，支持文件设置横向页签、未保存修改的继续/放弃/保存、失败保留输入和异步保存关闭保护。
- 保留启停、外部进程监控、分类推荐、目录与配置语义；目录浮层统一圆角并修正动态控件的DPI缩放。

# v0.2.0-beta.14 · 2026-10-19

- 同步用户确认的第2轮模块工作台：收紧原生卡片、强化状态、网页设置平面化、当前后缀固定与分类按需展开。
- 软件行内上下移保持勾选与草稿；恢复范围明确，保留真实发现、用户覆盖和帮助入口。
- 添加程序表单紧凑，失败时自动留出提示空间、完整显示原因并保留输入；工作台设置避免重复入场动画。
- 完整 Release 回归及三轮开关/设置检查通过，补充行内排序、分类折叠、错误提示完整可见和原生动效时序取证。

# v0.2.0-beta.13 · 2026-10-19

- 已选「02 · 模块工作台」移植到 WPF：炭灰与黄绿色、三张原生模块卡、外部程序表格、自绘标题栏和固定底栏。
- 开关立即显示请求方向，后台继续确认真实状态；失败回弹，保留状态文字和错误，防止重复启停。主页隐藏批量勾选，仅保留独立与全部启停。
- 通用、原生与外部程序设置统一为工作台内的遮罩弹窗，支持收放动画、外部点击／Esc 关闭、保存中保护及失败保留输入。
- 保留 beta.12 分类推荐和 beta.11 剪贴板修正；设置减少嵌套滚动、统一表单控件，打开面板 A/B 外观仍可单独配置。

# v0.2.0-beta.12 · 2026-10-19

- 文件快开按视频、音频、图片、文档、表格、演示、压缩包、代码与配置展示常见后缀，支持搜索及其他后缀输入。
- 默认优先系统默认应用、专业播放器及类型匹配软件；明确不匹配的关联默认收起，完整列表仍可显示并自定义启用。
- 切换后缀保留排序、显示状态和数量草稿；可恢复单后缀／整类推荐，同时保留自定义应用与数量。
- 新增推荐策略与 WPF 分类、草稿、应用／重开、写入失败／取消、A/B 最小尺寸及缩放图回归；包含 beta.11 剪贴板修正。

# v0.2.0-beta.11 · 2026-10-19

- 按读取完成时的 Windows 剪贴板序列号去重通知，避免延迟呈现把同一次复制计数两次；相同文本写入新序列仍正常计数。
- 补充真实 WPF 文本框复制到生产读取器的集成测试，浏览器启动由替身接收；不将合成通知测试当作微信或 Edge 实测。
- 自动打开仍需在实际助手设置中勾选并应用，旧配置默认关闭；微信／Edge 的实际 Ctrl+C 验证尚未完成。

# v0.2.0-beta.10 · 2026-10-19

- 网页快开新增默认关闭的“连续复制自动打开”和 2–10 次输入，默认 3 次；达到次数后打开文段中全部不同网址。
- 同一序列只打开一轮；文段改变、无链接、间隔超过 3 秒、模块停止或设置应用后重置。部分失败保留失败链接供手动重试。
- 优先立即读取复制通知，保留忙碌重试，避免快速连复制被原 80ms 等待合并；只在内存保存临时指纹。
- 新增阈值／重置／失败回归，设置克隆与持久化检查及快速消息集成，默认 Release 脚本纳入剪贴板场景。

# v0.2.0-beta.9 · 2026-10-19

- 修复关闭消息被 Windows 拒绝时仍显示普通超时的问题，准确提示管理员权限差异及可用的退出操作。
- 运行中的自定义程序允许单独保存超时强制结束选择，保留原工具、运行状态与勾选；保存失败保留原策略，名称／路径／启动参数仍需停止后编辑。
- 补充关闭权限拒绝、同权限正常关闭与运行中策略保存／回退的回归检查。

# v0.2.0-beta.8 · 2026-10-17

- 修复开关状态／能力连续通知重置动画：确认状态变化时滑动并渐变，重复通知保留在播动画，隐藏时释放时钟。
- 外部 EXE 返回“请求的操作需要提升”时按需进入 Windows UAC 授权流程；保留原路径、工作目录及参数，取消授权和拒绝访问显示中文原因。
- 助手自身不自动提权，退出命令不自动提权；启动后继续以真实进程检测确认结果。

# v0.2.0-beta.7 · 2026-10-16

- 使用用户提供的 Eous 角色图替换 EXE、窗口、任务栏与托盘图标，内置九档 ICO 尺寸。
- 首页和设置页加入浅色绿底、深色炭灰底角色头像，图片内嵌并随主题预览更新。
- “非Eous原生工具”的“＋ 添加程序”改为更宽的强调色实心按钮，保留键盘焦点、禁用和主题反馈。

# v0.2.0-beta.6 · 2026-10-16

- 支持普通 EXE 自定义监控：添加、编辑、移除、自动名称／图标、可选启动参数和持久化；即时列表更新、独立启停及批量操作。
- 自定义程序默认只正常关闭，允许用户选择超时强制结束；移除和退出助手保留外部程序。原监控暂停规则及进程身份校验保留。
- 新增配置／保存失败／重复／失效路径修复／真实宿主编辑器和专用进程参数及停止策略回归；通用设置草稿不覆盖自定义列表。

# v0.2.0-beta.5 · 2026-10-16

- A 单个工具的启动／停止合并为苹果绿滑动开关，保留独立设置、批量勾选及四项批量动作；B 保持原样。
- 接入真实状态与外部监控，处理中转圈、防重复；启动失败保持关闭，停止失败保留开启方向便于重试。
- 加入 UIA Toggle 请求及延迟／失败／外部观察／批量同步回归；浅深与缩放截图核对。

# v0.2.0-beta.4 · 2026-10-16

- A 皮肤落实 Figma：68 像素工具行、固定底栏、外观三张设置卡片和单列打开面板；保留全部启停、选择、设置、拖拽及失败恢复功能。
- A 加入一次入场、工具／选项错峰和面板滑入滑出；按原动效时长缩放，系统关闭动画或高对比度时立即显示。修复退出动画尚未结束时重新唤出被旧动画隐藏的问题。
- B 保持原有视觉；A 继续支持浅色／深色、局部预览及取消隔离，最小设置窗口缩减预览列宽度。

# v0.2.0-beta.3 · 2026-10-16

- 外部程序监控仅在控制面板显示时每两秒执行；最小化、隐藏或关闭到托盘后暂停，恢复立即刷新。失去焦点继续，已发起启停照常确认结果；新增宿主生命周期与并发回归。

- 后续精简：移除玻璃／透明质感及紧凑列表／图标网格，保留 A/B 皮肤与单列浮动命令面板；旧配置安全回退，其他偏好保留。

- 应用 A 原生 Fluent 与 B 深色效率皮肤；保留原 Mica ID、玻璃及透明材质配置。
- 外观设置加入独立皮肤缩略图、三种真实布局示意与本地实时预览；取消不影响当前运行皮肤。
- B 固定深色、薄荷色强调与紧凑间距；切回 A 保留原主题。统一按钮文字对比度、下拉菜单和滚动条。
- 新增 A/B 保存、重开、取消、最小尺寸和离屏缩放检查。实际显示器 DPI 和高对比度桌面仍需手工复测。

# v0.2.0-beta.2 · 2026-10-16

- 首页分为 Eous原生工具、非Eous原生工具，接入 Ditto、Everything、无线麦 Sayall。
- 新增真实进程状态监控、独立启停、路径设置与跨组批量操作；已运行不会重复启动。
- 限定用户、会话、路径与进程创建时间，保留 Everything 服务；退出助手不关闭外部程序。
- 64 项回归、六项工具布局、专用 EXE 的真实进程控制与本机只读监控检查通过。

# v0.2.0-beta.1 · 2026-10-16

- 产品升级为 EousAssistant（Eous 助手），包含文件快开、网页快开和文件夹快跳三个独立启停模块。
- 新增统一面板、四种批量操作、单托盘和第二次启动唤回面板。
- 接入 FolderHop 历史、收藏、搜索、原生对话框辅助与 Ctrl+Alt+J，支持导入旧历史。
- 配置迁移到 EousAssistant 数据目录，保留旧数据；修正 .NET 8 的 UNC 根路径和 WinForms 主题颜色兼容。
- 新增统一工具生命周期及外部类型扩展接口；本版没有外部软件管理。
- 验证边界见 docs/releases/v0.2.0-beta.1.md。

# Changelog

All notable changes to EousGate are documented here.

## [0.1.0-beta.3] - 2026-09-27

### Added

- Clipboard web links appear in a side popup and open in the default browser when clicked. Copying identical text triggers the popup again.
- Clipboard settings include an enable switch, left/right placement, and a 2–60 second lifetime (5 seconds by default). Hover and keyboard interaction pause the timer.
- Text detection stays local with no clipboard history, URL logging, or automatic network requests.
- Regression tests cover URL extraction, repeated copies, timing, preferences, and browser dispatch. The opt-in UI smoke test verifies Windows notification handling using a synthetic reader.

## [0.1.0-beta.2] - 2026-09-27

### Fixed

- Selecting Windows Photos now resolves its actual app identity and opens files through its Shell association handler. Localized names and icon resources are no longer treated as app identities.
- Removed the Explorer fallback that incorrectly reported successful file opening.
- Packaged apps receive a native Shell file selection; multi-file calls check whether the selected handler supports the complete selection.
- Added regression coverage for localized names, AppX registration, complete file delivery, and failure handling.

### Validation

- 49 Release tests passed. Single JPG and mixed JPG/PNG opened successfully in Photos 2026.11080.24002.0 on the development machine.
- Full mouse-drag replay, other Windows/Photos versions, clean-machine startup, DPI, and high-contrast scenarios still need broader field testing.

## [0.1.0-beta.1] - 2026-09-24

### Added

- Edge-triggered app picker for the left, right, and top edges of the primary display.
- Single-file and multi-file drag sessions, including mixed file types with common-app filtering.
- Windows desktop and packaged-app discovery, custom per-extension apps, ordering, and hiding.
- Light, dark, and high-contrast themes with configurable panel appearance and edge regions.
- Privacy-preserving local diagnostics that are disabled by default.

### Known limitations

- Windows 11 x64 and the primary display are the supported environment for this beta.
- Folders are not supported.
- The binaries are not digitally signed and may trigger a Microsoft Defender SmartScreen warning.
- Microsoft Photos multi-file activation, clean-machine startup, DPI, and high-contrast scenarios still need broader field testing.
