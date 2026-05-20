<?php
// Updater API configuration sample for IIS/PHP + Microsoft SQL Server 2022.
// Copy this file to config.php on the server and fill in your real SQL settings.
// Do not commit real credentials to a public repository.

return [
    'db' => [
        'server' => 'localhost',
        'database' => 'PS_UserData',
        'user' => 'CHANGE_ME',
        'password' => 'CHANGE_ME',
        'trust_server_certificate' => true,
        'encrypt' => false,
        'login_timeout' => 5,
        'query_timeout' => 15,
    ],
    'auth' => [
        'game_password_mode' => 'plain',
        'allow_saved_login_by_device' => true,
        'allowed_statuses' => [0, 16, 32, 48, 80],
    ],
    'security' => [
        'max_userid_length' => 18,
        'max_password_length' => 32,
    ],
];
