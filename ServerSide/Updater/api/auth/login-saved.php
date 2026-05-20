<?php
declare(strict_types=1);
require_once __DIR__ . '/../common.php';

$body = read_json_body();
$userId = clean_user_id((string)($body['userId'] ?? ''));
$deviceId = clean_device_id((string)($body['deviceId'] ?? ''));

$row = saved_account_is_allowed($deviceId, $userId);
if (!$row) {
    respond(['ok' => false, 'message' => 'Saved-account login is not available. Please enter your password again.']);
}
if (!ensure_active_login_row($row)) {
    respond(['ok' => false, 'message' => 'This account is not allowed to login. Status=' . (int)$row['Status']]);
}

$passwordForGame = isset($row['Pw']) ? (string)$row['Pw'] : '';
if ($passwordForGame === '') {
    respond(['ok' => false, 'message' => 'Saved-account login has no valid game password. Please enter your password again.']);
}

save_device_account($deviceId, (int)$row['UserUID'], (string)$row['UserID']);
respond(success_response((string)$row['UserID'], (int)$row['UserUID'], $passwordForGame));
