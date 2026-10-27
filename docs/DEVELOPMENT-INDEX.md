# EousAssistant 开发资料索引

这是项目开发文档的唯一入口。按“上下文 → 需求 → 技术 → 设计 → 执行 → 测试 → 发布”的顺序阅读，避免在没有验收标准的情况下直接扩大改动范围。

第一次参与项目开发时，先阅读 [`learning-roadmap.md`](learning-roadmap.md)。它把官方学习资料、仓库目录、调试、测试和发布串成一条可实践的路线。

## 当前阶段

**v0.2 beta.15.4：米哈游接力进程与快跳模态闪烁**。受限HYP适配、显式停止的定向授权、独立STA按钮线程与真实模态/专用进程回归通过；81项基础及完整Release已打包，真实UAC和启动器退出待用户复测，见[发布记录](releases/v0.2.0-beta.15.4.md)。

**v0.2 beta.15.3：文件快开启动拒绝访问**。修复真实权限查询缺少令牌Duplicate权限的回归；补真实调用测试，隔离宿主两轮完整启停及79项基础/完整Release通过，见[修复记录](releases/v0.2.0-beta.15.3.md)。

**v0.2 beta.15.2：文件设置页签与接收条起止范围**。页签裁切修复，三侧起点/终点百分比默认0–100、旧配置位置迁移、真实HWND更新与79项基础及完整Release回归通过，见[修复记录](releases/v0.2.0-beta.15.2.md)。

**v0.2 beta.15.1：关闭符号裁切与文件快开权限诊断**。关闭按钮布局已修复，管理员运行下明确提示普通启动方法；完整Release回归通过，用户确认 beta15 普通启动后拖拽恢复，见[修复记录](releases/v0.2.0-beta.15.1.md)。

**v0.2 beta.15：03圆角工具清单原生落地**。六行清单、原画Eous侧栏、12px控件、右侧设置抽屉及草稿保护，见[发布记录](releases/v0.2.0-beta.15.md)。实际DPI/高对比度和桌面拖拽沿用现有待验收项。

**v0.2 beta.14：第2轮确认设计同步**。更紧凑的状态卡、平面网页设置、按需后缀浏览和就近候选排序，见 [发布记录](releases/v0.2.0-beta.14.md)。

**v0.2 beta.13：模块工作台 UI 实际落地**。已确认的 02 方向从 HTML 移植到实际 WPF，主页、即时开关、设置遮罩与收放见 [发布记录](releases/v0.2.0-beta.13.md)。

**v0.2 beta.12：文件快开分类与智能推荐**。常见后缀分类、默认推荐与用户覆盖、切换草稿及整类恢复见 [发布记录](releases/v0.2.0-beta.12.md)。

**v0.2 beta.11：真实复制计数核验与通知去重**。真实 WPF 复制已验证，微信／Edge 现场验证尚未完成，见 [发布记录](releases/v0.2.0-beta.11.md)。

**v0.2 beta.10：网页快开连续复制直接打开**。可选开关、自定义次数、全部网址去重、快速消息与失败回退见 [发布记录](releases/v0.2.0-beta.10.md)。

**v0.2 beta.9：自定义程序关闭诊断与策略修复**。Windows 权限拒绝准确提示，运行中可单独保存超时强制结束选择，见 [发布记录](releases/v0.2.0-beta.9.md)。

**v0.2 beta.8：开关动画与权限启动修复**。连续通知下保留滑动／渐变动画，外部程序按需触发 Windows 授权及中文失败提示，见 [发布记录](releases/v0.2.0-beta.8.md)。

**v0.2 beta.7：Eous 角色图与添加入口**。应用／窗口／托盘图标、浅深头像及突出显示的添加程序按钮，见 [发布记录](releases/v0.2.0-beta.7.md)。

**v0.2 beta.6：用户自定义程序监控**。普通 EXE 添加、编辑、移除、参数及可选强制结束，见 [ADR 0006](adr/0006-custom-program-monitoring.md) 和 [发布记录](releases/v0.2.0-beta.6.md)。

**v0.2 beta.5：A 苹果绿启停开关**。单个工具按钮合并、真实状态与失败恢复、UIA 请求和批量同步见 [发布记录](releases/v0.2.0-beta.5.md)。

**v0.2 beta.4：A 方案 Figma 实现与动效**。首页、外观设置与打开面板已落实为 WPF，B 保留原样；参见 [发布记录](releases/v0.2.0-beta.4.md)。

**v0.2 beta.3：A/B 皮肤已落地并发布**。A 原生 Fluent、B 深色效率，以及局部预览与保存／重开检查见 [发布记录](releases/v0.2.0-beta.3.md)。实际 DPI 与高对比度桌面验收继续跟踪。

**EousAssistant v0.2 beta.2：原生／外部工具分组与进程控制**。新增 [ADR 0005](adr/0005-external-program-control.md) 和 [beta.2 发布记录](releases/v0.2.0-beta.2.md)。本次决策见 [ADR 0004](adr/0004-assistant-host.md)，验证与限制见 [v0.2.0-beta.1](releases/v0.2.0-beta.1.md)。下列旧阶段记录保留用于追溯。


**阶段 12：多文件同时拖动打开（已完成）**

阶段 0 至阶段 5、阶段 7 至阶段 10、阶段 12 已完成。阶段 11 已完成自包含发布脚本和本机包生成，仍需干净 Windows 环境启动及手工验收；阶段 12 已加入同类型与混合类型多文件打开。阶段 6 的 DPI 缩放验收仍由 TODO-006 跟踪。

## 标准文件

| 领域 | 路径 | 使用时机 |
| --- | --- | --- |
| 首版需求 | [`standards/requirements/v1-requirements.md`](standards/requirements/v1-requirements.md) | 判断功能是否属于首版范围 |
| 技术架构 | [`standards/architecture/technical-standards.md`](standards/architecture/technical-standards.md) | 修改领域、Infrastructure 或接口时 |
| 界面设计 | [`standards/design/ui-design-standards.md`](standards/design/ui-design-standards.md) | 修改窗口、主题、交互或文案时 |
| 开发执行 | [`standards/development/execution-standards.md`](standards/development/execution-standards.md) | 拆分任务、审查改动和处理回退时 |
| 测试验收 | [`standards/testing/test-standards.md`](standards/testing/test-standards.md) | 编译、测试和手工验收时 |
| 发布诊断 | [`standards/operations/release-and-diagnostics.md`](standards/operations/release-and-diagnostics.md) | 打包、日志和发布检查时 |

已有产品与领域资料：[`PRODUCT-DESIGN.md`](PRODUCT-DESIGN.md)、[`BEGINNER-GUIDE.md`](BEGINNER-GUIDE.md)、[`../CONTEXT.md`](../CONTEXT.md) 和 [`adr/`](adr/)。

## 日态与待办

- 开发排期与里程碑：[`DEVELOPMENT-PLAN.md`](DEVELOPMENT-PLAN.md)（UI 设计与落地优先于新功能，按投入估算逐阶段验收后推进）
- 日态规则：[`dev-status/README.md`](dev-status/README.md)
- 日志模板：[`dev-status/TEMPLATE.md`](dev-status/TEMPLATE.md)
- 当前待办：[`dev-status/TODO.md`](dev-status/TODO.md)
- 每日记录：`dev-status/YYYY-MM-DD.md`

## 单次工作流程

1. 读取 `CONTEXT.md`、本索引和任务相关标准。
2. 在当日日志登记一个目标，并确认它属于当前阶段。
3. 做最小范围改动，避免把多个行为重构合并在一次工作中。
4. 运行对应验证并把命令和结果写入日态。
5. 更新 `TODO.md`、相关规范或 ADR，然后决定当前阶段是否通过。
