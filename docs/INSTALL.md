# Clean Installation

## Server

1. Clean the target folder:

```txt
C:\inetpub\wwwroot\Updater
```

2. Copy the contents of `ServerSide/Updater/*` into that folder.
3. Copy `api/config.sample.php` to `api/config.php` on the server.
4. Edit `api/config.php` with your SQL Server settings.
5. Copy `launcher.sample.ini` to `launcher.ini` on the server.
6. Edit `launcher.ini` with your real URLs, game settings, and SAH/ESAH key.
7. Test the deployment:

```txt
http://127.0.0.1/Updater/test-env.php
http://127.0.0.1/Updater/test-db.php
http://127.0.0.1/Updater/test-config.php
```

Expected database response:

```json
{
  "ok": true,
  "message": "Database connection OK.",
  "database": "PS_UserData"
}
```

## Client / updater

Open `Updater/Updater.sln` with Visual Studio 2022 and build with the .NET Framework 4.8 Developer Pack installed.

To build only the updater from a command prompt:

```bat
build_updater_only.bat
```

## Patch folder

Patch files must be placed on the server here:

```txt
C:\inetpub\wwwroot\Updater\Patches
```

After adding a patch, update the `patches` list in `manifest.json`. Use this helper:

```txt
http://127.0.0.1/Updater/tools/patch_hashes.php
```

See `ServerSide/Updater/manifest.sample.json` for the expected format.

## Launcher-integrated loading

The game start flow does not open a separate loading window. The waiting screen is displayed inside the launcher UI. By default, `game.exe` is moved offscreen until character selection is expected to be ready.

Relevant settings are in `ServerSide/Updater/launcher.ini`:

```ini
HideGameWindowUntilReady=true
GameWindowHideMode=Offscreen
LoginOverlayRevealDelayMs=6000
```

## SAH / ESAH

There is no local client-side `updater.ini`. SAH/ESAH behavior is controlled by server-side `launcher.ini`.

Use a production-only 32-byte key and keep it out of public repositories.
