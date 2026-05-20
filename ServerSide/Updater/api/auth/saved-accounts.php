<?php
declare(strict_types=1);
require_once __DIR__ . '/../common.php';
$body = read_json_body();
$deviceId = clean_device_id((string)($body['deviceId'] ?? ''));
respond(['ok' => true, 'accounts' => read_saved_accounts($deviceId)]);
