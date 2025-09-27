
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

using ICSharpCode.SharpZipLib.BZip2;
using Newtonsoft.Json;

using MarvinsAIRARefactored.Classes;

using static IRSDKSharper.IRacingSdkSessionInfo.DriverInfoModel;

namespace MarvinsAIRARefactored.Components;

public class TradingPaints
{
	private static readonly HttpClient _http = CreateHttpClient();

	private readonly HashSet<int> _seenUserIds = [];
	private readonly Lock _lock = new();

	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private readonly AutoResetEvent _autoResetEvent = new( false );
	private Task? _processDriversTask;

	private string _cachePath = string.Empty;
	private readonly ConcurrentDictionary<string, string> _fileUrlToCarId = new( StringComparer.OrdinalIgnoreCase );

	public void Initialize()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[TradingPaints] Initialize >>>" );

		var settings = DataContext.DataContext.Instance.Settings;

		_cachePath = Path.Combine( settings.TradingPaintsFolder, "MarvinsAIRARefactored.cache" );

		Directory.CreateDirectory( settings.TradingPaintsFolder );

		LoadCache();

		_processDriversTask = Task.Run( () => UpdateAsync() );

		app.Logger.WriteLine( "[TradingPaints] <<< Initialize" );
	}

	public void Shutdown()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[TradingPaints] Shutdown >>>" );

		_cancellationTokenSource.Cancel();

		try
		{
			_autoResetEvent.Set();

			_processDriversTask?.Wait( 1000 );
		}
		catch
		{
		}

		app.Logger.WriteLine( "[TradingPaints] <<< Shutdown" );
	}

	public void Reset()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[TradingPaints] Reset >>>" );

		using ( _lock.EnterScope() )
		{
			_seenUserIds.Clear();
		}

		if ( app.Simulator.IsConnected )
		{
			_autoResetEvent.Set();
		}

		app.Logger.WriteLine( "[TradingPaints] <<< Reset" );
	}

	public void Update()
	{
		_autoResetEvent.Set();
	}

	private async Task UpdateAsync()
	{
		var app = App.Instance!;

		try
		{
			while ( !_cancellationTokenSource.IsCancellationRequested )
			{
				_autoResetEvent.WaitOne();

				if ( !_cancellationTokenSource.IsCancellationRequested )
				{
					var settings = DataContext.DataContext.Instance.Settings;

					if ( settings.TradingPaintsEnabled )
					{
						var sessionInfo = app.Simulator.IRSDK.Data.SessionInfo;

						var newDriverList = new List<DriverModel>();

						using ( _lock.EnterScope() )
						{
							foreach ( var driver in sessionInfo.DriverInfo.Drivers )
							{
								var userId = driver.UserID;

								if ( ( userId != -1 ) && !_seenUserIds.Contains( userId ) )
								{
									_seenUserIds.Add( userId );

									newDriverList.Add( driver );
								}
							}
						}

						if ( newDriverList.Count > 0 )
						{
							app.Logger.WriteLine( $"[TradingPaints] We have some new drivers in the session" );

							await ProcessDrivers( newDriverList );
						}
					}
				}
			}
		}
		catch
		{
		}
	}

	private async Task ProcessDrivers( List<DriverModel> driverList )
	{
		var app = App.Instance!;

		var settings = DataContext.DataContext.Instance.Settings;

		var sessionInfo = app.Simulator.IRSDK.Data.SessionInfo;

		// build the fetch query

		var playerTeamID = 0;
		var stringBuilder = new StringBuilder();

		stringBuilder.Append( "list=" );

		foreach ( var driver in driverList )
		{
			stringBuilder.Append( $"{driver.UserID}={driver.CarPath}={driver.TeamID}={driver.CarNumber}=," );

			if ( driver.UserID == sessionInfo.DriverInfo.DriverUserID )
			{
				playerTeamID = driver.TeamID;
			}
		}

		var loadNumTexturesString = app.Simulator.LoadNumTextures ? "True" : "False";

		stringBuilder.Append( $"&series={sessionInfo.WeekendInfo.SeriesID}" );
		stringBuilder.Append( $"&league={sessionInfo.WeekendInfo.LeagueID}" );
		stringBuilder.Append( $"&night={sessionInfo.WeekendInfo.WeekendOptions.TimeOfDay}" );
		stringBuilder.Append( $"&team={playerTeamID}" );
		stringBuilder.Append( $"&numbers={loadNumTexturesString}" );
		stringBuilder.Append( $"&user={sessionInfo.DriverInfo.DriverUserID}" );

		var query = stringBuilder.ToString();

		app.Logger.WriteLine( $"[TradingPaints] Fetch query: {query}" );

		// fetch the driver's XML from Trading Paints

		var fetchUri = new Uri( $"https://fetch.tradingpaints.gg/fetch.php" );

		using var req = new HttpRequestMessage( HttpMethod.Post, fetchUri )
		{
			Content = new StringContent( query, Encoding.UTF8, "application/x-www-form-urlencoded" )
		};

		using var resp = await _http.SendAsync( req, HttpCompletionOption.ResponseHeadersRead, _cancellationTokenSource.Token );

		resp.EnsureSuccessStatusCode();

		await using var xmlStream = await resp.Content.ReadAsStreamAsync( _cancellationTokenSource.Token );

		// get the assets we possibly need to download

		var assets = TradingPaintsXml.ParseAssets( xmlStream );

		if ( assets.Count == 0 )
		{
			return;
		}

		// download assets sequentially (await one by one)

		var reloadUserIDHashSet = new HashSet<long>();

		foreach ( var asset in assets )
		{
			// skip files that have not changed

			if ( !ShouldDownload( asset.FileURL, asset.FileId ) )
			{
				continue;
			}

			// add this guy to the reload list

			reloadUserIDHashSet.Add( asset.UserID );

			// prepare download folder for this car

			var carFolderFullPath = Path.Combine( settings.TradingPaintsFolder, asset.Directory );

			Directory.CreateDirectory( carFolderFullPath );

			// figure out the start of the file name

			var typeToken = asset.Type switch
			{
				TradingPaintsXml.Type.Car => "car",
				TradingPaintsXml.Type.CarNum => "car_num",
				TradingPaintsXml.Type.CarSpec => "car_spec",
				TradingPaintsXml.Type.CarDecal => "car_decal",
				TradingPaintsXml.Type.Suit => "suit",
				TradingPaintsXml.Type.Helmet => "helmet",
				_ => "file"
			};

			// figure out extension from the URL path (handles .mip, .tga, .tga.bz2)

			var (isBz2, fileExtension) = GetFileExtension( asset.FileURL );

			// figure out if we need to add some text before the file extension

			if ( !string.IsNullOrWhiteSpace( asset.Ext ) )
			{
				fileExtension = $"_{asset.Ext}{fileExtension}";
			}

			// build final filename: {Type}_{UserID}_{Ext}.{mip/tga}

			var finalFileName = $"{typeToken}_{asset.UserID}{fileExtension}";
			var finalPath = Path.Combine( settings.TradingPaintsFolder, asset.Directory, finalFileName );

			// download to a temp file first

			var temporaryPath = finalPath + ".part";

			try
			{
				app.Logger.WriteLine( $"[TradingPaints] Downloading paint file ({asset.FileURL}, {asset.FileId})" );

				await DownloadAsync( asset.FileURL, temporaryPath, _cancellationTokenSource.Token );

				if ( isBz2 )
				{
					app.Logger.WriteLine( $"[TradingPaints] Decompressing paint file ({asset.FileURL}, {asset.FileId})" );

					var ok = TryDecompressBZip2ToTga( temporaryPath, finalPath );

					if ( ok )
					{
						File.Delete( temporaryPath );
					}
					else
					{
						File.Delete( temporaryPath );
						continue;
					}
				}
				else
				{
					if ( File.Exists( finalPath ) )
					{
						File.Delete( finalPath );
					}

					File.Move( temporaryPath, finalPath );
				}

				_fileUrlToCarId[ asset.FileURL ] = asset.FileId;
			}
			catch
			{
				if ( File.Exists( temporaryPath ) )
				{
					File.Delete( temporaryPath );
				}
			}
		}

		// tell iracing to reload textures for this driver if anything got updated

		if ( reloadUserIDHashSet.Count > 0 )
		{
			SaveCache();

			foreach ( var userID in reloadUserIDHashSet )
			{
				foreach ( var driver in sessionInfo.DriverInfo.Drivers )
				{
					if ( driver.UserID == userID )
					{
						app.Logger.WriteLine( $"[TradingPaints] Telling iRacing to reload paint for {driver.UserName}" );

						App.Instance!.Simulator.IRSDK.ReloadTextures( IRSDKSharper.IRacingSdkEnum.ReloadTexturesMode.CarIdx, driver.CarIdx );
					}
				}
			}
		}
	}

	private static (bool isBz2, string finalExt) GetFileExtension( string fileUrl )
	{
		// we'll inspect the URL path (ignore querystring)

		var extPath = new Uri( fileUrl, UriKind.Absolute ).AbsolutePath;

		// normalize to lower for checks

		extPath = extPath.ToLowerInvariant();

		// .tga.bz2 => final .tga (but special handling needed)

		if ( extPath.EndsWith( ".tga.bz2", StringComparison.Ordinal ) )
		{
			return (true, ".tga");
		}

		// plain .tga

		if ( extPath.EndsWith( ".tga", StringComparison.Ordinal ) )
		{
			return (false, ".tga");
		}

		// .mip

		if ( extPath.EndsWith( ".mip", StringComparison.Ordinal ) )
		{
			return (false, ".mip");
		}

		// fallback: attempt to extract last extension; default to .tga

		var lastDot = extPath.LastIndexOf( '.', extPath.Length - 1 );

		var ext = lastDot >= 0 ? extPath[ lastDot.. ] : ".tga";

		return (false, ext);
	}

	private static async Task DownloadAsync( string url, string destPath, CancellationToken cancellationToken )
	{
		// simple retry with exponential backoff + 429 respect

		var delayMs = 400;

		for ( var attempt = 1; attempt <= 5; attempt++ )
		{
			cancellationToken.ThrowIfCancellationRequested();

			using var req = new HttpRequestMessage( HttpMethod.Get, url );
			using var resp = await _http.SendAsync( req, HttpCompletionOption.ResponseHeadersRead, cancellationToken );

			// handle 429 with Retry-After header (or exponential backoff if missing)

			if ( (int) resp.StatusCode == 429 )
			{
				var retryAfter = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromMilliseconds( delayMs );

				await Task.Delay( retryAfter, cancellationToken );

				delayMs = Math.Min( delayMs * 2, 8000 );

				continue;
			}

			// handle other non-success status codes

			if ( !resp.IsSuccessStatusCode )
			{
				if ( (int) resp.StatusCode >= 500 && attempt < 5 )
				{
					await Task.Delay( delayMs, cancellationToken );

					delayMs = Math.Min( delayMs * 2, 8000 );

					continue;
				}

				resp.EnsureSuccessStatusCode();
			}

			// success

			await using var fs = new FileStream( destPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true );

			await resp.Content.CopyToAsync( fs, cancellationToken );

			return;
		}

		throw new HttpRequestException( $"Failed to download after retries: {url}" );
	}

	private static HttpClient CreateHttpClient()
	{
		var handler = new HttpClientHandler
		{
			AllowAutoRedirect = true,
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
			MaxAutomaticRedirections = 10,
			UseCookies = true
		};

		var http = new HttpClient( handler, disposeHandler: true )
		{
			Timeout = TimeSpan.FromSeconds( 30 )
		};

		http.DefaultRequestHeaders.UserAgent.ParseAdd( "MAIRA/2.0 (+https://github.com/mherbold/MarvinsAIRARefactored)" );
		http.DefaultRequestHeaders.Accept.Add( new MediaTypeWithQualityHeaderValue( "*/*" ) );
		http.DefaultRequestHeaders.AcceptEncoding.Add( new StringWithQualityHeaderValue( "gzip" ) );
		http.DefaultRequestHeaders.AcceptEncoding.Add( new StringWithQualityHeaderValue( "deflate" ) );

		return http;
	}

	private bool ShouldDownload( string fileUrl, string carIdFromXml )
	{
		if ( !_fileUrlToCarId.TryGetValue( fileUrl, out var cachedCarId ) )
		{
			return true;
		}

		return !string.Equals( cachedCarId, carIdFromXml, StringComparison.Ordinal );
	}

	private void LoadCache()
	{
		try
		{
			if ( !File.Exists( _cachePath ) ) return;

			var json = File.ReadAllText( _cachePath, Encoding.UTF8 );
			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>( json ) ?? new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );

			foreach ( var kvp in dict )
			{
				_fileUrlToCarId.TryAdd( kvp.Key, kvp.Value );
			}
		}
		catch
		{
			// ignore cache load errors; start fresh
		}
	}

	private void SaveCache()
	{
		try
		{
			var snapshot = _fileUrlToCarId.ToDictionary( p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase );
			var json = JsonConvert.SerializeObject( snapshot, Formatting.Indented );

			var tmp = _cachePath + ".tmp";

			File.WriteAllText( tmp, json, Encoding.UTF8 );

			if ( File.Exists( _cachePath ) )
			{
				File.Delete( _cachePath );
			}

			File.Move( tmp, _cachePath );
		}
		catch
		{
			// ignore cache save errors (best effort)
		}
	}

	private static bool TryDecompressBZip2ToTga( string bz2Path, string tgaOutPath )
	{
		try
		{
			// write to a temp file first then atomically move into place

			var tmpOutPath = tgaOutPath + ".part2";

			using ( var inputStream = File.OpenRead( bz2Path ) )
			using ( var outputStream = new FileStream( tmpOutPath, FileMode.Create, FileAccess.Write, FileShare.None ) )
			{
				BZip2.Decompress( inputStream, outputStream, true );
			}

			if ( File.Exists( tgaOutPath ) )
			{
				File.Delete( tgaOutPath );
			}

			File.Move( tmpOutPath, tgaOutPath );

			return true;
		}
		catch
		{
			try
			{
				File.Delete( tgaOutPath + ".part2" );
			}
			catch
			{
			}

			return false;
		}
	}
}
