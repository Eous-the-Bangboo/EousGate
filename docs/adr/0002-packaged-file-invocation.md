# 使用真实应用标识和 Shell 原生文件对象打开打包应用

状态：已接受，2026-09-27。

## 问题

`IAssocHandler.GetName` 在本机“照片”上返回中文名称，不能当作 AUMID。间接图标资源也不能用来推断应用标识。旧实现拼接 `explorer.exe shell:AppsFolder\照片`，并把 Explorer 启动成功当成文件打开成功。

本机 Photos 2026.11080.24002.0 使用 `Windows.FullTrustApplication`。实测向其真实 AUMID 调用 `IApplicationActivationManager.ActivateForFile`，无论 `open` 还是空 verb 都返回 `0x80270254`，不能靠修正标识解决全部问题。

## 决定

- 发现时用 `IObjectWithAppUserModelID.GetAppID` 读取标识；AppX ProgID 可以解析其注册的 `Application\AppUserModelID`。
- 保留软件选项原有 ID，以兼容已保存的排序和隐藏设置。展示名称与调用标识分开处理。
- 打开时重新枚举文件关联，按真实 AUMID 精确匹配所选软件。
- 用 Shell 创建文件数组，并通过 `BHID_DataObject` 获得原生 `IDataObject` 指针。单文件调用 `IAssocHandler.Invoke`；多文件调用 `CreateInvoker`，确认 `SupportsSelection == S_OK` 后一次性交付整个集合。
- 不把托管 WPF DataObject 当作 Shell 原生对象传入。所有 COM 对象、原生接口引用和 PIDL 均按生命周期释放。
- 解析失败、应用移除、选区不支持或调用失败返回失败；不启动 Explorer，也不改用系统默认应用。

## 验证与限制

本机单 JPG、JPG+PNG、不同目录图片均获 Shell 成功响应；同目录 JPG+PNG 已逐窗口确认图片内容。拖拽输入仍复用既有面板流程，完整鼠标拖拽回放及其他 Windows/Photos 版本仍需手工覆盖。

多文件只调用一次处理器，最终窗口数量由目标应用决定；本机 Photos 为两张图片分别打开窗口。

## 参考

- [IObjectWithAppUserModelID](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-iobjectwithappusermodelid)
- [IAssocHandler.Invoke 与多文件调用约定](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-iassochandler-invoke)
- [IShellItemArray.BindToHandler](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellitemarray-bindtohandler)

