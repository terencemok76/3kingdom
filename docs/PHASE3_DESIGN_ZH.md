# Phase 3 設施、建設與徵兵系統設計

更新日期：2026-05-29

## 1. 文件定位

- 本文件整理 Phase 3 目前已落地與預計延伸的：
  - 城市設施
  - 建設點
  - 徵兵條件與成本
  - 攻城加成
  - `View / 城市資訊` 顯示
- 若與舊文件描述不一致，以本文件與目前程式行為為準。

## 2. 目標

- 把 `Construction` 從抽象內政，擴充為可指定設施的城市建設系統。
- 讓特殊兵種解鎖與城市基礎建設綁定。
- 讓徵兵從固定票價模型改為 `兵種 + 數量 + 資源限制` 模型。
- 為後續 faction-level `Technology` 留出接口，但 Phase 3 先不做完整科技樹。

## 3. 設施系統

### 3.1 目前設施

- `弓坊 / BowWorkshop`
- `工坊 / SiegeWorkshop`
- `馬場 / HorsePasture`

### 3.2 城市資料欄位

- `BowWorkshopLevel`
- `BowWorkshopProgress`
- `SiegeWorkshopLevel`
- `SiegeWorkshopProgress`
- `HorsePastureLevel`
- `HorsePastureProgress`

### 3.3 設施效果

- `BowWorkshopLevel >= 1`
  - 解鎖 `弩兵 / Crossbow`
- `SiegeWorkshopLevel >= 1`
  - 解鎖 `攻城兵 / Siege`
- `HorsePastureLevel`
  - 提高每年 1 月馬匹繁殖率
- `SiegeWorkshopLevel`
  - 提高 `Siege` 在攻城戰中對城防的壓制效率

## 4. 建設點系統

### 4.1 核心語意

- `Construction` 不再是「跑完一個 schedule 就直接 level +1」。
- 改為每月底把建設成果轉成 `建設點 / Construction Points`。
- 建設點累積到對應設施，達門檻後升級。
- 超出的建設點保留到下一級。

### 4.2 升級門檻

- `Lv0 -> Lv1`：`100`
- `Lv1 -> Lv2`：`200`
- `Lv2 -> Lv3`：`300`
- 其後依 `100 * (目前等級 + 1)` 遞增

### 4.3 建設點來源

- 目前每月底建設點由下列因素決定：
  - `每月投入金`
  - 指派武將 `Politics`
  - 指派武將 `Intelligence`
  - 指派武將 `Leadership`
  - 建設職系熟練 / progression bonus

### 4.4 建設項目

- `ConstructionProjectType.BowWorkshop`
- `ConstructionProjectType.SiegeWorkshop`
- `ConstructionProjectType.HorsePasture`

### 4.5 自動選案規則

- 若玩家未明確指定建設項目，系統目前優先：
  - 沒有 `弓坊` 先補 `弓坊`
  - 再補 `工坊`
  - 再補 `馬場`

## 5. 內政與成本

### 5.1 每月投入金

- `Internal Affairs` 目前採 `每月投入金` 語意。
- 若設定：
  - `3 個月`
  - `每月投入金 500`
- 則總成本為：
  - `1500`

### 5.2 扣款規則

- 不在建立 schedule 當下扣整筆。
- 每月底執行前檢查當月是否足以支付 `每月投入金`。
- 若足夠：
  - 扣當月金
  - 執行內政 / 建設效果
- 若不足：
  - schedule 自動轉為 `Paused`
  - 待後續資金恢復且有可用武將時自動恢復

### 5.3 建設與月數

- `月數` 目前決定的是：
  - 該排程最多持續多久
  - 每月能累積幾次建設點
- `月數` 本身不直接提高單月建設點倍率。

## 6. 徵兵系統

### 6.1 UI 流程

- 玩家先選兵種
- 系統顯示目前可徵募上限
- 玩家輸入徵兵數量
- UI 即時計算與顯示成本
- 玩家選定執行武將後確認

### 6.2 兵種解鎖條件

- `Crossbow`
  - 需 `BowWorkshopLevel >= 1`
- `Siege`
  - 需 `SiegeWorkshopLevel >= 1`
- `Cavalry`
  - 需城市持有馬匹

### 6.3 可徵募上限

- 目前上限由下列條件共同限制：
  - 城市 `Gold`
  - 城市 `Food`
  - 城市 `Population`
  - 特殊資源，例如 `Horses`
  - 兵種解鎖條件

### 6.4 成本顯示

- UI 會即時顯示：
  - `Gold`
  - `Food`
  - 若為 `Cavalry`，另外顯示 `Horse`

### 6.5 成本模型

- 徵兵成本已改為 `依兵種與數量計算`
- 不再使用舊版單次固定票價

### 6.6 月底結果

- 徵兵實際成果於月底結算
- 目前結果受以下因素影響：
  - 指派武將 `Charm`
  - 城市 `Loyalty`
  - 隨機值
  - 兵種難度修正

## 7. 戰鬥關聯

### 7.1 攻城兵

- 目前 `攻城兵 / Siege` 仍維持單一兵種，不拆子類型。
- 尚未拆成：
  - `衝車`
  - `投石車`
  - `雲梯`

### 7.2 工坊加成

- `SiegeWorkshopLevel` 會提高 `Siege` 對守方 `Defense` 的壓制效率。
- 目前屬於小幅策略層加成，不改變 UI 兵種結構。

## 8. UI 顯示

### 8.0 啟動流程

- 啟動遊戲後，預設先進入 `Main Menu`
- `Main Menu` 目前提供：
  - `Start Game`
  - `Load Game`
- `Start Game` 流程：
  - `Main Menu`
  - `Select Story`
  - `Select Lord`
  - `Game Mode`
- `Load Game` 流程：
  - `Main Menu`
  - `Load Game UI`
  - 選擇存檔
  - `Game Mode`
- 目前 `Story Select` 先列出已掛入 bootstrap 的 scenario
  - 現階段基線為 `黃巾之亂`
- `Lord Select` 會列出該劇本可扮演勢力
  - 確認後將該 faction 設為 `IsPlayer = true`
  - 其他 faction 設為 `false`
- 進入 `Game Mode` 時，不另建第二套世界流程，而是把選定 `WorldState` 注入既有：
  - `TurnManager`
  - `CommandResolver`
  - `AiController`
  - `HUD`
  - `MapScene`

### 8.1 城市資訊面板

- 左側 `城市資訊` 會顯示：
  - `弓坊`
  - `工坊`
  - `馬場`
- 顯示格式為：
  - `等級(目前進度/下級門檻)`
- 範例：
  - `0 (40/100)`

### 8.2 View Cities

- `View Cities` 目前也顯示：
  - `弓坊`
  - `工坊`
  - `馬場`
- 格式與城市資訊面板一致

### 8.3 內政排程列表

- 目前排程列表會顯示：
  - 內政項目
  - 建設子項目
  - 指派武將
  - 剩餘月數
  - 每月投入金
  - 狀態

### 8.4 君主選擇畫面

- `Select Lord` 目前顯示：
  - 劇本名稱
  - 可扮演勢力列表
  - 右側君主摘要
  - 君主人像
- 君主人像來源沿用既有 portrait atlas：
  - `assets/portrait/team1.png`
  - `assets/portrait/team2.png`
  - `assets/portrait/team3.png`
  - `assets/portrait/team4.png`
- 裁切定義來源：
  - `data/person/person_image_1.json`
  - `data/person/person_image_2.json`
  - `data/person/person_image_3.json`
  - `data/person/person_image_4.json`
- `person_image_*.json` 中的：
  - `x`
  - `y`
  - `width`
  - `height`
  代表 atlas 裁切區域，不是顯示縮放尺寸
- 因此若修改：
  - `width`
  - `height`
  會直接改變裁切框，造成圖片被 crop，而不是等比例縮放
- 若對應 `charId` 沒有 mapping，lord select 會退回 placeholder 文字，不阻塞選主流程

### 8.5 View 視窗與選城同步

- `View` dialog 開啟時，若玩家直接點地圖切換目前選中城市：
  - dialog 內容應跟隨新的 `_selectedCity` 自動刷新
- 目前已補：
  - `RefreshOfficerListChrome()`
  - `RefreshOfficerListContent()`
- 因此：
  - `本城武將`
  - `全勢力武將`
  - `城市`
  - `勢力道具`
  - `外交關係`
  都應吃到新的目前選中城市上下文

## 9. AI 現況

- AI 已經會使用 `internal affairs schedule`
- AI 目前也能走 `Recruit`
- 但 AI 對 Phase 3 的理解仍屬基線版：
  - 尚未完整判斷何時優先蓋 `弓坊`
  - 尚未完整判斷何時優先蓋 `工坊`
  - 尚未完整判斷何時優先蓋 `馬場`
- 後續可再補：
  - 前線城偏 `工坊 / 防衛`
  - 後方城偏 `弓坊 / 馬場 / 商業`

## 10. 已知限制

### 10.1 科技樹尚未實作

- 目前只有 `城市設施`
- 尚未有 faction-level `Technology`
- 尚未有：
  - 科技資料
  - 科技研究 UI
  - 科技前置關係

### 10.2 攻城子類型尚未實作

- `Siege` 尚未拆分攻城器械子類

### 10.3 View 表格渲染

- `View` 目前仍使用 Godot `Tree`
- row 背景與欄位寬度在初次顯示時，可能有 redraw timing 問題
- 點擊 row 後通常可觸發正確重繪
- 這屬 UI 呈現層問題，不影響資料正確性

### 10.4 劇本人物死亡年份規則

- 目前人物是否可在劇本開始時登場，採用：
  - `world.Year <= officer.DeathYear`
- 也就是：
  - 死亡年份「當年」仍可登場
  - 下一年起才視為死亡不可用
- 原因：
  - 若使用 `world.Year >= officer.DeathYear`
  - 會導致如 `184 年 1 月` 的 `張角` 劇本一開始就把張角系官員全部排除
- 這個規則已同步到：
  - scenario 載入
  - alive helper
  - free officer 可見性
  - spy target officer 列表

## 11. 後續建議

### 11.1 Must

- 穩定 `View` 表格 redraw / row fill 呈現
- 補 `Construction` 進度 log
- 補排程列表中的建設點變化提示

### 11.2 Should

- 做 faction-level `Technology` 最小版
- 讓 AI 依城市角色選擇建設方向

### 11.3 Nice-to-have

- 將 `Siege` 拆成攻城子類型
- 為建設加入更完整的預估完成時間 UI
