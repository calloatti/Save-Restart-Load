# Save Restart Load Mod - Agent Instructions

## 1. Overview

This mod adds 2 buttons to the **in-game (pause) menu** (`GameOptionsBox`), NOT the main menu.
Buttons are inserted after the "Load game" button:

- **Button 1: Restart + Load** — restarts the game and loads the current game (the save that is currently loaded).
- **Button 2: Save + Restart + Load** — saves the current game WITHOUT overwrite warning, restarts the game, and loads the saved game.

If there is no saved game reference (current game was never saved), show a dialog box with an OK button and abort the operation.

## 2. Mod Identity

| Field | Value |
|---|---|
| Mod Id | `calloatti.saverestartload` |
| Harmony Id | `calloatti.SaveRestartLoad` |
| Assembly name | `saverestartload` |
| Root namespace | `Calloatti.SaveRestartLoad` |
| Target game version | 1.0.x.x (`MinimumGameVersion` 1.0.0.0) |
| Harmony version | 2.4.1 (RequiredMod) |
| Current mod version | 1.0.0 |

## 3. Architecture

All source lives in `Version-1.0\Source\`. Build/deploy via `Save Restart Load.csproj` + `CommonModSettings.props` (prebuild/postbuild deploy to `%USERPROFILE%\Documents\Timberborn\Mods\Save Restart Load\Version-1.0`).

### Files

| File | Purpose |
|---|---|
| `Source\ModStarter.cs` | `IModStarter` entry point; `new Harmony("calloatti.SaveRestartLoad").PatchAll()` |
| `Source\ModConfigurator.cs` | `[Context("Game")]` configurator; binds `GameOptionsBoxPatchInitializer` |
| `Source\GameOptionsBoxPatch.cs` | Harmony Postfix on `GameOptionsBox.GetPanel()`; clones `LoadGameButton`, inserts the 2 new buttons; handles clicks + no-save dialog |
| `Source\GameRestarter.cs` | Relaunches `Timberborn.exe` with `-skipModManager -settlementName <X> -saveName <Y>`, then `Application.Quit()` |
| `manifest.json` | Mod metadata (see section 2) |
| `Localizations\*.csv` | enUS.csv + 14 locale files (CRLF line endings required) |

## 4. Key Implementation Details

### 4.1 Button insertion target

- Patched method: `Timberborn.OptionsGame.GameOptionsBox.GetPanel()` (Postfix).
- `GetPanel()` is called every time the pause menu opens (via `PanelStack`), so the Postfix must guard against duplicates: `if (__result.Q("RestartLoadButton") != null) return;`.
- Reference button: `__result.Q<Button>("LoadGameButton")`. Insert new buttons at `container.Insert(loadIndex + 1, restartLoadButton)` and `container.Insert(loadIndex + 2, saveRestartLoadButton)`.
- New buttons are cloned via `Activator.CreateInstance(referenceButton.GetType())` (type is `LocalizableButton`, internal but publicized via `Timberborn.CoreUI`). Copy `styleSheets`, `GetClasses()`, `style.width`, `style.height`.
- Localization: set `button.text = _loc.T(locKey)` (do NOT use a text-loc-key attribute on cloned buttons).

### 4.2 Service injection

`GameOptionsBoxPatchInitializer : ILoadableSingleton` (bound in Game context) injects into the static patch class via `GameOptionsBoxPatch.SetServices(...)`:

- `ILoc` — localization.
- `GameLoader` — current save via `LoadedSave` property.
- `GameSaver` — `SaveInstantlySkippingNameValidation(SaveReference, Action onSaveCompleted)`.
- `DialogBoxShower` — for the "no saved game reference" OK dialog: `.Create().SetMessage(...).SetConfirmButton(() => { }).Show()`.

### 4.3 Current-save logic

- Current save = `_gameLoader.LoadedSave` (set when a game is loaded from disk). It is null for a brand-new game that was never saved.
- If null: show dialog `Calloatti.SaveRestartLoad.Message.NoSaveReference` and return (abort).
- Button 2 saves via `SaveInstantlySkippingNameValidation` (skips name validation = no overwrite warning) and restarts in the completion callback.

### 4.4 Restart + Load mechanism

`GameRestarter.RequestRestartAndLoad(SaveReference)`:

1. Builds args: `-skipModManager -settlementName <SettlementReference.SettlementName> -saveName <SaveName>`.
2. On Windows, launches `powershell.exe` with a base64-encoded command: waits for the current PID (`Wait-Process`), then starts `Timberborn.exe` with the args.
3. Calls `Application.Quit()` to release runtime locks.

The game reads these CLI args in `AutoStarter.CheckAutoStarting()` (`Timberborn.MainMenuScene.cs`): `-saveName` triggers standalone auto-start; `-settlementName` is the settlement folder; `-skipModManager` skips the mod-manager screen.

## 5. Localization

- Keys:
  - `Calloatti.SaveRestartLoad.Button.RestartLoad` = "Restart + Load"
  - `Calloatti.SaveRestartLoad.Button.SaveRestartLoad` = "Save + Restart + Load"
  - `Calloatti.SaveRestartLoad.Message.NoSaveReference` = "There is no saved game to restart and load."
- enUS.csv is the master. Locale files (14): `deDE esES frFR itIT jaJP koKR plPL ptBR ruRU thTH trTR ukUA zhCN zhTW`.
- CSV format: header `ID,Text,Comment`; ID never quoted; Text always double-quoted; empty comment = empty. **CRLF (`\r\n`) line endings required**, UTF-8 no BOM.
- For translation workflow, follow `C:\Users\calloatti\source\repos\Mods\docs\localizations.md`.

## 6. Reference Mods

- **Restart + load logic** (copy architecture): `C:\Users\calloatti\source\repos\Mods\Sync Mods Pro\Version-1.0\Source\GameRestarter.cs` and `MainMenuPatch.cs`.
- **Button insertion / cloning** (UI Toolkit pattern): `C:\Users\calloatti\source\repos\Mods\Sync Mods\Version-1.0\Source\MainMenuPanelPatch.cs` and `LoadGameBoxPatch.cs`.
- **Game source** (decompiled): `C:\Users\calloatti\source\repos\timberborn-decompiled-1.0.13.1-b769e88-sw\` — notably `Timberborn.OptionsGame.cs`, `Timberborn.GameSaveRuntimeSystem.cs`, `Timberborn.GameSaveRepositorySystem.cs`, `Timberborn.MainMenuScene.cs`, `Timberborn.CoreUI.cs`, and UXML `UI\Views\Game\GameOptionsBox.uxml`.

## 7. Lessons Learned

- GameOptionsBox is the pause menu; do NOT patch `MainMenuPanel` (main menu) — the requirement is game menu only.
- Do not use `GetComponentFast` (does not exist); use `GetComponent<T>()`.
- ECS: `GetComponents<T>()` returns void; pass a pre-allocated `List<T>`.
- Use the native Timberborn Modding System (`IModStarter`), never BepInEx.
- User-facing text must be localized (never hardcode visible strings).
- `-skipModManager` is required so the restarted game boots straight into the save instead of showing the mod manager screen.

## Hard Rule
DO NOT EVER TOUCH THE DEPLOY FOLDER.

BUILD DOES EVERYTHING, NEVER EVER MESS WITH THE DEPLOY PROCESS.
