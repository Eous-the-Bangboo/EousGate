# 从需求到维护：EousGate 快速入门路线

这份路线给编程初学者使用。目标不是一次学完所有技术，而是能看懂一个软件如何从“要解决什么问题”走到“发布后如何稳定维护”。建议始终以当前仓库 EousGate 做练习对象：它是 Windows 11 优先、`.NET 8 + WPF` 的本地桌面工具。

## 先建立一张地图

软件开发不是“写完代码就结束”，而是一个可重复的闭环：

`发现问题 -> 写需求和验收条件 -> 设计边界 -> 小步实现 -> 自动/手工测试 -> 打包发布 -> 诊断和修复 -> 回到需求`

每次改动都应该能回答三个问题：

1. 用户要得到什么可观察的结果？
2. 哪些代码和文档负责这个结果？
3. 用什么命令或场景证明它没有回归？

## 本仓库的对应关系

| 生命周期环节 | 在 EousGate 中先读什么 | 你要产出的东西 |
| --- | --- | --- |
| 需求 | [`CONTEXT.md`](../CONTEXT.md)、[`standards/requirements/v1-requirements.md`](standards/requirements/v1-requirements.md) | 用户场景、范围、验收条件 |
| 设计 | [`PRODUCT-DESIGN.md`](PRODUCT-DESIGN.md)、[`adr/`](adr/)、[`standards/design/`](standards/design/) | 界面草图、状态转移、架构决策 |
| 开发执行 | [`DEVELOPMENT-INDEX.md`](DEVELOPMENT-INDEX.md)、[`standards/development/execution-standards.md`](standards/development/execution-standards.md) | 小任务、代码提交、日态记录 |
| 实现 | [`Domain/`](../Domain/)、[`Infrastructure/`](../Infrastructure/)、[`Presentation/`](../Presentation/) | 分层代码和接口实现 |
| 测试 | [`Tests/Program.cs`](../Tests/Program.cs)、[`standards/testing/`](standards/testing/) | 自动化测试、可重复手工场景 |
| 发布 | [`publish.ps1`](../publish.ps1)、[`standards/operations/`](standards/operations/) | Windows x64 绿色包、校验值 |
| 维护 | [`dev-status/`](dev-status/)、[`TODO.md`](dev-status/TODO.md) | 已知问题、诊断信息、回退方案 |

仓库已经把“按上下文 -> 需求 -> 技术 -> 设计 -> 执行 -> 测试 -> 发布”的阅读顺序写在 [`DEVELOPMENT-INDEX.md`](DEVELOPMENT-INDEX.md) 中；把它当作项目导航，而不是另起一套流程。

## 必修最小知识

按下面顺序学习，边学边在仓库运行命令。

1. **命令行和文件系统**：会进入目录、查看文件、运行脚本。
2. **零基础编程**：如果还没写过程序，先做哈佛 [CS50x](https://cs50.harvard.edu/x/) 的前几周；它从问题求解、C 和算法逐步进入更高层语言。
3. **C# 基础**：变量、条件、循环、方法、类、接口、异常、集合、异步；从 Microsoft 的 [C# Tour](https://learn.microsoft.com/dotnet/csharp/tour-of-csharp/) 开始。
4. **.NET 工具链**：理解 SDK、项目文件、还原、构建、测试、发布；查 [`.NET CLI overview`](https://learn.microsoft.com/dotnet/core/tools/)。
5. **Git 基础**：工作区、提交、分支、差异和回退；按 Git 官方书籍 [Pro Git](https://git-scm.com/book/en/v2) 的“Getting Started”和“Git Basics”练习。
6. **WPF 基础**：XAML、控件、事件、数据绑定、窗口生命周期；阅读 Microsoft 的 [WPF overview](https://learn.microsoft.com/dotnet/desktop/wpf/overview/)。

先不追求设计模式大全。能读懂一次完整的“输入 -> 状态 -> 输出”，比背术语更重要。

## 七个阶段的学习和落地

### 1. 发现问题与写需求

从用户行为开始写，而不是从技术开始写。例如：“用户拖动一个或多个文件到主屏幕边缘，看到共同可用的软件并选择一个打开”。随后补上边界：只支持普通文件、主屏幕、Windows 11；无候选、路径失效、权限不足和启动失败都必须有反馈。需求标准中的“必须满足/明确不包含/变更规则”就是可执行的范围控制。

推荐资料：

- [敏捷宣言](https://agilemanifesto.org/)：把可工作的软件、客户协作和响应变化放在核心位置。
- [Scrum Guide](https://scrumguides.org/scrum-guide.html)：了解 Product Goal、Product Backlog、Sprint 和 Definition of Done 等基本词汇；不必照搬 Scrum 仪式。
- GitHub 官方 [Issues 文档](https://docs.github.com/issues)：把需求、缺陷和决策写成可追踪条目。
- GitHub 官方 [Projects 概览](https://docs.github.com/en/issues/planning-and-tracking-with-projects/learning-about-projects/about-projects)：用表格、看板或路线图跟踪 Issue/PR 的状态。

练习：为一个小改动写一页需求，必须包含“用户动作、预期结果、拒绝/失败结果、验收步骤、明确不做什么”。

### 2. 设计边界和方案

先画状态和责任边界，再写 UI。EousGate 的分层约束是：`Domain/` 只放业务规则和契约，`Infrastructure/` 处理 Windows/注册表/进程/存储，`Presentation/` 只处理 WPF 窗口和用户操作。这样的边界让 Windows API 错误能够转换成领域可处理的失败结果，也让规则可以脱离桌面界面测试。

推荐资料：

- Microsoft [WPF architecture](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/wpf-architecture)：理解视觉树、布局、输入和数据绑定的大致关系。
- Microsoft [.NET architecture guides](https://learn.microsoft.com/dotnet/architecture/)：用来学习分层、依赖方向和部署考虑；按需阅读，不需要从头通读。
- [C4 model](https://c4model.com/)：用 Context、Container、Component、Code 四层图先整体后细节地表达架构。
- AWS Prescriptive Guidance 的 [Architecture Decision Records](https://docs.aws.amazon.com/prescriptive-guidance/latest/architectural-decision-records/welcome.html)：了解如何记录“为什么这样选”。本仓库的 [`adr/0001-standalone-wpf.md`](adr/0001-standalone-wpf.md) 是同类示例。

练习：针对一个 bug 画出“输入、状态、依赖、可观察结果”，并注明应该在哪一层修复；如果改变公共边界，先写 ADR。

### 3. 计划、协作和小步开发

把大目标拆成一次只改变一个主要行为的小任务。先在 `docs/dev-status/` 登记目标和验收方式，再修改代码；完成后记录命令、结果、阻塞和下一步。每个提交应能说明“改了什么、为什么、如何验证”。

推荐资料：

- Git 官方 [Recording Changes](https://git-scm.com/book/en/v2/Git-Basics-Recording-Changes-in-the-Repository) 和 [Branching](https://git-scm.com/book/en/v2/Git-Branching-Branching-Workflows)。
- GitHub 官方 [Pull request basics](https://docs.github.com/pull-requests/collaborating-with-pull-requests)：学习让别人审查变更、讨论风险和保留验证证据。

练习：为一个小修复建立分支，提交“先失败测试、再修复、再文档”的三个清晰提交（个人项目也可以只保留一个最终提交，但过程要能解释）。

### 4. 实现和本地运行

先理解程序入口和数据流，再逐个阅读文件。建议顺序：`App.xaml.cs` -> `Domain/` 契约和规则 -> `Infrastructure/` 适配器 -> `Presentation/` 窗口。运行时用最短反馈循环：改一小处，执行 `dotnet build EousGate.csproj --configuration Debug`，再做对应场景。

推荐资料：

- Microsoft [Create a .NET console application](https://learn.microsoft.com/dotnet/core/tutorials/with-visual-studio-code)（用来熟悉项目、编译和运行概念）。
- Microsoft [dotnet build](https://learn.microsoft.com/dotnet/core/tools/dotnet-build) 与 [dotnet run](https://learn.microsoft.com/dotnet/core/tools/dotnet-run)。
- Microsoft [C# exception handling](https://learn.microsoft.com/dotnet/csharp/fundamentals/exceptions/)：不要用空 `catch` 掩盖关键流程，应该转换为可理解的失败结果并保留必要诊断。

练习：跟踪一次拖拽会话从 `Detecting` 到 `Opened`、`Cancelled` 或 `Failed` 的状态变化，写出触发每次转移的事件。

### 5. 测试和验收

测试不是“最后点几下”，而是需求的可执行版本。EousGate 当前的 `Tests/Program.cs` 已覆盖领域规则、设置读写、候选过滤、DPI/布局和诊断隐私；先运行它，再为每个新 bug 添加能重现问题的测试。

推荐资料：

- Microsoft [.NET unit testing](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test)：测试项目组织、`dotnet test` 和测试结果。
- Microsoft [Unit testing best practices](https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices)：保持测试独立、可重复、快速，并清楚表达失败原因。
- Microsoft [Windows UI Automation](https://learn.microsoft.com/dotnet/framework/ui-automation/ui-automation-overview)：需要自动化桌面 UI 时再学习；不能自动化的场景按仓库验收矩阵记录手工步骤。

本仓库的最小验证命令：

```powershell
dotnet build EousGate.csproj --configuration Release
.\Tests\run-tests.ps1 -Configuration Release
```

手工验收至少覆盖：托盘、边缘唤出、候选超过 4 个、成功打开、Esc/面板外取消、无候选/失效路径/启动失败、浅色/深色/高对比度和 100%/125%/150% 缩放。

### 6. 发布和部署

发布是把“当前源码 + 依赖 + 配置”变成别人能运行的可验证产物。仓库的 `publish.ps1` 会先跑 Release 测试，再执行 `dotnet restore` 和 `dotnet publish`，生成 self-contained `win-x64` 目录、ZIP 和 SHA-256 校验文件。

推荐资料：

- Microsoft [dotnet publish](https://learn.microsoft.com/dotnet/core/tools/dotnet-publish) 与 [.NET application publishing overview](https://learn.microsoft.com/dotnet/core/deploying/)。
- Microsoft [Self-contained deployment](https://learn.microsoft.com/dotnet/core/deploying/#publish-self-contained)：理解为什么绿色包可以不要求目标机预装 .NET Runtime。
- GitHub Actions 官方 [.NET build and test](https://docs.github.com/actions/automating-builds-and-tests/building-and-testing-net)：以后需要持续集成时，用同一套构建/测试命令让每次提交自动验证。

练习：运行 `.\publish.ps1`，检查 `EousGate.exe`、`.runtimeconfig.json`、`.deps.json`、ZIP 和 `.sha256` 是否存在；在另一台干净 Windows 11 环境启动并按验收矩阵操作。

### 7. 维护、诊断和迭代

发布后关注“用户能否完成任务”和“失败是否可定位”。先记录固定事件名、时间戳和版本，再决定是否需要更多诊断；默认不要记录文件名、完整路径或内容。修复流程建议固定为：复现 -> 写回归测试 -> 最小修复 -> 跑全量验证 -> 更新 `TODO.md` 和日态 -> 发布可回退包。

推荐资料：

- Microsoft [.NET logging overview](https://learn.microsoft.com/dotnet/core/extensions/logging)：学习日志级别、结构化事件和按环境控制日志。
- [Semantic Versioning 2.0.0](https://semver.org/)：用 `MAJOR.MINOR.PATCH` 传达兼容性变化；即使个人项目也能避免“哪个包更新了”说不清。
- [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/)：按 Added/Changed/Deprecated/Removed/Fixed/Security 分类记录每版用户可见变化。
- NIST [Secure Software Development Framework (SSDF)](https://csrc.nist.gov/Projects/ssdf)：把准备、保护、产出安全软件和响应漏洞纳入整个生命周期。
- OWASP 官方 [Top 10](https://owasp.org/www-project-top-ten/)：建立输入校验、最小权限、敏感数据保护等安全意识；桌面程序同样要避免把路径和内容写入日志。

## 14 天最短实践计划

| 天数 | 任务 | 完成标志 |
| --- | --- | --- |
| 1 | 读 `README.md`、`CONTEXT.md`、`DEVELOPMENT-INDEX.md` | 能用自己的话解释文件、拖拽会话、打开面板、软件选项、打开动作 |
| 2 | 完成 C# Tour 中基础语法练习 | 能读懂一个简单类和方法 |
| 3 | 安装/确认 .NET 8 SDK，运行 `dotnet build` | 能解释项目文件和构建输出 |
| 4 | 学 Git 基础，查看一次 `git diff` 和提交 | 能安全保存和回退自己的改动 |
| 5 | 阅读 `Domain/`，画拖拽会话状态图 | 能指出规则应放在哪个文件 |
| 6 | 阅读 `Infrastructure/`，跟踪候选发现到进程启动 | 能说出 Windows API 依赖在哪里 |
| 7 | 阅读 `Presentation/`，运行 Debug 预览 | 能描述一个 UI 事件如何进入领域规则 |
| 8 | 阅读需求和验收矩阵，挑一个小缺陷 | 写出可执行验收条件 |
| 9 | 先写一个失败测试 | 测试稳定重现缺陷 |
| 10 | 做最小实现并运行 Release 测试 | 全部测试通过 |
| 11 | 做一次手工 UI 验收 | 记录步骤、结果和截图/日志位置 |
| 12 | 运行 `publish.ps1` 生成绿色包 | 产物和 SHA-256 可验证 |
| 13 | 阅读诊断标准，检查隐私边界 | 日志不含文件名、路径和内容 |
| 14 | 把过程写入 `docs/dev-status/YYYY-MM-DD.md` | 任何人可从记录复现你的验证 |

## “一次修好一个 bug”的检查单

1. 用最短步骤稳定复现，并写明环境、输入和预期结果。
2. 判断 bug 属于需求、领域规则、系统适配、界面状态还是发布配置；不要一上来跨层重构。
3. 先添加回归测试；如果是纯 UI 行为，写可重复手工场景。
4. 做最小改动，检查失败路径、取消路径和旧设置兼容。
5. 运行 `dotnet build`、`Tests/run-tests.ps1` 和相关手工验收。
6. 更新日态、`TODO.md`、需求/架构文档或 ADR，并保留回退方式。

## 官方资料清单（按可信度优先）

以下链接均指向资料的维护方或项目官方站点；阅读时以页面当前版本为准（整理日期：2026-09-04）。

| 主题 | 官方资料 |
| --- | --- |
| C# | [Microsoft C# Tour](https://learn.microsoft.com/dotnet/csharp/tour-of-csharp/) |
| 零基础编程 | [Harvard CS50x](https://cs50.harvard.edu/x/) |
| .NET CLI/构建/发布 | [Microsoft .NET CLI](https://learn.microsoft.com/dotnet/core/tools/)、[dotnet build](https://learn.microsoft.com/dotnet/core/tools/dotnet-build)、[dotnet publish](https://learn.microsoft.com/dotnet/core/tools/dotnet-publish) |
| WPF | [Microsoft WPF overview](https://learn.microsoft.com/dotnet/desktop/wpf/overview/) |
| 测试 | [Microsoft .NET testing](https://learn.microsoft.com/dotnet/core/testing/)、[Unit testing best practices](https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices) |
| 版本控制 | [Pro Git](https://git-scm.com/book/en/v2) |
| 协作与持续集成 | [GitHub Pull Requests](https://docs.github.com/pull-requests)、[Building and testing .NET](https://docs.github.com/actions/automating-builds-and-tests/building-and-testing-net) |
| 迭代方法 | [Agile Manifesto](https://agilemanifesto.org/)、[Scrum Guide](https://scrumguides.org/scrum-guide.html) |
| 版本约定 | [Semantic Versioning 2.0.0](https://semver.org/) |
| 变更记录 | [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)、[GitHub Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases) |
| 安全基线 | [OWASP Top 10](https://owasp.org/www-project-top-ten/)、[NIST SSDF](https://csrc.nist.gov/Projects/ssdf) |

遇到不确定的 API 或工具行为，优先回到对应官方文档和本仓库标准，不要以搜索结果中的二手博客作为最终依据。
