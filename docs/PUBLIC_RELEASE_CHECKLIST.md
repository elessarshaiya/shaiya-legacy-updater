# Public Release Checklist

Before publishing this repository publicly, review the following items:

## Secrets

- Do not commit real `ServerSide/Updater/api/config.php` credentials.
- Do not commit a production `ServerSide/Updater/launcher.ini` if it contains real keys or private URLs.
- Replace the public sample SAH/ESAH key in both `launcher.ini` and `SahCryptTool/main.cpp` before production use.
- Keep production keys outside the repository whenever possible.

## Assets and binaries

- Review UI image ownership before making the repository public.
- Review the bundled `Updater/lib/Ionic.Zip.dll` and keep any required third-party notices.
- Do not add client binaries, patch archives, full repair files, logs, or backup folders to Git.

## Runtime files

The `.gitignore` is configured to avoid common generated or deployment-only files, including:

- `bin/`, `obj/`, `.vs/`
- logs and backups
- real `config.php`
- real `launcher.ini`
- patch and repair payloads, while keeping their README files

## Build check

Before tagging a public release:

1. Build `Updater.LegacyUI.ModernCore.sln` in Release x86.
2. Deploy the PHP server files to a test IIS folder.
3. Configure real `config.php` and `launcher.ini` only on the test server.
4. Test login, saved login, patch download, patch apply, repair, and game launch.
5. Confirm that the launcher-integrated loading panel restores `game.exe` correctly.
