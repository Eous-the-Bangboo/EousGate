# 开发日态补充：单实例与正式版本启动治理

## 实际改动

- `App.xaml.cs` 使用命名 Mutex `Local\\EousGate.SingleInstance`，第二个 EousGate 启动后立即退出。
- 发布验证使用强制 `Rebuild`，避免增量构建缓存继续复用旧 DLL 或 apphost。
- 启动前检测并关闭旧的 EousGate 进程，确保正式 Release 目录只保留一个运行实例。

## 验证

- Debug/Release 构建和自动化测试均通过。
- 正式启动后检查进程数量为 1。
- 强制 Release Rebuild 后 `bin\\Release\\net8.0-windows\\EousGate.dll` 与 `EousGate.exe` 同时更新到 11:33:58。
- 连续启动两次正式 EXE 后，进程检测仍为 1 个实例。
