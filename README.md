# AbyssMod

用舊版 2.1.0_fix 的人，只要換一個檔案就好，不用重裝：

1. 關掉遊戲。
2. 從 [Releases](../../releases/latest) 下載 `AbyssMod.dll`（約 135 KB，不用下載整包）。
3. 覆蓋到遊戲資料夾的 `BepInEx\plugins\AbyssMod\AbyssMod.dll`。
4. 照常開遊戲。

這樣設定檔、翻譯快取、dump 都會保留，BepInEx 也不用重新生成檔案。升級後第一次開遊戲會再完整下載一次翻譯，之後就會直接用本地快取。

不建議舊版玩家下載整包直接覆蓋：整包裡的 `AbyssMod.cfg` 和 dump 會蓋掉自己的，dump 是 mod 記錄的「還沒翻譯的原文」。
