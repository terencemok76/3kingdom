# Phase 4 戰鬥系統 Kickoff

更新日期：2026-07-19

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

- 主選單已可切入 `Battle / 戰鬥`
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

### 7.0 正式方向確認

- 自 `2026-07-19` 起，`Phase 4` 戰場架構正式採用混合式方向：
  - `TileMap` 只負責 scene map data 與 WYSIWYG 地圖編輯
  - `Scenario Data` 負責戰場規則補充、部署、勝敗條件與 AI hint
  - runtime `BattleMapData` 負責 controller、戰鬥規則、A*、AI 實際使用的乾淨資料
- `BattleMapData` 的 code-defined layout 仍可保留作為 prototype 與 regression 測試基線：
  - 但不應作為正式多戰場內容的主要維護方式
  - 正式地圖應逐步改由 Godot `TileMap` 手工編排
- controller 不應直接把 scene node 當成規則來源：
  - 應先將 `TileMap` 與 `Scenario Data` 轉成 runtime `BattleMapData`
  - 後續的移動、阻擋、射程、攻城器、城門、城牆、HUD 與 AI 都只依賴 `BattleMapData`

### 7.1 工作包

1. 戰場資料模型定型
   - 固定 `25x25` 戰場格資料結構
   - runtime `BattleMapData` 每格至少有：`ground_type`、`terrain_type`、`structure_type`、`height_tag`、`move_cost`、`block_state`
   - 攻城戰專用欄位至少有：`wall_segment`、`gate_segment`、`inside_city`、`siege_deploy_zone`
   - 視覺與遮擋相關欄位至少要能描述：`structure_facing`、`foreground_occlusion`
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
   - 正式內容以 Godot `TileMap` 手工編圖為主，保留 WYSIWYG 工作流
   - `BattleMapData` 作為 runtime 轉換結果，不直接手改為正式內容來源
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
  - 牆頂可移動格使用 `overlay.png` 的 waypoint tile 高亮，以避免與地面可移動格混淆
  - `NorthEast` / `NorthWest` 場景的牆頂 waypoint 高亮可使用各自的獨立 offset，對齊牆頂 cross / 站位點，而不影響牆頂單位站位
  - 若牆頂單位與 waypoint 位於同一個 `BattleGridKey`，兩者應共用同一個 walltop anchor position，避免視覺上出現同格不同站位
  - 點擊牆頂上的 battle team 時，若 selected grid 與該單位是同一個 walltop 格，則不另外顯示 selected grid 高亮
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
  - 參戰部隊、勝敗條件與回合限制
  - 可啟用的規則模組
  - 部署區、特殊目標點、事件點
  - AI hint，例如主攻路線、防守重點區、破門優先度
  - TileSet、結構視覺、BGM 等戰場主題資料
- `TileMap` 只負責 scene map data 與視覺可編輯內容：
  - `ground_type`
  - `structure_type`
  - `height_tag` 或可推導其對應資訊
  - `base_block_state`
  - `base_move_cost`
  - 可選的 `visual mask hint`
- 若 `NorthEast` / `NorthWest` 戰場需要不同牆線、道路、護城河或城門位置：
  - 應直接維護各自的 Godot `TileMap`
  - 以手工編圖維持 WYSIWYG，不依賴 runtime 自動 mirror
  - controller 仍只讀 `TileMap -> BattleMapData` 的轉換結果，不直接依賴 scene node 判規則
- runtime `BattleMapData` 負責 controller、戰鬥規則、A*、AI 使用的格子狀態：
  - `terrain_type`
  - `structure_type`
  - `height_tag`
  - `move_cost`
  - `block_state`
  - `structure_facing`
  - `foreground_occlusion`
  - `structure_hp / gate_open_state`
- `BattleScene` 共用處理：
  - 回合流程
  - 選取、移動、攻擊
  - A* pathfinding
  - HUD、動畫與結果結算
- controller 不應直接讀 scene node 來判規則：
  - 應先經過 `TileMap -> BattleMapData` 的標準化轉換
  - 城門、城牆、雲梯、護城河等差異，應由規則模組與 scenario data 決定，而不是以固定座標寫入 controller。
- AI 不應直接依賴 TileMap node：
  - 應只依賴 runtime `BattleMapData` 與 `BattleScenarioDefinition` 提供的資訊

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
- 正式版不應假設所有城牆／城門都固定朝 `NorthEast`：
  - 至少需支援 `NorthEast` 與 `NorthWest`
  - 未來如有更多戰場朝向，應沿用同一套 facing-driven 規則擴充
- 現行雙格城門 prototype 的開門視覺暫定為：
  - 左側 gate 格可正常顯示單位
  - 最右側 gate 格仍顯示 silhouette
- 此規則是現有素材的暫定視覺處理；正式版應改為由 `structure_facing` 與 scenario data 驅動的前景遮罩區域，不能依賴固定座標或 gate 群組左右順序。
- 2026-07-18 起，battle runtime 已先將城牆／城門前景遮擋旗標移入 `BattleMapData`：
  - `SiegeAssault / MoatSiegeBattle` 由 scenario layout 明確標記需要 silhouette 的 `L0` 格
  - `Use Editor Authored Layout` 模式則會依已烘焙的牆／門結構自動推導預設遮擋旗標
  - `NorthEast` 城牆的遮擋深度朝 `Y-` 延伸；`NorthWest` 城牆則朝 `X-` 延伸，確保 NW 內城一側（例如 `(6, 7)`）的攻城器也會顯示 silhouette
- 正式版的 silhouette、foreground occlusion、gate open 後的前景保留側、wall-top depth sorting：
  - 都必須改為 facing-driven
  - 不應再把 `NorthEast` 牆面遮擋或「最右側 gate 保留 silhouette」寫死在 controller
- 牆頂移動高亮與其他 `L2` 單位不可被 `L0` gate／wall 的固定高 Z 值覆蓋；結構與單位的深度規則必須以層級與地圖深度共同決定。
- 投石車一般攻擊會在發射動畫期間使用 `assets/battle/object/catapult_stone.png` 顯示旋轉石彈飛向目標格；飛行時會壓縮原素材的橫向比例，使其更接近單顆石彈，並在命中位置顯示衝擊效果。部隊與城門目標均適用。
- 弓兵與弩兵的一般攻擊會顯示飛箭，由攻擊者格子的畫面座標飛向目標格的畫面座標；飛行時間會納入攻擊效果時序，避免被擊潰單位在箭矢抵達前被移除。
- 一般戰鬥隊伍可使用 prototype `Union Attack`：
  - 選取的普通戰鬥隊伍若與敵方戰鬥隊伍四方向相鄰，且同陣營共有 `2` 到 `4` 支合格隊伍以四方向接觸同一名敵方目標，指令選單會顯示 `Union Attack (N)`。
  - 合擊目前限制在同一個 `BattleGridKey.Level`，不跨地面／牆頂層判定，也不包含攻城器。
  - 可參與合擊的兵種為 `Infantry`、`Spearman`、`Cavalry`、`Archer`、`Crossbow`、`Worker`。
  - 第一版傷害規則為主攻者 `100%` 一般攻擊傷害，加上每名支援者 `50%` 一般攻擊傷害；所有參與隊伍會面向目標播放攻擊動畫，目標只播放一次受擊動畫。
- battle team 可使用 prototype `Retreat`：選取隊伍後可直接撤出戰場，該隊伍會從目前格子與畫面移除，並釋放佔用格；top bar 的對應陣營總兵力會扣除該隊伍撤退時的剩餘 `TroopCount`。
- battle team 具有 prototype `Morale` 屬性：
  - 一般戰鬥隊伍預設 `Morale 100`，Worker 預設 `Morale 80`。
  - `Ram`、`Ladder`、`Catapult` 等攻城器沒有 morale，UI 以 `-` 表示。
  - 目前 morale 先作為顯示屬性，尚未接入傷害、撤退或 AI 判斷。
- 一般部隊將領可使用 prototype `Duel / 單挑`：
  - 選取有將領的一般 battle team，若 2 格範圍內八方向有敵方將領隊伍，command menu 會顯示 `Duel`。
  - 單挑可跨 `L0` / `L2` 層，例如地面隊伍可挑戰牆頂隊伍，牆頂隊伍也可挑戰地面隊伍。
  - 點擊後可選擇相鄰 opponent；攻城器與 Worker 不可發起或接受單挑。
  - opponent 是否接受由 prototype battle score 判斷；差距過大時弱勢方可拒絕。
  - 單挑只比較 officer / general battle attribute，不套用兵種修正或 morale bonus；勝方 morale 增加，敗方將領被俘且該 battle team 離場；平手時雙方保留並各自小幅增加 morale。
- top bar 的 Team A / Team B 總兵力會隨部隊損兵更新；一般攻擊、合擊、牆頂投放攻擊與 fire damage 造成的 `TroopCount` loss 都會扣到對應陣營。
  - `Troops` 代表一般戰鬥隊伍兵員，包含 Worker，但不包含攻城器 HP。
  - `Generals` 顯示目前仍在戰場上的一般部隊將領數量；`Worker` 與攻城器不算將領。
  - `Siege` 另行顯示目前仍在戰場上的攻城器數量；攻城器撤退或被摧毀時會扣除。
  - `Ram`、`Ladder`、`Catapult` 不掛 officer name；name plate 與 log 直接顯示攻城器名稱。
- 右下角新增 prototype `Battle Log` 面板：
  - 記錄 battle team 的 `Move`、`Attack`、`Hurt`、`Retreat`、`Strategy`、`Action` 與回合切換事件。
  - battle team 受傷紀錄會顯示受擊隊伍、傷害數字，以及造成傷害的攻擊隊伍；fire damage 則標記為 fire 來源。
  - log 可切換 `All` 或 `Self`；`Self` 目前依當前 acting side 顯示該隊伍相關紀錄。
  - 面板採用 gameplay floating log 風格，支援拖曳移動、最小化／還原，以及右下角 resize grip 調整大小。
- 牆頂單位可使用 prototype 專用攻擊：
  - `Drop Stone`：對城牆面向正前方的 `L0` 敵方部隊造成 `1,200` 傷害；`NorthEast` 為 `(x, y+1, L0)`，`NorthWest` 為 `(x+1, y, L0)`。
  - `Pour Oil`：對同一個 facing-driven 目標格的敵方部隊造成 `1,000` 傷害。
  - 兩者只在單位位於可行走的城牆／城門／塔樓 `L2` 時顯示；可直接投向正前方空格，只有該格有敵軍時才結算傷害。
  - 每支部隊預設有 `Drop Stone x3`、`Pour Oil x2`；次數會顯示在按鈕上，並可在 Inspector 調整。
  - `Drop Stone` 會顯示由牆頂落向目標的石塊與落點衝擊效果，並沿用一般受擊動畫。
  - `Pour Oil` 會顯示由牆頂傾倒的熱油流與落點濺射效果，並沿用一般受擊動畫。
  - 兩種牆頂投放攻擊均不播放一般武器攻擊動畫；兩者的範圍傷害與狀態效果，留待後續規則模組處理。
- battle scenario 現在可定義 `TimeOfDay`、`Weather`、`WindDirection` 與 `WindPower`：
  - top bar 會以按鈕顯示目前戰場時段、天氣、風向與風力；點擊可循環切換 prototype runtime 狀態。
  - `Dawn / Morning / Afternoon / Night` 先作為輕量全域戰鬥條件；目前 `Night` 會令弓兵／弩兵／投石車有效攻擊射程 -1，但最少保留 1 格。
  - 時段會以戰場 overlay 呈現：`Dawn` 偏淡橙、`Morning` 微暖、`Afternoon` 偏金色、`Night` 偏深藍；切換時段時會用短 tween 過渡，且不遮住 top bar、tile info、command menu 或 battle log。
  - 天氣會以第二層戰場 overlay 呈現：`Sunny` 透明，`Cloudy` 套用灰藍 tint，`Rain` 套用更深灰藍 tint 並顯示斜向雨線；weather overlay 疊在 time overlay 上方、UI panel 下方，不攔截滑鼠。
  - `Tile Info` 不再重複顯示 scenario、weather、wind、time 等全域戰場狀態，只保留格子、地形、結構、部署與單位資訊。
  - `Strategy` 的第一個落地版本為 fire tactic：目前由弓兵／弩兵／投石車使用，選擇 `L0` 目標格後建立 fire。
  - fire 會在每次 `End Turn` 結算傷害，並依 `WindDirection`、鄰格方向與地形權重擴散；風向提供偏好，但不再只沿單一直線延燒。
  - `WindPower` 會影響 fire spread speed：`Calm` 只允許森林／草地／木柵欄慢速帶火，每 3 個 burn tick 最多擴 1 格；`Strong` 會增加擴散格數並縮短 spread interval。
  - 森林與草地會燒得較久且擴散較快；道路、橋與庭院擴散較慢；護城河、城牆與牆頂不會成為 fire spread 目標。
  - fire visual 的 depth band 會高於 battle team，避免火焰被單位 sprite 蓋住。
  - `Sunny / Cloudy / Rain` 會分別影響 fire 的持續回合與每回合傷害；`Rain` 會明顯抑制 fire spread。

### 10.5 建議落地順序

1. 將目前固定的 `BuildSiegeAssaultLayout()` 抽為第一份 `SiegeScenarioDefinition`。
2. 建立不含城牆結構的 `FieldScenarioDefinition`，驗證同一個 `BattleScene` 可載入野戰。
3. 新增 `Moat`、`Bridge` 地形與 A* 可通行／移動成本規則。
4. 將城門、牆頂與 silhouette 的視覺遮擋改為資料驅動的前景遮罩設定。
5. 為野戰、一般攻城與護城河攻城各建立最少一張測試地圖，覆蓋移動、攻擊、部署與勝敗條件。

### 10.6 已實作的基線

- `BattleSceneController` 已提供 Inspector `Scenario Type`：
  - `SiegeAssault`
  - `FieldBattle`
  - `MoatSiegeBattle`
- 三種 scenario 目前共用同一個 `BattleScene`、HUD、單位 marker、移動與 A*。
- `FieldBattle` 會建立不含城牆／城門的道路、森林、障礙物與雙方部署區。
- `SiegeAssault` 與 `MoatSiegeBattle` 會各部署一名攻方與守方 `Worker`：使用 `worker_idle_ne.png`／`worker_idle_se.png` 的四格 idle animation，具備低近戰與移動能力。
  - Worker 移動時使用 `worker_move_ne.png`／`worker_move_se.png` 的三格 walk animation；NW／SW 以對應方向的水平翻轉顯示。
  - 選取 Worker 後顯示專用的 `Work`、`Install Wood Fence`、`Uninstall Wood Fence` actions，不顯示 `Strategy`；每個 action 只會標示四向相鄰的有效施工格，並使用綠色菱形 highlight。
  - Worker 可在 `MoatSiegeBattle` 的相鄰 `Moat` 格建造橋、修復受損橋面、修復受損城門及移除 `Trap`；橋梁需兩次 `Work` 才會完成，第一次為半 HP 的施工中橋面且不可通行，第二次回滿 HP 後才可通行。
  - `Install Wood Fence` 只能放置到無單位、無結構的乾地；`Uninstall Wood Fence` 只可拆除相鄰木柵欄。
  - 木柵欄有 `600 HP`，所有戰鬥單位可依其結構傷害攻擊；HP 歸零後會立即移除木柵欄視覺並恢復通行。
  - 橋梁有獨立 HP，所有戰鬥單位可依其結構傷害攻擊橋面；HP 歸零時橋面會移除並還原為阻擋移動的 `Moat`。施工、破壞完成後立即更新 `MoatLayer`、`ObjectLayer` 與通行規則。
  - 施工時播放 `worker_work.png` 的工作動畫，Worker 會面向目標格；上排為 NE、下排為 SE，各為四格；NW／SW 以對應排的水平翻轉顯示。
  - Worker 受擊時使用 `worker_hurt_ne.png`／`worker_hurt_se.png` 的兩排、每排三格動畫；NW／SW 以水平翻轉顯示。
  - Worker 攻擊時使用 `worker_attack_ne.png`／`worker_attack_se.png` 的兩排、每排三格動畫；NW／SW 以水平翻轉顯示。
- `MoatSiegeBattle` 會在攻城 prototype 的接近路線加入：
  - 不可直接通行的 `Moat`
  - 位於中央接近路線、可通行的 `Bridge`
- 護城河使用 `assets/battle/floor/floor.png` 的第六格 river tile；橋格使用 `assets/battle/object/object_01.png` 的第四格 bridge tile，繪製於 `ObjectLayer`。
- `MoatSiegeBattle` 現在應以 scene 的 `MoatLayer` 承載護城河資料；`GroundLayer` / `ObjectLayer` 繼續承載道路、庭院與橋面，不再把 moat/bridge/road/courtyard 座標硬寫進 `.tres`。
- `TileMap -> BattleMapData` 轉換時，只有 `ScenarioType == MoatSiegeBattle` 可以讀入 `MoatLayer`；`SiegeAssault` / `FieldBattle` 即使共用同一份 scene、且 scene 內保留了 moat tiles，也不能讓隱藏的 moat data 繼續阻擋移動、攻城車或 A*。
- `ObjectLayer` 內的 bridge tile 在 `MoatSiegeBattle` 應讀成 `Bridge` 地形；同一份 scene 在 `SiegeAssault` 模式下，這些格應回填為 `Road`，避免接近路線中斷成草地。
- `assets/battle/object/object_01.png` 的第 5 格（atlas 索引 `4`）為 `WoodenFence`；`ObjectLayer` 讀取該圖塊時應建立可阻擋移動、可由 Worker 移除的木柵欄。
- runtime 需額外保留 bridge 的 visual flag，讓 `MoatLayer` 的河水底圖與 `ObjectLayer` 的橋面 sprite 可以同時存在，不會因 terrain 正規化而把橋面吃掉。
- 若橋面在 editor 內使用了 `Flip H`，runtime 也必須保留該 bridge tile 的 `alternativeTile` 水平翻轉設定，否則 `NW` scene 進入遊戲後橋面方向會跑掉。
- 讀取 `Use Editor Authored Layout` 的 scene 時，應先讀取原始 `TileMapLayer` 內容，再重建 shared tileset 與 runtime 視覺；不能在讀取前先重新指定 layer tileset，否則 bridge 之類的 editor-authored flip 資訊可能會遺失。
- 目前 `NorthWest` 戰場的 bridge visual 應預設跟隨 `DefaultStructureFacing = NorthWest` 套用水平翻轉，避免即使 editor flip 資訊遺失，遊戲內橋面方向仍與 `NW` 場景相反。
- `NE` / `NW` 戰場的「城內地面格」判定不應靠掃描牆線方向推測，應直接使用 runtime `BattleMapData` 的 `Terrain == Courtyard` 作為內城地面判定；這樣可同時避免 `NW` 關門守軍退城誤判，也不會讓 `SiegeAssault` 的攻城車在外城草地／道路被錯誤當成城內而無法移動。
- `Battle / 戰鬥` 入口目前已支援 5 個模式選項：
  - `FieldBattle`
  - `NE SiegeAssault`
  - `NE MoatSiegeBattle`
  - `NW SiegeAssault`
  - `NW MoatSiegeBattle`
- `BattleScene.tscn` 與 `BattleSceneNorthWest.tscn` 目前都以 scene 內手工編排的 `TileMapLayer` 為主。
- runtime 會直接讀取這兩個 scene 的 editor-authored layout，讓 `NorthEast` / `NorthWest` 都維持 WYSIWYG 工作流。
- 若需測試其他手工編輯 TileMap 場景，可在 Inspector 開啟 `Use Editor Authored Layout`，讓 controller 讀取 scene 內 baked layout，而不是用 scenario data 重建。
