
using SharpDX.XAudio2;
using System.IO;

using MarvinsALMUARefactored.Classes;

namespace MarvinsALMUARefactored.Components;

public sealed class AudioManager : IDisposable
{
	private readonly Lock _lock = new();

	private readonly string _soundsDirectory = Path.Combine( App.DocumentsFolder, "Sounds" );

	private readonly Dictionary<string, CachedSound> _soundCache = [];
	private readonly Dictionary<string, CachedSoundPlayer> _soundPlayerCache = [];

	private FileSystemWatcher? _fileSystemWatcher = null;

	private XAudio2? _xaudio2;
	private MasteringVoice? _masteringVoice;

	private readonly Dictionary<string, DateTime> _debounceMap = [];

	public AudioManager()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[AudioManager] Constructor >>>" );

		app.Logger.WriteLine( "[AudioManager] <<< Constructor" );
	}

	public void OpenDevice()
	{
		using ( _lock.EnterScope() )
		{
			if ( _xaudio2 != null )
			{
				return; // Already open
			}

			var app = App.Instance!;

			app.Logger.WriteLine( "[AudioManager] OpenDevice >>>" );

			try
			{
				_xaudio2 = new XAudio2();
				_masteringVoice = new MasteringVoice( _xaudio2 );

				// Reload all cached sounds
				var soundPaths = _soundCache.Keys.ToList();

				foreach ( var key in soundPaths )
				{
					var sound = _soundCache[ key ];
					var player = new CachedSoundPlayer( sound, _xaudio2 );

					_soundPlayerCache[ key ] = player;
				}

				app.Logger.WriteLine( "[AudioManager] Audio device opened successfully" );
			}
			catch ( Exception exception )
			{
				_xaudio2 = null;
				_masteringVoice = null;

				app.Logger.WriteLine( $"[AudioManager] Failed to open audio device: {exception.Message}" );
			}

			app.Logger.WriteLine( "[AudioManager] <<< OpenDevice" );
		}
	}

	public void CloseDevice()
	{
		using ( _lock.EnterScope() )
		{
			if ( _xaudio2 == null )
			{
				return; // Already closed
			}

			var app = App.Instance!;

			app.Logger.WriteLine( "[AudioManager] CloseDevice >>>" );

			// Dispose all sound players
			foreach ( var player in _soundPlayerCache.Values )
			{
				player.Dispose();
			}
			_soundPlayerCache.Clear();

			// Dispose XAudio2 resources
			_masteringVoice?.Dispose();
			_xaudio2?.Dispose();

			_masteringVoice = null;
			_xaudio2 = null;

			app.Logger.WriteLine( "[AudioManager] Audio device closed" );

			app.Logger.WriteLine( "[AudioManager] <<< CloseDevice" );
		}
	}

	public void Initialize()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[AudioManager] Initialize >>>" );

		if ( !Directory.Exists( _soundsDirectory ) )
		{
			Directory.CreateDirectory( _soundsDirectory );
		}

		_fileSystemWatcher = new FileSystemWatcher( _soundsDirectory, "*.wav" )
		{
			NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
			EnableRaisingEvents = true,
			IncludeSubdirectories = true
		};

		_fileSystemWatcher.Changed += OnSoundFileChanged;
		_fileSystemWatcher.Created += OnSoundFileChanged;
		_fileSystemWatcher.Renamed += OnSoundFileChanged;

		// Open device if master sound is enabled
		if ( DataContext.DataContext.Instance.Settings.SoundsMasterEnabled )
		{
			OpenDevice();
		}

		app.Logger.WriteLine( "[AudioManager] <<< Initialize" );
	}

	public void LoadSounds( string directory, string[] soundKeys )
	{
		foreach ( var soundKey in soundKeys )
		{
			LoadSound( directory, soundKey );
		}
	}

	public void LoadSound( string directory, string soundKey )
	{
		var path = Path.Combine( _soundsDirectory, directory, $"{soundKey}.wav" );

		LoadSound( path );

		path = Path.Combine( _soundsDirectory, directory, $"{soundKey}_custom.wav" );

		LoadSound( path );
	}

	private void OnSoundFileChanged( object sender, FileSystemEventArgs e )
	{
		using ( _lock.EnterScope() )
		{
			var now = DateTime.Now;

			var expiredKeys = _debounceMap.Where( kvp => ( now - kvp.Value ).TotalSeconds > 10 ).Select( kvp => kvp.Key ).ToList();

			foreach ( var key in expiredKeys )
			{
				_debounceMap.Remove( key );
			}

			if ( _debounceMap.TryGetValue( e.FullPath, out var lastTime ) )
			{
				if ( ( now - lastTime ).TotalMilliseconds < 500 )
				{
					return;
				}

				_debounceMap[ e.FullPath ] = now;
			}
			else
			{
				_debounceMap.Add( e.FullPath, now );
			}
		}

		Task.Delay( 1000 ).ContinueWith( _ =>
		{
			var app = App.Instance!;

			app.Logger.WriteLine( "[AudioManager] OnSoundFileChanged >>>" );

			try
			{
				LoadSound( e.FullPath );

				app.Logger.WriteLine( $"[AudioManager] Hot-reloaded sound: {e.FullPath}" );
			}
			catch ( Exception exception )
			{
				app.Logger.WriteLine( $"[AudioManager] Failed to reload {e.FullPath}: {exception.Message}" );
			}

			app.Logger.WriteLine( "[AudioManager] <<< OnSoundFileChanged" );
		} );
	}

	private void LoadSound( string path )
	{
		if ( File.Exists( path ) )
		{
			var key = Path.GetFileNameWithoutExtension( path )?.ToLower();

			if ( key != null )
			{
				var sound = new CachedSound( path );

				using ( _lock.EnterScope() )
				{
					_soundCache[ key ] = sound;

					// Only create player if device is open
					if ( _xaudio2 != null )
					{
						var player = new CachedSoundPlayer( sound, _xaudio2 );

						if ( _soundPlayerCache.TryGetValue( key, out var existing ) )
						{
							existing.Stop();
							existing.Dispose();
						}

						_soundPlayerCache[ key ] = player;
					}
				}
			}
		}
	}

	public void Play( string key, float volume, float frequencyRatio = 1f, bool loop = false )
	{
		using ( _lock.EnterScope() )
		{
			if ( !_soundPlayerCache.TryGetValue( $"{key}_custom", out var player ) )
			{
				if ( !_soundPlayerCache.TryGetValue( key, out player ) )
				{
					player = null;

					App.Instance!.Logger.WriteLine( $"[AudioManager] Trying to play sound {key} but it was not loaded" );
				}
			}

			player?.Play( volume, frequencyRatio, loop );
		}
	}

	public void Update( string key, float volume, float frequencyRatio = 1f )
	{
		using ( _lock.EnterScope() )
		{
			if ( !_soundPlayerCache.TryGetValue( $"{key}_custom", out var player ) )
			{
				if ( !_soundPlayerCache.TryGetValue( key, out player ) )
				{
					player = null;
				}
			}

			player?.Update( volume, frequencyRatio );
		}
	}

	public void Stop( string key )
	{
		using ( _lock.EnterScope() )
		{
			if ( _soundPlayerCache.TryGetValue( $"{key}_custom", out var player ) )
			{
				player?.Stop();
			}

			if ( _soundPlayerCache.TryGetValue( key, out player ) )
			{
				player?.Stop();
			}
		}
	}

	public bool IsPlaying( string key )
	{
		using ( _lock.EnterScope() )
		{
			if ( !_soundPlayerCache.TryGetValue( $"{key}_custom", out var player ) )
			{
				if ( !_soundPlayerCache.TryGetValue( key, out player ) )
				{
					player = null;
				}
			}

			return player?.IsPlaying() ?? false;
		}
	}

	public void Dispose()
	{
		_fileSystemWatcher?.Dispose();

		CloseDevice();

		using ( _lock.EnterScope() )
		{
			_soundCache.Clear();
		}
	}
}
