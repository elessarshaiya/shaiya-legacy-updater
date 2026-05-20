<?php
// Compatibility-oriented common API code for IIS + PHP sqlsrv.
// QueryTimeout is used only on sqlsrv_query, never sqlsrv_connect.

ini_set('display_errors', '0');
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');

$configFile = __DIR__ . '/config.php';
if (!is_file($configFile)) {
    $configFile = __DIR__ . '/config.sample.php';
}
$config = require $configFile;

function respond($data, $code = 200) {
    http_response_code($code);
    echo json_encode($data, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    exit;
}

function read_json_body() {
    $raw = file_get_contents('php://input');
    if ($raw === false || trim($raw) === '') return [];
    $data = json_decode($raw, true);
    return is_array($data) ? $data : [];
}

function client_ip() {
    foreach (['HTTP_CF_CONNECTING_IP', 'HTTP_X_FORWARDED_FOR', 'REMOTE_ADDR'] as $key) {
        if (empty($_SERVER[$key])) continue;
        $value = trim((string)$_SERVER[$key]);
        if ($key === 'HTTP_X_FORWARDED_FOR') {
            $parts = explode(',', $value);
            $value = trim($parts[0]);
        }
        if ($value !== '') return substr($value, 0, 45);
    }
    return '0.0.0.0';
}

function require_sqlsrv() {
    if (!function_exists('sqlsrv_connect')) {
        respond([
            'ok' => false,
            'message' => 'PHP sqlsrv extension is not loaded.',
            'hint' => 'Enable Microsoft Drivers for PHP for SQL Server: php_sqlsrv.dll.'
        ], 500);
    }
}

function db_error_text() {
    if (!function_exists('sqlsrv_errors')) return 'sqlsrv is unavailable';
    $errors = sqlsrv_errors(SQLSRV_ERR_ERRORS);
    if (!$errors) return 'unknown database error';
    $out = [];
    foreach ($errors as $e) {
        $out[] = '[' . ($e['SQLSTATE'] ?? '') . '/' . ($e['code'] ?? '') . '] ' . ($e['message'] ?? '');
    }
    return implode(' | ', $out);
}

function db_conn() {
    static $conn = null;
    global $config;
    if ($conn) return $conn;

    require_sqlsrv();

    $db = $config['db'];
    $opts = [
        'Database' => $db['database'],
        'CharacterSet' => 'UTF-8',
        'LoginTimeout' => isset($db['login_timeout']) ? (int)$db['login_timeout'] : 5,
        'TrustServerCertificate' => !empty($db['trust_server_certificate']),
        'Encrypt' => !empty($db['encrypt']),
    ];

    if (!empty($db['user'])) {
        $opts['UID'] = $db['user'];
        $opts['PWD'] = $db['password'];
    }

    $conn = sqlsrv_connect($db['server'], $opts);
    if (!$conn) {
        respond(['ok' => false, 'message' => 'Database connection failed.', 'detail' => db_error_text()], 500);
    }
    return $conn;
}

function db_query($sql, $params = []) {
    global $config;
    $queryOptions = [];
    if (isset($config['db']['query_timeout'])) {
        $queryOptions['QueryTimeout'] = (int)$config['db']['query_timeout'];
    }
    $stmt = sqlsrv_query(db_conn(), $sql, $params, $queryOptions);
    if (!$stmt) {
        respond(['ok' => false, 'message' => 'Database query failed.', 'detail' => db_error_text(), 'sql' => $sql], 500);
    }
    return $stmt;
}

function db_fetch_one($sql, $params = []) {
    $stmt = db_query($sql, $params);
    $row = sqlsrv_fetch_array($stmt, SQLSRV_FETCH_ASSOC);
    if ($row === false || $row === null) return null;
    return $row;
}

function clean_user_id($userId) {
    global $config;
    $userId = trim((string)$userId);
    $max = (int)$config['security']['max_userid_length'];
    if ($userId === '' || strlen($userId) > $max) respond(['ok' => false, 'message' => 'Invalid user ID.']);
    if (!preg_match('/^[A-Za-z0-9_\-]+$/', $userId)) respond(['ok' => false, 'message' => 'Invalid user ID format.']);
    return $userId;
}

function clean_password($password) {
    global $config;
    $password = (string)$password;
    $max = (int)$config['security']['max_password_length'];
    if ($password === '' || strlen($password) > $max) respond(['ok' => false, 'message' => 'Invalid password.']);
    return $password;
}

function clean_device_id($deviceId) {
    $deviceId = trim((string)$deviceId);
    if ($deviceId === '') return '';
    return substr($deviceId, 0, 128);
}

function is_allowed_status($status) {
    global $config;
    $allowed = array_map('intval', $config['auth']['allowed_statuses']);
    return in_array((int)$status, $allowed, true);
}

function table_exists($tableName) {
    $row = db_fetch_one("SELECT CASE WHEN OBJECT_ID(?, N'U') IS NULL THEN 0 ELSE 1 END AS ExistsFlag", [$tableName]);
    return $row && (int)$row['ExistsFlag'] === 1;
}

function check_password_safe($userId, $password) {
    $stmt = db_query("EXEC dbo.CheckPw_Safe @UserID=?, @Pw=?", [$userId, $password]);
    $row = sqlsrv_fetch_array($stmt, SQLSRV_FETCH_ASSOC);
    if ($row === false || $row === null) return null;
    return $row;
}

function get_db_password_for_game($userId, $fallbackPassword) {
    $row = db_fetch_one(
        "SELECT TOP 1 Pw FROM dbo.Users_Master WITH (NOLOCK)
         WHERE UserID COLLATE SQL_Latin1_General_CP1_CS_AS = ?",
        [$userId]
    );
    $pw = ($row && isset($row['Pw'])) ? (string)$row['Pw'] : '';
    return $pw !== '' ? $pw : $fallbackPassword;
}

function ensure_active_login_row($row) {
    if (!isset($row['Status'])) return false;
    if (!is_allowed_status((int)$row['Status'])) return false;
    if (isset($row['Leave']) && (int)$row['Leave'] !== 0) return false;
    return true;
}

function save_device_account($deviceId, $userUid, $userId) {
    if ($deviceId === '') return;
    if (!table_exists('dbo.Updater_SavedDeviceAccount')) return;
    $ip = client_ip();
    $sql = "
DECLARE @DeviceHash varbinary(32) = HASHBYTES('SHA2_256', CONVERT(varbinary(256), ?));
IF EXISTS (SELECT 1 FROM dbo.Updater_SavedDeviceAccount WHERE DeviceIdHash=@DeviceHash AND UserUID=?)
BEGIN
    UPDATE dbo.Updater_SavedDeviceAccount
    SET UserID=?, LastUsedAt=SYSUTCDATETIME(), LastIP=?
    WHERE DeviceIdHash=@DeviceHash AND UserUID=?;
END
ELSE
BEGIN
    INSERT INTO dbo.Updater_SavedDeviceAccount(DeviceIdHash, UserUID, UserID, CreatedAt, LastUsedAt, LastIP)
    VALUES(@DeviceHash, ?, ?, SYSUTCDATETIME(), SYSUTCDATETIME(), ?);
END";
    db_query($sql, [$deviceId, (int)$userUid, $userId, $ip, (int)$userUid, (int)$userUid, $userId, $ip]);
}

function saved_account_is_allowed($deviceId, $userId) {
    if ($deviceId === '' || !table_exists('dbo.Updater_SavedDeviceAccount')) return null;
    return db_fetch_one(
        "DECLARE @DeviceHash varbinary(32) = HASHBYTES('SHA2_256', CONVERT(varbinary(256), ?));
         SELECT TOP 1 u.UserUID, u.UserID, u.Pw, u.Status, u.Leave
         FROM dbo.Updater_SavedDeviceAccount s WITH (NOLOCK)
         INNER JOIN dbo.Users_Master u WITH (NOLOCK) ON u.UserUID = s.UserUID
         WHERE s.DeviceIdHash = @DeviceHash AND u.UserID COLLATE SQL_Latin1_General_CP1_CS_AS = ?",
        [$deviceId, $userId]
    );
}

function read_saved_accounts($deviceId) {
    if ($deviceId === '' || !table_exists('dbo.Updater_SavedDeviceAccount')) return [];
    $stmt = db_query(
        "DECLARE @DeviceHash varbinary(32) = HASHBYTES('SHA2_256', CONVERT(varbinary(256), ?));
         SELECT u.UserID
         FROM dbo.Updater_SavedDeviceAccount s WITH (NOLOCK)
         INNER JOIN dbo.Users_Master u WITH (NOLOCK) ON u.UserUID = s.UserUID
         WHERE s.DeviceIdHash = @DeviceHash
         ORDER BY s.LastUsedAt DESC, s.CreatedAt DESC",
        [$deviceId]
    );
    $out = [];
    while (($row = sqlsrv_fetch_array($stmt, SQLSRV_FETCH_ASSOC)) !== null) {
        if ($row !== false && isset($row['UserID'])) $out[] = (string)$row['UserID'];
    }
    return $out;
}

function remove_saved_account($deviceId, $userId) {
    if ($deviceId === '' || !table_exists('dbo.Updater_SavedDeviceAccount')) return;
    db_query(
        "DECLARE @DeviceHash varbinary(32) = HASHBYTES('SHA2_256', CONVERT(varbinary(256), ?));
         DELETE s
         FROM dbo.Updater_SavedDeviceAccount s
         INNER JOIN dbo.Users_Master u ON u.UserUID = s.UserUID
         WHERE s.DeviceIdHash = @DeviceHash AND u.UserID COLLATE SQL_Latin1_General_CP1_CS_AS = ?",
        [$deviceId, $userId]
    );
}

function success_response($userId, $userUid, $passwordForGame) {
    return [
        'ok' => true,
        'message' => 'Login successful.',
        'userId' => $userId,
        'userUid' => (int)$userUid,
        'passwordForGame' => $passwordForGame,
    ];
}
