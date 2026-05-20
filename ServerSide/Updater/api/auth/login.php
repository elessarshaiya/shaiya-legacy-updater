<?php
declare(strict_types=1);
require_once __DIR__ . '/../common.php';

$body = read_json_body();
$userId = clean_user_id((string)($body['userId'] ?? ''));
$password = clean_password((string)($body['password'] ?? ''));
$deviceId = clean_device_id((string)($body['deviceId'] ?? ''));
$saveAccount = !empty($body['saveAccount']);

$row = check_password_safe($userId, $password);
if (!$row) {
    respond(['ok' => false, 'message' => 'Invalid ID or password.']);
}

$userUid = (int)$row['UserUID'];
$status = (int)$row['Status'];
if (!ensure_active_login_row($row)) {
    respond(['ok' => false, 'message' => 'This account is not allowed to login. Status=' . $status]);
}

if ($saveAccount) {
    save_device_account($deviceId, $userUid, $userId);
}

// Critical compatibility behavior:
// API validates typed password, but game.exe receives the exact Users_Master.Pw value.
// Example: UserID=2, Pw=2 => client receives/writes 2.
$passwordForGame = get_db_password_for_game($userId, $password);
respond(success_response($userId, $userUid, $passwordForGame));
