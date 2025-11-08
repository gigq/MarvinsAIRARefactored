<?php
/* =========================
 * File: rebuild_percentages.php
 * Recalculate completion percentages for all locales and rewrite percentages.php
 * Requirements: PHP 8.1+, ext-dom, ext-simplexml, ext-xmlwriter
 * ========================= */

declare(strict_types=1);

header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/resx_utils.php';
require_once __DIR__ . '/percentages_utils.php';

try {
	// Build fresh map from disk (base + all Resources.*.resx)
	$map = compute_completion_percentages_full();
	if (!$map) {
		http_response_code(500);
		echo "ERROR: No base strings or nothing to compute.\n";
		exit;
	}
	
	// Persist to Translate/percentages.php
	write_percentages_php($map);
	
	echo "OK: Rebuilt percentages.php\n";
	echo "Locales updated: " . count($map) . "\n\n";
	foreach ($map as $code => $pct) {
		echo "{$code} => {$pct}%\n";
	}
} catch (Throwable $e) {
	http_response_code(500);
	echo "ERROR: " . $e->getMessage() . "\n";
}
