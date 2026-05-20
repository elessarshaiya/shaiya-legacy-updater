# Legacy Shaiya Updater with Modern Core

This repository contains a legacy-style Shaiya launcher UI with a modernized login, update, patch, repair, and SAH/ESAH handling core.

The UI keeps the classic launcher look, while runtime behavior is driven by server-side configuration instead of local `updater.ini` files.

## Main features

- Legacy WPF launcher UI targeting **.NET Framework 4.8** and **x86**.
- Plain PS_UserData login through PHP + Microsoft SQL Server.
- Saved-account support using a device hash and `UserUID`, without storing the real password in a separate updater table.
- Server-side configuration through `ServerSide/Updater/launcher.ini` or `launcher.php`.
- Patch manifest support through `manifest.json`.
- Patch download to a temporary `.download` file before validation.
- SHA-256 and file-size validation before applying patches.
- ZIP path traversal protection.
- Transactional patch backup and rollback.
- ESAH support for `data.sah` and `update.sah` during patching.
- Repair/verify support through the `manifest.json` `files` list.
- Launcher-integrated game loading panel; no separate topmost mini-loading window is created.
- Optional offscreen hiding of `game.exe` until character selection is expected to be ready.

## Important public-release notes

This package was cleaned for GitHub-style source sharing:

- User-facing text and documentation were converted to English.
- Real deployment secrets are not required in source.
- `ServerSide/Updater/api/config.sample.php` is the public SQL config template.
- `ServerSide/Updater/api/config.php` is ignored by Git and should contain real server credentials only on the deployment machine.
- `ServerSide/Updater/launcher.sample.ini` is the public launcher config template.
- `ServerSide/Updater/launcher.ini` is ignored by Git and should contain real production keys only on the deployment machine.
- The SAH/ESAH key included in sample files and `SahCryptTool` is a public sample key only. Replace it before production use.
- The bundled UI image assets and third-party binaries should be reviewed before publishing a public repository.

## Project layout

```txt
Updater.LegacyUI.ModernCore.sln   Main Visual Studio solution
Updater/                          WPF launcher source
Updater/Game/                     Game.exe offsets, window control, and server-select automation
Updater/Update/                   Patch, repair, manifest, transaction, and SAH/ESAH logic
Updater/Auth/                     Login API client and local saved-account helpers
Updater/Core/                     Config, paths, INI, logging, and web timeout helpers
ServerSide/Updater/               PHP API, launcher config, manifest, tools, patch folders
ServerSide_SQL/                   SQL scripts for saved-account support
SahCryptTool/                     C++ ESAH helper tool

docs/                             Install, login, cleanup, and release notes
```

## Build requirements

- Windows
- Visual Studio 2022
- .NET Framework 4.8 Developer Pack
- x86 build target

Build only the updater:

```bat
build_updater_only.bat
```

Build the full solution:

```bat
build.bat
```

You can also open:

```txt
Updater.LegacyUI.ModernCore.sln
```

## Server deployment

Default IIS path:

```txt
C:\inetpub\wwwroot\Updater
```

Basic steps:

1. Copy `ServerSide/Updater/*` to `C:\inetpub\wwwroot\Updater`.
2. Copy `ServerSide/Updater/api/config.sample.php` to `ServerSide/Updater/api/config.php` on the server.
3. Edit `config.php` with real SQL Server credentials.
4. Copy `ServerSide/Updater/launcher.sample.ini` to `ServerSide/Updater/launcher.ini` on the server.
5. Edit `launcher.ini` with real URLs and the production SAH/ESAH key.
6. Run the SQL script if saved-account login is needed:

```txt
ServerSide_SQL/01_updater_auth_tables.sql
```

Test the server:

```txt
http://127.0.0.1/Updater/test-env.php
http://127.0.0.1/Updater/test-db.php
http://127.0.0.1/Updater/test-config.php
```

Expected DB test shape:

```json
{
  "ok": true,
  "message": "Database connection OK.",
  "database": "PS_UserData"
}
```

## Patch deployment

Put patch files here:

```txt
C:\inetpub\wwwroot\Updater\Patches
```

Then generate manifest entries:

```txt
http://127.0.0.1/Updater/tools/patch_hashes.php
```

Copy the generated `patches` array into `manifest.json`.

Patch files are expected to be ZIP files with a `.patch` extension. For data patches, include `update.sah` and `update.saf` inside the ZIP.

## Repair/verify deployment

Put full repair files here:

```txt
C:\inetpub\wwwroot\Updater\Files
```

Then generate manifest entries:

```txt
http://127.0.0.1/Updater/tools/file_hashes.php
```

Copy the generated `files` array into `manifest.json`.

## Active game offset set

The current active offset set targets `game-pt-ps0182.exe`:

```txt
LoginPointer    = 0x007C48FC
PatchLoginFlow1 = 0x004D4EBF
PatchLoginFlow2 = 0x004D1F5F
PatchLoginFlow3 = 0x004D0DCD
```

EP5 fallback offsets remain commented in `Updater/Game/GameOffsets.cs` for rollback or comparison.

## PT0182 automatic server selection

After login ID/password injection and login-flow patches, the updater scans process memory for the `CSelectServer` object. Once the server list is initialized, it writes the selected server index and uses a one-shot UI-thread hook to execute the original server-selection block once.

Active values:

```txt
CSelectServer vtable       = 0x007519B4
SelectedIndex offset #1    = +0x0BD8
SelectedIndex offset #2    = +0x1E98
Global state pointer       = 0x022EED30
Server list offset         = +0x0270
Server count offset        = +0x0274
One-shot branch            = 0x0050CC01
Original select block      = 0x0050CCCE
```

Server-side toggle:

```ini
[Game]
AutoSelectServer=true
AutoSelectServerDelayMs=5000
AutoSelectServerIndex=0
AutoSelectServerRetries=30
AutoSelectServerRetryDelayMs=500
```

Disable it without rebuilding:

```ini
AutoSelectServer=false
```

## Launcher-integrated character selection loading

When the game starts, the launcher does not close immediately and does not open a separate loading window. Instead, the launcher shows a large internal loading panel while `game.exe` runs the normal login and server-selection state machine in the background.

By default, the real game window is moved offscreen until character selection is expected to be ready. Then it is restored and brought forward.

Relevant server config:

```ini
[Game]
HideLoginScreens=true
LoginOverlayText=Connecting to the server...
LoginOverlaySubText=Preparing character selection. Please wait.
LoginOverlayRevealDelayMs=6000
LoginOverlayFallbackRevealDelayMs=1000
HideGameWindowUntilReady=true
GameWindowHideMode=Offscreen
GameWindowHideWaitMs=8000
GameWindowOffscreenX=-32000
GameWindowOffscreenY=-32000
```

Notes:

- If the game appears before character selection is ready, increase `LoginOverlayRevealDelayMs` to `8000` or `10000`.
- `Offscreen` is the recommended mode for older Direct3D clients.
- For debugging, set `HideGameWindowUntilReady=false` to watch the original client screens.
- This method does not skip the original login/server-select state machine. It only hides transitional screens from the player.

## More documentation

- `docs/INSTALL.md`
- `docs/PLAIN_LOGIN.md`
- `docs/CLEANUP_CHANGELOG.md`
- `docs/PUBLIC_RELEASE_CHECKLIST.md`
- `ServerSide/Updater/README.md`
- `SahCryptTool/README.md`
