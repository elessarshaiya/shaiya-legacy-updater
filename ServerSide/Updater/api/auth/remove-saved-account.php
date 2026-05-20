<?php
declare(strict_types=1);
require_once __DIR__ . '/../common.php';
$body = read_json_body();
$userId = clean_user_id((string)($body['userId'] ?? ''));
$deviceId = clean_device_id((string)($body['deviceId'] ?? ''));
remove_saved_account($deviceId, $userId);
respond(['ok' => true, 'message' => 'Saved account removed.']);
