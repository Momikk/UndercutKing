# 来源与许可

压价之王 UndercutKing 由 Momikk 开发和维护，Copyright (C) 2026 Momikk，采用 GNU Affero General Public License version 3 or later（AGPL-3.0-or-later）。完整条款见 LICENSE.md。本软件不提供任何担保。

库存容器 Hook、市场请求 Hook、雇员销售列表顺序映射与高亮交互部分改编自 Critical_Impact 的 Allagan Market（https://github.com/Critical-Impact/AllaganMarket），其原有许可为 AGPL-3.0-or-later，原作者贡献属于原作者。基线为上游 ae150f9366e3c42173362c566f7817c8de56fbd7，以及 Momikk fork 987f688 的国服 7.57 签名修复。

2026-10-13：Momikk 提取并重写为独立插件，移除销售历史、统计、CSV、DalaMock、AllaganLib 和后台 WebSocket；改为当前雇员售价读取、公开 Universalis 查询、游戏市场响应缓存和可恢复的界面高亮。保留原项目来源与许可。

发布二进制时，对应版本的完整源码与构建说明在 RepoUrl 指向的仓库提供。依赖 Dalamud SDK 与 FFXIVClientStructs，遵守各自许可，不在安装包中分发框架程序集。
