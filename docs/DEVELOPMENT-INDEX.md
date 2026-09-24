# EousGate 开发资料索引

这是项目开发文档的唯一入口。按“上下文 → 需求 → 技术 → 设计 → 执行 → 测试 → 发布”的顺序阅读，避免在没有验收标准的情况下直接扩大改动范围。

第一次参与项目开发时，先阅读 [`learning-roadmap.md`](learning-roadmap.md)。它把官方学习资料、仓库目录、调试、测试和发布串成一条可实践的路线。

## 当前阶段

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
