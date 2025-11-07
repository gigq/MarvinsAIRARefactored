<?php
/* =========================
 * File: percentages_utils.php
 * Incremental percentages maintenance
 * ========================= */

declare( strict_types = 1 );

require_once __DIR__ . '/resx_utils.php';

const PERCENTAGES_FILE = __DIR__ . '/percentages.php';

/** Load existing percentages map from percentages.php (if present). */
function load_percentages_map(): array
{
	if ( file_exists( PERCENTAGES_FILE ) )
	{
		// Isolate scope: include returns void; use defined() to extract constant.
		require_once PERCENTAGES_FILE;
		if ( defined( 'PERCENTAGES' ) && is_array( PERCENTAGES ) )
		{
			return PERCENTAGES;
		}
	}
	return [];
}

/**
 * Compute completion percentage for a single locale
 * using already-loaded arrays from resx_utils.php (no extra I/O).
 * $base: array 'key' => ['value' => ..., 'comment' => ...]
 * $loc:  array 'key' => ['value' => ..., 'comment' => ...]
 */
function compute_locale_percentage_from_arrays( array $base, array $loc ): int
{
	$total = count( $base );
	if ( $total === 0 ) return 0;
	
	$translated = 0;
	foreach ( $base as $k => $baseItem )
	{
		$v = trim( (string) ( $loc[ $k ][ 'value' ] ?? '' ) );
		if ( $v !== '' )
		{
			$translated++;
		}
	}
	
	return (int) floor( 100 * $translated / $total );
}

/** Write the given map back to percentages.php as const PERCENTAGES = [ ... ]; */
function write_percentages_php( array $map ): void
{
	// Sort by percentage desc, then code asc for stable output
	arsort( $map, SORT_NUMERIC );
	
	$lines = [ "<?php", "", "const PERCENTAGES = [" ];
	foreach ( $map as $code => $pct )
	{
		$lines[] = "\t'{$code}' => {$pct},";
	}
	$lines[] = "];";
	$php = implode( "\n", $lines ) . "\n";
	
	$tmp = PERCENTAGES_FILE . '.tmp';
	file_put_contents( $tmp, $php );
	rename( $tmp, PERCENTAGES_FILE );
}

/** Maintenance: full rebuild (kept for admin/CLI use) */
function compute_completion_percentages_full(): array
{
	$basePath = RESX_DIR . '/Resources.resx';
	$base = load_resx_safe( $basePath );
	$total = count( $base );
	if ( $total === 0 ) return [];
	
	$percentages = [];
	foreach ( glob( RESX_DIR . '/Resources.*.resx' ) as $path )
	{
		if ( preg_match( '#Resources\.([^.]+)\.resx$#', basename( $path ), $m ) !== 1 ) continue;
		$code = $m[ 1 ];
		if ( $code === 'resx' ) continue;
		$loc = load_resx_safe( $path );
		$pct = compute_locale_percentage_from_arrays( $base, $loc );
		if ( $pct > 0 )
		{
			$percentages[ $code ] = $pct;
		}
	}
	arsort( $percentages, SORT_NUMERIC );
	return $percentages;
}
