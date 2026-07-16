# Phase 4 戰鬥系統 Kickoff

更新日期：2026-07-16

## 1. 文件定位

- 本文件用於啟動 `Phase 4`。
- `Phase 3` 以城市設施、建設、徵兵、俘虜與地圖 UI 收尾為主。
- `Phase 4` 改以：
  - 戰鬥系統
  - 兵種互動
  - 攻城與守城表現
  - 戰鬥 UI
  - AI 戰鬥決策
  為主線。

## 2. 啟動前提

- `Phase 3` 已視為完成：
  - 設施 / 建設點 / 徵兵解鎖
  - 攻城器基線版
  - 俘虜管理與脫逃
  - View / live localization 收尾
  - map marker / road presentation 可用版
- 因此 `Phase 4` 可直接建立在既有：
  - troop allocation
  - siege engine inventory
  - city defense
  - attack deployment UI
  之上繼續深化。

## 2.1 目前 Prototype 狀態

- 主選單已可切入 `Battle Prototype / 戰鬥原型`
- 已有 `25x25` isometric 攻城戰原型場景
- 已支援：
  - 滑鼠右鍵拖曳地圖
  - top bar 顯示 hover / click tile 座標
  - 攻方 / 守方 / 攻城器 marker 顯示
  - 點擊單位後顯示指令選單
  - `Move / Attack / Strategy / End Turn` 基線互動
- 地圖表現現況：
  - `Ground / Terrain / Overlay` 進入 `TileMapLayer` 流程
  - `Structure / Unit / Effect / UI` 暫時維持節點式混合架構
- 攻城結構基線規則已接上：
  - 城牆與城門已有耐久值
  - 耐久歸零後視為 `破壞`
  - `破壞` 的城牆 / 城門不再阻擋移動
  - 守軍可在城門地面層使用 `Open Gate / Close Gate` 指令切換城門狀態
  - 右側 tile info 會顯示結構 `耐久 / 狀態`
- 目前仍屬 battle prototype，尚未進入正式素材管線與完整戰鬥規則階段

## 3. Phase 4 目標

- 讓戰鬥從目前可運作基線版，提升到更清楚、更可調、更有策略差異的系統。
- 優先處理：
  - 戰鬥流程可讀性
  - 兵種差異
  - 攻城器與城防的戰鬥意義
  - 玩家與 AI 的戰鬥決策品質

## 4. 建議範圍

### 4.1 Must

- 釐清戰鬥 phases：
  - 開戰前配置
  - 兵種對抗
  - 士氣 / 傷亡 / 城防壓制
  - 勝敗與俘虜結果
- 重整 troop interaction baseline：
  - 各兵種攻防定位
  - 克制與弱點
  - 成本與戰場價值一致性
- 補攻城戰專用規則：
  - `SiegeWorkshop`
  - `Siege`
  - `Ram / Catapult / Ladder`
  - `Defense`
  之間的交互明確化
  - 城門 / 城牆耐久、破壞、通行與攻城器互動語意
- 提升戰鬥結果可讀性：
  - summary
  - log
  - UI 顯示

### 4.2 Should

- AI 攻擊 / 防守部署邏輯補強
- 攻城器在戰鬥中的使用傾向與限制補強
- 戰鬥前摘要區顯示更清楚的兵力、攻城器與城防預估

### 4.3 Nice-to-have

- 士氣或陣型層的最小版
- 不同地形對戰鬥結果的輕量修正
- 更細的戰報呈現

## 5. 非本階段必做

- faction-level `Technology`
- 攻城器耐久與完整運補系統
- 完整 fog / intelligence 戰場情報系統
- 大規模戰場演出或動畫化戰鬥表現

註：
- 城門 / 城牆耐久已進入 prototype 基線，不再列為完全未開始項目。
- 但目前仍未接上正式的受損來源、數值平衡、破壞演出與完整 AI 目標選擇。

## 6. 建議切入順序

1. 先整理 `CombatResolver` 的戰鬥結算結構與參數責任。
2. 再重整 troop / siege / defense 的數值語意。
3. 接著改善 `Attack` UI 與結果顯示。
4. 最後補 AI 的部署與出兵判斷。

## 7. Prototype -> 正式 TileMap 版拆分

### 7.1 工作包

1. 戰場資料模型定型
   - 固定 `25x25` 戰場格資料結構
   - 每格至少有：`ground_type`、`terrain_type`、`structure_type`、`height_tag`、`move_cost`、`block_state`
   - 攻城戰專用欄位至少有：`wall_segment`、`gate_segment`、`inside_city`、`siege_deploy_zone`
2. TileSet 規格定型
   - 正式 tile 尺寸先以 `128x64` 為基準
   - 圖層建議切為：`Ground`、`Road/Terrain`、`StructureBase`、`StructureOverlay`
   - 部隊、攻城器、選取提示、浮字與特效暫不 tile 化
3. 建立正式 battle tilemap scene
   - 用多層 `TileMapLayer` 取代 prototype 的地表層
   - 保留 `UnitLayer`、`EffectLayer`、`UiLayer`
   - 保留 hover / click / drag camera 的互動流程
4. 先落地最小可用 TileSet
   - 草地
   - 土路
   - 城內地面
   - 城牆步道
   - 部分樹叢 / 區域 overlay
5. 結構層採混合式
   - 地表交給 TileMap
   - 城牆、城門、塔樓、主建物可先保留獨立 scene / node
   - 等規則穩定後再決定要不要完全 tile 化
6. 全面改成 grid-first 戰鬥互動
   - 視覺用 TileMap
   - 邏輯用獨立 grid data
   - 所有 hover、click、移動、阻擋、射程都以 `Vector2I grid` 為主
7. 建立場景維護流程
   - 先支援 battle map data 檔或 code-defined layout
   - 早期不強制自製 editor
   - 等地圖數量擴大後再評估 battle map editor tool
8. 再進入正式戰鬥系統
   - 部隊選取
   - 可移動格
   - 攻擊 / 射程
   - 城門 / 城牆耐久與破壞
   - 爬梯 / 衝車 / 投石互動
   - AI 攻守行為

### 7.2 里程碑建議

1. `P4-A`：TileMap 地表替換完成
2. `P4-B`：攻城結構混合式完成
3. `P4-C`：格子規則層完成
4. `P4-D`：第一個可操作攻城戰 prototype

## 8. 完成判定

- 玩家可理解為什麼戰鬥勝負發生。
- 兵種與攻城器選擇有實際差異。
- 城防與攻城器在攻城戰中有清楚存在感。
- AI 不會長期做出明顯錯誤的部署或器械使用。

## 9. 目前已落地但尚未 signed off 的 Prototype 規則

- 戰場為 `25x25` isometric grid，地表已使用外部 texture tileset。
- 結構層目前仍是混合式：
  - 地表 / terrain / overlay 使用 `TileMapLayer`
  - 城牆 / 城門 / 塔樓仍以 node / renderer 繪製
- 城門與城牆已有耐久欄位：
  - 城牆：`1200`
  - 城門：`1800`
- 當城門或城牆耐久降至 `0`：
  - 視為 `已破壞`
  - 該格改為 `non-block`
  - 右側資訊面板會顯示 `耐久` 與 `狀態`
- 城門通行與開關目前採 prototype 規則：
  - 城門可透過 `Open Gate / Close Gate` 指令切換開關狀態
  - 可切換目標以單位所在的城門地面層 `L0` 為準
  - 已破壞城門會保持可通行狀態
  - 關閉城門會阻擋外側地面進入或離開城門地面層
  - 城內地面仍可轉入相鄰城牆 / 城門 / 塔樓的牆頂層 `L2`
  - 攻方仍需透過開門、破門、雲梯橋接或其他有效路徑進入牆頂 / 城內路線
- 多層格互動目前以 `BattleGridKey (x, y, level)` 區分：
  - 城門、城牆、塔樓可同時存在地面層 `L0` 與牆頂層 `L2` 語意
  - hover / click / 移動 / 攻擊選格會優先解析對應層級
  - 牆頂可移動格使用不同顏色高亮，以避免與地面可移動格混淆
- 下一步應接的不是資料欄位，而是：
  - 受損來源與數值平衡
  - 破壞後 visual feedback
  - AI 是否優先打門、翻牆或壓城

## 10. 多戰場架構方向

### 10.1 戰場類型

- 正式系統預計至少支援：
  - `FieldBattle / 野戰`
  - `SiegeBattle / 攻城戰`
  - `MoatSiegeBattle / 有護城河的攻城戰`
- 不應為每種戰場複製一套 controller 或 UI scene。
- 應保留共用的 `BattleScene`，由不同的戰場定義資料決定地圖、規則與視覺主題。

### 10.2 資料責任

- 建議建立 `BattleScenarioDefinition`：
  - 戰場類型與地圖尺寸
  - 地形、結構、高度與部署區
  - 參戰部隊、勝敗條件與回合限制
  - 可啟用的規則模組
  - TileSet、結構視覺、BGM 等戰場主題資料
- `BattleMapData` 只負責格子狀態：
  - `terrain_type`
  - `structure_type`
  - `height_tag`
  - `move_cost`
  - `block_state`
- `BattleScene` 共用處理：
  - 回合流程
  - 選取、移動、攻擊
  - A* pathfinding
  - HUD、動畫與結果結算
- 城門、城牆、雲梯、護城河等差異，應由規則模組與 scenario data 決定，而不是以固定座標寫入 controller。

### 10.3 各戰場的最小規則差異

- `FieldBattle / 野戰`：
  - 平原、道路、森林、丘陵、河流等地形為主
  - 不啟用城門、城牆、雲梯與攻城破壞規則
  - 騎兵機動與地形移動成本應成為主要差異
- `SiegeBattle / 攻城戰`：
  - 啟用城牆、城門、牆頂 `L2`、破壞、雲梯、衝車與投石車規則
  - 攻方需經由開門、破門、雲梯或其他有效路線進入城內／牆頂
- `MoatSiegeBattle / 有護城河的攻城戰`：
  - 在攻城戰規則上加入 `Moat / 護城河` 與 `Bridge / 橋` 格
  - 第一版護城河應阻擋步兵、騎兵與攻城器直接進入
  - 橋格、填平後格、或後續的浮橋／舟橋才可通過
  - 雲梯不應直接跨越護城河；攻城器也應先取得可通過的接近路線

### 10.4 L0、L2 與視覺遮擋約定

- `BattleGridKey (x, y, level)` 持續作為地面與牆頂的唯一格子識別。
- `L0` 表示地面／城門通道／城牆後方位置；`L2` 表示牆頂可行走位置。
- 位於城牆後方或完整城門前景遮擋區的 `L0` 單位，可用 silhouette 表示而非直接顯示完整 sprite。
- 現行雙格城門 prototype 的開門視覺暫定為：
  - 左側 gate 格可正常顯示單位
  - 最右側 gate 格仍顯示 silhouette
- 此規則是現有素材的暫定視覺處理；正式版應改為 scenario data 定義的前景遮罩區域，不能依賴固定座標或 gate 群組左右順序。
- 牆頂移動高亮與其他 `L2` 單位不可被 `L0` gate／wall 的固定高 Z 值覆蓋；結構與單位的深度規則必須以層級與地圖深度共同決定。

### 10.5 建議落地順序

1. 將目前固定的 `BuildSiegeAssaultLayout()` 抽為第一份 `SiegeScenarioDefinition`。
2. 建立不含城牆結構的 `FieldScenarioDefinition`，驗證同一個 `BattleScene` 可載入野戰。
3. 新增 `Moat`、`Bridge` 地形與 A* 可通行／移動成本規則。
4. 將城門、牆頂與 silhouette 的視覺遮擋改為資料驅動的前景遮罩設定。
5. 為野戰、一般攻城與護城河攻城各建立最少一張測試地圖，覆蓋移動、攻擊、部署與勝敗條件。

### 10.6 已實作的基線

- `BattlePrototypeSceneController` 已提供 Inspector `Scenario Type`：
  - `SiegeAssault`
  - `FieldBattle`
  - `MoatSiegeBattle`
- 三種 scenario 目前共用同一個 `BattlePrototypeScene`、HUD、單位 marker、移動與 A*。
- `FieldBattle` 會建立不含城牆／城門的道路、森林、障礙物與雙方部署區。
- `MoatSiegeBattle` 會在攻城 prototype 的接近路線加入：
  - 不可直接通行的 `Moat`
  - 位於中央接近路線、可通行的 `Bridge`
- 護城河第一版已有程式化水面與木橋覆蓋顯示，之後可替換為正式 TileSet 素材。
- 預設 `SiegeAssault` 仍會沿用既有 editor-authored tile layout，以避免改變現有 prototype 測試流程；切換至野戰或護城河攻城時會由 scenario 重建 TileMap。
