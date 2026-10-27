# EousGate 待办清单

维护全部待办及其最终状态。每项必须有明确验收条件；完成后保留原记录并将状态改为“已完成”，不得删除。

| 编号 | 优先级 | 状态 | 事项 | 验收条件 | 关联标准 |
| --- | --- | --- | --- | --- | --- |
| TODO-001 | P0 | 已完成 | 建立稳定构建基线 | Debug 构建成功并记录运行环境 | [`test-standards.md`](../standards/testing/test-standards.md) |
| TODO-002 | P0 | 已完成 | 建立领域与适配层测试边界 | DragSession、软件发现、打开动作和设置存储有可验证行为 | [`technical-standards.md`](../standards/architecture/technical-standards.md) |
| TODO-003 | P0 | 已完成 | 收敛单文件拖拽会话 | 单文件可触发；文件夹和多文件不会执行打开动作 | [`v1-requirements.md`](../standards/requirements/v1-requirements.md) |
| TODO-004 | P0 | 已完成 | 完成打开面板主流程 | 显示候选、选择打开、取消、失败重试和无候选入口均可验收 | [`ui-design-standards.md`](../standards/design/ui-design-standards.md) |
| TODO-005 | P1 | 已完成 | 完成软件发现与失效候选处理 | 候选去重、路径失效和 Windows 错误都有明确结果 | [`technical-standards.md`](../standards/architecture/technical-standards.md) |
| TODO-006 | P1 | 进行中 | 完成设置、主题和缩放验收 | 100%/125%/150% 缩放下无截断或重叠，设置可保存恢复；全局 UI 语义资源和控件状态已接入，待完成预览及 DPI 手工验收 | [`ui-design-standards.md`](../standards/design/ui-design-standards.md) |
| TODO-007 | P1 | 已完成 | 建立自动化与手工测试矩阵 | 构建、领域测试和首版拖拽验收矩阵可重复执行 | [`test-standards.md`](../standards/testing/test-standards.md) |
| TODO-008 | P1 | 进行中 | 准备绿色发布检查 | 无自动启动、无网络、日志脱敏，发布包可独立运行 | [`release-and-diagnostics.md`](../standards/operations/release-and-diagnostics.md) |
| TODO-009 | P1 | 已完成 | 完成视觉动效与状态反馈 | 边缘唤出、打开面板、软件选项和设置窗口有流畅且不阻塞操作的动效 | [`ui-design-standards.md`](../standards/design/ui-design-standards.md) |
| TODO-010 | P1 | 已完成 | 丰富设置中心与颜色选择器 | 更多外观参数可保存恢复；颜色可通过色板或系统对话框选择 | [`ui-design-standards.md`](../standards/design/ui-design-standards.md) |
| TODO-011 | P1 | 已完成 | 重构打开面板视觉设计 | 打开面板具备清晰层次、统一组件、状态动效和实际截图验收 | [`ui-design-standards.md`](../standards/design/ui-design-standards.md) |
| TODO-012 | P1 | 已完成 | 建立可扩展设计中心并收录方案一至三 | 方案一至三具备真实结构缩略图，可选择并持久化，未知方案可安全回退 | [`ui-design-standards.md`](../standards/design/ui-design-standards.md) |
| TODO-013 | P1 | 已完成 | 设置应用按钮与自定义打开方式 | 应用可保持窗口并即时刷新；自定义 exe 可按扩展名保存、排序、隐藏和删除；不改变 Windows 默认关联 | [`ui-design-standards.md`](../standards/design/ui-design-standards.md) |
| TODO-014 | P1 | 已完成 | 鼠标感知与多侧接收条 | 接收条可勾选左侧/右侧/上侧任意组合；面板跟随触发鼠标并自动避让；候选悬停有清晰高亮 | [`ui-design-standards.md`](../standards/design/ui-design-standards.md) |
| TODO-015 | P2 | 已完成 | Windows 11 Mica 材质 | 设置中心和自定义应用窗口启用 DWM Mica；旧系统和高对比度自动回退 | [`ui-design-standards.md`](../standards/design/ui-design-standards.md) |
| TODO-016 | P1 | 已完成 | 单实例与正式版本启动治理 | 启动时阻止第二个 EousGate；正式 Release 强制重建并验证 DLL/EXE 为同一版本 | [`release-and-diagnostics.md`](../standards/operations/release-and-diagnostics.md) |
| TODO-017 | P1 | 已完成 | 接收条远离后的自动收起 | 面板在接收条和面板外超过配置时长自动收起；拖拽会话不误打开，返回接收条可重新触发 | [`ui-design-standards.md`](../standards/design/ui-design-standards.md) |
| TODO-018 | P1 | 已完成 | 接收条尺寸配置与边缘覆盖收敛 | 左/右/上侧接收条可独立设置长度、宽度和偏移；未覆盖边缘区域不被接收条窗口屏蔽；旧配置兼容回退 | [`ui-design-standards.md`](../standards/design/ui-design-standards.md) |
| TODO-019 | P1 | 已完成 | 多文件同时拖动打开 | 同类型与混合类型文件只显示共同软件；选择后单次交付全部文件；文件夹整批拒绝 | [`v1-requirements.md`](../standards/requirements/v1-requirements.md) |
| TODO-020 | P1 | 已完成 | 修复“照片”误打开资源管理器 | 解析真实 AUMID，经 Shell 原生文件对象调用所选处理器；本机单图及多图已实测，49/49 回归通过；完整鼠标拖拽和其他系统版本仍待手工覆盖 | [`2026-09-27.md`](2026-09-27.md) |
| TODO-021 | P1 | 已完成 | 剪贴板网页链接提示 | 每次复制触发侧边提示，重复文本重置计时；可设置停留时间和位置，点击用默认浏览器打开；57 项测试和消息/UI 联动已通过，原生剪贴板写入到浏览器完整链路待手工复测 | [`2026-09-27.md`](2026-09-27.md) |
| TODO-022 | P1 | 待验证 | 剪贴板真实桌面链路复测 | 实际复制同一网页文本两次均弹出提示，点击由默认浏览器打开；验证设置时长和左右侧 | [`2026-09-27.md`](2026-09-27.md) |
| TODO-023 | P1 | 进行中 | 辅助窗口退出系统切换列表 | 源码修复、57/57 回归、STA 原生样式集成检查与修复包已完成；待桌面确认 Win+Tab、Alt+Tab、任务栏无辅助条目，设置及真实交互正常 | [`2026-09-28.md`](2026-09-28.md) |

| TODO-024 | P1 | 实现完成，桌面待验收 | EousAssistant 三模块整合 | 统一面板、四种批量操作、迁移、外部类型接口及绿色包完成；60 项回归、离屏与真实宿主启停检查通过 | [2026-10-16](2026-10-16.md) |
| TODO-025 | P1 | 待验证 | 新宿主 Edge 与实际桌面兼容 | Computer Use 被用户 Escape 停止后保留：Edge 按钮与中文路径／文件名、跨进程导航、实际 DPI／高对比度和完整拖拽 | [v0.2.0-beta.1](../releases/v0.2.0-beta.1.md) |
| TODO-026 | P1 | 已完成实现和自动验证 | 三款外部软件监控启停 | Ditto、Everything、无线麦 Sayall；64 项回归、专用进程启停与实际只读监控通过，真实应用退出保存行为待按需实测 | [beta.2](../releases/v0.2.0-beta.2.md) |
| TODO-027 | P1 | 已完成实现和自动验证 | A/B 皮肤落地 | A 原生 Fluent、B 深色效率可选择保存；65 项回归、预览隔离、应用重开、取消、对比度和最小尺寸及离屏缩放检查通过；实际 DPI／高对比度沿用 TODO-025 | [beta.3](../releases/v0.2.0-beta.3.md) |
| TODO-028 | P1 | 已完成实现和自动验证，桌面待验收 | 按控制面板状态暂停外部监控 | 66 项基础回归、新宿主生命周期／并发／操作确认检查、专用进程控制、真实只读检测与 Release 发布通过；实际任务栏恢复和托盘鼠标重开待复测 | [ADR 0005](../adr/0005-external-program-control.md) |

- 2026-10-16 外观精简：按用户截图移除两种旧材质及两种额外布局，沿用 TODO-025 的实际 DPI／高对比度和完整拖拽验收，不新增外观方案。
| TODO-029 | P1 | 实现与自动验证完成，桌面待验收 | A 方案与动效 | Figma 三页面落实为 WPF，B 基准截图一致；动画终态／中断／快速重开、默认六工具布局及草稿隔离通过；实际 DPI、高对比度及拖拽沿用 TODO-025 | [beta.4](../releases/v0.2.0-beta.4.md) |
| TODO-030 | P1 | 实现与自动验证完成 | A 苹果绿启停开关 | 独立按钮合并、真实状态、忙碌、防重复、失败恢复、外部／批量同步、UIA 和离屏截图通过，B 基准一致；实际 DPI／高对比度沿用 TODO-025 | [beta.5](../releases/v0.2.0-beta.5.md) |
| TODO-031 | P1 | 实现与自动验证及发布完成 | 用户自定义程序监控 | EXE 添加／编辑／移除、参数、正常关闭及可选强制、图标、动态列表、保存失败回退与草稿合并；68 项回归及宿主／专用进程检查通过，实际 DPI／桌面交互沿用 TODO-025 | [beta.6](../releases/v0.2.0-beta.6.md) |
| TODO-032 | P1 | 实现与自动验证及发布完成 | Eous 角色图与显眼添加入口 | 内嵌多尺寸应用／窗口／托盘图标及浅深头像；A/B 强调色添加按钮、最小窗口预览、68 项回归与完整发布通过；实际 DPI／高对比度沿用 TODO-025 | [beta.7](../releases/v0.2.0-beta.7.md) |
| TODO-033 | P1 | 实现与自动验证完成，目标电脑待复测 | 开关动画与外部程序提权启动 | 连续通知不重启动画；Windows 740 按需 Shell 授权、取消中文提示；69 项回归及完整集成检查通过，TrafficMonitor 真实 UAC 在目标电脑复测 | [2026-10-17](2026-10-17.md) |
| TODO-034 | P1 | 已完成 | 建立开发排期表 | 记录现有验收、新功能提案、时间窗口、交付物、完成条件、依赖及维护规则；开发索引可访问 | [开发排期表](../DEVELOPMENT-PLAN.md) |
| TODO-035 | P0 | 设计、原生实现与自动验证完成，桌面待验收 | 完整 UI/UX 设计与落地 | 03原画Eous圆角清单已确认并落实，完整原型、独立视觉评审及整改复核、Release回归与beta.15发布完成；实际DPI/高对比度及桌面场景继续验收 | [开发排期表阶段 2–3](../DEVELOPMENT-PLAN.md) |
| TODO-036 | P1 | 实现、完整自动验证与发布完成 | 自定义程序关闭权限诊断与策略保存 | LiteMonitor 管理员权限差异已只读确认；关闭消息访问拒绝准确提示，运行中单独保存强制结束选择，70 项基础与全部集成回归通过，beta.9 已发布；未实际关闭用户软件 | [2026-10-19](2026-10-19.md) |

| TODO-037 | P1 | 实现、完整验证与发布完成 | 网页快开连续复制直接打开 | 可选开关、2–10 次、全部网址去重、3 秒连续间隔与单轮触发；73 项基础及完整 Release 集成通过，beta.10 已打包发布 | [beta.10](../releases/v0.2.0-beta.10.md) |

| TODO-038 | P1 | 计数修正已验证，应用现场待验收 | 相同文段连续复制真实链路 | WPF Copy→真实 Windows 剪贴板→生产读取器已通过；同序列去重修复。微信／Edge Ctrl+C 未通过现场验收，不能用合成消息代替；先确认实际自动打开开关已应用且网页快开已启动 | [beta.11](../releases/v0.2.0-beta.11.md) |
| TODO-039 | P1 | 实现、完整自动验证与发布完成 | 文件快开分类与智能推荐 | 常见后缀分类、默认匹配排序、完整关联显示及用户覆盖、切换草稿、单后缀／整类恢复；76 项基础、WPF 与完整 Release 通过；beta.12 独立完整包已发布 | [beta.12](../releases/v0.2.0-beta.12.md) |

| TODO-040 | P0 | 实现、完整回归与发布完成 | 02 模块工作台 WPF 落地 | 三原生卡、外部表格、隐藏批量选择、即时开关／失败回弹、设置遮罩／收放／外部关闭与保存保护；保留 beta.12 功能 | [beta.13](../releases/v0.2.0-beta.13.md) |

| TODO-041 | P0 | 实现、完整回归与发布完成 | 第2轮确认设计同步 | 原生卡片与状态层级、网页平面设置、后缀按需浏览及行内排序、恢复范围、紧凑程序表单；独立评审发现错误裁切后修正并复核，beta.14完整包已发布 | [beta.14](../releases/v0.2.0-beta.14.md) |

| TODO-042 | P0 | 原生实现、完整自动回归与发布完成，桌面待验收 | 03原画Eous圆角清单落地 | 两组工具清单、原画角色、统一12px圆角、滑动开关、右侧设置抽屉与草稿关闭保护；76项基础及完整Release集成通过，独立视觉整改复核完成，beta.15自包含完整包交付；现场欠项沿用TODO-025等 | [beta.15](../releases/v0.2.0-beta.15.md) |
| TODO-043 | P0 | 修复与完整自动回归完成，用户确认普通启动拖拽恢复 | beta15关闭符号与管理员拖放 | 关闭符号内容区修复；新增文件快开权限失败反馈；beta.15.1完整包已生成，用户在 beta15 普通启动后确认本次拖拽故障恢复 | [beta.15.1](../releases/v0.2.0-beta.15.1.md) |
| TODO-044 | P0 | 实现、完整自动回归与发布完成 | 文件设置页签裁切与接收条起止范围 | 旧模板裁切已反证；三侧百分比默认0–100、旧位置迁移、非法输入与跨页错误处理、真实HWND范围更新和79项基础及完整Release通过；beta.15.2已打包，实际DPI和完整桌面矩阵沿用既有验收项 | [beta.15.2](../releases/v0.2.0-beta.15.2.md) |
| TODO-045 | P0 | 修复、真实宿主验证与完整发布完成 | 文件快开启动拒绝访问 | 补足权限查询的令牌Duplicate权限；旧代码真实调用准确复现、修复后查询与两轮宿主启停通过；默认回归新增实际调用，79项基础及完整Release通过，beta.15.3已打包 | [beta.15.3](../releases/v0.2.0-beta.15.3.md) |
| TODO-046 | P0 | 实现/完整回归及发布完成，实际UAC退出待复测 | 米哈游接力状态/关闭与快跳模态闪烁 | HYP有限路径适配、显式停止窄授权、默认保留强制选择；独立STA按钮线程、真实模态两轮显示/点击/清理、81项基础与完整Release通过。真实启动器只读识别，授权/退出待用户复测 | [beta.15.4](../releases/v0.2.0-beta.15.4.md) |
