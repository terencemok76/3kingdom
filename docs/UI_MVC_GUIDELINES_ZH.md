# UI MVC Guidelines

本文件整理目前 `ThreeKingdom` 專案 UI 重構後的實務規則。

目標不是追求教科書式 MVC，而是提供一套適合目前 Godot 專案的穩定做法，避免後續開發又把邏輯塞回 `HudController`，讓結構重新長回大總控。

## 1. 目前 UI 結構方向

目前專案採用的是「輕量 MVC + domain controller」風格：

- `HudController`
  - 主 HUD host
  - 負責初始化、全域協調、少量 shared helper
- `XxxUiController`
  - 某個 UI domain 的 coordinator
  - 例如 `PersonnelUiController`、`SystemUiController`、`ViewUiController`
- `XxxDialogController` / `XxxPanelController`
  - 單一 dialog 或 panel 的流程控制者
- `XxxUiContext`
  - 提供 controller 所需的 state / service / host bridge
- `.tscn`
  - UI view 本體
- scene script
  - 只在真的需要時加上少量 node 封裝或視圖輔助

## 2. 分層責任

### Model

放這些：

- `WorldState`
- `CityData`
- `OfficerData`
- `ItemData`
- `TurnManager`
- `CommandResolver`
- `WorldRepository`

規則：

- 不依賴 UI
- 不操作 Godot node
- 不負責 dialog 顯示狀態

### View

包含：

- `.tscn`
- 少量 scene script，例如 `SelectOfficerDialog.cs`

規則：

- 只知道自己的 node
- 只做顯示、排版、小型 signal 轉送
- 不放遊戲規則
- 不直接執行 command

### Controller

包含：

- `XxxUiController`
- `XxxDialogController`
- `XxxPanelController`

規則：

- 處理互動流程
- 驗證輸入
- 刷新 UI
- 呼叫 model/service
- 管理該畫面的狀態

### Context

規則：

- 提供 controller 需要的最小能力
- 提供 state / service / host action bridge
- 不應該變成 `HudController` 的完整代理

## 3. HudController 應該做什麼

`HudController` 應保留以下責任：

- 持有全域 service 參考
  - `_turnManager`
  - `_localization`
  - `_worldRepository`
  - `_mapController`
- 建立各個 domain controller
- 處理主 HUD 初始化
- 處理少量跨 domain 協調
- 提供必要 shared helper

`HudController` 不應再承擔：

- 某個單一 domain 的大量 dialog widget 欄位
- 某個單一 dialog 的事件接線細節
- 某個單一 domain 的流程狀態全集
- 某個單一表格的 populate 細節
- 一整包 if/else UI 切換流程

## 4. Domain 拆分原則

一個 domain 建議至少有：

- `XxxUiController`
- `XxxUiContext`
- 1 到多個 `XxxDialogController` / `XxxPanelController`

例如：

- `system`
  - `SystemUiController`
  - `OptionDialogController`
  - `SaveLoadDialogController`
  - `SaveLoadConfirmDialogController`
- `view`
  - `ViewUiController`
  - `OfficerListDialogController`
  - `OfficerDetailDialogController`
- `main`
  - `MainHudUiController`
  - `TopBarController`
  - `CityInfoPanelController`
  - `LogPanelController`

## 5. 一個 dialog / panel 的 ownership 規則

原則只有一句：

誰的畫面，誰持有 node。

例如：

- `OptionDialog` 內的 button / slider
  - 應由 `OptionDialogController` 管
- `OfficerListDialog` 內的 table / toolbar
  - 應由 `OfficerListDialogController` 管
- `TopBar` 內的 label / button
  - 應由 `TopBarController` 管

不要再把這些 node 長期留在 `HudController`，除非它是整個 HUD 的 shared root node。

## 6. View script 規則

View script 可以做：

- `GetNodeOrNull`
- 小型顯示 helper
- 封裝固定 widget access
- 單純轉送 signal

View script 不應做：

- `ExecutePlayerCommand(...)`
- 大量 `TurnManager.World` 規則判斷
- faction / city / officer 排序邏輯
- 跨 scene / 跨 domain 控制

## 7. Context 規則

Context 適合放：

- `World`
- `Localization`
- `SelectedCity`
- `AddLog(...)`
- `PlayUiClickSfx()`
- `SelectCityById(...)`
- 少量 host bridge

Context 不適合長期放：

- 大量單一 dialog 專屬 helper
- 幾十個一對一 forwarding method
- 大量 table builder 細節
- 任意存取整個 HUD 的能力

實務判斷：

- `Context` 可以像插座
- 但不應該變成整捲延長線加轉接器集合

## 8. 新增 UI 功能時的標準流程

建議固定照以下順序：

1. 先建立 `.tscn`
2. 判斷它屬於哪個 domain
3. 建立對應 controller
4. 需要共用 state / service 時，加到該 domain `Context`
5. 由 `HudController` 只做初始化與掛接
6. 最後補 theme / localization / save-load hide 行為

這樣做可以降低再把邏輯塞回 `HudController` 的機率。

## 9. 命名規則

建議固定命名：

- domain coordinator
  - `XxxUiController`
- dialog controller
  - `XxxDialogController`
- panel controller
  - `XxxPanelController`
- context
  - `XxxUiContext`
- access bridge
  - `HudController.XxxAccess.cs`
- scene script
  - 跟 scene 同名

例如：

- `ViewUiController`
- `OfficerListDialogController`
- `OfficerDetailDialogController`
- `ViewUiContext`

## 10. 狀態應該放哪裡

### 放在 Model

- 世界資料
- 城市資料
- 官員資料
- 道具資料

### 放在 UiController / DialogController

- 畫面模式
- 目前選擇狀態
- pending UI flow state
- dialog open / close 狀態

### 放在 HudController

- 全域 service 參考
- domain controller 實例
- 少量 shared flow state

不要混在一起：

- UI 選取狀態
- 世界規則狀態
- 存檔資料狀態

## 11. Helper 抽取原則

可以抽 helper 的時機：

- 同樣 table row styling 重複出現
- 同樣 sortable title builder 重複出現
- 同樣 item/officer summary formatting 重複出現
- 同樣 dialog theme 重複出現

不要太早抽的東西：

- 抽象但只用一次的 base class
- 沒有明確使用者的 generic utility
- 為了看起來像 MVC 而做的空洞介面

## 12. 什麼時候該停下來重構

看到以下情況就該停一下：

- `HudController` 又開始新增一串 `_button/_label/_dialog`
- 新功能需要修改多個無關 domain
- 單一 controller 同時管太多不相關 dialog
- `Context` 已經變成大代理
- 單一方法同時混合 UI、規則、資料整理而且很長

## 13. 目前專案的建議節奏

目前 UI refactor 已經進入「可用結構」階段。

現階段建議：

- 繼續用這套結構開發功能
- 先不要為了更純而過度拆分
- 當某個 domain 真正卡手時，再做下一輪小重構

不建議現在做的事：

- 為了理論純度，把所有 helper 都抽成很多小類別
- 把 `Context` 再包成多層抽象
- 沒有實際痛點就大改 `move/attack` 深層流程

## 14. 當前實務結論

最適合這個專案的是：

- `HudController` 當 host
- 每個 domain 一個 `UiController`
- 每個主要 dialog / panel 一個 controller
- `Context` 當 bridge
- table/data helper 先留在 domain 內的 helper file
- 真的重複了再抽共用層

這樣的做法比「很重的教科書式 MVC」更符合目前專案狀態，也比較不容易再長回大總控。
