<?php
/* =========================
 * File: resx_utils.php
 * Helpers to load/save .resx with comments & IDs
 * ========================= */

declare( strict_types = 1 );

const RESX_DIR = __DIR__ . '/resx';

function load_resx_safe( string $path ): array
{
	if ( !file_exists( $path ) ) return [];
	libxml_use_internal_errors( true );
	$xml = simplexml_load_file( $path );
	if ( $xml === false ) throw new RuntimeException( 'Invalid RESX: ' . basename( $path ) );
	$ns = $xml->getDocNamespaces( true );
	$out = [];
	foreach ( $xml->data as $data )
	{
		$name = (string) $data[ 'name' ];
		$value = isset( $data->value ) ? (string) $data->value : '';
		$comment = isset( $data->comment ) ? (string) $data->comment : '';
		$id = isset( $data[ 'xml:space' ] ) ? null : null; // placeholder; IDs can be stored in comment or separate custom convention
		$out[ $name ] = [ 'id' => $id, 'comment' => $comment, 'value' => $value ];
	}
	return $out;
}

function save_resx_safe( string $path, string $backupPath, array $entries, array $baseForOrdering ): void
{
	// Ensure folder
	$dir = dirname( $path );
	if ( !is_dir( $dir ) ) @mkdir( $dir, 0775, true );
	
	$backupDir = dirname( $backupPath );
	if ( !is_dir( $backupDir ) ) @mkdir( $backupDir, 0775, true );
	
	// Create backup
	if ( file_exists( $path ) )
	{
		@copy( $path, $backupPath . '.' . date( 'Ymd_His' ) . '.bak' );
	}
	
	// Preserve ordering based on base file when possible
	$orderedKeys = array_keys( $baseForOrdering );
	$extra = array_diff( array_keys( $entries ), $orderedKeys );
	$keys = array_merge( $orderedKeys, $extra );
	
	$dom = new DOMDocument( '1.0', 'utf-8' );
	$dom->formatOutput = true;
	$root = $dom->createElement( 'root' );
	$dom->appendChild( $root );
	
	// Standard headers copied from a typical .resx
	$xschema = $dom->createElement( 'xsd:schema' );
	$xschema->setAttribute( 'id', 'root' );
	$xschema->setAttribute( 'xmlns', '' );
	$xschema->setAttribute( 'xmlns:xsd', 'http://www.w3.org/2001/XMLSchema' );
	$xschema->setAttribute( 'xmlns:msdata', 'urn:schemas-microsoft-com:xml-msdata' );
	$root->appendChild( $xschema );
	
	$xresheader1 = $dom->createElement( 'resheader' );
	$xresheader1->setAttribute( 'name', 'resmimetype' );
	$xresheader1->appendChild( $dom->createElement( 'value', 'text/microsoft-resx' ) );
	$root->appendChild( $xresheader1 );
	
	$xresheader2 = $dom->createElement( 'resheader' );
	$xresheader2->setAttribute( 'name', 'version' );
	$xresheader2->appendChild( $dom->createElement( 'value', '2.0' ) );
	$root->appendChild( $xresheader2 );
	
	$xresheader3 = $dom->createElement( 'resheader' );
	$xresheader3->setAttribute( 'name', 'reader' );
	$xresheader3->appendChild( $dom->createElement( 'value', 'System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' ) );
	$root->appendChild( $xresheader3 );
	
	$xresheader4 = $dom->createElement( 'resheader' );
	$xresheader4->setAttribute( 'name', 'writer' );
	$xresheader4->appendChild( $dom->createElement( 'value', 'System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' ) );
	$root->appendChild( $xresheader4 );
	
	foreach ( $keys as $name )
	{
		if ( !isset( $entries[ $name ] ) ) continue;
		$e = $entries[ $name ];
		$data = $dom->createElement( 'data' );
		$data->setAttribute( 'name', $name );
		$data->setAttribute( 'xml:space', 'preserve' );
		$value = $dom->createElement( 'value' );
		$value->appendChild( $dom->createTextNode( (string) ( $e[ 'value' ] ?? '' ) ) );
		$data->appendChild( $value );
		$commentText = (string) ( $e[ 'comment' ] ?? '' );
		if ( $commentText !== '' )
		{
			$comment = $dom->createElement( 'comment' );
			$comment->appendChild( $dom->createTextNode( $commentText ) );
			$data->appendChild( $comment );
		}
		$root->appendChild( $data );
	}
	
	$tmp = $path . '.tmp';
	$dom->save( $tmp );
	rename( $tmp, $path );
}
