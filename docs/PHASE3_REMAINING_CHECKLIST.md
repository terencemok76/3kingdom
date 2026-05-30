# Phase 3 Remaining Checklist

更新日期：2026-05-30

## 1. 文件定位

- 本文件整理目前 `Phase 3` 尚未完全收尾的工作。
- 這裡的 `Phase 3` 以：
  - `docs/PHASE3_DESIGN_ZH.md`
  的設施、建設、徵兵、攻城器、UI 顯示這條線為主。
- 另：
  - `docs/MILESTONES.md`
  中的 `Phase3-Started-v1`
  帶有另一條較高層的：
  - prefect autonomy
  - advisor comment
  - buff
  - fog / intelligence
  - event chain
  規劃。
- 因此實作追蹤時，應避免把兩條 `Phase 3` 範圍混為同一份完成清單。

## 2. Must

### 2.1 驗證 View 表格 redraw 收尾

- 需驗證 `View` 相關 `Tree` 在以下情境下是否穩定：
  - 初次開啟
  - 切換 `City Officers / Faction Officers / Items / Diplomacy Relations / Cities`
  - 切換目前選中城市
  - 點欄位排序
  - 開著 dialog 切語言
- 重點觀察：
  - row 背景 striping 是否立即正確
  - selected row highlight 是否保留
  - 欄寬與欄位標題是否同步刷新
  - 不需再靠手動點 row 才補正顯示

### 2.2 驗證 Construction 建設點提示 / log

- 目前已補：
  - `Construction` 月底結果會追加建設點與進度資訊
- 仍需驗證：
  - 設施建設時顯示的點數與升級進度是否正確
  - 攻城器建設時顯示的點數與件數進度是否正確
  - 本月完成升級或產出時的數值是否正確
  - player-related log 與非玩家 log 顯示是否符合預期

### 2.3 補 playtest checklist

- 應把下列測試步驟落到文件：
  - `Move` live localization
  - `Attack` live localization
  - `Construction` 建設點提示 / log
  - `View` redraw / row fill / selection highlight
- 避免之後回歸時，只記得功能有做，卻沒有固定驗證流程。

### 2.4 釐清 Phase 3 範圍文件

- 目前至少有兩份文件使用 `Phase 3` 名稱：
  - `docs/PHASE3_DESIGN_ZH.md`
  - `docs/MILESTONES.md`
- 應補一份簡短說明或在原文件註記：
  - 哪一份追的是設施 / 建設 / 徵兵 / 攻城器
  - 哪一份追的是 prefect / comment / fog / event

## 3. Should

### 3.1 補排程列表中的建設點變化提示

- 目前 `Internal Affairs` 排程列已可顯示：
  - 建設項目
  - 當前建設進度
- 但仍可補：
  - 本月建設點 `+N`
  - 是否升級 / 是否完成 1 件攻城器
  - 更直觀的完成預估

### 3.2 強化 AI 建設選案

- 目前 AI 已能排 `Construction`
- 但仍屬 baseline：
  - 尚未完整依城市角色做更細緻判斷
- 建議補強方向：
  - 前線城更偏 `SiegeWorkshop / Ram / Catapult / Ladder`
  - 後方城更偏 `BowWorkshop / HorsePasture`
  - 視鄰敵威脅與既有 troop 結構調整建設方向

### 3.3 專用化 Construction 結果訊息

- 目前 `Construction` 月底結果仍以：
  - `cmd.internal_affairs.resolved`
  再附加 suffix
- 後續可視需要拆出更專用的：
  - construction-specific result format
  - 完成升級 / 完成攻城器產出 / 無進度突破
  等訊息分類

### 3.4 穩定 View 的互動一致性

- 除 redraw 本身外，仍可補：
  - 切頁籤時保留目前 selection
  - 排序後高亮行為一致
  - 非 officer 類內容與 officer 類內容切換時，detail pane 行為更一致

## 4. Nice-to-have

### 4.1 faction-level Technology 最小版

- `PHASE3_DESIGN_ZH.md` 已明列：
  - `Technology` 尚未實作
- 若要延伸，可先做最小版：
  - 科技資料
  - 少量科技項目
  - 基本研究 UI
  - 前置依賴

### 4.2 更完整的建設預估 UI

- 可補：
  - 依目前每月投入金預估幾月後升級
  - 顯示距下一級 / 下一件還差多少建設點
  - 若更換武將時的預估變化

### 4.3 攻城器系統 v2

- 目前仍為 `v1`
- 後續可擴充：
  - 攻城器耐久
  - 單位化運載量
  - 守城方器械
  - 更細的戰鬥修正

### 4.4 固定化回歸驗證

- 建議補：
  - 固定測試種子
  - 針對建設 / 徵兵 / 攻城器 / View UI 的回歸案例
- 讓 Phase 3 後續收尾不只靠手動點 UI 驗證。

## 5. 目前已補但仍需驗證的項目

- `Move` dialog 的 officer table 已補 live localization refresh
- `Construction` 月底結果已補建設點 / 進度提示
- `View` dialog 已補較強制的 deferred table visual refresh

以上項目建議都視為：
- `implemented`
- 但尚未 `fully signed off`

在正式標記 `Phase 3 complete` 前，應至少再做一輪 focused playtest。
