<?php
/* =========================
 * File: index.php
 * PHP RESX Translator — Simple single-site app for editing .resx translations
 * Requirements: PHP 8.1+, ext-dom, ext-simplexml, ext-xmlwriter; web server write access to RESX_DIR
 * ========================= */

session_start();

// --- CONFIG ---
const RESX_DIR = __DIR__ . '/resx';                 // Folder containing .resx files (mounted from your repo or a copy)
const BASE_FILE = 'Resources.resx';               // The English/base RESX filename
const FILE_STEM = 'Resources';                    // Stem used to create localized files, e.g. Localization.fr.resx
const CSRF_KEY = 'csrf_token';

if ( !file_exists( RESX_DIR ) )
{
	@mkdir( RESX_DIR, 0775, true );
}

if ( empty( $_SESSION[ CSRF_KEY ] ) )
{
	$_SESSION[ CSRF_KEY ] = bin2hex( random_bytes( 16 ) );
}

$csrf = $_SESSION[ CSRF_KEY ];

// Helper to safely echo
function h( $s )
{
	return htmlspecialchars( (string) $s, ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8' );
}

// Load language codes list
require_once __DIR__ . '/languages.php'; // defines LANG_CODES (code => display name)
require_once __DIR__ . '/percentages.php'; // defines LANG_CODES (code => display name)

// Selected language (default example)
$selected = $_GET[ 'lang' ] ?? '';
$selected = preg_replace( '~[^A-Za-z0-9_-]~', '', $selected );

// Precompute file paths
$basePath = RESX_DIR . '/' . BASE_FILE;
$localePath = $selected !== '' ? ( RESX_DIR . '/' . FILE_STEM . '.' . $selected . '.resx' ) : null;

// Basic existence checks
$baseExists = file_exists( $basePath );
$localeExists = $localePath ? file_exists( $localePath ) : false;

/**
 * Build <option> tags sorted:
 *  1) Languages with a percentage, descending by percentage
 *  2) Then the rest, by native name (visible label)
 * Each option text: "<native> (...) - <English> — <pct>%"
 */
function build_language_options( string $selected ): string
{
	$langs = LANG_CODES;
	$pcts = defined( 'PERCENTAGES' ) ? PERCENTAGES : [];

	$withPct = [];
	$noPct = [];

	foreach ( $langs as $code => $label )
	{
		if ( isset( $pcts[ $code ] ) )
		{
			$withPct[ $code ] = [ 'label' => $label, 'pct' => (int) $pcts[ $code ] ];
		}
		else
		{
			$noPct[ $code ] = $label;
		}
	}

	// Sort withPct by pct desc, then label asc
	uasort( $withPct, function ( $a, $b )
	{
		if ( $a[ 'pct' ] !== $b[ 'pct' ] ) return $b[ 'pct' ] <=> $a[ 'pct' ];
		return strcasecmp( $a[ 'label' ], $b[ 'label' ] );
	} );

	// Build HTML
	$html = '';

	foreach ( $withPct as $code => $row )
	{
		$label = $row[ 'label' ];
		$pct = $row[ 'pct' ];
		$html .= sprintf( '<option class="em" value="%s"%s>%s — %d%%</option>' . "\n", h( $code ), $selected == $code ? ' selected' : '', h( $label ), $pct );
	}

	foreach ( $noPct as $code => $label )
	{
		$html .= sprintf( '<option value="%s"%s>%s</option>' . "\n", h( $code ), $selected == $code ? ' selected' : '', h( $label ) );
	}

	return $html;
}

$language_options = build_language_options( $selected );
?>
<!DOCTYPE html>
<html lang="en">
	<head>
		<meta charset="utf-8"/>
		<meta name="viewport" content="width=device-width, initial-scale=1"/>
		<title>Translations - Marvin's Awesome Le Mans Ultimate App</title>
		<link rel="preconnect" href="https://fonts.googleapis.com">
		<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
		<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&display=swap" rel="stylesheet">
		<style>
            :root {
                --bg: #0b0f14;
                --panel: #121821;
                --muted: #9aa4b2;
                --fg: #e5ecf5;
                --accent: #2e7eff;
                --accent2: #ffb22e;
                --danger: #ff2e2e;
                --ok: #39d98a;
            }

            * {
                box-sizing: border-box;
            }

            body {
                margin: 0;
                font-family: Inter, system-ui, -apple-system, Segoe UI, Roboto, "Helvetica Neue", Arial, "Apple Color Emoji", "Segoe UI Emoji";
                background: var(--bg);
                color: var(--fg);
            }

            header {
                padding: 16px 20px;
                background: linear-gradient(90deg, rgba(46, 126, 255, .2), rgba(255, 178, 46, .15));
                border-bottom: 1px solid #223;
            }

            header h1 {
                margin: 0;
                font-size: 20px;
                font-weight: 600;
            }

            .container {
                padding: 16px 20px 32px;
            }

            .container > .sticky-stuff {
                position: sticky;
                top: 0;
                background: var(--bg);
                z-index: 10;
            }

            .controls {
                display: flex;
                gap: 12px;
                align-items: center;
                flex-wrap: wrap;
                margin-bottom: 16px;
            }

            select, input[type=text] {
                background: #10151e;
                color: var(--fg);
                border: 1px solid #2a3342;
                border-radius: 10px;
                padding: 8px 10px;
            }

            button {
                background: var(--accent);
                color: #fff;
                border: 0;
                padding: 8px 12px;
                border-radius: 10px;
                font-weight: 600;
                cursor: pointer;
            }

            button.secondary {
                background: #263249;
                color: var(--fg);
            }

            button.ghost {
                background: transparent;
                border: 1px solid #2a3342;
            }

            button:disabled {
                opacity: .6;
                cursor: not-allowed;
            }

            .status {
                margin-left: auto;
                color: var(--muted);
                font-size: 13px;
            }

            .tablewrap {
                background: var(--panel);
                border: 1px solid #1b2230;
                border-radius: 14px;
                overflow-x: auto;
                overflow-y: auto;
                -webkit-overflow-scrolling: touch; /* smooth on iOS */
            }

            table {
                min-width: 720px; /* optional: keep a sensible floor */
                border-collapse: collapse;
                font-size: 14px;
            }

            thead {
                background: #0f1420;
                position: sticky;
                top: 0;
                z-index: 2;
            }

            th, td {
                padding: 10px 12px;
                border-bottom: 1px solid #1b2230;
                vertical-align: top;
            }

            th {
                text-align: left;
                color: var(--muted);
                font-weight: 600;
            }


            tbody tr:nth-child(odd) {
                background: #111726;
            }

            .mono {
                font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
            }

            .muted {
                color: var(--muted);
            }

            .chip {
                display: inline-block;
                padding: 2px 6px;
                border-radius: 999px;
                font-size: 12px;
                border: 1px solid #2a3342;
                color: var(--muted);
            }

            .missing {
                background: rgba(255, 46, 46, .08) !important;
            }

            tbody tr:nth-child(odd).missing {
                background: rgba(255, 46, 46, .12) !important;
            }

            .toolbar {
                display: flex;
                gap: 8px;
                align-items: center;
                padding-bottom: 8px;
            }

            .move-to-right {
                margin-left: auto;
            }

            /* Modal */
            dialog {
                border: 0;
                border-radius: 16px;
                padding: 0;
                background: #0e1522;
                color: var(--fg);
                width: min(720px, 92vw);
            }

            dialog::backdrop {
                background: rgba(0, 0, 0, .6);
            }

            .modal-header {
                padding: 14px 16px;
                border-bottom: 1px solid #1b2230;
                display: flex;
                align-items: center;
                gap: 12px;
            }

            .modal-content {
                padding: 16px;
                display: flex;
                flex-direction: column;
                gap: 8px;
            }

            textarea {
                width: 100%;
                min-height: 140px;
                resize: vertical;
                background: #0c121d;
                color: var(--fg);
                border: 1px solid #2a3342;
                border-radius: 12px;
                padding: 10px;
            }

            #contribName::placeholder {
                color: #f66;
                opacity: 1;
            }

            #editEn {
                margin-bottom: 1rem;
            }

            option.em {
	            font-weight: bold;
	            color: deepskyblue;
            }
		</style>
	</head>
	<body>

		<header>
			<h1>Translations - Marvin's Awesome Le Mans Ultimate App</h1>
		</header>

		<div class="container">

			<div class="sticky-stuff">
				<form class="controls" method="get">
					<label for="lang">Language:</label>
					<select id="lang" name="lang">
						<option value="" disabled <?= $selected === '' ? 'selected' : '' ?>>&mdash; Select a language &mdash;</option>
						<?= $language_options ?>
					</select>
					<button type="submit">Switch</button>
					<span class="status">Base file: <span class="chip <?= $baseExists ? '' : 'missing' ?>"><?= h( BASE_FILE ) ?> <?= $baseExists ? '' : '— missing' ?></span>&nbsp;•&nbsp;Locale file: <span class="chip <?= $localeExists ? '' : 'missing' ?>"><?= h( FILE_STEM . ".{$selected}.resx" ) ?> <!--?= $localeExists ? '' : '— will be created on first save' ?--></span></span>
				</form>

				<div class="toolbar">
					<input type="text" id="filter" style="width:20rem" placeholder="Filter by location/key/English/translation…"/>
					<button type="button" class="secondary" id="showMissingBtn">Show only missing</button>
					<input type="text" id="contribName" placeholder="Type your name here (for credits)" style="margin-left:auto; margin-right;auto; width:18rem"/>
					<button type="button" class="ghost move-to-right" id="downloadResxBtn">Download RESX file</button>
				</div>
			</div>

			<div class="tablewrap">
				<table id="grid">
					<thead>
						<tr>
							<!--th style="width:12ch">ID</th-->
							<th>Location in App</th>
							<th style="width:24ch">Key</th>
							<th>English</th>
							<th>Translation (<?= h( $selected ) ?>)</th>
							<th style="width:10ch">Edit</th>
						</tr>
					</thead>
					<tbody id="tbody">
						<tr>
							<td colspan="6" class="muted">
								Select a language above and press <strong>Switch</strong>.
							</td>
						</tr>
					</tbody>
				</table>
			</div>
		</div>

		<dialog id="editor">
			<form method="dialog">
				<div class="modal-header">
					<strong>Edit translation</strong>
					<span id="editKey" class="chip mono" style="display:none"></span>
					<span id="editId" class="chip mono" style="display:none"></span>
					<button type="submit" class="ghost" style="margin-left:auto">Close</button>
				</div>
				<div class="modal-content">
					<label class="muted">English</label>
					<div id="editEn" class="mono" style="white-space:pre-wrap"></div>
					<label class="muted">Translation (<?= h( $selected ) ?>)</label>
					<textarea id="editValue" spellcheck="false"></textarea>
					<div style="display:flex; gap:8px; justify-content:flex-end; margin-top:6px">
						<button value="cancel" class="secondary">Cancel</button>
						<button id="saveBtn" value="default">Save</button>
					</div>
				</div>
			</form>
		</dialog>

		<script>
			const csrf = <?= json_encode( $csrf ) ?>;
			const currentLang = <?= json_encode( $selected ) ?>;

			const tbody = document.getElementById( 'tbody' );
			const filterInput = document.getElementById( 'filter' );
			const showMissingBtn = document.getElementById( 'showMissingBtn' );
			const nameInput = document.getElementById( 'contribName' );
			const downloadResxBtn = document.getElementById( 'downloadResxBtn' );
			const editor = document.getElementById( 'editor' );
			const editKeyChip = document.getElementById( 'editKey' );
			const editIdChip = document.getElementById( 'editId' );
			const editEn = document.getElementById( 'editEn' );
			const editValue = document.getElementById( 'editValue' );
			const saveBtn = document.getElementById( 'saveBtn' );

			let rows = [];
			let showOnlyMissing = false;

			async function fetchRows()
			{
				tbody.innerHTML = '<tr><td colspan="6" class="muted">Loading…</td></tr>';
				const res = await fetch( 'api.php?action=list&lang=' + encodeURIComponent( currentLang ) );
				const data = await res.json();
				rows = data.rows || [];
				render();
			}

			// --- Scroll helpers ---
			const scroller = document.scrollingElement || document.documentElement;
			let savedScrollY = 0;
			let lastEditedKey = null;

			function saveScroll()
			{
				savedScrollY = scroller.scrollTop;
			}

			function restoreScroll()
			{
				// restore after DOM paint
				requestAnimationFrame( () =>
				{
					scroller.scrollTop = savedScrollY;
				} );
			}

			function centerRowByKey( key )
			{
				if ( !key ) return;
				const btn = tbody.querySelector( `button[data-key="${ encodeURIComponent( key ) }"]` );
				if ( !btn ) return;
				btn.closest( 'tr' )?.scrollIntoView( { block: 'center' } );
			}

			// --- Render: preserve scroll, then (optionally) center last edited row ---
			function render()
			{
				saveScroll(); // 1) remember scroll

				const q = filterInput.value.toLowerCase().trim();
				const frag = document.createDocumentFragment();
				let visible = 0;

				rows
					.filter( r => !showOnlyMissing || !r.value )
					.filter( r => !q ||
						(r.id && r.id.toLowerCase().includes( q )) ||
						(r.key && r.key.toLowerCase().includes( q )) ||
						(r.comment && r.comment.toLowerCase().includes( q )) ||
						(r.en && r.en.toLowerCase().includes( q )) ||
						(r.value && r.value.toLowerCase().includes( q ))
					)
					.forEach( r =>
					{
						const tr = document.createElement( 'tr' );
						if ( !r.value ) tr.classList.add( 'missing' );
						// <td class="mono">${ escapeHtml( r.id ?? '' ) }</td>
						tr.innerHTML = `
        <td>${ escapeHtml( r.comment ?? '' ) }</td>
        <td class="mono">${ escapeHtml( r.key ?? '' ) }</td>
        <td>${ escapeHtml( r.en ?? '' ) }</td>
        <td>${ escapeHtml( r.value ?? '' ) }</td>
        <td><button data-key="${ encodeURIComponent( r.key ) }">Edit</button></td>`;
						frag.appendChild( tr );
						visible++;
					} );

				tbody.innerHTML = '';
				if ( !visible )
				{
					const tr = document.createElement( 'tr' );
					tr.innerHTML = '<td colspan="6" class="muted">No matches.</td>';
					tbody.appendChild( tr );
				}
				else
				{
					tbody.appendChild( frag );
				}

				restoreScroll();              // 2) restore scroll
				if ( lastEditedKey )
				{
					// after we’ve restored page position, center the edited row for context
					requestAnimationFrame( () =>
					{
						centerRowByKey( lastEditedKey );
						lastEditedKey = null;
					} );
				}
			}

			tbody.addEventListener( 'click', ( e ) =>
			{
				const btn = e.target.closest( 'button' );
				if ( !btn ) return;
				const key = decodeURIComponent( btn.dataset.key );
				openEditor( key );
			} );

			filterInput.addEventListener( 'input', render );
			showMissingBtn.addEventListener( 'click', () =>
			{
				showOnlyMissing = !showOnlyMissing;
				showMissingBtn.textContent = showOnlyMissing ? 'Show all' : 'Show only missing';
				render();
			} );

			downloadResxBtn.addEventListener( 'click', function ()
			{
				window.location.href = 'api.php?action=download_resx&lang=' + encodeURIComponent( currentLang );
			} );

			function openEditor( key )
			{
				const r = rows.find( x => x.key === key );
				if ( !r ) return;
				editKeyChip.textContent = r.key || '';
				editIdChip.textContent = r.id || '';
				editEn.textContent = r.en || '';
				editValue.value = r.value || '';
				editor.returnValue = '';
				editor.showModal();
			}

			editor.addEventListener( 'close', async () =>
			{
				if ( editor.returnValue === 'cancel' ) return;
				const key = editKeyChip.textContent;
				const value = editValue.value;
				saveBtn.disabled = true;
				try
				{
					const resp = await fetch( 'api.php?action=save', {
						method: 'POST',
						headers: { 'Content-Type': 'application/json' },
						body: JSON.stringify( {
							csrf,
							lang: currentLang,
							key,
							value,
							contributorName: (nameInput?.value || '').trim() || null
						} )
					} );
					const data = await resp.json();
					if ( !data.ok )
					{
						alert( 'Save failed: ' + (data.error || 'unknown') );
						return;
					}
					await fetchRows();
				} finally
				{
					saveBtn.disabled = false;
				}
			} );

			function escapeHtml( s )
			{
				return (s ?? '').replace( /[&<>"']/g, c => ({
					'&': '&amp;',
					'<': '&lt;',
					'>': '&gt;',
					'"': '&quot;',
					'\'': '&#39;'
				}[ c ]) );
			}

			if ( currentLang )
			{
				fetchRows();
			}

			function setToolbarEnabled( enabled )
			{
				[ filterInput, showMissingBtn, nameInput, downloadResxBtn ].forEach( el => el.disabled = !enabled );
			}

			setToolbarEnabled( !!currentLang );

			const LS_CONTRIB_NAME = 'translator:name';
			nameInput.value = localStorage.getItem( LS_CONTRIB_NAME ) || '';
			nameInput.addEventListener( 'input', () =>
			{
				localStorage.setItem( LS_CONTRIB_NAME, nameInput.value.trim() );
			} );
		</script>
	</body>
</html>
