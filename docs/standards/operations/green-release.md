# 绿色发布

## 目标

EousGate 首版发布为 Windows 11 x64 自包含绿色包。目标电脑不需要预先安装 .NET 8，程序不写入开机启动项、不修改 Windows 默认文件关联，也不依赖网络资源。

## 构建

在项目根目录运行：

```powershell
.\publish.ps1
```

脚本会先运行 Release 自动化测试，再执行 `win-x64`、`Release`、`self-contained` 发布，输出到 `artifacts\EousGate-win-x64`，并根据项目版本生成 `EousGate-v<version>-win-x64.zip` 及配套的 `.sha256` 校验文件。若发布目录仍被运行中的 EousGate 占用，脚本会提示先退出程序。如需跳过重复测试，仅在已完成同一源码版本验证后使用：

```powershell
.\publish.ps1 -SkipTests
```

## 包内容

发布目录只保留发布命令生成的运行文件，不应加入 `bin/`、`obj/`、`.tmp-build4/`、测试输出或本机设置文件。发布前应在未安装 .NET 8 Desktop Runtime 的 Windows 11 x64 环境启动 `EousGate.exe`，并按首版验收矩阵记录结果。

## 当前限制

- DPI 100%/125%/150% 视觉验收暂缓，仍由 TODO-006 跟踪。
- 多显示器、文件夹、自动启动和 Windows 默认关联不属于当前范围。
