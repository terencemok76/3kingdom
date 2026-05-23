# UI Event / Observe 指南

本文整理 `ThreeKingdom` 目前 UI 層的 `event / observe` 規則，目標是減少 dialog 之間彼此直接呼叫 refresh，降低維護成本。

## 1. 目的

以前許多 UI 在操作成功後，會直接呼叫其他 UI 的 refresh，例如：

- `AssignRoleDialogController -> RefreshViewDialogIfOpen()`
- `XxxDialogController -> RefreshSelectedCity()`

這種做法短期可用，但當 UI 變多後會有幾個問題：

- dialog 彼此耦合
- 後續新增 observer 時，容易到處補 hardcode
- 很難追蹤「哪個操作應該刷新哪些 UI」

因此目前改成：

- 操作成功的 dialog 負責 `publish event`
- 已開啟的 UI 自己 `observe event`
- observer 自行決定要不要 refresh

## 2. 核心元件

事件中心：

- [UiEventHub.cs](/D:/sandbox_ai/godot/3kingdom/scripts/ui/UiEventHub.cs:1)

目前 `HudController` 內持有單一共用 hub：

- [HudController.cs](/D:/sandbox_ai/godot/3kingdom/scripts/ui/HudController.cs:240)

各 domain `UiContext` 若需要發事件，應暴露：

- `public UiEventHub UiEventHub => _owner.UiEventHub;`

## 3. 目前事件

### `CityStateChanged`

用途：

- 城市資源、兵力、忠誠、排程、城市持有人等狀態變化

payload：

- `CityId`
- `FactionId`

適合情境：

- `Merchant`
- `Move`
- `Civil Relief`
- `Diplomacy`
- `Internal Affairs`
- `Search / Visit Citizen`

### `OfficerStateChanged`

用途：

- 單一武將狀態變化，但不一定是職位變化

payload：

- `OfficerId`
- `CityId`
- `FactionId`

適合情境：

- `Hire Officer`
- `Fire Officer`
- `Recruit`
- `Attack`
- `Move`
- `Spy`
- `Personnel Bonus`
- `Request Item`

### `OfficerAppointmentsChanged`

用途：

- 武將職位或兼任職位變化

payload：

- `OfficerId`
- `CityId`
- `FactionId`

適合情境：

- `AssignRoleDialogController`

### `FactionLeadershipChanged`

用途：

- 勢力領導層變化

payload：

- `FactionId`
- `CityId`

適合情境：

- `Chancellor` / `Chief Strategist` 任命
- `Succession`

## 4. Publish 規則

原則：

- 只有「操作成功」才 publish
- publish 應放在 dialog/controller 成功結果分支裡
- publish 後不再直接 hardcode 呼叫其他 UI refresh

範例：

```csharp
if (result.Success)
{
    _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
    _context.UiEventHub.PublishOfficerStateChanged(officerId, city.Id, city.OwnerFactionId);
}
```

如果是職位變動：

```csharp
if (result.Success)
{
    _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
    _context.UiEventHub.PublishOfficerAppointmentsChanged(officerId, sourceCityId, city.OwnerFactionId);
}
```

如果是勢力領導層變動：

```csharp
if (result.Success)
{
    _context.UiEventHub.PublishFactionLeadershipChanged(factionId, cityId);
}
```

## 5. Observe 規則

observer 原則：

- 只在 `Initialize()` 訂閱
- 只在 `Shutdown()` 取消訂閱
- UI 沒開著時，可以直接忽略事件
- observer 應只做自己責任範圍內的 refresh

範例：

```csharp
public void Initialize()
{
    _uiEventHub.CityStateChanged += OnWorldStateChanged;
}

public void Shutdown()
{
    _uiEventHub.CityStateChanged -= OnWorldStateChanged;
}
```

## 6. 目前已 observe 的 UI

### Main HUD / City Info

- [MainHudUiController.cs](/D:/sandbox_ai/godot/3kingdom/scripts/ui/main/MainHudUiController.cs:1)

目前會 observe：

- `CityStateChanged`
- `OfficerStateChanged`
- `OfficerAppointmentsChanged`
- `FactionLeadershipChanged`

收到後行為：

- `RefreshSelectedCity()`

### 查看武將 / 武將詳情

- [ViewUiController.cs](/D:/sandbox_ai/godot/3kingdom/scripts/ui/view/ViewUiController.cs:1)
- [OfficerDetailDialogController.cs](/D:/sandbox_ai/godot/3kingdom/scripts/ui/view/OfficerDetailDialogController.cs:1)

目前會 observe：

- `CityStateChanged`
- `OfficerStateChanged`
- `OfficerAppointmentsChanged`
- `FactionLeadershipChanged`

收到後行為：

- 若 `查看武將` 開著，refresh list
- 若 `武將詳情` 開著，refresh shown officer

### Advisor

- [AdvisorUiController.cs](/D:/sandbox_ai/godot/3kingdom/scripts/ui/advisor/AdvisorUiController.cs:1)

目前會 observe：

- `CityStateChanged`
- `OfficerStateChanged`
- `OfficerAppointmentsChanged`
- `FactionLeadershipChanged`

收到後行為：

- 若 `AdvisorDialog` 開著，refresh text / button state / history display

## 7. 目前已 publish event 的入口

目前已改成 publish 的操作包括：

- `Assign Role`
- `Personnel Bonus`
- `Fire Officer`
- `Request Item`
- `Hire Officer`
- `Succession`
- `Civil Relief`
- `Visit Citizen`
- `Merchant`
- `Diplomacy`
- `Spy`
- `Internal Affairs`
- `Move`
- `Recruit Troop`
- `Attack`
- `View dialog` 內的 command-selection confirm

## 8. 何時應新增事件

如果某個新操作成功後，會影響：

- 城市資源
- 武將位置
- 武將忠誠/狀態
- 武將職位
- 勢力領導層

就應優先考慮 publish 既有事件，而不是直接呼叫某個 UI controller。

只有在既有事件無法清楚表意時，才新增新事件。

## 9. 不建議的做法

避免：

- `DialogA` 直接呼叫 `DialogB.Refresh...()`
- 某個 domain controller 知道太多別的 UI 細節
- 一個成功操作後，手動串很多 `RefreshSelectedCity()`、`RefreshViewDialogIfOpen()`、`RefreshAdvisorIfOpen()`

這些都會讓 UI 維護成本快速上升。

## 10. 下一步建議

後續若再擴充，可優先沿用這份規則：

1. 先判斷是否可重用既有 4 個事件
2. publish 放在成功分支
3. observer 只 refresh 自己
4. UI 沒開時直接忽略事件

這樣可以讓整個專案的 UI 同步邏輯維持一致。
