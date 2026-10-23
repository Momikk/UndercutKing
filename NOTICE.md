# 来源与许可

压价之王 UndercutKing 由 Momikk 开发和维护，Copyright (C) 2026 Momikk，采用 GNU Affero General Public License version 3 or later（AGPL-3.0-or-later）。完整条款见 LICENSE.md。本软件不提供任何担保。

库存容器 Hook、市场请求 Hook、雇员销售列表顺序映射与高亮交互部分改编自 Critical_Impact 的 Allagan Market（https://github.com/Critical-Impact/AllaganMarket），其原有许可为 AGPL-3.0-or-later，原作者贡献属于原作者。基线为上游 ae150f9366e3c42173362c566f7817c8de56fbd7，以及 Momikk fork 987f688 的国服 7.57 签名修复。

2026-10-13：Momikk 提取并重写为独立插件，移除销售历史、统计、CSV、DalaMock、AllaganLib 和后台 WebSocket；改为当前雇员售价读取、公开 Universalis 查询、游戏市场响应缓存和可恢复的界面高亮。保留原项目来源与许可。

发布二进制时，对应版本的完整源码与构建说明在 RepoUrl 指向的仓库提供。依赖 Dalamud SDK 与 FFXIVClientStructs，遵守各自许可，不在安装包中分发框架程序集。

2026-10-14，0.1.0.7：新增独立实现的 Universalis BSON 消息订阅、批量查询与全局退避。协议及处理思路参考 AllaganLib.Universalis 2.2.0（提交 5b3f414b0796a70ef88d9d77b42ea3c779dcaa16）和 Allagan Market，不引入 AllaganLib 程序集。推送仅触发已扫描商品的完整报价补查，不以增量消息直接替换完整行情。

安装包包含未修改的 MongoDB.Bson 3.5.2，Copyright © 2010-present MongoDB Inc.，采用 Apache-2.0；来源与完整许可见 [THIRD-PARTY-LICENSES.md](THIRD-PARTY-LICENSES.md)。压价之王本身继续采用 AGPL-3.0-or-later。

2026-10-15，0.1.0.10：新增背包匹配格子的临时描框显示，布局与排序映射研究参考 HaselTweaks 的 InventoryHighlight（https://github.com/Haselnussbomber/HaselTweaks，AGPL-3.0）及 FFXIVClientStructs 的 ItemOrderModule/AddonInventoryGrid 结构；格子描框、当前雇员筛选及设置由 Momikk 独立实现，不引入上述插件程序集。

2026-10-17，0.1.0.13：Momikk 将在售数据读取与出售列表显示顺序分离，召唤雇员进入主菜单后即可读取有效记录；增加当前雇员加载归属、在售数量核对及显示顺序验证。沿用前述 Allagan Market 库存交互来源与许可。

2026-10-18，0.1.0.14：Momikk 参考 Allagan Market 的 RetainerMarketService 菜单生命周期读取与 MarketPriceUpdaterService.PostOpen，在 SelectString 显示和关闭前刷新当前雇员记录；保留本插件的槽位映射、有效售价和数据隔离检查。

2026-10-18，0.1.0.16：Momikk 将设置重构为独立实现的原生左侧分类窗口。原生组件使用 KamiToolKit（MidoriKami，MIT），采用套装之王中的 API 15 源码快照，依赖程序集独立命名为 UndercutKing.KamiToolKit。完整来源、修改和许可见 THIRD-PARTY-LICENSES.md 与 vendor/KamiToolKit/LICENSE。

2026-10-18，0.1.0.17：Momikk 将原生设置改为顶部标签，修复 KamiToolKit CheckboxNode.DisableAutoResize 读取值与设置值相反造成的标签截断，扩大帮助按钮以容纳文字。

2026-10-23，0.1.0.21：Momikk 增加可选的回收之王 IPC 兼容。背包蓝框排除正在使用粉色回收标记的物品；无对端或不可调用时保留原有行为。
