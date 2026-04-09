
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;

using MarvinsALMUARefactored.Classes;
using MarvinsALMUARefactored.Windows;

using Newtonsoft.Json;

namespace MarvinsALMUARefactored.Components;

public class CloudService
{
	public Guid NetworkIdGuid { get; private set; } = Guid.Empty;

	public bool CheckingForUpdate { get; private set; } = false;
	public bool DownloadingUpdate { get; private set; } = false;

	public void Initialize()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[CloudService] Initialize >>>" );

		var networkInterfaceList = NetworkInterface.GetAllNetworkInterfaces();

		var networkInterface = networkInterfaceList.FirstOrDefault();

		if ( networkInterface != null )
		{
			if ( Guid.TryParse( networkInterface.Id, out var networkIdGuid ) )
			{
				NetworkIdGuid = networkIdGuid;

				app.Logger.WriteLine( $"[CloudService] Network ID = {NetworkIdGuid}" );
			}
		}

		app.Logger.WriteLine( "[CloudService] <<< Initialize" );
	}

	class GetCurrentVersionResponse
	{
		public string currentVersion = string.Empty;
		public string downloadUrl = string.Empty;
		public string changeLog = string.Empty;
	}

	class GitHubReleaseAsset
	{
		[JsonProperty( "name" )]
		public string Name { get; set; } = string.Empty;

		[JsonProperty( "browser_download_url" )]
		public string BrowserDownloadUrl { get; set; } = string.Empty;
	}

	class GitHubLatestReleaseResponse
	{
		[JsonProperty( "tag_name" )]
		public string TagName { get; set; } = string.Empty;

		[JsonProperty( "body" )]
		public string Body { get; set; } = string.Empty;

		[JsonProperty( "html_url" )]
		public string HtmlUrl { get; set; } = string.Empty;

		[JsonProperty( "assets" )]
		public List<GitHubReleaseAsset> Assets { get; set; } = [];
	}

	private static readonly Uri LatestReleaseApiUri = new( "https://api.github.com/repos/gigq/MarvinsAIRARefactored/releases/latest" );

	private static bool IsNewerVersion( string currentVersion, string availableVersion )
	{
		if ( Version.TryParse( currentVersion, out var current ) && Version.TryParse( availableVersion, out var available ) )
		{
			return available > current;
		}

		return !string.Equals( currentVersion, availableVersion, StringComparison.OrdinalIgnoreCase );
	}

	private static GetCurrentVersionResponse? MapLatestRelease( GitHubLatestReleaseResponse? release )
	{
		if ( release == null || string.IsNullOrWhiteSpace( release.TagName ) )
		{
			return null;
		}

		var installerAsset = release.Assets.FirstOrDefault( asset =>
			asset.Name.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ) &&
			asset.Name.Contains( "Setup", StringComparison.OrdinalIgnoreCase ) );

		var downloadUrl = installerAsset?.BrowserDownloadUrl;

		if ( string.IsNullOrWhiteSpace( downloadUrl ) )
		{
			downloadUrl = release.Assets.FirstOrDefault( asset => asset.Name.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ) )?.BrowserDownloadUrl;
		}

		if ( string.IsNullOrWhiteSpace( downloadUrl ) )
		{
			downloadUrl = release.HtmlUrl;
		}

		return new GetCurrentVersionResponse
		{
			currentVersion = release.TagName.Trim(),
			downloadUrl = downloadUrl ?? string.Empty,
			changeLog = string.IsNullOrWhiteSpace( release.Body ) ? $"See release notes: {release.HtmlUrl}" : release.Body.Trim()
		};
	}

	public async Task CheckForUpdates( bool manuallyLaunched )
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[CloudService] CheckForUpdates >>>" );

		try
		{
			CheckingForUpdate = true;

			app.MainWindow.UpdateStatus();

			using var httpClient = new HttpClient();
			httpClient.DefaultRequestHeaders.UserAgent.ParseAdd( "MarvinsALMUARefactored-UpdateChecker" );
			httpClient.DefaultRequestHeaders.Accept.ParseAdd( "application/vnd.github+json" );

			var jsonString = await httpClient.GetStringAsync( LatestReleaseApiUri );

			app.Logger.WriteLine( jsonString );

			var latestRelease = JsonConvert.DeserializeObject<GitHubLatestReleaseResponse>( jsonString );
			var getCurrentVersionResponse = MapLatestRelease( latestRelease );

			if ( getCurrentVersionResponse != null )
			{
				var appVersion = Misc.GetVersion();

				if ( IsNewerVersion( appVersion, getCurrentVersionResponse.currentVersion ) )
				{
					app.Logger.WriteLine( "[CloudService] Newer version is available" );

					var localFilePath = Path.Combine( App.DocumentsFolder, $"MarvinsALMUARefactored-Setup-{getCurrentVersionResponse.currentVersion}.exe" );

					var updateDownloaded = File.Exists( localFilePath );

					if ( updateDownloaded && !manuallyLaunched )
					{
						app.Logger.WriteLine( "[CloudService] File is already downloaded; skipping update process" );
					}
					else
					{
						if ( !updateDownloaded )
						{
							var downloadUpdate = false;

							app.Logger.WriteLine( "[CloudService] Asking user if they want to download the update" );

							var window = new NewVersionAvailableWindow( getCurrentVersionResponse.currentVersion, getCurrentVersionResponse.changeLog )
							{
								Owner = app.MainWindow
							};

							window.ShowDialog();

							downloadUpdate = window.DownloadUpdate;

							if ( downloadUpdate )
							{
								CheckingForUpdate = false;
								DownloadingUpdate = true;

								app.MainWindow.UpdateStatus();

								app.Logger.WriteLine( $"[CloudService] Downloading update from {getCurrentVersionResponse.downloadUrl}" );

								var httpResponseMessage = await httpClient.GetAsync( getCurrentVersionResponse.downloadUrl, HttpCompletionOption.ResponseHeadersRead );

								httpResponseMessage.EnsureSuccessStatusCode();

								var contentLength = httpResponseMessage.Content.Headers.ContentLength;

								using var fileStream = new FileStream( localFilePath, FileMode.Create, FileAccess.Write, FileShare.None );

								using var stream = await httpResponseMessage.Content.ReadAsStreamAsync();

								var buffer = new byte[ 1024 * 1024 ];

								var totalBytesRead = 0;

								while ( true )
								{
									var bytesRead = await stream.ReadAsync( buffer );

									if ( bytesRead == 0 )
									{
										break;
									}

									await fileStream.WriteAsync( buffer.AsMemory( 0, bytesRead ) );

									totalBytesRead += bytesRead;

									if ( contentLength.HasValue && ( contentLength.Value > 0 ) )
									{
										var progressPct = 100f * (float) totalBytesRead / (float) contentLength.Value;
									}
								}

								app.Logger.WriteLine( $"[CloudService] Update downloaded" );

								updateDownloaded = true;
							}
						}

						if ( updateDownloaded )
						{
							app.Logger.WriteLine( "[CloudService] Asking user if they want to install the update" );

							var window = new RunInstallerWindow( localFilePath )
							{
								Owner = app.MainWindow
							};

							window.ShowDialog();

							if ( window.InstallUpdate )
							{
								app.MainWindow.CloseAndLaunchInstaller( localFilePath );
							}
						}
					}
				}
			}

			CheckingForUpdate = false;
			DownloadingUpdate = false;

			app.MainWindow.UpdateStatus();
		}
		catch ( Exception exception )
		{
			app.Logger.WriteLine( $"[CloudService] Failed trying to check for updates: {exception.Message.Trim()}" );

			CheckingForUpdate = false;
			DownloadingUpdate = false;

			app.MainWindow.UpdateStatus();
		}

		app.Logger.WriteLine( "[CloudService] <<< CheckForUpdates" );
	}
}
