# 开发与验证

## 构建

需要 .NET 10 SDK 与 API 15 的 Dalamud 开发程序集。

```powershell
.\build.ps1
dotnet run --project tests/PriceRules.Tests.csproj -c Release
```

脚本优先使用 XIVLauncherCN，或通过 `DALAMUD_HOME` 指定开发程序集目录。产物位于 `UndercutKing/bin/Release/`，SDK 生成的安装 ZIP 为 `UndercutKing/bin/Release/UndercutKing/latest.zip`。完整源代码包含 tests、scripts、许可和构建配置。

`scripts/check-signatures.py` 只读检查本机国服游戏文件，确认两条 Hook 特征在 .text 中唯一匹配。游戏更新可能改变内部结构或签名；静态匹配和编译通过不等同于游戏内验证。


## 验证记录

构建、价格判断回归测试、静态签名检查及安装包核对见 [validation.md](validation.md)。实机交互的验证状态以该记录为准。

原生设置窗口使用 vendor/KamiToolKit 源码，构建会生成独立的 UndercutKing.KamiToolKit.dll。原生窗口和节点操作应在 Framework 线程执行；关闭与卸载需等待节点销毁。额外生命周期检查：dotnet run --project tests-native-ui/NativeUiLifecycle.Tests.csproj -c Release（先运行 Release 构建）。
