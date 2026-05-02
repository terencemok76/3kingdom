# 三國策略遊戲設計文件（Godot 4.6.2 / C#）
## Phase 1 / Phase 1.5 繁體中文版

## 功能凍結
- 凍結標籤：`Phase1-Locked-v1`
- 凍結日期：`2026-04-12`
- 完成里程碑：`Phase1-Completed-v1`
- 完成日期：`2026-04-27`
- 完成狀態：Phase 1 核心可玩循環已完成並通過驗證。
- 目的：鎖定 Phase 1 實作範圍，控制複雜度，避免功能漂移。

## Phase 1.5 設計鎖定
- 設計鎖定標籤：`Phase1.5-Design-Locked-v1`
- 設計鎖定日期：`2026-04-28`
- 目的：在 Phase 1.5 實作前鎖定系統設計。
- 狀態：設計已鎖定，部分 UI / 指令流程已開始實作。
- 可在實作期間微調的項目：
- 數值 buff 百分比。
- 經驗值取得量與升階門檻。
- 兵種成本、維持費與相剋倍率。
- 道具稀有度、歸屬、位置與能力加成。
- AI 優先權權重與平衡常數。

### Phase 1 已鎖定範圍
- 2D 桌面版可玩地圖循環。
- 每月回合流程：玩家行動 -> AI 行動 -> 月份推進。
- 城市基本資源與歸屬：
- `Gold`、`Food`、`Troops`、`Officers`、`Farm`、`Commercial`、`Defense`、`Loyalty`。
- 城市選取與 HUD 城市資料顯示。
- 玩家 UI 指令：
- `Develop` / `Internal Affairs`、`Recruit`、`Move`、`Merchant`、`Attack`、`Civil Affairs`。
- 基礎 AI 回合執行。
- 以城市控制為核心的勝敗判定。

### Phase 1.5+ 延後項目
- 更完整的官職、職階、職位 buff 系統。
- 道具 / 裝備系統：`SpecialWeapon`、`SpecialItem`、`SpecialHorse`。
- 武將年齡、身體狀態、血緣關係與更深層人際系統。
- 人口容量、城市升級與完整平衡。
- 繼承、叛變、暴動、救濟與穩定度事件鏈。
- 進階 AI：官職、道具、繼承與戰略判斷。

### 變更控制規則
- 未列入 Phase 1 範圍的功能，預設延後。
- 新增功能需要明確批准，並建立新的 lock tag revision。

## 1. 概覽
- 工作名稱：`3Kingdom`
- 目標平台：Godot 4.6.2 + C# 的 2D 桌面策略遊戲。
- 靈感來源：早期《三國志》式戰略流程，但 Phase 1 採簡化範圍。
- 核心循環：以月份為單位進行城市管理、武將指令、AI 勢力行動與擴張。

## 1.1 劇本 / 故事
- 目前劇本只有一個：
- `storyId`：`yellow_turban_rebellion`
- 英文名稱：`Yellow Turban Rebellion`
- 中文名稱：`黃巾之亂`
- 開始日期：`184 年 1 月`
- 每個劇本檔應自行持有：
- 故事 metadata。
- 開始年份與月份。
- 勢力設定。
- `cityStarts`。
- `factionStarts`。
- 未來可以加入多劇本 JSON 與劇本選擇流程，不應再依賴全域 `scenario_setup.json`。

## 2. Phase 1 目標
- 交付一個可玩的 vertical slice。
- 2D 地圖包含可連線城市。
- 月份推進與回合流程可運作。
- 城市與武將有基本資料模擬。
- 玩家可執行核心指令。
- AI 勢力可自動行動。
- 基本勝敗條件可完成一局遊戲。

## 3. Phase 1 範圍外
- 外交系統：同盟、條約、婚姻。
- 詳細戰鬥地圖或戰術戰鬥。
- 天氣、災害、世界隨機事件。
- 武將技能、特技、裝備完整系統。
- 存檔 / 讀檔 UI polish。
- 音效、動畫與美術 polish。

## 3.1 Phase 1.5：內政與官職擴充
- Phase 1 的 `Develop` 將演化為更完整的 `Internal Affairs` / `內政` 系統。
- 內政是一個可多月持續的武將派任系統。
- 每個內政工作需要一名武將。
- 同一名武將同時只能執行一種內政工作。
- 玩家可在 UI 中中止內政排程。
- 戰爭或事件日後可中斷排程。

### 內政工作
- `Farm`：農業。
- `Commercial`：商業。
- `Defend`：防衛。
- `WaterControl`：防災。主要提升城市 `防災`，並少量提升 `忠誠`。
- `Construction`：建設。

## 3.2 Phase 1.5：道具系統
- 加入武將裝備與特殊道具。
- 道具會影響武將屬性，進而影響內政、城市與戰鬥結果。
- Phase 1.5 的道具系統只屬於戰略層，不加入獨立戰鬥場景裝備 UI。

## 4. 核心遊戲模型
### 4.1 時間系統
- 時間單位：每回合 1 個月。
- 每月流程：
1. 玩家階段：對己方城市下達指令。
2. AI 勢力階段：各 AI 勢力依序行動。
3. 月底結算：
- 結算排程中的 `Develop` / `Internal Affairs`。
- 結算排程中的 `Recruit`。
- `Search` 功能已遷移至 `民事 > 訪察民情`，不再作為獨立玩家按鈕。
- 結算排程中的 `Move`。
- 結算排程中的 `Attack` 並進入戰鬥判定。
- 套用兵糧維持費與忠誠 / 民心壓力。
- 推進月份。
- 季節收入：
- `Gold` 在每年 4 月一次收取全年累積。
- `Food` 在每年 8 月一次收取全年累積。
- 全年累積使用城市目前月收入公式乘以 `12`。

### 4.2 勢力與城市
- 世界包含多個勢力與中立城市。
- 城市有：
- 擁有勢力。
- 資源。
- 武將列表。
- 連接城市列表。
- 地圖座標。
- 城市只可被一個勢力持有。
- 中立城市 `OwnerFactionId = 0`。
- 玩家起始城市會在遊戲開始後自動選中。

### 4.3 武將
- 武將資料來源：`data/person/officer.json`。
- 武將以 `Id` 作為唯一識別。
- 顯示名稱支援英文與繁中。
- 年齡由 `currentYear - birth_year` 計算。
- `death_year` 暫作歷史參考，不在 Phase 1 強制死亡。
- 未滿 18 歲的武將：
- 不會在劇本起始加入勢力。
- 不會被搜尋出仕。
- 不可被登用。
- 武將基本屬性：
- `Strength`：武力。
- `Intelligence`：智力。
- `Charm`：魅力。
- `Leadership`：統率。
- `Politics`：政治。
- `Combat`：戰鬥。
- `Loyalty`：忠誠。
- `Ambition`：野心。
- 武將狀態：
- `Idle`：待命。
- `Develop` / `Internal Affairs`。
- `Recruit`。
- `Move`。
- `Attack`。
- `Civil Investigation` / `訪察民情`。

### 4.4 官職、職階與經驗
- Phase 1.5 將武將成長拆成多條路線。
- 內政職階、軍事職階、軍師職階、文官職階彼此獨立。
- 每條職階可以提供不同 buff。

### 4.4.1 內政官職
#### 農業官
- 司農 / Minister of Agriculture
- 屯田校尉 / Commander of Agricultural Garrisons
- 典農中郎將 / Director of Farming
- 勸農使 / Commissioner of Agriculture
- 農政官 / Agricultural Officer

#### 商業官
- 度支尚書 / Minister of Finance
- 市令 / Market Supervisor
- 司市 / Market Director
- 商政官 / Commerce Officer
- 平準令 / Price Stabilization Officer

#### 水利官
- 都水使者 / Chief of Waterworks
- 河渠令 / Director of River Works
- 治河使 / River Control Commissioner
- 水衡都尉 / Superintendent of Waterworks
- 水利官 / Waterworks Officer

#### 建設官
- 將作大匠 / Chief Engineer / Chief Architect
- 工部尚書 / Minister of Works
- 營造官 / Construction Officer
- 將作監 / Directorate of Construction
- 修城校尉 / Fortification Officer

#### 防禦官
- 鎮軍將軍 / General Who Pacifies the Army
- 護軍 / Protector-General
- 城防都尉 / City Defense Commander
- 守城校尉 / Garrison Commander
- 戍衛將軍 / Defensive General

### 4.4.2 軍事官職
#### 高級將軍
- 大將軍 / General-in-Chief
- 驃騎將軍 / General of Agile Cavalry
- 車騎將軍 / General of Chariots and Cavalry
- 衛將軍 / General of the Guards

#### 中級將軍
- 前將軍 / General of the Vanguard
- 後將軍 / General of the Rear
- 左將軍 / General of the Left
- 右將軍 / General of the Right

#### 方位 / 出征將軍
- 征東將軍 / General Who Conquers the East
- 征西將軍 / General Who Conquers the West
- 征南將軍 / General Who Conquers the South
- 征北將軍 / General Who Conquers the North
- 鎮東將軍 / General Who Pacifies the East
- 鎮南將軍 / General Who Pacifies the South
- 安西將軍 / General Who Secures the West

#### 低階雜號將軍
- 裨將軍 / Deputy General
- 偏將軍 / Subordinate General
- 牙門將軍 / Gate Guard General
- 中郎將 / General of the Household

### 4.4.3 軍師職位
- 軍師 / Strategist
- 參軍 / Military Advisor
- 謀士 / Tactician
- 軍師中郎將 / Chief Strategist
- 大軍師 / Grand Strategist

### 4.4.4 文官職位
#### 高級文官
- 丞相 / Chancellor
- 司徒 / Minister of the People
- 司空 / Minister of Works
- 太尉 / Grand Commandant

#### 中央官職
- 尚書令 / Director of Secretariat
- 侍中 / Palace Attendant
- 中書令 / Director of the Imperial Secretariat

#### 地方官
- 太守 / Prefect
- 刺史 / Inspector
- 州牧 / Governor

### 4.5 兵種
- Phase 1.5 基本兵種：
- 步兵 / `Infantry`
- 槍兵 / `Spearman`
- 騎兵 / `Cavalry`
- 弓兵 / `Archer`
- 弩兵 / `Crossbow`
- 投石車 / `Siege`
- 城市儲存六兵種數量，不再只用單一 `Troops`。
- `Troops` 可作為總兵力相容欄位，但實際兵力結算以六兵種合計為準。
- 騎兵徵募需要城市持有馬匹，並受現有馬匹數量上限限制。
- 弩兵徵募需要 `弓坊`。
- 投石車 / 攻城兵徵募需要 `工坊`。
- 城市之間移動兵力時，可同時移動金、糧、馬與武將。
- 每年 1 月城市馬匹會自然繁殖；目前基礎出生率為 10%，後續可由養馬設施追加加成。

### 4.5.1 基礎相剋
- 步兵 vs 弓兵。
- 槍兵 vs 騎兵。
- 騎兵 vs 弓兵。
- 弓兵 vs 槍兵。
- 弩兵 vs 騎兵。
- 投石車 vs 城防。

### 4.5.2 戰場型態
- Phase 1.5 的 `Attack` 戰鬥分成 `野戰` 與 `攻城戰` 兩種型態。
- 兩者都仍屬戰略層數值結算，不進入獨立戰術地圖。

#### 野戰
- 用途：預留給未來的攔截、出城迎擊、行軍遭遇、城市外接戰。
- 核心規則：
  - 不吃城市 `Defense` 守城加成。
  - `投石車 / Siege` 不提供城防壓制，只視為低機動重裝兵力。
  - 兵種相剋、武將 `Leadership / Combat / Strength`、兵種配比是主要勝負因子。
  - 騎兵、弓兵、弩兵在野戰中的機動與相剋價值應高於攻城戰。

#### 攻城戰
- 用途：當 `Attack` 目標是敵方持有城市時，預設使用 `攻城戰`。
- 核心規則：
  - 吃城市 `Defense` 與守軍兵種配置。
  - `投石車 / Siege` 可壓制守方城防，降低守方有效防禦倍率。
  - 守軍依附城市駐軍作戰，守城方享有防禦優勢。
  - 攻方若獲勝，城市易主，攻方殘存兵力轉為新城留守兵力。
  - 攻方若失敗，生還兵與出征武將返回原城，攜帶金糧只返還部分。

#### 目前實作對應
- 目前專案中的 `Attack` 一律視為 `攻城戰`。
- `CombatResolver` 目前已套用：
  - 城市 `Defense`
  - 投石車壓制城防
  - 六兵種相剋
  - 攻方逐武將配兵後的兵種比例
- `野戰` 尚未獨立落地為可觸發型態，但設計上需保留未來擴充接口。

### 4.6 道具分類
- 名武器：
- 青龍偃月刀 / Green Dragon Crescent Blade
- 方天畫戟 / Sky-Piercing Halberd
- 丈八蛇矛 / Serpent Spear
- 雌雄雙股劍
- 青釭劍
- 倚天劍
- 七星寶刀
- 古錠刀
- 名馬：
- 赤兔
- 的盧
- 絕影
- 爪黃飛電
- 烏騅
- 道具：
- 孫子兵法
- 孟德新書
- 太平要術
- 青囊書
- 傳國玉璽

## 5. 玩家指令
### 5.1 內政
- UI 顯示為 `內政`。
- 內政包含：
- 農業。
- 商業。
- 防衛。
- 治水。
- 建設。
- 玩家選擇工作、武將與執行月份。
- 每名武將同時只能被派任一項工作。
- 內政每月底結算一次。
- 完成每月工作後，城市相關能力提升。
- 未來會加入經驗與職階升級。

### 5.2 徵兵
- 徵兵已整合入 `軍事` dialog。
- 每城每月可執行一次徵兵。
- 徵兵需要指派一名可用武將。
- 指令確認後消耗資源。
- 效果於月底結算。

### 5.3 移動
- 移動已整合入 `軍事` dialog。
- 可移動：
- 兵力。
- 金。
- 糧。
- 武將。
- 目標必須是相連的己方城市。
- 移動於月底結算。
- 被派去移動的武將，本月不可再被派去其他移動 / 攻擊 / 指令。

### 5.4 搜索 / 訪察遷移
- 獨立 `Search` 玩家按鈕已移除 / 隱藏。
- 原本「尋找在野武將」的功能遷移至 `民事 > 訪察民情`。
- 舊 `Search` backend 可暫時保留給 AI / regression 相容，但新玩家流程以 `訪察民情` 為準。

### 5.5 商人
- 玩家可買糧或賣糧。
- UI 會即時顯示金 / 糧增減預覽。
- 交易確認後即時生效。

### 5.6 攻擊
- 攻擊已整合入 `軍事` dialog。
- 目標必須是相連的敵方或中立城市。
- 玩家需要輸入：
- 出兵數。
- 攜帶金。
- 攜帶糧。
- 出征武將列表。
- 至少需要一名武將才能確認攻擊。
- 出征兵力、金、糧會在確認時即時扣除。
- 攻擊於月底發生戰鬥。
- 勝利時：
- 目標城市歸屬改為攻擊方。
- 出征武將留在佔領城市。
- 攜帶金 / 糧帶入佔領城市。
- 失敗時：
- 出征武將返回原城。
- 部分糧草保留，部分損耗。
- log 需要顯示攻防雙方君主名稱，方便閱讀。

### 5.7 人事
- `人事` dialog 包含：
- 賞賜武將。
- 指派官職。
- 登用他地武將。

#### 賞賜武將
- 對本城非君主武將賞賜金 / 糧。
- UI 需顯示武將忠誠。
- 賞賜確認後即時消耗城市資源。
- 忠誠提升公式：
- 金 100 -> 忠誠 +1。
- 糧 500 -> 忠誠 +1。

#### 指派官職
- 對本城非君主武將指派職位。
- 目前支援：
- `General`
- `Strategist`
- `Advisor`
- `Governor`
- 未來會改為更完整的官職 / 職階 / buff 系統。

#### 登用他地武將
- 可登用其他勢力或在野的成年武將。
- 隱藏在野人士需要先透過 `訪察民情` 找出行蹤，現身後才會出現在登用列表。
- 在野人士不一定接受登用；拒絕時不會加入城市或勢力。
- 不能登用其他勢力君主。
- 未滿 18 歲不可登用。
- 同勢力武將應使用 `Move` 調動，不使用登用。
- 忠誠高於門檻的武將會拒絕。
- 登用成功後：
- 消耗城市金。
- 武將移至登用城市。
- 武將加入玩家勢力。
- 從原城市 / 原勢力移除。

### 5.8 民事
- `民事` dialog 包含：
- 救濟。
- 訪察民情。

#### 救濟
- 玩家可投入金 / 糧安撫人民。
- 確認後即時消耗城市資源。
- 城市忠誠提升公式：
- 金 100 -> 城市忠誠 +10。
- 糧 1000 -> 城市忠誠 +10。

#### 訪察民情
- 即時執行。
- 可能結果：
- 發現在野成年人士的行蹤，使其在本城現身。
- 訪察不會直接招募在野人士；玩家需要再用 `人事 > 登用人士` 嘗試登用。
- 城市忠誠提升。
- 發現民間餘糧。
- 收得額外金。
- 採納農業建議，提高農業與忠誠。

## 6. AI 設計
### 6.1 AI Profile
- Phase 1 AI 以簡單規則運作。
- AI 可執行：
- 徵兵。
- 開發 / 內政。
- 搜索。
- 移動。
- 攻擊。
- AI 目標是維持資源、補兵、擴張。

### 6.2 AI 限制
- AI 不應作弊取得不存在資源。
- AI 指令需走相同 resolver。
- AI 每月行動後，世界狀態不可出現負數資源。
- AI soak test 需能穩定跑多個月。

### 6.3 Phase 1.5 AI
- 未來 AI 需考慮：
- 內政工作分配。
- 武將職階與專長。
- 道具配置。
- 敵我兵種與城市防禦。
- 忠誠、叛變與穩定度風險。

## 7. 資料模型
### 7.1 主要 Domain Classes
- `WorldState`
- `CityData`
- `OfficerData`
- `FactionData`
- `CommandRequest`
- `PendingCommandData`
- `InternalAffairsScheduleData`

### 7.2 CityData
- `Id`
- `NameEn`
- `NameZhHant`
- `OwnerFactionId`
- `Gold`
- `Food`
- `Horses`
- `Troops`
- `InfantryTroops`
- `SpearmanTroops`
- `CavalryTroops`
- `ArcherTroops`
- `CrossbowTroops`
- `SiegeTroops`
- `Farm`
- `Commercial`
- `Defense`
- `DisasterPrevention`
- `Loyalty`
- `HasBowWorkshop`
- `HasSiegeWorkshop`
- `OfficerIds`
- `ConnectedCityIds`

### 7.3 OfficerData
- `Id`
- `Name`
- `NameZhHant`
- `Role`
- `Belongs`
- `BirthYear`
- `DeathYear`
- `Strength`
- `Intelligence`
- `Charm`
- `Leadership`
- `Politics`
- `Loyalty`
- `Ambition`
- `Combat`
- `RelationshipType`
- `CityId`
- `LastAssignedYear`
- `LastAssignedMonth`
- `LastAssignedCommand`

### 7.4 InternalAffairsScheduleData
- `Id`
- `CityId`
- `OfficerId`
- `JobType`
- `RemainingMonths`
- `TotalMonths`
- `StartedYear`
- `StartedMonth`
- `State`

### 7.5 ItemData（Phase 1.5）
- `Id`
- `NameEn`
- `NameZhHant`
- `ItemType`
- `StrengthBonus`
- `IntelligenceBonus`
- `CharmBonus`
- `LeadershipBonus`
- `PoliticsBonus`
- `CombatBonus`
- `LoyaltyBonus`
- `OwnerFactionId`
- `OwnerCityId`
- `EquippedOfficerId`
- `Rarity`

## 8. Runtime Services
- `WorldRepository`：載入地圖、武將、劇本與起始配置。
- `TurnManager`：管理月份、玩家勢力、月底結算與經濟。
- `CommandResolver`：處理玩家 / AI 指令與月底結果。
- `CombatResolver`：處理簡化攻城戰。
- `AiController`：AI 行動決策。
- `LocalizationService`：多語言文字。

## 9. Godot Scene Architecture
- `GameBootstrap`：初始化世界、服務與 UI。
- `MapController`：城市節點、路線、地圖視覺更新。
- `HudController`：HUD、dialog、玩家輸入、log 顯示。
- `CityNode`：地圖上的單一城市節點。
- `RouteRenderer`：城市連線。
- `ChinaBackgroundMap`：背景地圖顯示。

## 10. 建議資料夾結構
```text
data/
  localization/
	locale.json
  map_locations-40.json
  person/
	officer.json
	portraits_names.json
  scenarios/
	phase1_scenario.json
assets/
  portrait/
scripts/
  core/
  data/
  map/
  ui/
scenes/
  ui/
docs/
tools/
  AiHarness/
```

## 11. UI / UX 流程
- 開局自動選中玩家起始城市。
- 左側 HUD 顯示城市資料與本城武將。
- 地圖城市名稱顯示 `城市名(cityId)`。
- `查看` dialog 支援：
- 本城武將。
- 全勢力武將。
- 城市資料。
- 表格支援欄位排序、表頭箭頭、斑馬紋與水平捲動。
- 武將 row 可 double click 開啟詳情。
- 城市 row double click 會切換地圖與左側 HUD 到該城市。
- 武將詳情 UI 支援 portrait 圖像。
- 所有中文 UI 文字必須放在 `data/localization/locale.json`。
- C# 不應 hardcode 中文 UI 文案。

## 12. 資源經濟
- 金：
- 每年 4 月一次收取全年累積。
- log 顯示玩家勢力各城市金收入。
- 糧：
- 每年 8 月一次收取全年累積。
- log 顯示玩家勢力各城市糧收入。
- 兵糧維持費每月扣除。
- 維持費不足時：
- 糧歸 0。
- 兵力下降。
- 城市忠誠下降。

## 13. 勝敗條件
- 玩家失去所有城市：失敗。
- 玩家控制所有城市：勝利。
- 勢力失去所有城市後，視為滅亡。

## 14. 實作里程碑
### M1：Project Skeleton
- Godot C# 專案可 build。
- 核心 service 可初始化。

### M2：Map + Selection + HUD
- 城市節點顯示。
- 城市可點選。
- HUD 正確更新。

### M3：Command Execution
- 玩家核心指令可用。
- 指令可建立 pending command。
- 部分即時指令可即時更新 UI。

### M4：Turn & AI
- 玩家結束回合後 AI 行動。
- 月底結算正確執行。

### M5：Stabilization
- Build 通過。
- AI harness 通過。
- 多月 soak test 穩定。

### M6：Officer Jobs and Rank（Phase 1.5）
- 內政工作排程。
- 官職與經驗。
- 職階 buff。

### M7：Item System（Phase 1.5）
- 道具資料。
- 道具歸屬。
- 武將裝備與屬性影響。

### M8：Officer Profile Extensions（Phase 1.5）
- 年齡。
- 身體狀態。
- 關係。
- 忠誠與野心互動。

### M9：Stability Events（Phase 1.5）
- 救濟。
- 暴動。
- 叛變。
- 繼承。
- 穩定度事件。

## 15. 測試清單
### Phase 1
- 遊戲啟動後自動選中玩家起始城市。
- 城市 HUD 顯示正確。
- 開發 / 徵兵 / 搜索每城每月限制正確。
- 移動可轉移兵、金、糧與武將到相連己方城市。
- 攻擊需要至少一名武將。
- 攻擊成功 / 失敗後武將位置正確。
- 4 月收全年金。
- 8 月收全年糧。
- AI 多月 soak test 不產生負資源或 pending command 殘留。
- 勝敗判定正確。

### Phase 1.5 內政
- 每個內政工作需要武將。
- 同一名武將不能同時做多個內政工作。
- 多月排程每月底正確扣月份。
- UI 可中止排程。
- 城市屬性提升正確。
- `防災` 內政會提升城市防災值，並降低後續災害發生率與損失幅度。
- 月底可能發生災害事件；高 `防災` 城市的副作用較小。

### Phase 1.5 人事 / 民事
- 賞賜不能選君主。
- 賞賜消耗資源並提升忠誠。
- 指派官職不能選君主。
- 登用他地武將不能選君主。
- 未成年武將不能登用。
- 高忠誠武將會拒絕登用。
- 救濟消耗資源並提升城市忠誠。
- 訪察民情可產生隨機正面結果。
- 訪察民情與救濟皆需指派武將，且同城每月各只能執行一次。
- 訪察民情找到的名物直接收入勢力道具庫存。

### Phase 1.5 兵種
- 兵種資料可載入。
- 招募可指定兵種。
- 戰鬥公式可套用兵種相剋。
- 投石車對城防有特殊效果。
- 城市可持有並顯示六兵種與馬匹資源。
- 騎兵招募會消耗馬匹，且成軍數量不會超過現有馬匹。
- 商人可購買馬匹。
- 移動指令可轉移馬匹。

### Phase 1.5 道具
- 道具資料可載入。
- 道具可歸屬勢力或武將；搜索 / 訪察民情取得的道具先進入勢力庫存。
- 裝備後屬性加成正確。
- 勢力滅亡後道具歸屬處理正確。
- 賞賜道具來源為勢力庫存；索回道具後回到勢力庫存。

## 16. 固定規則
- 中文 UI 必須走 `locale.json`。
- 劇本資料必須由 scenario JSON 持有，不再使用獨立 `scenario_setup.json`。
- 武將唯一識別使用 `Id`。
- 城市唯一識別使用 `cityId`。
- 起始勢力武將由 scenario 的 faction / city 設定指定。
- `officer.Belongs` 只作歷史傾向參考，不等於真實當前勢力。
- 未滿 18 歲不能起始加入、不能搜索出仕、不能登用。

## 17. 風險與緩解
- 風險：HUD 與 CommandResolver 變得過大。
- 緩解：以 partial class 或 service 拆分，保持小型功能檔案。
- 風險：Phase 1.5 功能互相牽連，導致 scope creep。
- 緩解：維持 design lock，未列入範圍的功能延後。
- 風險：多語言文字散落在 C#。
- 緩解：所有 UI 文案只放 `locale.json`。
- 風險：資料 JSON 格式錯誤。
- 緩解：啟動前 / 測試中加入 JSON parse 驗證。

## 18. Phase 1 交付定義
- 玩家可以開局、選城、下達指令、結束回合。
- AI 可以執行基本策略。
- 月底可以結算資源、移動、搜索、攻擊。
- 玩家可以勝利或失敗。
- Build 與 regression harness 通過。
- Phase 1.5 設計已鎖定，可在此基礎上逐步實作。

## 19. Phase 1.5+ 外交與間諜系統

### 19.1 外交
- 外交系統屬於戰略層玩法，在大地圖與月回合流程內解決，不進入獨立戰鬥場景。
- 後續會新增獨立的 `外交` 指令分類。
- 基本外交行動：
- `同盟`：雙方進入友好或同盟狀態，在固定月數內大幅降低互相進攻傾向。
- `停戰`：雙方暫停交戰，但不等同正式同盟。
- `進貢 / 贈禮`：送出 `金`、`糧`、`馬`、`道具` 以改善關係。
- `威逼 / 要求`：向弱勢勢力索取資源、政治讓步或投降。
- `毀約`：主動解除同盟或停戰，通常伴隨信任與關係懲罰。
- 外交成功率主要受以下因素影響：
- 君主 `魅力`
- 外交使者或軍師的 `智力 / 魅力`
- `軍師階 / 文官階` 加成
- 雙方目前關係
- 雙方勢力強弱差距
- 近期戰爭與背約紀錄
- 外交命令在 `月底` 結算，與其他戰略命令共用月末流程。
- 外交狀態至少應追蹤：
- 關係層級（`敵對 / 中立 / 友好 / 同盟`）
- 條約種類
- 剩餘月數
- 背約或失信懲罰

### 19.2 間諜系統
- 間諜系統屬於戰略層的祕密行動，應與公開外交分開處理。
- 每次間諜任務需要指派一位武將，並在 `月底` 結算。
- 基準規則下，`君主` 不可直接執行間諜任務。
- 間諜任務成功率主要受以下因素影響：
- 執行武將 `智力`
- 執行武將 `魅力`
- `軍師階 / 軍師號` 加成
- 目標城市 `忠誠`
- 目標君主與目標武將的警戒能力
- 兩勢力目前外交關係
- 每項間諜任務都帶有 `暴露風險`。
- 若任務暴露，應對發動方造成關係惡化，並可能出現：
- 間諜身份暴露
- 任務失敗
- 被捕
- 被處決
- 生還返回但忠誠下降

### 19.3 間諜任務種類
- `偵查`：
- 揭露目標城市的 `金`、`糧`、總兵力、六兵種數量、守城武將、城防、防災、進行中的內政，以及在野 / 隱藏武將線索，效果維持數月。
- `破壞`：
- 破壞目標城市的 `糧`、`金`、`防禦`、`防災`、兵種儲備、工坊 / 弓坊，或拖慢目前內政進度。
- `策反`：
- 降低目標武將忠誠或城市忠誠，並嘗試將低忠誠武將、在野武將或地方民心轉向我方。
- `行刺`：
- 嘗試刺殺或重傷目標武將或君主。
- `行刺` 應具有最高的失敗與暴露風險，並受到目標忠誠、城市防備、護衛與身份地位強烈影響。

### 19.4 設計約束
- 外交與間諜應優先共用既有 `月底排程`、`log`、`command resolver` 架構。
- 複雜的外交與間諜表單 UI 應使用 `Window`，不使用 `AcceptDialog`。
- 所有外交與間諜可見文字必須放在 `data/localization/locale.json`。

### 19.5 指派武將與行動月數
- 外交行動必須指派一位武將執行，通常應由 `君主以外` 的使者、軍師或文官出任。
- 間諜行動也必須指派一位武將執行，且執行中的武將在任務期間不可再接其他月令。
- 外交與間諜都應支援兩種時間模型：
- `單月行動`：當月下令，月底結算結果。
- `多月行動`：持續滲透、斡旋、施壓、送禮、離間等，需要累積多月後才結算或逐月累積成功率。
- 多月外交與多月間諜行動應記錄至少以下資料：
- 指派武將 `OfficerId`
- 來源勢力 / 來源城市
- 目標勢力 / 目標城市 / 目標武將
- 行動種類
- 已執行月數
- 剩餘月數
- 是否暴露
- 目前成功率修正
- 建議的時間模型：
- `偵查`：可做單月，也可做多月；時間越久，情報越完整。
- `破壞`：通常至少需要一段準備期，再於月底結算破壞結果。
- `策反`：偏向多月行動，逐月累積影響力與成功率。
- `行刺`：可作高風險單月突擊，也可先多月佈局，再選擇執行月份下手。

### 19.6 情報可視範圍與開發用 God Mode
- 在正常遊戲規則下，`他方勢力`、`他方城市`、`他方武將` 的詳細資訊預設應為隱藏。
- 若沒有成功進行 `偵查` 或其他間諜情報行動，玩家只能看見有限的公開資訊，其餘欄位應顯示為 `??` 或未知狀態。
- 預設應隱藏或模糊的資訊包括：
- 城市 `金`、`糧`、兵力總數、六兵種構成
- 城市 `防禦`、`防災`、工坊 / 弓坊、進行中的內政
- 他方武將的 `忠誠`、`官職`、`職階`、`稱號`、裝備 / 道具
- 他方勢力的真實軍力、後勤儲備與內政狀態
- 可保留為公開資訊的內容包括：
- 城市名稱
- 所屬勢力
- 基本外交關係
- 城市是否存在、是否相鄰
- 已經透過劇情、初始設定或既有情報得知的公開武將姓名
- `偵查` 應是揭露他方城市 / 武將詳細資訊的主要手段。
- 情報應有 `時效性`：
- 已揭露資訊可維持若干月
- 時間過久後可視為過時，需重新偵查
- 多月偵查或高階間諜可揭露更完整資料
- 系統應保留一個 `開發 / 測試專用 God Mode` 或 `View All Information` 開關。
- 啟用後，可直接查看所有城市、勢力、武將、兵種、道具、排程資訊，不受情報迷霧限制。
- 此模式僅供開發、測試、平衡調整、debug 與 UI 驗證，不屬於正式遊戲預設規則。
