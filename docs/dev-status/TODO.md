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
