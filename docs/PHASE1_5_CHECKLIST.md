# Phase 1.5 Done / Partial / Missing Checklist

- 更新日期：`2026-05-02`
- 目的：快速追蹤 Phase 1.5 已完成、部分完成、尚未完成項目
- 依據：目前 codebase、`PHASE1_DESIGN_ZH.md`、`MILESTONES.md`

## 2026-05-07 Sync Update

- This section overrides older checklist lines that still mark `Diplomacy` and `Spy` as fully missing.
- `Diplomacy` is no longer `Missing`.
- Current status: `Partial / Baseline Implemented`
- Implemented:
- HUD `Diplomacy` command category
- `Alliance / Truce / Gift`
- one-officer assignment from current city
- month-end resolution
- diplomacy relation state and `View -> 外交關係`
- diplomacy progression now has gameplay-backed experience sources
- Still missing:
- advanced diplomacy actions such as `Demand / Break Pact / Marriage / Pressure`
- multi-month advanced diplomacy model beyond the current baseline

- `Spy` is no longer `Missing`.
- Current status: `Partial / Baseline Implemented`
- Implemented:
- HUD `Spy` command category
- `Reconnaissance / Sabotage / Incite`
- one-officer assignment from current city
- ruler blocked from spy assignment
- month-end resolution
- exposure risk
- relation penalty and officer loyalty penalty on exposure
- `SpyExperience` now has gameplay-backed experience sources
- Still missing:
- `Assassination`
- multi-month infiltration / lurking
- intelligence fog-of-war and reveal-duration system
- advanced capture / execution / return-state outcomes

- `View` expansion is no longer missing.
- Implemented:
- `View -> 勢力道具`
- `View -> 外交關係`

- Recommended next checklist focus:
- attack / troop-type UX polish
- intelligence fog-of-war
- advanced diplomacy actions
- advanced spy outcomes
- AI Phase 1.5 follow-up

## Done

### 內政 / 多月排程
- `Develop` 已由 `內政` 系統取代。
- `Farm`、`Commercial`、`Defend`、`WaterControl(防災)`、`Construction` 已有基本指派與月底結算。
- 內政支援多月排程、指派武將、終止排程。
- 內政武將 UI 已改成 table 風格。
- 每座城市每種內政工作同時只允許一位武將執行。

### 民事
- `訪察民情` 已改為需要指派武將，並在月底結算。
- `救濟` 已改為需要指派武將，並在月底結算。
- `訪察民情`、`救濟` 都有限制同城每月一次。
- 民事主選單已可直接顯示 `本月已執行` 禁用提示。

### 人事 / 道具 / 勢力庫存
- 已有 `武器`、`馬`、`書`、`寶物` 四大類道具。
- `訪察民情 / Search` 找到的道具會進入 `勢力庫存`。
- `賞賜` 會從 `勢力庫存` 取出道具交給本勢力武將。
- `索回道具` 會把武將持有道具放回 `勢力庫存`。
- `查看道具` 已可同時查看：
- 本勢力武將持有道具
- 本勢力無主道具

### 六兵種 / 馬匹資源
- 城市已持有六兵種數量，不再只靠單一 `Troops`。
- 六兵種已包含：
- `步兵`
- `槍兵`
- `騎兵`
- `弓兵`
- `弩兵`
- `攻城兵`
- 城市已加入 `馬` 資源。
- `騎兵` 徵募受馬匹數量限制。
- `弩兵` 需要 `弓坊`。
- `攻城兵` 需要 `工坊`。
- `移動` 已可搬運 `馬`。
- 商人已可 `買馬`。
- 每年 1 月已有馬匹自然繁殖。

### 攻擊 / 戰鬥基礎
- `攻擊` 已改為複雜 `Window` 表單，不再依賴 `AcceptDialog`。
- 攻方已支援 `逐武將配兵`。
- 攻擊流程已能指定每位出征武將的兵種與兵數。
- 已有六兵種資料帶入攻擊流程。
- 已有兵種相剋第一版。
- 已有攻城兵壓制城防效果。
- 攻擊成功 / 失敗 / 取消三條主流程已做過程式化驗證。

### 經驗 / 職階 / 稱號
- 已有 `BattleExperience / MilitaryRank / GeneralTitle`。
- 已有 `StrategistExperience / StrategistRank / StrategistTitle`。
- 已有 `CivilExperience / CivilRank / CivilTitle`。
- 已有 `SpyExperience / SpyRank / SpyTitle` 資料欄位。
- 已有 `DiplomacyExperience / DiplomacyRank / DiplomacyTitle` 資料欄位。
- 五條內政專線已存在：
- `FarmExperience`
- `CommercialExperience`
- `DefendExperience`
- `DisasterPreventionExperience`
- `ConstructionExperience`
- Officer detail 已可顯示：
- 軍事 / 軍師 / 間諜 / 外交 / 文官 progression
- 戰鬥經驗
- 五條內政經驗
- 五條內政經驗若已升階，會顯示對應稱號

### 防災 / 災害
- `治水` 已正式改名為 `防災`。
- 城市已有 `防災` 數值。
- `防災` 內政會提升城市防災值。
- 月底可能發生災害事件。
- `防災` 越高，災害發生率與損失幅度越小。

### Locale / 文件基礎
- locale 已從單一 `locale.json` 拆成多個 `*.locale.json`。
- 已建立 [LOCALIZATION_GUIDE.md](/D:/sandbox_ai/godot/3kingdom/docs/LOCALIZATION_GUIDE.md)。
- 已建立多檔 locale 載入規則。
- 已建立 `Phase 1.5` checklist 文件本身。

## Partial

### 攻擊 / 戰鬥 UI 收尾
- 攻擊視窗已可用，但仍持續調整版面與操作手感。
- 攻擊視窗資料流已穩定很多，但 UI/UX 仍未算完全收尾。

### 六兵種戰鬥深度
- 已有六兵種資料層與攻擊配置層。
- 已有兵種相剋第一版。
- 但 `野戰 / 攻城戰` 尚未真正分成兩條完整可切換流程。
- 守軍逐武將配兵仍未完整。

### 經驗 / 稱號 buff 深度
- 已有 rank、title、第一版 buff。
- 但 buff 仍偏 first-pass，未完全細分到更多行動與玩法。
- `Spy`、`Diplomacy` progression 目前主要是資料結構與 UI 準備，尚未接到正式玩法來源。

### 道具系統補完
- 已有勢力庫存、賞賜、索回、搜索取得。
- 但戰利品、更多名物、平衡與擴充規則仍可再補。

### AI Phase 1.5
- AI 已開始考慮內政、防災、招募等因素。
- 但 AI 尚未完整運用：
- 六兵種深度戰鬥
- 道具分配
- 稱號 / progression 策略
- 未來外交 / 間諜系統

## Missing

### 設施 / 科技系統
- `弓坊` 尚未做成正式建設 / 解鎖流程。
- `工坊` 尚未做成正式建設 / 解鎖流程。
- `養馬設施` 尚未實作。
- 科技樹與設施系統整體尚未落地。

### 外交系統
- `外交` 指令分類尚未加入遊戲流程。
- `同盟 / 停戰 / 贈禮 / 威逼 / 毀約` 尚未實作。
- 外交 officer 指派與多月外交任務尚未實作。

### 間諜系統
- `偵查` 尚未實作。
- `破壞` 尚未實作。
- `策反` 尚未實作。
- `行刺` 尚未實作。
- 間諜 officer 指派、多月潛伏、暴露風險尚未實作。

### 情報迷霧 / God Mode
- 他方城市 / 他方勢力 / 他方武將資訊顯示 `??` 尚未實作。
- `偵查後解鎖資訊` 尚未實作。
- 開發用 `God Mode / View All Information` 開關尚未實作。
- AI 與玩家共用情報限制的可視資訊層尚未實作。

### 安定事件鏈
- 已有防災與災害。
- 但完整的 `安定事件鏈` 尚未實作，例如：
- 缺糧動亂
- 災後民怨
- 暴動
- 叛逃

### 人口 / 容量 / 更深後勤
- 完整人口系統尚未實作。
- 城市容量與更深後勤壓力尚未實作。
- 人口與內政、忠誠、災害、兵源的完整互動尚未實作。

## 建議優先順序

1. 收尾 `攻擊 / 戰鬥 UI` 與 `野戰 / 攻城戰` 分流
2. 補 `弓坊 / 工坊 / 養馬設施` 的正式設施或科技流程
3. 實作 `外交` 指令與 officer 指派
4. 實作 `間諜` 指令與情報迷霧
5. 補 `安定事件鏈`
6. 強化 `AI Phase 1.5`
