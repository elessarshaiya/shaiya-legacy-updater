# Cleanup and Launcher UI Loading Changes

## Code

- Removed the separate `GameLoadingOverlay.cs` mini-window system.
- Moved the game start waiting screen into `mainwindow.xaml` as `GameLaunchPanel`.
- Moved the launch flow to a background thread so the WPF UI remains responsive.
- Blocked launcher closing during active launch to prevent `game.exe` from staying offscreen.
- Restored and foregrounded `game.exe` when character selection is expected to be ready.
- On launch failure, the game process is closed, the launcher panel is reset, and Start Game can be used again.
- Removed unused `Cryptography.cs` remnants from the previous cleanup pass.
- Removed token/session model remnants; login now uses `passwordForGame` from the API response.
- Replaced the embedded custom font with a system font for safer source sharing.

## Server / docs

- Removed the disabled `verify-game-token.php` endpoint from the previous cleanup pass.
- Removed unused JSON storage remnants from the previous cleanup pass.
- Removed old reference archives from the previous cleanup pass.
- Converted documentation to English.
- Added GitHub-friendly ignore rules and public config samples.
- Replaced example secrets and ESAH key values with sample placeholders.
- Removed unused `LoginOverlayWidth`, `LoginOverlayHeight`, and `LoginOverlayTopmost` settings.

## Validation performed in this environment

- Compared `Updater.csproj` Compile/Page/Resource entries with the file system.
- Parsed XAML files as XML.
- Checked for remaining Turkish text in source/docs, excluding binary image/resource payloads.

A real Windows build was not executed in this environment because Visual Studio/MSBuild is not available here.
