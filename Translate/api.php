<?php
/* =========================
 * File: api.php
 * Backend endpoints for listing, saving, exporting, and adding keys
 * ========================= */

declare( strict_types = 1 );

session_start();
header( 'Content-Type: application/json; charset=utf-8' );

require_once __DIR__ . '/resx_utils.php';
require_once __DIR__ . '/languages.php';
require_once __DIR__ . '/secrets.php';
require_once __DIR__ . '/percentages_utils.php';

const RESX_BACKUP_DIR = __DIR__ . '/resx-backups';
const BASE_FILE = 'Resources.resx';
const FILE_STEM = 'Resources';
const CSRF_KEY = 'csrf_token';
const CONTRIBUTORS_FILE = __DIR__ . '/contributors.json';

$action = $_GET[ 'action' ] ?? '';

try
{
	switch ( $action )
	{
		case 'list':
			list_action();
			break;
		case 'save':
			save_action();
			break;
		case 'download_resx':
			download_resx_action();
			break;
		default:
			echo json_encode( [ 'ok' => false, 'error' => 'Unknown action' ] );
	}
}
catch ( Throwable $e )
{
	http_response_code( 500 );
	echo json_encode( [ 'ok' => false, 'error' => $e->getMessage() ] );
}

function list_action(): void
{
	header( 'Content-Type: application/json; charset=utf-8' );
	$lang = get_lang();
	$base = load_resx_safe( RESX_DIR . '/' . BASE_FILE );
	$locPath = RESX_DIR . '/' . FILE_STEM . ".{$lang}.resx";
	$loc = file_exists( $locPath ) ? load_resx_safe( $locPath ) : [];
	
	$rows = [];
	foreach ( $base as $name => $item )
	{
		$rows[] = [
			'id' => $item[ 'id' ] ?? '',
			'key' => $name,
			'comment' => $item[ 'comment' ] ?? '',
			'en' => $item[ 'value' ] ?? '',
			'value' => $loc[ $name ][ 'value' ] ?? '',
		];
	}
	
	usort( $rows, function ( $a, $b )
	{
		$ac = trim( (string) ( $a[ 'comment' ] ?? '' ) );
		$bc = trim( (string) ( $b[ 'comment' ] ?? '' ) );
		if ( $ac === '' && $bc !== '' ) return 1; // empty comments last
		if ( $ac !== '' && $bc === '' ) return -1; // non-empty comments first
		$cmp = strcasecmp( $ac, $bc );
		if ( $cmp !== 0 ) return $cmp;
		return strcasecmp( (string) ( $a[ 'key' ] ?? '' ), (string) ( $b[ 'key' ] ?? '' ) );
	} );
	
	echo json_encode( [ 'ok' => true, 'rows' => $rows ] );
}

function save_action(): void
{
	require_json();
	csrf_check();
	
	$payload = json_decode( file_get_contents( 'php://input' ), true ) ?? [];
	
	$lang = sanitize_lang( $payload[ 'lang' ] ?? '' );
	$key = trim( (string) ( $payload[ 'key' ] ?? '' ) );
	$val = (string) ( $payload[ 'value' ] ?? '' );
	$contributorName = trim( (string) ( $payload[ 'contributorName' ] ?? '' ) );
	
	if ( $lang === '' || $key === '' )
	{
		throw new RuntimeException( 'Missing lang or key' );
	}
	
	$locPath = RESX_DIR . '/' . FILE_STEM . ".{$lang}.resx";
	$backupPath = RESX_BACKUP_DIR . '/' . FILE_STEM . ".{$lang}.resx";
	$basePath = RESX_DIR . '/' . BASE_FILE;
	
	$base = load_resx_safe( $basePath );
	
	// Ensure locale file exists & contains at least base keys
	$loc = file_exists( $locPath ) ? load_resx_safe( $locPath ) : [];
	foreach ( $base as $name => $item )
	{
		if ( !isset( $loc[ $name ] ) )
		{
			$loc[ $name ] = [
				'id' => $item[ 'id' ] ?? null,
				'comment' => $item[ 'comment' ] ?? null,
				'value' => ''
			];
		}
	}
	
	// Upsert only if changed
	if ( !isset( $loc[ $key ] ) )
	{
		$loc[ $key ] = [ 'id' => null, 'comment' => null, 'value' => '' ];
	}
	$old = (string) ( $loc[ $key ][ 'value' ] ?? '' );
	$changed = ( $old !== $val );
	
	// If the translation actually changed then update it and notify
	if ( $changed )
	{
		// Save .resx (with backup)
		$loc[ $key ][ 'value' ] = $val;
		save_resx_safe( $locPath, $backupPath, $loc, $base );
		
		try
		{
			$map = load_percentages_map();
			$pct = compute_locale_percentage_from_arrays( $base, $loc );
			if ( $pct > 0 )
			{
				$map[ $lang ] = $pct;
			}
			else
			{
				// Remove if no completed strings
				unset( $map[ $lang ] );
			}
			write_percentages_php( $map );
		}
		catch ( Throwable $e )
		{
			// Non-fatal; optionally log
			// error_log('percentages update failed: ' . $e->getMessage());
		}
		
		// notify
		$ip = get_client_ip();
		$english = (string) ( $base[ $key ][ 'value' ] ?? '' );
		$comment = (string) ( $base[ $key ][ 'comment' ] ?? null );
		send_discord_translation_update( $ip, $contributorName, $lang, $key, $english, $val, $comment );
		
		// record contributor name
		if ( $contributorName !== '' )
		{
			record_contributor_name( $lang, $contributorName );
		}
	}
	
	echo json_encode( [ 'ok' => true, 'changed' => $changed ] );
}

function download_resx_action(): void
{
	$lang = get_lang();
	$locPath = RESX_DIR . '/' . FILE_STEM . ".{$lang}.resx";
	
	if ( file_exists( $locPath ) )
	{
		header( 'Content-Type: application/xml; charset=UTF-8' );
		header( 'Content-Disposition: attachment; filename="' . basename( $locPath ) . '"' );
		header( 'Cache-Control: no-store, no-cache, must-revalidate, max-age=0' );
		
		echo file_get_contents( $locPath );
	}
	else
	{
		http_response_code( 404 );
		
		echo "RESX file for language {$lang} does not exist yet.";
	}
	
	exit;
}

function get_lang(): string
{
	$lang = sanitize_lang( $_GET[ 'lang' ] ?? '' );
	if ( $lang === '' ) throw new RuntimeException( 'Missing lang' );
	return $lang;
}

function sanitize_lang( string $s ): string
{
	return preg_replace( '~[^A-Za-z0-9_-]~', '', $s );
}

function require_json(): void
{
	if ( stripos( $_SERVER[ 'CONTENT_TYPE' ] ?? '', 'application/json' ) === false )
	{
		throw new RuntimeException( 'Expected JSON' );
	}
}

function csrf_check(): void
{
	if ( !hash_equals( $_SESSION[ CSRF_KEY ] ?? '', ( json_decode( file_get_contents( 'php://input' ), true )[ 'csrf' ] ?? '' ) ) ) throw new RuntimeException( 'CSRF failed' );
}

/**
 * Keep a per-language unique list of contributor names.
 * File format:
 * {
 *   "it-IT": ["Alice", "Carlo"],
 *   "nb-NO": ["Ola Nordmann"]
 * }
 */
function record_contributor_name( string $lang, string $name ): void
{
	$path = CONTRIBUTORS_FILE;
	
	// Create file if missing
	if ( !file_exists( $path ) )
	{
		@file_put_contents( $path, json_encode( new stdClass(), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE ) );
	}
	
	$fp = @fopen( $path, 'c+' );
	if ( !$fp ) return;
	
	try
	{
		if ( !flock( $fp, LOCK_EX ) )
		{
			fclose( $fp );
			return;
		}
		
		$size = filesize( $path );
		$raw = $size > 0 ? fread( $fp, $size ) : '{}';
		$data = json_decode( $raw ?: '{}', true );
		if ( !is_array( $data ) ) $data = [];
		
		if ( !isset( $data[ $lang ] ) || !is_array( $data[ $lang ] ) ) $data[ $lang ] = [];
		
		$trimmed = trim( $name );
		if ( $trimmed !== '' )
		{
			$exists = false;
			foreach ( $data[ $lang ] as $n )
			{
				if ( mb_strtolower( trim( (string) $n ) ) === mb_strtolower( $trimmed ) )
				{
					$exists = true;
					break;
				}
			}
			if ( !$exists )
			{
				$data[ $lang ][] = $trimmed;
				ftruncate( $fp, 0 );
				rewind( $fp );
				fwrite( $fp, json_encode( $data, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE ) );
			}
		}
		
		flock( $fp, LOCK_UN );
	}
	finally
	{
		fclose( $fp );
	}
}

/**
 * Try to get a real client IP (behind proxies/CDN if present).
 */
function get_client_ip(): string
{
	$keys = [
		'HTTP_CF_CONNECTING_IP',     // Cloudflare
		'HTTP_X_REAL_IP',
		'HTTP_X_FORWARDED_FOR',      // could be a list
		'HTTP_CLIENT_IP',
		'REMOTE_ADDR',
	];
	foreach ( $keys as $k )
	{
		if ( !empty( $_SERVER[ $k ] ) )
		{
			$ip = trim( explode( ',', (string) $_SERVER[ $k ] )[ 0 ] );
			// very light sanity check
			if ( $ip !== '' ) return $ip;
		}
	}
	return 'unknown';
}

/** Discord embed field hard limit is 1024 chars; be safe. */
function trunc( string $s, int $limit = 1000 ): string
{
	if ( mb_strlen( $s ) <= $limit ) return $s;
	return mb_substr( $s, 0, $limit - 3 ) . '…';
}

/**
 * Send a single-embed Discord webhook notification.
 * Non-fatal: swallows transport errors; saving must not fail due to webhook.
 */
function send_discord_translation_update( string $ip, string $contributorName, string $lang, string $key, string $english, string $localized, ?string $comment = null ): void
{
	$url = DISCORD_WEBHOOK_URL;
	if ( !$url ) return; // disabled
	
	$embed = [
		'title' => 'Translation updated',
		'timestamp' => gmdate( 'c' ),
		'color' => 0x2e7eff, // MALMUA blue 💙
		'fields' => [
			[ 'name' => 'Language', 'value' => $lang, 'inline' => true ],
			[ 'name' => 'Key', 'value' => trunc( $key, 256 ), 'inline' => true ],
			[ 'name' => 'Contributor', 'value' => ( $contributorName !== '' ? $contributorName : '—' ), 'inline' => true ],
			[ 'name' => 'IP', 'value' => $ip, 'inline' => true ],
		],
	];
	
	if ( $comment )
	{
		$embed[ 'fields' ][] = [ 'name' => 'Location in App', 'value' => trunc( $comment, 1024 ), 'inline' => false ];
	}
	
	$embed[ 'fields' ][] = [ 'name' => 'English', 'value' => trunc( $english, 1024 ), 'inline' => false ];
	$embed[ 'fields' ][] = [ 'name' => 'Translation', 'value' => trunc( $localized, 1024 ), 'inline' => false ];
	
	$payload = json_encode( [
		// You can also set 'content' => 'optional plain text line'
		'embeds' => [ $embed ],
	], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES );
	
	// cURL POST
	$ch = @curl_init( $url );
	
	if ( !$ch ) return;
	
	@curl_setopt_array( $ch, [
		CURLOPT_POST => true,
		CURLOPT_HTTPHEADER => [ 'Content-Type: application/json' ],
		CURLOPT_POSTFIELDS => $payload,
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_TIMEOUT => 3,    // be snappy, don’t block
		CURLOPT_CONNECTTIMEOUT => 2,
		CURLOPT_USERAGENT => 'MALMUA-Translator/1.0',
	] );
	@curl_exec( $ch );
	@curl_close( $ch );
}
