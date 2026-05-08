# Phase 1.5 完成 / 部分完成 / 未完成清單

- 文件更新日：`2026-05-07`
- 本文件用來同步目前 `Phase 1.5` 的真實實作狀態。
- 若與較早期設計文件或舊檢查清單衝突，以目前 codebase 與近期同步結果為準。

## 1. 目前總結

- `Phase 1.5` 不是完全未完成，也不是完全收尾。
- 目前狀態較準確的描述是：
  - 已完成多個核心基線系統
  - 還有數個區塊屬於部分完成
  - 仍有一些進階功能尚未開始或尚未正式落地

## 2. 已完成

### 2.1 內政 / 內政長期工作

- 已將 `Develop` 延伸為 `Internal Affairs` 長期工作系統。
- 已實作：
  - `Farm`
  - `Commercial`
  - `Defend`
  - `WaterControl`
  - `Construction`
- 已完成：
  - 指派流程
  - 月度進度推進
  - 月末結算
  - 武將占用狀態管理
- 內政武將 UI 已改成 table 風格。

### 2.2 搜尋 / 登用

- `Search` 已完成基線流程。
- `Search` 可搜尋：
  - 在野武將
  - 物品
- 搜尋結果已接到對應資料與顯示。

### 2.3 物品系統基線

- 已建立 `WorldState.Items`。
- 已完成勢力物品庫存基線。
- 已完成：
  - 搜尋取得物品
  - 勢力持有物品
  - `Grant Item`
  - `Recall Item`
  - `View -> Faction Items`
- 物品檢視已能顯示基礎欄位與持有狀態。

### 2.4 外交基線

- `Diplomacy` 已不再是缺失項。
- 目前已實作基線動作：
  - `Alliance`
  - `Truce`
  - `Gift`
  - `Demand`
  - `Break Pact`
- 已完成：
  - HUD 外交指令入口
  - 單武將指派
  - 月末結算
  - 外交關係資料狀態
  - `View -> Diplomacy Relations`
  - AI 對玩家 `Alliance / Truce / Gift` 提案彈窗
  - 玩家可對 AI 的 `Alliance / Truce / Gift` 選擇 `接受 / 拒絕`
- 外交關係表 UI 已完成一輪 polish：
  - table header 可見
  - selected row highlight 較清楚
  - headers 左對齊

### 2.5 諜報基線

- `Spy` 已不再是缺失項。
- 目前已實作基線動作：
  - `Reconnaissance`
  - `Sabotage`
  - `Incite`
- 已完成：
  - HUD 諜報指令入口
  - 單武將指派
  - 月末結算
  - 曝光風險
  - 曝光後忠誠懲罰
  - 曝光後外交關係懲罰
  - `SpyExperience` 成長來源
- 君主 / 太守目前可執行諜報工作。
- 諜報對話框已能顯示君主於武將名單中。

### 2.6 戰爭迷霧 / 情報可見性基線

- 已完成敵方資訊遮罩基線。
- 未取得情報時，外國城市 / 武將 / 物品資訊會顯示為 `??`。
- 偵察成功後可取得暫時城市情報。
- 城市情報會依剩餘月數遞減並過期。
- 城市情報剩餘時間目前可顯示於：
  - 左側選中城市資訊
  - `View -> Cities` 相關欄位
- AI 目前已遵守情報可見性規則，不會直接用 God Mode 邏輯做攻擊判斷。

### 2.7 God Mode 基線

- HUD 已有 God Mode toggle。
- God Mode 目前僅影響玩家 / Debug UI 顯示。
- AI 不使用 God Mode。

### 2.8 AI 諜報使用基線

- AI 已可主動使用諜報。
- 目前基線行為包括：
  - 優先偵察隱藏的相鄰敵城
  - 對可見且高價值目標進行破壞
  - 對高忠誠目標進行煽動
  - 視需要刷新即將到期的情報

### 2.9 玩家被攻擊防守彈窗

- 若 AI 在月末攻擊玩家城市，會出現防守彈窗。
- 玩家可配置：
  - 防守武將
  - 防守兵種
  - 防守兵力
- 防守部署目前已接入戰鬥結算。
- 攻擊對話框目前已支援 dual mode：
  - attack mode
  - defense mode

### 2.10 兵種基線

- 已完成城市兵力拆分基線：
  - `Infantry`
  - `Spearman`
  - `Cavalry`
  - `Archer`
  - `Crossbow`
  - `Siege`
- 已完成基本兵種相剋與戰鬥接線。

### 2.11 進度 / 稱號資料基線

- 已建立並使用下列 progression 資料欄位：
  - `BattleExperience / MilitaryRank / GeneralTitle`
  - `CivilExperience / CivilRank / CivilTitle`
  - `SpyExperience / SpyRank / SpyTitle`
  - `DiplomacyExperience / DiplomacyRank / DiplomacyTitle`
- 內政相關經驗欄位也已存在並可累積。

### 2.12 View 擴充

- `View` 對話框已擴充為 table 型式檢視。
- 目前至少已包含：
  - 城市 / 武將
  - 勢力武將
  - 勢力物品
  - 城市
  - 外交關係

### 2.13 人像系統

- 人像來源已切換為：
  - `assets/portrait/team1.png` + `data/person/person_image_1.json`
  - `team2.png` + `person_image_2.json`
  - `team3.png` + `person_image_3.json`
  - `team4.png` + `person_image_4.json`
- `charId` 已對應 `officer.json id`
- 已完成：
  - 標準 JSON 解析
  - regex fallback
  - `person_image_2/3/4.json` 修復為有效 JSON
  - officer id `1..100` 覆蓋檢查

## 3. 部分完成

### 3.1 攻擊 / 戰鬥 UI 與體驗收尾

- 主流程已可用，但仍屬部分完成。
- 目前仍可能需要收尾的方向：
  - 攻擊 / 防守 UI 細節 polish
  - 兵種與兵力資訊呈現
  - 戰鬥結果回饋清晰度
  - 若未來要做野戰 / 攻城分流，仍未正式展開

### 3.2 玩家藍色日誌覆蓋

- 目前已補很多玩家相關藍色日誌。
- 已知已納入藍色的項目包括：
  - 玩家下達指令結果
  - 玩家結束回合
  - 玩家相關攻防結果
  - 玩家城市收入 / 災害 / 月份推進
  - 玩家諜報日誌
  - 玩家城市內政指派日誌
  - 玩家城市外交指派日誌
  - 玩家城市內政月進度日誌
  - 勝利 / 失敗
- 但仍需實際檢查是否還有漏網 `AddLog` call site。

### 3.3 AI Phase 1.5 深化

- AI 已有明顯超出 Phase 1 基線的能力。
- 但若以完整 `Phase 1.5 AI` 來看，仍屬部分完成。
- 仍待補強的方向可能包括：
  - 進階外交策略
  - 進階諜報策略
  - 更穩定的長期情報維護
  - 更多多月實機 soak / regression 驗證

### 3.4 progression / rank / title 的玩法深度

- 資料欄位、部分成長來源與 UI 已有基線。
- 但若要做成完整 `buff / 差異化成長 / 更明顯策略影響`，目前仍屬部分完成。

### 3.5 文件同步收尾

- `PHASE1_DESIGN_ZH.md` 已反映多項新系統。
- `AI_PLAYTEST_CHECKLIST.md` 已更新為繁體中文並對齊目前系統。
- 但完整文件體系仍可能需要後續再做一次總同步，避免舊段落留下過時狀態。

## 4. 未完成

### 4.1 進階外交動作

- 已進入第一批進階外交動作：
  - `Demand`
  - `Break Pact`
- 目前仍未正式實作：
  - `Marriage`
  - `Pressure`
- 也尚未建立更深層的多月外交模型。

### 4.2 進階諜報動作與狀態

- 目前仍未正式實作：
  - `Assassination`
  - 多月潛伏 / 滲透
  - 更完整的抓捕 / 處決 / 釋回分支

### 4.3 更深層的情報 / 迷霧 progression

- 雖然迷霧基線與暫時情報已完成，但若要做更深層系統，仍未完成，例如：
  - 情報等級化
  - 多來源疊加
  - 長期滲透對視野的持續影響
  - 更進一步的情報衰退與維持模型

## 5. 建議優先順序

### 5.1 第一優先

- 實機驗證一次完整鏈路：
  - `AI 攻城 -> 玩家防守彈窗 -> 配置守軍 -> 戰鬥結算 -> 日誌`
- 目的：
  - 確認高風險流程穩定
  - 確認 UI / 戰鬥 / 月末接線沒有殘留問題

### 5.2 第二優先

- 檢查並補齊玩家藍色日誌漏網項目。

### 5.3 第三優先

- 補做 `AI Phase 1.5` 實機與 soak 驗證。

### 5.4 第四優先

- 在新增功能中先做進階外交。

### 5.5 第五優先

- 進階諜報收尾。

## 6. 結論

- `Phase 1.5` 目前不是缺少基線功能，而是：
  - 已完成多個核心基線模組
  - 還差進階外交
  - 還差進階諜報
  - 還差 AI 深化與實機驗證
  - 還差部分軍事 UX / 日誌 / 文件收尾

- 若要用一句話描述目前狀態：
  - `Phase 1.5 基線已成形，但仍未完整收尾。`
