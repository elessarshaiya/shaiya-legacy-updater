# Plain Login Mode for PS_UserData

This package keeps the legacy launcher appearance while using a modern PHP API for login and update control. It does not add a token validation requirement to `game.exe`.

## Flow

1. The updater sends UserID and password to the PHP API.
2. The PHP API validates the credentials through `PS_UserData.dbo.CheckPw_Safe`.
3. If validation succeeds, the API reads `PS_UserData.dbo.Users_Master.Pw` again.
4. The updater writes UserID and the database `Pw` value into `game.exe`.

## Example

```txt
Users_Master.UserID = 2
Users_Master.Pw     = 2
```

The player enters `ID=2 / Password=2` in the updater. The API validates the credentials and the updater writes `2 / 2` into `game.exe`.

## Setup

- Copy `ServerSide/Updater` to `C:\inetpub\wwwroot\Updater`.
- Keep `web.config.optional` disabled unless IIS needs an explicit `.patch` MIME rule.
- Copy `ServerSide/Updater/api/config.sample.php` to `ServerSide/Updater/api/config.php` on the server.
- Edit SQL settings in `config.php`.

Default template values are placeholders:

```txt
server   = localhost
database = PS_UserData
user     = CHANGE_ME
password = CHANGE_ME
```

## Test

```txt
http://127.0.0.1/Updater/test-db.php
http://127.0.0.1/Updater/test-env.php
```

PowerShell login test:

```powershell
$body = @{ userId = "2"; password = "2"; deviceId = "TEST"; saveAccount = $false } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "http://127.0.0.1/Updater/api/auth/login.php" -ContentType "application/json" -Body $body
```

The expected response includes `passwordForGame` as the database password that must be written into the client.

## Saved accounts

For one-click saved-account login, run:

```txt
ServerSide_SQL/01_updater_auth_tables.sql
```

This creates `dbo.Updater_SavedDeviceAccount`. The updater does not store real passwords in this table. Saved login resolves the account through device hash + `UserUID`, then reads `Users_Master.Pw` again.
