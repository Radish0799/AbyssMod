# Changelog

All notable changes to this project will be documented in this file.

## [2.1.1] - 2026-11-06

### 修正 / Fixed

- 快取閘門永不命中：改以下載內容的位元組 md5（存於快取旁 `.md5`）比對 manifest，沒更新時不再重抓。
- add-on / other 只同步 manifest 列出的分類；遠端空檔也記錄，避免每次重抓。

### 其他

- 以 2.1.0_fix 反編譯碼為基礎，補上專案檔並修正反編譯造成的型別歧義。

## [1.2.0] - 2026-06-23

### 新增 / Added

- **Masterdata 全量接线**：`master_mapping.json` 扩展至 **128 张**可翻译表（`dict_types` 129 项含 `names`/`ui_texts`），涵盖道具、武器、防具、设施、关卡、图鉴、酒馆、付费文案等
- **就地翻译字段**：`MItems`、`MWeapons`、`MBuildings`、`MDungeons`、`MEnemies` 等表在 masterdata 写入 cache 前自动替换

### 变更 / Changed

- 首次安装或升级后请**删除** `BepInEx/cache/translations/` 以重新 sync 全部 `m_*` 字典

### 备注

- 新增表译文部分为 OpenCC 机翻，详见翻译仓库 `reports/machine_translated.md`
- IL2CPP 类名无法解析的表会在日志中 `type not resolved` 并跳过（不影响启动）

---

## [1.1.3] - 2026-06-26

### 修復 / Fixed

- **`Texts` 合併優先級**：`MergeLocalAddOnFallbackAsync` 改為 legacy/add-on 先合併、m_* 權威層後覆蓋；add-on 不再覆蓋已在 m_* 表中的 key（修復 equipment_effect 等 9 條兜底譯文被覆蓋）

---

## [1.1.2] - 2026-06-26

### 修復 / Fixed

- **主界面 UI 不翻译**：Unity 6000 / IL2CPP 下找不到 `TMP_Text.m_text`，`RefreshVisibleText` 与 `OnEnable` 静默失败；改以 `TMP_Text.text` 属性回退写入
- **`ui_texts` 兜底**：合并进 `Texts` 字典，`TextTranslator` 亦直接查表

---

## [1.1.1] - 2026-06-26

### 修復 / Fixed

- **`master_mapping.json` 嵌入資源名稱**：SDK 預設為 `AbyssMod.AbyssMod.master_mapping.json`，導致 `ContentTypes` 為空、`m_*` 字典從不載入
- **manifest 快取寫入失敗**：下載 manifest 時未建立 `cache/translations/manifest/` 子目錄

---

## [1.1.0] - 2026-06-26

### 新增 / Added

- **MasterData 就地翻譯**：移植作者 `MasterDataTranslationPatch` + `master_mapping.json`，masterdata 寫入 cache 前翻譯酒館卡、任務、技能等欄位
- **`m_*` 扁平 CDN 載入**：`TranslationPaths.ContentTypes` 由 mapping 驅動；`Manifest.GetFileHash` 支援動態 `m_*` hash
- **`ui_texts` 查表**：`UiTextTranslator` + `OnEnable` Postfix，靜態 prefab UI 精準翻譯
- **`legacy/add-on/ui_misc` 兜底**：保留 `GeneralTextPatch` + `RefreshVisibleText` + `MachineTranslator` 雙層架構

### 變更 / Changed

- **版本號 1.1.0**：需刪除舊 `cache/translations/` 後重新 sync
- **`TranslationManager`**：新增 `GetTable()` / `EnsureStaticTranslationsLoaded()` 供 masterdata patch 使用

---

## [1.0.8] - 2026-06-21

### 修復 / Fixed

- **`add-on/` 初次安裝不從 CDN 下載**：新增 `SyncAddOnFromCdnAsync()`，啟動時從 `{cdn}/add-on/{category}/` 同步社群精翻（12 個子類別）
- **同步順序調整**：先拉取 `add-on/` 再拉取 `other/`，確保 `PruneGraduatedKeys` 能正確清理已被精翻收錄的機翻條目
- **角色詳情頁技能不翻譯**：技能描述被誤分類為 `equipment_effect`；改為優先走 `ability_descriptions`，並跳過机翻缓存干扰
- **`ability_descriptions` 模板匹配**：新增 `AbilityTextMatcher`，支援 `{[...]}` 模板与 UI 已代入数值（如 `414.2%`）的模糊匹配
- **CDN 為空時**：仅读取本地 cache，不再发起无效 HTTP 请求
- **`ability_descriptions` 合并策略**：远端更新与本机条目合并，避免旧 CDN 覆盖本地较新译法
- **任務/圖鑑/設定等頁面翻譯缺口**：`TextTranslator` 新增 `titles` / `descriptions` 查詢與 `{0}` 數字模板匹配（`TemplateTextMatcher`）
- **底部導航等 SetText 路徑**：**完全不 patch `SetText`**（IL2CPP 下 Prefix/Postfix 皆會 trampoline 自我遞迴 → Stack Overflow）；改由 `Hotkey.Update` 每 0.5s 節流掃描畫面 TMP，經 `m_text` 直寫 + `ForceMeshUpdate` 套用譯文
- **頁面上下文分類**：任務、深淵代碼、劇情回想、設定選單依 UI 層級優先歸類，避免誤入 `equipment_effect`

### 新增 / Added

- **`manifest.add_on` 可選雜湊欄位**：支援對 add-on/ 各子類別做 cache 驗證
- **`manifest.ability_descriptions` 雜湊欄位**：支援技能描述 cache 增量更新

---

## [1.0.7] - 2026-06-21

### 變更 / Changed

- **首次啟動預設 CDN**：改為 `s88037zz/dotabyss-translation` 社群翻譯 repo
- **首次啟動預設語言**：改為 `zh_Hant`（繁體）

---

## [1.0.6] - 2026-06-21

### 新增 / Added

- **`other/` CDN 自動同步**：啟動時從 CDN 拉取 `other/{category}/` 譯文，合併至本機 cache（遠端同 key 優先，本機獨有 key 保留）
- **`MachineTranslator.ReloadFromDisk()`**：CDN 同步完成後重新載入 other/ 記憶體快取
- **`manifest.other` 可選雜湊欄位**：支援對 other/ 各子類別做 cache 驗證（可選）

### 修復 / Fixed

- **other/ 校對譯文需開啟機翻才顯示**：命中 other/ 快取時，即使 `[MachineTranslation] Enabled = false` 也會顯示已校對譯文

---

## [1.0.5] - 2026-06-21

### 新增 / Added

- **介面文字翻譯擴展**：新增 `TextTranslator`、`TextCollector`、`TextClassifier` 三個服務，涵蓋道具說明、裝備效果、技能、酒館系統、設施、任務等介面文字
- **啟發式分類器**（`TextClassifier`）：將未翻日文依語意歸入 9 個細分子類別，便於人工校對與社群分工
- **add-on 資料夾**：`cache/translations/add-on/` 作為社群自訂翻譯的獨立命名空間，與作者 CDN 管理類別明確隔離
- **機翻繁簡語言切換**：機翻提示詞（System Prompt + Few-shot 範例）依 `Language` 設定自動切換繁體（台灣用語）或簡體
- **機翻支援雲端 API**：新增 `Engine = openai`（相容 DeepSeek、OpenAI）及 `Engine = claude` 配置，`ApiKey` / `Endpoint` / `Model` 全部可配置
- **角色名全介面共用**：`TextTranslator.Process` 優先查詢 `names` 字典，讓強化、編隊等介面角色名與劇情介面保持一致
- **角色名自動收集**：透過 GameObject 名稱啟發（`CharaName`、`TextName`）識別名字欄位，新角色名寫入 `name_raw.json`，不走機翻，待人工補入 `names/`
- **other/ 清洗機制**（`PruneGraduatedKeys`）：每次啟動自動移除 `other/` 中已被 `add-on/` 收錄的 key，保持機翻暫存區乾淨
- **`GameDir` 改為環境變數配置**：`ABYSS_GAME_DIR` 環境變數優先，方便多人協作不需硬編路徑
- 新增 `CHANGELOG.md` 與更新 `README.md`（社群發布說明）

### 修復 / Fixed

- **`SetText` 堆疊溢出崩潰**：移除對 `TMP_Text.SetText(string)` 及 `SetText(string, bool)` 的 Harmony Hook（IL2CPP 環境下兩者互相觸發形成遞迴），改為僅 hook `set_text` 屬性 setter
- **IL2CPP 參數名稱錯誤**：`HarmonyPrefix` 的 `ref string text` 改為 `ref string __0`（IL2CPP 反混淆後的參數名）
- **機翻輸出語言不一致**：修正繁體中文模式下機翻仍輸出簡體的問題；並對既有 `other/` 快取執行 OpenCC（s2twp）轉換

### 變更 / Changed

- 翻譯資料路徑從 `cache/zh_Hans/` 重構為 `cache/translations/`，子目錄對齊原作者 CDN 結構
- `MachineTranslator._pending` 由單純字串集合改為 `{template: category}` 映射，使批翻後能正確分類寫入
- `TranslationManager` 現在動態掃描 `add-on/` 子目錄並以最高優先級合併，不再需要逐一硬編路徑

---

## [1.0.4] 以前

基於 [anosu/AbyssMod](https://github.com/anosu/AbyssMod) 原版，包含劇情翻譯（`novels/`）、角色名（`names/`）、標題（`titles/`）、技能（`ability_descriptions/`）等 CDN 管理翻譯資料。
