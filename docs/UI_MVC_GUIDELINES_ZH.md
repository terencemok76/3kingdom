# UI MVC 指南

本文整理 `ThreeKingdom` 目前 UI 層的實作約定，特別是已完成的 `Window -> Control + PanelContainer` 浮動視窗重構。

## 1. 目標

UI 層的設計目標：

- 保持 Godot 節點結構清楚
- 把 UI 顯示邏輯與遊戲資料邏輯分開
- 避免 `HudController` 直接承擔所有 dialog 細節
- 讓新 UI 可以共用拖曳、置中、關閉、置頂、樣式等能力

目前採用：

- MVC 風格切分
- `HudController` 作為 host
- 各 domain 使用 `UiController + DialogController + UiContext`
- 浮動視窗統一走 `FloatingOverlayController`

## 2. 分層

### Model

負責資料與規則：

- `WorldState`
- `CityData`
- `OfficerData`
- `ItemData`
- `FactionData`
- `TurnManager`
- `CommandResolver`
- `WorldRepository`

Model 不應依賴：

- Godot `Node`
- `.tscn`
- dialog 顯示狀態

### View

負責畫面結構與基礎元件：

- `.tscn`
- 少量 scene script

View 應負責：

- 節點樹
- 基本 signal 發出
- 純視覺層元件組合

View 不應負責：

- 執行 command
- 直接讀寫 `TurnManager.World`
- 判斷 faction / city / officer 規則

### Controller

負責 UI 行為與流程：

- `XxxUiController`
- `XxxDialogController`
- `XxxPanelController`

Controller 應負責：

- 初始化 widget
- 綁定 signal
- 將 model/state 映射到 UI
- 驅動跨 dialog 流程

### Context

`XxxUiContext` 是 dialog controller 與 `HudController` / service 之間的 bridge。

Context 應負責：

- 暴露 `World` / `Localization` / `SelectedCity`
- 暴露 `AddLog()`、`RefreshSelectedCity()`、`PlayUiClickSfx()` 等 host 能力
- 把 `HudController` 的存取包成較小且可理解的方法

Context 不應變成：

- 第二個 `HudController`
- 超大型 helper 集合

## 3. HudController 的角色

`HudController` 是 UI host，不是每個 dialog 的直接實作者。

它應負責：

- 持有 shared service 與 state
- 建立各 domain `UiController`
- 提供跨 domain 的 bridge
- 持有 HUD 根節點與共享視覺 helper

它不應負責：

- 每個 dialog 的 widget 初始化
- 每個 dialog 的所有 signal 綁定
- 每個畫面的所有 populate / refresh 細節

## 4. Domain 結構

每個 domain 建議最少包含：

- `XxxUiController`
- `XxxUiContext`
- 一個以上 `XxxDialogController` 或 `XxxPanelController`

例子：

- `advisor`
- `civil`
- `diplomacy`
- `internal_affairs`
- `merchant`
- `military`
- `personnel`
- `spy`
- `system`
- `view`

## 5. 浮動視窗標準

目前大多數 UI dialog 已改成浮動 overlay，不再依賴 `Window`。

共用基底：

- [FloatingOverlayController.cs](/D:/sandbox_ai/godot/3kingdom/scripts/ui/FloatingOverlayController.cs:1)

### 標準節點結構

建議 scene 結構：

1. `OverlayRoot`：`Control`
2. `CenterContainer`：`Control`
3. `AdvisorDialogPanel`：`PanelContainer`
4. `AdvisorDialogRoot`：`VBoxContainer`
5. `TitleBarPanel`
6. `TitleBar`
7. `TitleLabel`
8. `CloseButton`

目前 `FloatingOverlayController` 預設就是用以下路徑抓節點：

- `CenterContainer/AdvisorDialogPanel`
- `CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot`
- `CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/TitleBarPanel/TitleBar`

所以新 scene 應盡量遵守這套命名。

### 共用能力

`FloatingOverlayController` 目前提供：

- overlay 建立
- 顯示 / 隱藏
- title bar 拖曳
- 點擊置頂
- 第一次打開預設置中
- 初次 layout 穩定前持續置中
- 視窗 clamp 在可視範圍內
- close button 行為
- `OptionButton` 共用強化樣式

### 適用情境

優先使用 `FloatingOverlayController` 的情況：

- 遊戲內浮動命令視窗
- 可以和其他 UI 重疊的非 modal 面板
- 需要拖曳、置頂、保留 session 內位置的 dialog

仍可保留 `Window` 的情況：

- 原生 popup 行為更合適
- 短期內不值得重構
- 特殊平台行為仍依賴 Godot `Window`

## 6. 顯示與關閉規則

### Show 流程

推薦順序：

1. `EnsureOverlayReady()`
2. `Populate()`
3. `RefreshText()`
4. `ShowOverlay()`

理由：

- 避免 `RefreshText()` 先讀到尚未填入的 `OptionButton`
- 避免 `Selected = -1` 時提前讀 metadata

### Hide 流程

若 Confirm 之後要切到下一個 dialog：

- 依需求決定主命令視窗是否保留
- 若怕同一個 click 造成穿透，可改用 deferred 切換

目前：

- `Military` / `Personnel` / `Civil` 主命令視窗在 confirm 後會保留開著
- 子 dialog 另行打開

## 7. 樣式規則

### 按鈕

多數命令按鈕直接複用 `City Info` 按鈕風格。

建議：

- 透過 `HudController.XxxAccess.cs` 的 theme helper 套用
- 不要每個 dialog 各自複製一套顏色常數

### Dropdown / OptionButton

現在 overlay 內的 `OptionButton` 會由 `FloatingOverlayController` 自動套用共用樣式：

- 深色底
- 金棕色邊框
- hover / focus 時邊框更亮
- disabled 狀態可辨識

如果某個 dialog 需要特殊輸入樣式：

- 可以在 scene 自己 override
- 但要確認不會破壞整體 UI 一致性

### 表格 / Tree / ItemList

表格配色與選中列建議集中放在 helper：

- `HudController.ViewTableHelpers.cs`
- `HudController.Presentation.cs`
- `SelectOfficerDialog.cs`

不要把同一套 row striping 複製到多個 dialog。

## 8. Context 規則

`UiContext` 應該是薄 bridge，不應塞進大量業務判斷。

適合放在 context 的內容：

- `TurnManager`
- `Localization`
- `SelectedCity`
- `PopupDialog(...)`
- `BringOverlayToFront(...)`
- `AddLog(...)`
- `ShowOfficerSelectorDialog(...)`

不適合放在 context 的內容：

- 複雜 table 排序規則
- 大量 advice / AI / strategy 決策
- 完整 command 流程編排

那些應回到 dialog controller 或 domain helper。

## 9. 新增 UI 的建議流程

新增一個新浮動 dialog，建議步驟：

1. 建 `.tscn`
2. root 採用標準 overlay 節點結構
3. 新增 `XxxDialogController`
4. 繼承 `FloatingOverlayController`
5. 在 `OnOverlayContentReady()` 綁定節點
6. 把 host bridge 放進 `XxxUiContext`
7. 在 `XxxUiController` 中接入
8. 在 `HudController` 建立 access method

## 10. 已完成的重構方向

目前專案已大量將 `Window` 轉為 `PanelContainer` overlay。

已採用共用 overlay 邏輯的類型包含：

- `Advisor`
- `Merchant`
- `Internal Affairs`
- `Diplomacy`
- `Spy`
- `Civil`
- `Military`
- `Personnel`
- `View`
- 多數次級 command dialog

因此後續新 UI 應優先沿用 overlay 架構，而不是回到 `Window + PopupCentered()`。

## 11. 常見錯誤

### 1. Refresh 太早

問題：

- `RefreshText()` 先於 `Populate()` 執行
- `OptionButton.Selected == -1`
- `GetItemMetadata(-1)` 直接炸錯

做法：

- 先 populate 再 refresh
- 對 `ItemCount == 0` / `Selected < 0` 加防呆

### 2. 把所有邏輯塞回 HudController

問題：

- 維護困難
- domain 邊界模糊

做法：

- 流程放 `UiController`
- dialog 細節放 `DialogController`
- `HudController` 保持 host 角色

### 3. 每個 dialog 自己發明一套樣式

問題：

- 視覺不一致
- 之後難統一

做法：

- 按鈕共用主 HUD 樣式
- dropdown 走 overlay base 樣式
- 表格配色集中管理

## 12. 文件更新原則

當以下任一項發生時，應同步更新本文件：

- 新的 UI 架構被採納
- `FloatingOverlayController` 能力有新增
- 大量 dialog 從 `Window` 改成 overlay
- theme / input / positioning 規則改變
- 顯示流程與互動規則改變

本文件的目的不是列出所有檔案，而是保證之後做 UI 時，大家遵循的是同一套實際存在的規則。
