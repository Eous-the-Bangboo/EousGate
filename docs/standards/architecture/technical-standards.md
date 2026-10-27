# 技术与架构标准

v0.2 助手扩展遵循 [ADR 0004](../../adr/0004-assistant-host.md)。工具生命周期、模块隔离和新数据目录以该决策为准。
外部工具监控与进程控制遵循 [ADR 0005](../../adr/0005-external-program-control.md)，仅管理匹配路径、会话和当前用户的应用进程。
自定义普通 EXE 的配置、动态注册和停止策略遵循 [ADR 0006](../../adr/0006-custom-program-monitoring.md)。
米哈游启动器接力识别与访问拒绝后的单次授权关闭遵循[ADR 0007](../../adr/0007-launcher-handoff-and-elevated-stop.md)，不改变默认强制策略、不提升主助手或递归控制游戏进程。

文件夹快跳接收按钮使用独立STA消息循环，避免被宿主WPF标准文件选择框的同线程模态激活循环抢占；点击后通过宿主Picker.BeginInvoke派发，停止时关闭并等待按钮线程释放。文件夹历史、Picker和后台采样仍归原有宿主生命周期，不把按钮线程扩大为另一套历史写入者。

## 分层

- `Domain/` 只表达拖拽会话、候选软件、打开结果、用户设置和接口契约。
- `Infrastructure/` 承担注册表、Windows Shell、进程启动、文件存储和托盘适配。
- `Presentation/` 只负责 WPF 窗口、事件、状态展示和用户操作。
- Windows API、注册表和 COM 调用不得进入领域层。

## 行为约束

- 接收条新范围用可空 `StartPercent`/`EndPercent` 保存，UserSettings新建各侧显式0/100；双空识别旧LengthPercent/OffsetPixels配置。运行时统一由EdgeBandLayoutMath计算物理边界，编辑旧配置时按实际夹取后的位置迁移，不能以默认0/100覆盖旧局部范围。新端点不合法时加载回退整条，界面提交则拒绝并保留草稿。

- `DragSession` 状态必须有明确的开始、取消、失败和完成转移。
- `DragSession` 保存不可变、有序且非空的文件集合；打开动作不得拆批或逐文件启动。
- 软件候选按可用性过滤并去重；失效路径不能直接启动。
- 多类型文件的候选必须按每种扩展名应用设置后求交集；同一自定义候选的启动参数不一致时不得进入交集。
- 打开动作必须安全传递文件参数，使用选定软件的工作目录，不修改系统默认关联。
- 打包应用必须按真实 AUMID 匹配 Shell 处理器，传递 Shell 原生文件对象；多文件先确认处理器支持选区。不得以 Explorer 启动成功代替文件交付成功，见 [`ADR 0002`](../../adr/0002-packaged-file-invocation.md)。
- 设置读取失败回退到默认值；写入失败不得让 UI 崩溃，并应在诊断开启时提供本机错误信息。
- 外部系统错误转换为领域可处理的失败结果，禁止用空 catch 掩盖关键流程状态。

## 隐私与依赖

默认不记录文件路径、文件名和内容；首版不引入网络服务或远程资源。

## 文件类型推荐（beta.12）
- Domain/FileTypeCatalog 提供本地常见类型目录；AppRecommendationPolicy 为无平台依赖的保守推荐策略。Infrastructure/AppDiscovery 读取真实默认／Shell 关联，再应用推荐和用户覆盖；不凭空增加未注册软件、不修改系统默认关联。
- 复用 FileTypePreference 的排序／隐藏／CustomApps，无新增持久化字段。显式排序保留旧配置行为，默认空排序持续自动推荐。
