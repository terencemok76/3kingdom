# Phase 4 戰鬥系統 Kickoff

更新日期：2026-07-26

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
  - 進入 battle mode 時會切換至 `bgm_battle_01.ogg` / `bgm_battle_02.ogg` 戰鬥 BGM playlist，並沿用玩家設定的 BGM 開關與音量
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
- 野戰 AI 測試基線：可對攻方、守方或雙方啟用 AI；以 `Start Round`、逐隊 `Next` 與玩家確認的 `End Turn` 檢閱共用移動／射程規則的決策。詳見 `docs/FIELD_BATTLE_AI_DESIGN_ZH.md`。
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
- 弓兵與弩兵的一般攻擊會顯示飛箭，由攻擊者格子的畫面座標飛向目標格的畫面座標；動畫順序為攻擊 → 飛箭抵達 → 目標受傷、傷害數字 popup 與傷害結算，避免被擊潰單位在箭矢抵達前被移除。
- 一般戰鬥隊伍可使用 prototype `Union Attack`：
  - 選取的普通戰鬥隊伍若與敵方戰鬥隊伍四方向相鄰，且同陣營共有 `2` 到 `4` 支合格隊伍以四方向接觸同一名敵方目標，指令選單會顯示 `Union Attack (N)`。
  - 合擊目前限制在同一個 `BattleGridKey.Level`，不跨地面／牆頂層判定，也不包含攻城器。
  - 可參與合擊的兵種為 `Infantry`、`Spearman`、`Cavalry`、`Archer`、`Crossbow`、`Worker`。
  - 第一版傷害規則為主攻者 `100%` 一般攻擊傷害，加上每名支援者 `50%` 一般攻擊傷害；所有參與隊伍會面向目標播放攻擊動畫，目標只播放一次受擊動畫。
- 野戰 AI 的攻擊決策會把直接攻擊、合擊、合法的「移動後保留 `5` 能量攻擊」、Worker 工程與堡壘目標放入同一候選集，以既有預估傷害／目標 score、武將 Intelligence 與 Combat 加上可重現的微小變異排序；移動後攻擊不再只在其他方案沒有選擇時才考慮。低兵力戰隊的生存評估只計入敵軍依能量、地形路徑及攻擊格實際可達的威脅，並會比較直接攻擊、合擊與移動後攻擊收益。
- 補給車僅在相鄰友軍有重傷、士氣不高於 `30` 或受損投石車時，優先於一般戰隊行動；一般補給與移動後補給會以回復量、士氣缺口、修理量及路徑成本計分，再與攻擊、工程與堡壘候選共同排序。
- 野戰 AI 的一般戰隊可在符合安全距離與接敵條件的森林格選擇 `Hide`；隱藏後，對進入合法攻擊範圍的可見敵軍會提高攻擊候選分數以形成伏擊。AI 不會直接讀取敵方隱藏單位作為可見目標，但玩家仍可保留對森林格的盲攻。
- 野戰 AI 會比較雙方 Food 可供應日數：敵方少於 `2` 日、且比己方更接近斷糧時，提高守堡、Guard、Hide 與攻擊敵方 `SupplyCart` 的候選分數；只剩 `1` 日或更少時，位於 Building／己方 Fortress 的一般戰隊會將 Guard 放入全域候選，並降低一般非必殺攻擊的分數，讓 AI 優先拖延或切斷補給線。例外是存在任何合法直接攻擊目標（包含弓／弩遠距）時：Guard 不會列入候選，直接攻擊不會受低糧扣分並獲得同等戰術加分；必殺與最後堡壘勝利仍保留既有高分。已在其目標 Fortress 的單位不會產生接近同一堡壘的移動候選，沒有直接攻擊時 Guard 留守。
- 野戰 top bar 提供僅供測試的「攻方糧食：1 日」與「守方糧食：1 日」按鈕，將指定方 Food 設為其目前 active troops 的一日消耗，方便驗證低糧 AI 行為，不修改 Scenario 初始資源。一日為攻、守雙方各完成一次回合；每次完成完整一日，會結算雙方日常補給並讓戰場日期前進。
- 若 Food 不足以支付整日消耗，Food 會歸零，該方全隊扣 `15` 士氣，並讓每支一般戰隊立即流失 `10%` 現役兵力；流失量以傷害數字與 Battle Log 顯示，令斷糧效果可觀察且可測試。
- Food 為 `0` 時，所有一般戰隊、攻城器與補給車會在各自回合開始依連續零糧完整日數，將 Energy 上限由 `10` 依序降至 `7`、`5`、`5`；Food 回復後，下一個己方回合立即回復 `10`。第 3 日起為固定瀕餓作戰：移動範圍最多 `1` 格，且一般攻擊、合擊與 Charge 傷害均為正常 `50%`，移動後沒有足夠 Energy 再攻擊。車體本身不會因而扣 HP，但 Energy 低於 `5` 時補給車無法補給／補彈，投石車無法一般射擊。
- battle team 可使用 prototype `Retreat`：選取隊伍後可直接撤出戰場，該隊伍會從目前格子與畫面移除，並釋放佔用格；top bar 的對應陣營總兵力會扣除該隊伍撤退時的剩餘 `TroopCount`。無論玩家或 AI 觸發撤退，畫面中央皆會顯示「武將名／兵種」及 `Retreat` 的提示框約 2 秒後自動消失。
- `Unit Command` menu 的 action buttons 超過 `4` 個可見指令時，指令區會限制高度並啟用垂直捲動；title 與 unit info 保持固定顯示。
- 點選非目前 `Acting Side` 的單位時仍會顯示 unit info menu，但 action buttons 會隱藏，並在資訊中標示 `Command: Not Acting Side`，避免看起來像 command menu 無法彈出。
- battle team 具有 prototype `Morale` 屬性：
  - 一般戰鬥隊伍預設 `Morale 100`，Worker 預設 `Morale 80`。
  - 一般 battle team 的兵力分為 `Active Troops` 與 `Wounded Troops`：`Active` 會參與戰鬥與 HUD 總兵力，`Wounded` 暫時不能戰鬥但可被糧草車恢復。
  - 一般 battle team 的 marker HP bar 採三段顯示：綠色為 `Active Troops`、粉紅色為 `Wounded Troops`、黑色為 `Dead / Killed Troops`。
  - HP 傷害、HP 維修與士氣變化會以 battle piece 上方浮字呈現，所有這些增減浮字顯示 `4` 秒；士氣浮字與 HP 傷害浮字使用不同顏色，`Morale +N` / `士氣 +N` 偏藍，`Morale -N` / `士氣 -N` 偏琥珀，避免和紅色傷害數字混淆。
  - 一般攻擊造成的有效傷害目前拆為約 `40% killed / 60% wounded`；fire damage 拆為約 `70% killed / 30% wounded`。Battle Log 會顯示 killed 與 wounded 數字。
  - `Ram`、`Ladder`、`Catapult`、`SupplyCart` 等攻城器／後勤車沒有 morale，UI 以 `-` 表示。
  - 糧草車 `SupplyCart` 可每回合一次使用後勤行動，`Recovery / Repair` 與 `Resupply Weapon` 共用同一個每回合次數。
  - `Recovery / Repair`：對八方向 1 格內的同隊一般 battle team 恢復 `Morale +8`，上限仍為 `120`；若附近同隊 battle team 有 wounded，則每次最多恢復 `600` 名傷兵回到 `Active Troops`；若自己或八方向 1 格內同隊攻城器受損，則可維修 `HP +450`，不超過各自最大 HP。
  - `Building`／己方 `Fortress` 的休整：一般戰隊在己方回合結束時，若 Food 大於 `0`、仍駐守該格、格子未起火、八方向沒有敵軍，且該回合未移動或使用指令，最多可將 `150` 名 wounded troops 回復為 active troops。休整不復活 dead troops，並且弱於補給車的 `600` 傷兵恢復。
  - `Resupply Weapon`：對八方向 1 格內同隊 `Archer`、`Crossbow`、`Catapult` 補滿武器彈藥。
  - `Archer` 武器彈藥為 `6/6`、`Crossbow` 為 `4/4`、`Catapult` 為 `3/3`；普通攻擊、合擊中的遠程參與者，以及 `Strategy (Fire)` 各消耗 `1` 發。`Archer` / `Crossbow` / `Catapult` 彈藥為 `0` 時仍可使用 1 格 `Weak Close Attack`，傷害約為各自原攻擊的 `35%`，且不播放箭矢／石彈彈道；`Strategy (Fire)` 仍需要彈藥。
  - `Archer` / `Crossbow` 使用 `Strategy (Fire)` 時會播放攻擊動畫與箭矢彈道；`Catapult` 則播放投石車攻擊動畫與投石彈道，再於目標格點燃 fire。
  - 每個完整 battle day（Team B 結束後、Turn 前進時）會消耗雙方 Gold / Food：目前 prototype 以每 `100` active troops 消耗 `Food 5` 與 `Gold 1` 計算；若當日糧食不足，Food 歸零、該隊一般 battle team `Morale -15`，且每支一般戰隊流失 `10%` active troops；若剩餘糧食低於下一日需求則 `Morale -6`。攻城器與補給車沒有 morale，也不套用兵員流失；但 Food 持續為 `0` 時，它們和一般戰隊一樣會在各自回合的 Energy 上限按零糧完整日數降為 `7`、`5`、`5`。第 3 日起為固定瀕餓作戰：移動範圍最多 `1` 格、一般攻擊／合擊／Charge 傷害為 `50%`；Food 回復後下一個己方回合回到 `10`。
  - 糧草車被摧毀時，同隊 Gold / Food 會額外損失 `25%`，同隊仍在場的一般 battle team 士氣會立刻降為目前值的 `50%`；糧草車主動撤退不觸發此懲罰。
  - 一般 battle team 若相鄰敵方 `SupplyCart`，command menu 會顯示 `Capture Cart`；成功後敵方 Gold / Food 損失 `25%` 並轉移給俘獲方，敵方仍在場的一般 battle team 士氣降為目前值的 `50%`，俘獲方一般 battle team `Morale +10`，且該糧草車會轉為俘獲方陣營並留在戰場，可由俘獲方後續控制與使用。
  - 一般 battle team 若同層 2 格內有敵方 officer / general 隊伍，command menu 會顯示 `Hire Officer`；點擊後會 highlight 所有可招降目標，玩家再點選要招降的目標。花費 Gold 可直接招降該隊伍並轉入己方，目前 prototype 先固定成本為 `100 Gold`。招降成功後會播放金色牽引線、目標格光環與 `招降成功` 浮字，招降方仍在場的一般 battle team `Morale +8`，被招降方仍在場的一般 battle team `Morale -8`。
  - Battle piece 在 `Forest` 地形可使用 prototype `Hide`，包含一般 battle team 與 `Ram` / `Ladder` / `Catapult` / `SupplyCart` 等攻城／後勤車；hidden 後己方仍可看見 marker，單位／車體本體使用暗綠半透明 hidden visual，name plate、HP bar、team arrow 與 `HIDE` 狀態 badge 保持正常清楚顯示，但不再額外顯示 hidden 狀態外圈 ring；敵方回合則完全看不到、不能選取，也不能以可見單位目標方式被 Union Attack / Duel / Strategy (Mess) / Hire Officer 指定。若敵方直接攻擊 hidden 單位所在的 `Forest` tile，或該 `Forest` tile 起火造成 fire damage，hidden 單位仍會像一般 battle team 一樣受傷並顯示傷害結果。hidden 單位執行 Move / Attack / Strategy / Duel / Capture Cart / Hire Officer 不會自動解除 hidden；只有移動後離開 `Forest` 才會轉回非 hidden。
  - `Cavalry` 不可將移動目的地設在 `Forest`；可從其他地形繞行，若因既有佈署或存檔已在森林內，仍可正常移出森林。
  - `Cavalry` 可使用 prototype `Charge`：只支援 `L0` 四方向相鄰敵方 battle team，目標格不可是 `Forest`，且目標後方同方向一格必須在地圖內、不是 `Forest`、沒有任何 battle team，並可通行。執行時騎兵先對目標造成 `1,650` charge damage，再穿過目標格落到後方格；即使目標被擊破，騎兵仍會完成穿越。若目標是 `Spearman`，charge damage 降為 `900`，且騎兵會承受 `600` counter damage；反傷不會阻止穿越。使用 `Charge` 後，該騎兵本回合不能再使用 `Move` / `Attack` / `Union Attack`。
  - 一般 `Attack`、Guard 反擊與 `Union Attack` 已套用固定兵種克制；有利方傷害為基礎傷害的 `125%`：`Infantry → Spearman`、`Spearman → Cavalry`、`Cavalry → Archer / Crossbow`、`Archer / Crossbow → Infantry`。只作用於一般隊伍間的傷害，不作用於攻城器、建築／城門或單挑。`Charge` 對 Spearman 的既有 `900` 傷害與 `600` 反傷為獨立特殊規則，不再額外疊加此 25%。Battle Log 會標記觸發的克制。
  - `Farm` 內的騎兵不可使用 `Charge`，且 Charge 的目標格或穿越後落點也不可是 `Farm`；騎兵仍可正常進出農田與使用一般攻擊。
  - `Hill` 保持每格移動消耗 `2`；站在 Hill 的 `Archer`／`Crossbow` 普通攻擊距離 `+1`。此加成不套用於 `Catapult`、彈藥耗盡的 weak close attack 或 `Strategy (Fire)`；夜晚遠程距離減少仍會先套用。
  - `SupplyCart` 目前使用 `supplycar_idle_ne.png` / `supplycar_idle_sw.png` 專用 idle 動畫素材；`NE/NW` 為 `2x2` 四幀，`SW/SE` 為 `1x3` 三幀。
  - morale 已接入 prototype 士氣壓制：`Morale <= 30` 的一般 battle team 有移動懲罰，effective move range 會降低 `1`，但最少保留 `1`；`Mess` 狀態中的隊伍 effective move range 會被壓到 `1`。
  - `Morale <= 15` 的一般 battle team 在自己回合開始時有機率自動陷入 `Mess`；`Mess` 狀態中不能 Attack、Union Attack、Duel 或使用 Strategy，Worker 也不能 Bridge / Wood Fence，且自己回合開始會有 `5%` active troops 離隊。
  - `Mess` 狀態會在 battle marker 上長駐顯示 `MESS` 徽章與橙紅色警示外圈，直到狀態被 `Calm` 清除或回合倒數歸零。
- 一般部隊將領可使用 prototype `Duel / 單挑`：
  - 選取有將領的一般 battle team，若 2 格範圍內八方向有敵方將領隊伍，command menu 會顯示 `Duel`。
  - 單挑可跨 `L0` / `L2` 層，例如地面隊伍可挑戰牆頂隊伍，牆頂隊伍也可挑戰地面隊伍。
  - 點擊後可選擇相鄰 opponent；攻城器與 Worker 不可發起或接受單挑。
  - opponent 是否接受由 prototype battle score 判斷；差距過大時弱勢方可拒絕。
  - 單挑只比較 officer / general battle attribute，不套用兵種修正或 morale bonus；勝方 morale 增加，敗方將領被俘且該 battle team 離場；平手時雙方保留並各自小幅增加 morale。
- top bar 的 Team A / Team B 總兵力會隨部隊損兵更新；一般攻擊、合擊、牆頂投放攻擊與 fire damage 造成的 `TroopCount` loss 都會扣到對應陣營。
  - `Troops` 以 `active / wounded` 顯示一般戰鬥隊伍兵員，包含 Worker，但不包含攻城器 HP；active 會參與戰鬥，wounded 暫時不能戰鬥。
  - `Generals` 顯示目前仍在戰場上的一般部隊將領數量；`Worker` 與攻城器不算將領。
  - 任一方 `Generals` 變成 `0` 時 battle 立即結束；畫面中央會顯示 `Battle Finished` result overlay、勝利方與敗方沒有將領的原因，並停止 End turn / command 操作。
  - `Siege` 另行顯示目前仍在戰場上的攻城器數量；攻城器撤退或被摧毀時會扣除。
  - `Ram`、`Ladder`、`Catapult`、`SupplyCart` 不掛 officer name；name plate 與 log 直接顯示攻城器／後勤車名稱。
- 右下角新增 prototype `Battle Log` 面板：
  - 記錄 battle team 的 `Move`、`Attack`、`Hurt`、`Retreat`、`Strategy`、`Action` 與回合切換事件。
  - battle team 受傷紀錄會顯示受擊隊伍、傷害數字，以及造成傷害的攻擊隊伍；fire damage 則標記為 fire 來源。
  - log 可切換 `All` 或 `Self`；`Self` 目前依當前 acting side 顯示該隊伍相關紀錄。
  - 面板採用 gameplay floating log 風格，支援拖曳移動、最小化／還原，以及右下角 resize grip 調整大小。
- Battle UI 已接入 `LocalizationService`，會跟隨 options language 顯示繁中或英文；目前涵蓋 top bar、battle log panel chrome、command menu、unit menu info、Tile Info/Selected Piece 面板，以及地形／建物／天候／時段／風向／指令狀態等可見 UI 文案。Tile Info/Selected Piece 是 debug 用資訊面板，已改為 scrollable text，方便檢查較長的格子與單位狀態。Top bar 只保留 battle-only `Option` 入口；popup 內可執行 battle quick save/load、切換語言、切換 BGM/SFX、調整 BGM/SFX volume，並透過 `OptionSettingsStore` 儲存設定；音訊設定與主遊戲共用同一份 options 存檔。Battle Log 的逐條事件敘述仍保留 prototype 文字，待後續整理成 log locale keys。
- Command menu 只顯示目前可實際執行的 action；若 Move / Attack / Strategy / Charge / Hide / Capture Cart / Hire Officer / Supply / Worker action 等沒有合法目標或條件不足，就不在 action list 佔位顯示 disabled button。
- Command menu 開啟時維持在 Battle Log 之上，因此兩個可拖曳面板重疊時，指令按鈕仍可正常點擊；它仍低於 battle option 與 battle finished overlay。
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
  - 一般 battle team 可使用 prototype `Strategy (Mess / Calm)`：對同層 `3` 格 Chebyshev 範圍內敵方一般 battle team 可施加 `Mess`，成功後目標 `Mess 2 turns` 並 `Morale -12`；對同隊已 `Mess` 的一般 battle team 可使用 `Calm`，解除 `Mess` 並 `Morale +15`。
  - Team A / Team B 各自有 `Strategy Plans 6/6` 限制；`Strategy (Fire)`、`Strategy (Mess)`、`Strategy (Calm)` 每次成功進入施放流程消耗 `1` 點，Mess 失敗也算已消耗計策。
  - 同一 battle team 每個己方行動回合只能使用一次 Strategy；用過後直到下一次己方回合前不可再次使用。
  - `Mess` 成功率會依施術者與目標 officer battle attribute、目標 morale 與低士氣狀態調整；第一版不允許混亂部隊攻擊友軍，以避免隨機失控造成過強負回饋。
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
  - 野戰不部署 `Ram` 或 `Ladder`；兩者僅保留給攻城／護城河攻城情境。
  - 目前野戰仍以程序式地形為主，但會額外疊加 scene `ObjectLayer` 內的 `Building` 圖塊，讓共用 `BattleScene` 的測試建築可直接出現在野戰模式。
- 戰鬥選單保留洛陽、漢中野戰，並新增鄴、晉陽、下邳、建業、襄陽、江陵六張地區野戰；各 `FIELD_<CITY>` 都載入自己的 `scenes/battle/field/FieldBattle<City>.tscn`，繼承共用戰鬥 UI，並以 `UseEditorAuthoredLayout` 讀取可在 Godot 直接編輯的 `GroundLayer`、`ObjectLayer` 與 `OverlayLayer`。
- 新增野戰時須遵循 `docs/FIELD_BATTLE_DESIGN_RULES_ZH.md`：先定義核心行軍問題、前線與縱深，再配置堡壘、地形及部署；未來四張野戰沿用該文件的檢查表。
- `FieldBattle` 的 editor preview 必須隱藏繼承自共用攻城 scene 的 `CastleLayer`，避免父 scene 的城牆資料在編輯器殘留顯示；野戰場景的 WYSIWYG 預覽應與 runtime 地圖一致。
- 所有戰鬥場景共用 `BattleSceneController` 的相機縮放：滑鼠滾輪上為放大、下為縮小，`Camera2D.Zoom` 限制在 `0.75..1.35`（`0.75` 最近、`1.35` 最遠）；每次縮放後會重新夾住地圖位置，避免露出地圖外部。滾輪若被 UI 控制項處理，則不觸發戰場縮放。
- 新城市野戰應從 `scenes/battle/field/FieldBattleTemplate.tscn` 複製，建立對應的 `data/scenarios/battle/field_<city>.tres`；地圖視覺仍在 TileMap，runtime 規則、A* 與 AI 則由轉換後的 `BattleMapData` 使用。
  - `FieldBattleLuoyang.tscn` 以洛陽盆地的縮尺地形作為第一個城市野戰範例：北方以連續邙山山脊與疏密相間的山腳森林界定戰場，中央保留洛水河谷、兩岸濕地／岸地與穿越河谷的渡口道路；渡口北岸有兩座聚落建築，南方道路旁有兩座農舍與開闊農地。渡口三格寬的主線使用「官道」，山腳與農地支線使用「磨損道路」。`RiverEffectLayer` 只覆蓋洛水深水河格，沿用既有 `moat_water.gdshader` 呈現流動水光，渡口、淺水與河岸過渡保持靜態。場景使用既有 floor、building、tree、rock、forest、wood、hill、mountain 與 farm tiles。投石車初始部署由洛陽專屬 Scenario Data 設為南岸可通行格 `(14,17)`，避免共用預設 `(14,15)` 落在河面。
  - `FieldBattleHanzhong.tscn` 以漢中盆地為第二個城市野戰範例：北側秦嶺與南側大巴山的山林夾住中央漢水，河道在戰場內蜿蜒；單格中央官道直接接到 `bridge_01.png` 第二列的雙格石橋 `(12,11)`、`(12,12)`，橫越河段後連接南側平壩道路，南岸配置農地與聚落。橋前南北主線使用「官道」，平壩與聚落支線使用「磨損道路」；西側另有三格寬的淺灘 `(2..4,10..11)`，可通行但每格消耗 `2` 點移動力，形成較慢的側翼／伏擊路線。兩座堡壘後撤至北岸山前縱深 `(9,5)`、`(16,5)`，分別由守軍出生點 `(10,6)`、`(14,6)` 掩護；攻方必須先通過主橋或淺灘、再深入北岸防區才可同時奪取，不能把渡河本身當成直接勝利。兩個石橋格各自保留可通行、可受攻擊的 `Bridge` 地形、耐久度與水面底圖；`RiverEffectLayer` 只覆蓋深水河格，沿用既有 `moat_water.gdshader` 呈現流動水光，並置於橋物件下方；場景僅使用既有 floor、building、forest、swamp、hill、mountain、wood、farm、rock 與 bridge tiles；所有部署皆由 `field_hanzhong.tres` 定義。
  - 六張新增地區野戰均為 25×25，且只使用既有 floor／forest／swamp／hill／mountain／farm／bridge 與河岸物件圖集；它們刻意有不同的行軍問題：鄴是沒有河橋阻隔的開闊農田會戰，中央作物會抑制騎兵 Charge、兩翼草地可包抄；雙格可通行乾溪是中央低窪減速帶，守軍直接駐守北側堡壘 `(10,4)`、`(14,5)`，攻方必須突破後再分兵奪堡。晉陽以雙格汾河、兩格主橋與西側淺灘構成狹河谷搶橋；下邳的兩條水道把戰場分成數個陸塊，兩處小橋與堤路決定支援路線；建業將長江放在不可跨的西側邊界，東側以丘陵、森林與山地壓縮行軍；襄陽以雙格漢水、雙格橋頭和北側丘陵讓弓弩部隊控制渡口；江陵不放橋梁或深水主河，而以蘆葦沼澤、淺水渠與兩條堤路形成慢速濕地伏擊戰。各場景另有 `field_<city>.tres` 保存天候、時段與日後可獨立調整的部署資料；`RiverEffectLayer` 只由該場景的深水格生成，不能沿用其他場景的水面資料。
- 晉陽與襄陽野戰在相同「單河、單橋」基礎上刻意採取相反地貌：晉陽是單條雙格汾河及唯一雙格主橋，南北兩岸的左右側各有多格厚山體、內側接可通行丘陵，中央河谷和橋頭才容得下主力推進；橋北出口的 `(10,8)`、`(12,8)` 是左右兩座小型建築，圍繞主路據點 `(11,8)` 形成步兵防線。晉陽另以大面積林緣樹群描出兩側山腳、在南岸農地形成兩組補給農莊，並以船筏與河道物件加強汾河寬度，使峽谷在縮小檢視時仍清晰可辨。襄陽則以單條雙格漢水及唯一雙格主橋形成橋頭正面攻防，北岸為大面積、可通行的丘陵弓弩陣地，最北端山脊只作背景與側翼界線，沒有大片濕地。兩圖的初始部隊改為緊湊的橋頭／谷口兵團，而非橫跨地圖的兩排；指揮與工人留在後方丘陵支援；所有野戰的一般戰鬥單位與 Worker 可直接出生於 Building／Fortress 格，表現駐守與據點防禦，攻城車與補給車則不會出生於建築格。船筏、木樁與礁石只放在深水河格，農地與森林邊緣則加入農事及林緣物件；這些分層地物只加強辨識度，不改變底層地形、移動、A* 或 AI 規則。
- 下邳兩處橋梁各使用兩格 bridge tile；依實際視覺方向，左側／南側 `(7,17)`、`(8,17)` 的兩格在 TileMap alternative tile 寫入 `Flip H`，右側／東側 `(17,10)`、`(17,11)` 的兩格則保留 `bridge_01.png` 原圖方向。runtime 讀取與 rebuild 會保留各橋格的水平鏡像，不改變橋梁的地形、耐久、移動或 A* 規則。
- 下邳補強為水網縱深戰：西南水道的 `(6..9,21..22)` 是較寬、但消耗移動力的淺水側路，提供不經橋梁的代價性包抄；北側水網後方以穀倉聚落建築 `(13,8)`、`(15,8)` 作步兵支點，兩座堡壘 `(16,3)`、`(20,6)` 分守不同堤路出口，讓攻方過河後仍須選擇深入方向。船筏、木樁、河岸物件只放在深水格，農具只用於聚落辨識；這些裝飾均為 visual-only，不改變底層地形、移動、A* 或 AI。
- 建業重構為石頭城、鍾山與長江防線：西側長江保持不可跨越，沿江低地以 wet grass／mud 表現濕地行軍代價但不形成淺灘；江岸石頭城堡壘 `(4,5)` 與建築 `(3,6)`、`(5,6)` 控制沿江支路，鍾山外圈以連續山體、內側兩團較密森林與階梯狀小丘壓縮東側行軍，驛站堡壘 `(16,7)` 與聚落 `(13,8)`、`(15,8)` 形成第二道防線。深水以不均勻的新船 `(0,2)`、礁石 `(1,2)`、船筏與木樁表現航道及岩岸；石頭城、濕地岸線、鍾山山腳與南側農莊分別加入林緣或農事 visual-only 裝飾。建業不加入橋梁或淺灘，底層移動、A* 與 AI 規則維持不變。
- 江陵補強為雙堤路與蘆葦沼澤伏擊戰：兩條橫向淺水渠不設深水或石橋，西、東堤路北端的濕地寨堡 `(5,3)`、`(15,3)` 位於兩條水渠與中央泥沼之後，守軍可出生於堡壘與相鄰建築 `(6,3)`、`(14,3)`，兩座寨堡後方另有堤上補給聚落。攻方必須跨水、穿越泥地縱深後才可奪取。淺水渠以不均勻分布的木樁、小舟及水面物件辨識；三片泥沼邊緣加密蘆葦感林緣、竹叢、倒木與樹樁，農具集中在南側高地農莊。這些物件均為 visual-only，不改變堤路、淺水、泥地、A* 或 AI。
- `SiegeAssault` 與 `MoatSiegeBattle` 會各部署一名攻方與守方 `Worker`：使用 `worker_idle_ne.png`／`worker_idle_se.png` 的四格 idle animation，具備低近戰與移動能力。
  - Worker 移動時使用 `worker_move_ne.png`／`worker_move_se.png` 的三格 walk animation；NW／SW 以對應方向的水平翻轉顯示。
  - 選取 Worker 後顯示專用的 `Bridge` 與 `Wood Fence` actions，不顯示 `Strategy`；每個 action 只會標示四向相鄰的有效施工格，並使用綠色菱形 highlight。
  - `Bridge` 可在 MoatSiegeBattle 的護城河上建橋，或修復受損橋面；橋面 `Bridge HP < Bridge Max HP` 時視為 damaged，`Blocks Movement = true`，battle team 不可通行，必須修到滿血才解除阻擋。
- Worker 可在 `MoatSiegeBattle` 的相鄰 `Moat` 格建造橋、修復受損橋面、修復受損城門及移除 `Trap`；橋梁需兩次 `Work` 才會完成，第一次為半 HP 的施工中橋面且不可通行，第二次回滿 HP 後才可通行。
  - `FieldBattle` 的 Worker 亦可在相鄰深水 `River` 格架設木橋；每格需兩次 `Work`，完成後供一般 battle team 與 Worker 通行，但 `Catapult`、`SupplyCart`、`Ram`、`Ladder` 等攻城器不可通過。雙格河道須依序完成兩格，單名 Worker 最少需四次施工才能跨河；木橋被摧毀時回復為 `River`，可再次施工，且 quick save/load 會保存其狀態。
  - `Wood Fence` 會合併安裝與拆除：點選無單位、無結構的乾地會安裝木柵欄；點選相鄰木柵欄則拆除。Worker 新建木柵欄時會隨機套用水平翻轉，避免連續柵欄視覺過於單一。
  - 木柵欄有 `600 HP`，所有戰鬥單位可依其結構傷害攻擊；HP 歸零後會立即移除木柵欄視覺並恢復通行。
  - 橋梁有獨立 HP，所有戰鬥單位可依其結構傷害攻擊橋面；HP 歸零時橋面會移除並還原為阻擋移動的 `Moat`。施工、破壞完成後立即更新 `MoatLayer`、`ObjectLayer` 與通行規則。
  - 施工時播放 `worker_work.png` 的工作動畫，Worker 會面向目標格；上排為 NE、下排為 SE，各為四格；NW／SW 以對應排的水平翻轉顯示。
  - Worker 受擊時使用 `worker_hurt_ne.png`／`worker_hurt_se.png` 的兩排、每排三格動畫；NW／SW 以水平翻轉顯示。
  - Worker 攻擊時使用 `worker_attack_ne.png`／`worker_attack_se.png` 的兩排、每排三格動畫；NW／SW 以水平翻轉顯示。
- `MoatSiegeBattle` 會在攻城 prototype 的接近路線加入：
  - 不可直接通行的 `Moat`
  - 位於中央接近路線、可通行的 `Bridge`
- `assets/battle/floor/floor.png` 是共用 floor atlas；第一列依序為 grass、road、courtyard、wall walk、forest、river（Moat）、river2（River）與 swamp；第二列依序為 coast、wet grass、mud、pebble、shallow water、worn road、official road、dry river bed。shallow water 是可涉渡淺灘，可通行但每格移動消耗為 `2`。wet grass 依 Grass 規則、mud 依 Swamp 規則、pebble／worn road／official road／dry river bed 依 Road 規則。乾河床預留給宛、襄陽、涼州等野戰場景；River 不可通行，Swamp 可通行但移動消耗為 2，Coast 是可通行岸地；只有 river（Moat）可套用攻城橋樑規則。橋格使用 `assets/battle/object/object_01.png` 的第四格 bridge tile，繪製於 `ObjectLayer`。
- `MoatLayer` 會額外套用輕量水流 shader，對 river tile 做緩慢 UV 位移與微弱明暗起伏；此效果僅為場景表現，不改變 `BattleMapData`、橋樑施工/破壞或任何移動／A* 規則。
- `assets/battle/object/river_object_01.png` 為 `512x384` 的 `4x3` atlas：前兩列保留既有八種船、輕舟、棧橋、木棧台、蘆葦蓮葉、礁石、水面擾動與木筏物件；第三列只登錄新增的兩格：`(0,2)` 為船、`(1,2)` 為礁石。共用 Object TileSet 以 source 9 登錄。野戰場景將其放在獨立的 `RiverObjectLayer`，因此只作河道視覺裝飾，不會改變水域、橋格、移動、A* 或 AI 規則。
- `assets/battle/object/farm_object_01.png` 提供草垛、稻草人、收穫木車、穀袋、蔬果籃與其他農事擺設；共用 Object TileSet 以 source 10 登錄。野戰場景將其放在獨立的 `FarmObjectLayer`（z=6，高於農田所在的 `ObjectLayer` z=5），只作農地視覺裝飾，並避開道路、建築、作物與部署格，不改變地形、移動、A* 或 AI 規則。
- `assets/battle/object/forest_object_01.png` 提供倒木、樹樁、高樹、灌木、枯木、苔石、竹叢與果實灌木；共用 Object TileSet 以 source 11 登錄。野戰場景將其放在獨立的 `ForestEdgeObjectLayer`，配置於既有森林外緣，只作視覺裝飾，不改變森林地形、車輛可否停留、騎兵森林限制、移動、A* 或 AI 規則。
- `MoatSiegeBattle` 現在應以 scene 的 `MoatLayer` 承載護城河資料；`GroundLayer` / `ObjectLayer` 繼續承載道路、庭院與橋面，不再把 moat/bridge/road/courtyard 座標硬寫進 `.tres`。
- `TileMap -> BattleMapData` 轉換時，只有 `ScenarioType == MoatSiegeBattle` 可以讀入 `MoatLayer`；`SiegeAssault` / `FieldBattle` 即使共用同一份 scene、且 scene 內保留了 moat tiles，也不能讓隱藏的 moat data 繼續阻擋移動、攻城車或 A*。
- `ObjectLayer` 內的 bridge tile 在 `MoatSiegeBattle` 應讀成 `Bridge` 地形；同一份 scene 在 `SiegeAssault` 模式下，這些格應回填為 `Road`，避免接近路線中斷成草地。
- `assets/battle/object/object_01.png` 的第 5 格（atlas 索引 `4`）為 `WoodenFence`；`ObjectLayer` 讀取該圖塊時應建立可阻擋移動、可由 Worker 移除的木柵欄。
- `assets/battle/object/building_01.png` 是 `ObjectLayer` 的 building atlas source，規格為 `1 row x 3 frame`、每格 `128x128`；三個 frame 都讀成可駐守的 `Building` 結構，runtime rebuild / battle quick save/load 需保留選到的 building frame。
- `assets/battle/object/fortress_01.png` 是 `ObjectLayer` source ID `12` 的防禦堡壘 atlas，規格為 `1 row x 3 frame`、每格 `128x128`，只登錄前兩格。堡壘沿用 `Building` 的可駐守、20% 直接傷害減免及攻城器禁止進入規則；初始歸守方，runtime 額外顯示藍旗。任何一般戰鬥單位抵達堡壘格後，堡壘即改歸該隊，攻方為紅旗、守方為藍旗；旗幟不會在移動動畫尚未抵達前提早切換。歸屬與選用 frame 會被 battle quick save/load 保存。堡壘是縱深防線與反擊目標，不得貼近橋頭、渡口等前線突破格；應位於渡河／過隘之後的丘陵、道路分岔或防區內，讓攻方必須先突破前線、再深入才能爭奪勝利目標。晉陽以橋北主路控制點 `(11,8)` 配合東側山麓側防點 `(15,6)`；襄陽則有四座堡壘：西側丘陵 `(4,4)`、東側丘陵 `(19,4)`、北側預備線 `(10,5)` 與東側橋頭 `(14,8)`。襄陽攻方可選擇殲滅守軍，或在守方回合結束前同時佔領所有堡壘取勝。
- 野戰勝利條件為：任一方沒有仍在場的帶 officer 一般 `Battle Team`（`Worker`、無 officer 的一般單位、`Ram`／`Ladder`／`Catapult`／`SupplyCart` 均不計入）即敗北；因此只剩車輛不能繼續戰鬥。此外，攻方只要所有防禦堡壘均為攻方歸屬，並在守方回合結束後仍保持該狀態，即以堡壘控制獲勝。此時點讓守方獲得完整一回合反奪堡壘的機會；守方沒有對稱的堡壘反佔勝利，仍以擊潰或逼退攻方取勝。
- `assets/battle/object/forest_01.png` 是 `ObjectLayer` source ID `2` 的 forest atlas，規格為 `2 rows x 4 frame`、每格 `128x128`；八個 frame 都讀成 `Forest` 地形並保留選到的變體，供 WYSIWYG 場景編輯與 runtime rebuild / battle quick save/load 使用。
- `assets/battle/object/wood_01.png` 是 `ObjectLayer` source ID `6` 的第二組 forest atlas，規格為 `2 rows x 4 frame`、每格 `128x128`；規則與 `forest_01.png` 相同，但 runtime rebuild / battle quick save/load 會額外保留圖集來源，避免將 wood 變體換回第一組森林圖。
- `assets/battle/object/swamp_01.png` 是 `ObjectLayer` source ID `3` 的 swamp atlas，規格為 `2 rows x 4 frame`、每格 `128x128`；八個 frame 都讀成 `Swamp` 地形並保留選到的變體，移動消耗仍為 2，供 WYSIWYG 場景編輯與 runtime rebuild / battle quick save/load 使用。
- `assets/battle/object/hill_01.png` 是 `ObjectLayer` source ID `4` 的 hill atlas，規格為 `2 rows x 4 frame`、每格 `128x128`；八個 frame 都讀成可通行的 `Hill` 地形，移動消耗為 2，供 WYSIWYG 場景編輯與 runtime rebuild / battle quick save/load 使用。
- `assets/battle/object/mountain_01.png` 是 `ObjectLayer` source ID `5` 的 mountain atlas，規格為 `2 rows x 4 frame`、每格 `128x128`；八個 frame 都讀成不可通行、不可起火的 `Mountain` 地形，供 WYSIWYG 場景編輯與 runtime rebuild / battle quick save/load 使用。
- `assets/battle/object/farm_01.png` 是 `ObjectLayer` source ID `7` 的 farm atlas，規格為 `3 rows x 4 frame`、每格 `128x128`；十二個 frame 都讀成可通行、移動消耗為 1 的 `Farm` 地形，不提供建築防禦或森林 Hide，供 WYSIWYG 場景編輯與 runtime rebuild / battle quick save/load 使用。
- `Building` 格只可容納一般人類 battle team，攻城梯、衝車、投石車與補給車等攻城器不可進入；駐守時承受的直接傷害降低 `20%`；火焰傷害不受減免，且建築格正在燃燒時防禦加成暫時失效。騎兵不可對建築格發動或穿越 Charge，格子資訊會顯示建築防禦狀態。
- runtime 會將 Building 從固定 `ObjectLayer` 提升至與單位共用的 `BattleDepthLayer`，以 `grid.X + grid.Y` 排序：位於建築後方的單位會被遮住，位於前方或同格駐守的單位會顯示在建築前。可移動／施工及選取格高亮與 Building 在同一深度帶，但排序在建築之後、單位之前；因此合法移動至或直接點選防禦據點時，菱形 grid 仍清楚可見。
- `BattleScene.tscn` 的 `ObjectLayer` 在 `(6, 12)` 放置 `building_01` frame 0，作為戰場內可直接測試的建築防禦據點。
- runtime 需額外保留 bridge 的 visual flag，讓 `MoatLayer` 的河水底圖與 `ObjectLayer` 的橋面 sprite 可以同時存在，不會因 terrain 正規化而把橋面吃掉。
- 若橋面或木柵欄在 editor 內使用了 `Flip H`，runtime 也必須保留該 object tile 的 `alternativeTile` 水平翻轉設定，否則 `NW` scene 進入遊戲後橋面方向或柵欄變化會跑掉。
- 讀取 `Use Editor Authored Layout` 的 scene 時，應先讀取原始 `TileMapLayer` 內容，再重建 shared tileset 與 runtime 視覺；不能在讀取前先重新指定 layer tileset，否則 bridge 之類的 editor-authored flip 資訊可能會遺失。
- 目前 `NorthWest` 戰場的 bridge visual 應預設跟隨 `DefaultStructureFacing = NorthWest` 套用水平翻轉，避免即使 editor flip 資訊遺失，遊戲內橋面方向仍與 `NW` 場景相反。
- `NE` / `NW` 戰場的「城內地面格」判定不應靠掃描牆線方向推測，應直接使用 runtime `BattleMapData` 的 `Terrain == Courtyard` 作為內城地面判定；這樣可同時避免 `NW` 關門守軍退城誤判，也不會讓 `SiegeAssault` 的攻城車在外城草地／道路被錯誤當成城內而無法移動。
- battle prototype top bar 具備 battle-only `Save` / `Load`：存到 `user://saves/battle_quicksave.json`，讀回同一個 battle scene 的戰鬥狀態。第一版保存 scenario type、turn、time/weather/wind、Gold/Food、strategy plan、可變地圖 cell、存活單位位置與兵力/HP/士氣/彈藥/hidden 狀態、牆頂攻擊剩餘次數、fire state 與 battle log；Load 時會重用 scene 既有 battle piece marker 並更新位置／陣營／狀態，避免重複建立單位；尚未併入大地圖 `WorldState` slot save/load。
- `Battle / 戰鬥` 入口目前已支援 5 個模式選項：
  - `FieldBattle`
  - `NE SiegeAssault`
  - `NE MoatSiegeBattle`
  - `NW SiegeAssault`
  - `NW MoatSiegeBattle`
- `BattleScene.tscn` 與 `BattleSceneNorthWest.tscn` 目前都以 scene 內手工編排的 `TileMapLayer` 為主。
- runtime 會直接讀取這兩個 scene 的 editor-authored layout，讓 `NorthEast` / `NorthWest` 都維持 WYSIWYG 工作流。
- 若需測試其他手工編輯 TileMap 場景，可在 Inspector 開啟 `Use Editor Authored Layout`，讓 controller 讀取 scene 內 baked layout，而不是用 scenario data 重建。
- 每個 battle team 單位在其所屬行動方開始時，會回復至 `10/10` 行動力。移動會按選定路徑扣除行動力：道路每格 `1`、一般地每格 `2`、森林／沼澤／丘陵／淺水等高移動成本地形每格 `3`。一般攻擊消耗 `5` 行動力；單位可先移動後攻擊，但攻擊成功後會標記為已攻擊，本次己方行動階段不可再移動或再次一般攻擊。選取單位時，指令視窗與 Tile Info 會顯示目前行動力；Move／Attack 按鈕也會顯示路徑耗能或攻擊成本。進入 Move 選擇時，移動後仍有至少 `5` 行動力的格子顯示綠色，仍可到達但不足一般攻擊成本的格子顯示黃色；指向格子時 top bar 會同步顯示耗能、剩餘行動力、剩餘移動格數與能否攻擊。battle quick save/load 會保存單位行動力與本次行動方是否已攻擊。
- `MoveRange` 同時是每個單位在其所屬行動方內累積可走的最大格數，不是每次點選 Move 的獨立上限。移動每跨一格扣除 `1` 點剩餘移動格數；因此步兵的 `4/4` 在先走兩格後會成為 `2/4`，即使行動力仍足夠也不能在同一行動方再走超過兩格。騎兵維持自己的較高 `MoveRange`（目前為 `6`），同樣受行動力與累積移動格數雙重限制。battle quick save/load 亦保存剩餘移動格數。
- 合擊由發起單位與相鄰、符合攻擊條件的同陣營友軍共同執行。發起單位消耗 `5` 行動力並標記為已攻擊；每名實際加入的友軍消耗 `4` 行動力，但不標記為已攻擊，仍可依其剩餘行動力與移動格數在同一行動方移動或一般攻擊。本階段不設定每回合合擊參與次數上限；合擊按鈕會顯示參與人數、發起成本與每名友軍成本。
- 一般人類戰鬥單位（不含 Worker）可使用 `Guard`，消耗 `5` 行動力並完成行動。Guard 持續到下一次己方行動階段開始：受到近戰、弓兵或弩兵的一般攻擊時，初始傷害減少率暫定為 `20%`；每次有效攻擊後，下一次減傷率乘以 `0.5`（20%、10%、5%、2.5%…）。只有首次近戰直傷會以一般攻擊力 `40%` 反擊一次；投石、火計與牆頂投放均不減傷且不觸發反擊。每次 Guard 減傷都會在戰鬥紀錄顯示比例與減傷前後數值；單位狀態會顯示下次減傷率與反擊是否已用，quick save/load 會保存該狀態。
- 補給車的恢復／維修與補充武器均消耗 `5` 行動力並完成行動。AI 會先嘗試原地補給，再將可用的一般攻擊、合擊與可達防禦堡壘放入同一批候選行動，以預計傷害、擊破獎勵、目標武將價值、合擊友軍行動力機會成本及堡壘目標分數計分；同分或相近分數以可重現的小幅變異打破固定行為。堡壘不是固定優先：攻方奪取未佔堡壘、守方回奪失守堡壘／防衛受威脅堡壘的分數均扣除距離，並受該 battle team officer Intelligence 影響；最後一座攻方未佔堡壘另有勝利加分。失守／敵方堡壘上的敵軍也會獲得額外 attack score。若可用行動力扣除路徑成本後仍保留 `5`，則會先完成移動動畫，再直接攻擊規劃時保留的目標（不在移動後重選）；Battle Log 會記錄該 score／variance，若攻擊因單位狀態、目標或射程失效而取消，也會記錄明確原因。若選擇純移動，log 會額外列出路徑耗能、移動後剩餘行動力／移動格數，以及沒有可用移動後攻擊方案的原因。否則僅移動至最接近目標的最遠合法位置並完成行動。
- 野戰 Worker 會把相鄰河格的新木橋及未完工木橋視為工程候選；AI 模擬單格或連續最多四格河段全數完工後，友軍一般戰隊抵達敵軍或爭奪堡壘的最短路徑。僅在至少縮短 `3` 格、並以施工段數、接近施工岸距離、敵方威脅與 officer Intelligence 計分後勝過其他攻擊／合擊／堡壘候選時，Worker 才會先移往施工岸，跨回合完成施工／修復。開局與工程 Battle Log 會列出橋格、目標、路徑縮短值、score 與 variance。
- 野戰 Worker 的木柵工程也屬同一 AI score 池：AI 只會在敵軍距離 `5` 格內、附近有己方戰隊支援、且木柵會讓敵軍到己方已佔 Fortress 的最短路徑增加至少 `3` 格時設置；若模擬顯示木柵會令己方通往敵軍的主路徑增加超過 `2` 格或中斷，則不會設置。現有木柵若已造成該己方路徑阻塞，Worker 可拆除。工程 Battle Log 會列出 build/remove、保護目標或重新開路理由、路徑影響、score 與 variance。
- 設置木柵消耗 `5` 行動力，拆除消耗 `3` 行動力，兩者均完成 Worker 本回合行動；不足行動力時不顯示為可選工作目標。AI 木柵候選 score 會扣除此工程行動力機會成本。
- Worker 木柵使用獨立 `WorkerFenceLayer`（z=7，且在農田物件之後）覆蓋於 `FarmObjectLayer` 之上；因此農田與其他 visual-only 地物會保留，木柵、其阻擋格與耐久資料也能同時一致可見。
- 低兵力／HP（最大值 45% 以下）的 AI 一般戰隊另有存活決策：它會將目前可攻擊收益與預估敵方射程威脅比較，並依 officer Intelligence 評估移至 Building／己方 Fortress、接近補給車、Guard／留守或撤退。低於 22% 且威脅高於可立即攻擊收益時，撤退會成為候選；所有存活決策都會在 Battle Log 寫出原因、score 與 Intelligence。每次 AI 回合開始亦寫入開局堡壘／殲敵計畫與低兵力隊伍數。
- 投石車一般攻擊的動畫順序為：投石車攻擊 → 石彈飛行完成 → 目標受傷與傷害數字 popup；弓兵與弩兵同樣在箭矢命中後才觸發傷害結算、戰鬥紀錄與 Guard 判定。
- 頂欄會依目前語系顯示戰場名稱、日期與 Field Battle AI 控制鈕；繁中日期格式為 `YYYY 年 MM 月 DD 日`，英文維持 `YYYY Apr D`。
