# 压价之王 UndercutKing

<img src="images/icon.png" alt="压价之王图标" width="128" />

轻量 Dalamud 插件，专注雇员商品的压价检测和高亮。作者及维护者：Momikk。版本 0.1.0.1，面向国服当前 Dalamud API 15。

## 功能

读取当前雇员销售物品与单价，按相同品质（HQ/NQ）比对市场最低价，在游戏原生销售列表和雇员列表中提示状态。红色表示被压价，黄色表示价格未知或过期。修改售价后自动重新读取游戏中的单价，解除压价时恢复普通颜色。设置关闭或插件卸载时恢复插件改动的文本与颜色。

自动查询 Universalis，并使用玩家手动打开“比较价格”后收到的完整游戏市场列表。Universalis 可能滞后；未知、过期和不完整报价不会假装是实时行情。每个物品有查询间隔保护，并限制并发请求。

服务器信息栏默认显示当前角色的被压价上架位数量，点击可打开设置。`压价 N+` 表示还有待刷新物品，`压价 ?` 表示尚未确认；可在设置里关闭。数量随改价、市场响应和缓存过期重新计算。

只缓存已扫描的雇员当前上架快照，供雇员列表判断使用；不保存销售历史，不提供统计、CSV、自动改价、自动操作雇员或交易功能。

## 安装

在 Dalamud 设置 → 第三方插件仓库中使用统一库链：

```text
https://raw.githubusercontent.com/Momikk/DalamudPlugins/main/pluginmaster.json
```

刷新后搜索“压价之王”或 UndercutKing。安装后依次打开各雇员的销售列表进行初始扫描。输入 `/uking` 或 `/undercutking` 打开设置与当前物品价格状态。

请先关闭 Allagan Market 的物品及雇员高亮，或禁用该插件，以免两款插件同时修改相同游戏文本。此插件不是 AllaganMarket 的更新包，InternalName 独立，原插件配置不会迁移。

加载插件时雇员窗口已经打开的，请退出雇员交互后重新召唤，使库存数据重新到达。尚未扫描或价格过期时出现黄色是预期行为。退出、移除或出售商品的快照，在下次打开该雇员销售列表时更新。

## 构建

需要 .NET 10 SDK 与 API 15 的 Dalamud 开发程序集。

```powershell
.\build.ps1
dotnet run --project tests/PriceRules.Tests.csproj -c Release
```

脚本优先使用 XIVLauncherCN，或通过 `DALAMUD_HOME` 指定开发程序集目录。产物位于 `UndercutKing/bin/Release/`，SDK 生成的安装 ZIP 为 `UndercutKing/bin/Release/UndercutKing/latest.zip`。完整源代码包含 tests、scripts、许可和构建配置。

`scripts/check-signatures.py` 只读检查本机国服游戏文件，确认两条 Hook 特征在 .text 中唯一匹配。游戏更新可能改变内部结构或签名；静态匹配和编译通过不等同于游戏内验证。

## 许可与来源

采用 **AGPL-3.0-or-later**。库存与界面交互部分改编自 Critical_Impact 的 [Allagan Market](https://github.com/Critical-Impact/AllaganMarket)，独立提取和修改由 Momikk 完成。完整来源说明见 [NOTICE.md](NOTICE.md)，许可证见 [LICENSE.md](LICENSE.md)。对应安装包的完整源码在本仓库提供。

## 验证状态

构建、价格判断回归测试、静态签名检查及安装包核对见 [docs/validation.md](docs/validation.md)。首版仍需游戏内验收：首次扫描、改价后解除红色、重新比较市场价格、切换雇员、禁用高亮和卸载恢复。不能把构建成功作为上述交互已通过的证据。
