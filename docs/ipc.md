# UndercutKing IPC

从 0.1.1.2 起提供两个 Dalamud IPC 接口。调用方安装并启用压价之王即可使用，无需引用其程序集。

| 接口 | 签名 | 用途 |
| --- | --- | --- |
| `UndercutKing.GetSnapshot` | `() => string` | 返回当前角色的 JSON 快照，`apiVersion` 为 `1`。 |
| `UndercutKing.RequestRefresh` | `() => bool` | 请求刷新当前角色已扫描商品的 Universalis 报价。 |

```csharp
var snapshot = pluginInterface.GetIpcSubscriber<string>("UndercutKing.GetSnapshot");
var refresh = pluginInterface.GetIpcSubscriber<bool>("UndercutKing.RequestRefresh");
using var report = System.Text.Json.JsonDocument.Parse(snapshot.InvokeFunc());
// 用户主动点击刷新时再调用。
bool accepted = refresh.InvokeFunc();
```

快照约每 250 毫秒更新一次。登出时返回 `characterId: "0"` 和空列表，登录后雇员数据就绪才会列出雇员。角色和雇员 ID 用十进制字符串表示。

```json
{
  "apiVersion": 1,
  "characterId": "123",
  "retainers": [{
    "retainerId": "456",
    "name": "我的雇员",
    "capturedAt": "2026-11-17T10:00:00+00:00",
    "undercutCount": 1,
    "unknownCount": 0,
    "listings": [{
      "slot": 4,
      "itemId": 100,
      "hq": false,
      "quantity": 2,
      "unitPrice": 1878,
      "priceStatus": "undercut",
      "highlightStatus": "bagMatch"
    }]
  }]
}
```

- 同一雇员内用 `slot`（0–19 的库存槽位）区分同一物品的不同挂牌。数据来自该雇员的最近一次扫描，`capturedAt` 是扫描时间。尚未扫描时，`capturedAt`、两项计数均为 `null`，挂牌列表为空。确认无货时计数为 0。
- `priceStatus` 为 `normal`、`undercut` 或 `unknown`。筛选 `priceStatus == "undercut"` 即得被压价的挂牌，计数按挂牌计算。已确认的展示非卖品不在结果中。
- `highlightStatus` 是按商品高亮开关和当前背包匹配上下文计算的出售列表颜色。值为 `normal`（原色）、`undercut`（红）、`unknown`（黄）或 `bagMatch`（蓝）。例如同一条挂牌可以被计入压价，界面颜色按优先级显示蓝色。这个字段不代表对应出售窗口正在打开。
- `RequestRefresh` 返回 `true` 表示已接收请求，结果随快照更新。它与设置中的「刷新报价」共用请求队列和限流规则，同批请求会合并。无可刷新挂牌时返回 `false`，切换角色或登出会清除待处理请求。
- 压价之王未加载或版本过旧时，调用方应处理 IPC 未注册异常。
