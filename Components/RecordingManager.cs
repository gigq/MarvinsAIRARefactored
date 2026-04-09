
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

using CsvHelper;

using MarvinsALMUARefactored.Classes;
using MarvinsALMUARefactored.Windows;

namespace MarvinsALMUARefactored.Components;

public sealed class RecordingManager : IDisposable
{
	private string CurrentRecordingsDirectory => App.GetSimulatorContentDirectory( App.Instance!.Simulator.ContextSimId, "Recordings" );

	public Dictionary<string, Recording> Recordings { get; private set; } = [];

	public Recording? Recording
	{
		get
		{
			var selectedRecording = DataContext.DataContext.Instance.Settings.RacingWheelSelectedRecording;

			if ( string.IsNullOrEmpty( selectedRecording ) )
			{
				return null;
			}

			if ( Recordings.TryGetValue( selectedRecording, out var value ) )
			{
				return value;
			}
			else
			{
				return null;
			}
		}
	}

	public bool IsRecording { get; private set; } = false;

	private FileSystemWatcher? _fileSystemWatcher = null;

	private readonly RecordingData[] _recordingData = new RecordingData[ 3840 ];

	private int _recordingDataIndex = 0;
	private int _trackPosition = 0;

	public void Initialize()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[RecordingManager] Initialize >>>" );

		ReloadForCurrentSimulator();

		for ( var i = 0; i < _recordingData.Length; i++ )
		{
			_recordingData[ i ] = new RecordingData();
		}

		app.Logger.WriteLine( "[RecordingManager] <<< Initialize" );
	}

	public void ReloadForCurrentSimulator()
	{
		var app = App.Instance!;
		var settings = DataContext.DataContext.Instance.Settings;
		var recordingsDirectory = CurrentRecordingsDirectory;

		app.Logger.WriteLine( $"[RecordingManager] ReloadForCurrentSimulator >>> {recordingsDirectory}" );

		_fileSystemWatcher?.Dispose();
		_fileSystemWatcher = null;
		Recordings.Clear();

		if ( !Directory.Exists( recordingsDirectory ) )
		{
			Directory.CreateDirectory( recordingsDirectory );
		}

		_fileSystemWatcher = new FileSystemWatcher( recordingsDirectory, "*.csv" )
		{
			NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
			EnableRaisingEvents = true,
			IncludeSubdirectories = false
		};

		_fileSystemWatcher.Changed += OnRecordingFilesChanged;
		_fileSystemWatcher.Created += OnRecordingFilesChanged;
		_fileSystemWatcher.Renamed += OnRecordingFilesChanged;

		var files = Directory.GetFiles( recordingsDirectory, "*.csv" );

		foreach ( var filePath in files )
		{
			LoadRecording( filePath );
		}

		if ( string.IsNullOrEmpty( settings.RacingWheelSelectedRecording ) || !Recordings.ContainsKey( settings.RacingWheelSelectedRecording ) )
		{
			settings.RacingWheelSelectedRecording = Recordings.Keys.FirstOrDefault() ?? string.Empty;
		}

		if ( app.Ready )
		{
			MainWindow._racingWheelPage.UpdatePreviewRecordingsOptions();
		}

		app.Logger.WriteLine( "[RecordingManager] <<< ReloadForCurrentSimulator" );
	}

	private void OnRecordingFilesChanged( object sender, FileSystemEventArgs e )
	{
		Task.Delay( 2000 ).ContinueWith( _ =>
		{
			var app = App.Instance!;

			app.Logger.WriteLine( "[RecordingManager] OnRecordingChanged >>>" );

			try
			{
				LoadRecording( e.FullPath );

				MainWindow._racingWheelPage.UpdatePreviewRecordingsOptions();

				app.Logger.WriteLine( $"[RecordingManager] Hot-reloaded recording: {e.FullPath}" );
			}
			catch ( Exception exception )
			{
				app.Logger.WriteLine( $"[RecordingManager] Failed to reload {e.FullPath}: {exception.Message}" );
			}

			app.Logger.WriteLine( "[RecordingManager] <<< OnRecordingChanged" );
		} );
	}

	private void LoadRecording( string filePath )
	{
		if ( File.Exists( filePath ) )
		{
			var key = Path.GetFileNameWithoutExtension( filePath )?.ToLower();

			if ( key != null )
			{
				var recording = new Recording( filePath );

				if ( recording.IsValid )
				{
					Recordings[ recording.Path! ] = recording;
				}
			}
		}
	}

	public void Dispose()
	{
		_fileSystemWatcher?.Dispose();

		Recordings.Clear();
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public void AddRecordingData( float inputTorque60Hz, float inputTorque500Hz )
	{
		if ( IsRecording )
		{
			var app = App.Instance!;

			if ( app.Simulator.IsOnTrack == false )
			{
				IsRecording = false;
			}
			else
			{
				ref var recordingData = ref _recordingData[ _recordingDataIndex++ ];

				recordingData.InputTorque60Hz = inputTorque60Hz;
				recordingData.InputTorque500Hz = inputTorque500Hz;

				_recordingDataIndex++;

				if ( _recordingDataIndex == _recordingData.Length / 2 )
				{
					_trackPosition = (int) MathF.Round( app.Simulator.LapDistPct * 100f );
				}

				if ( _recordingDataIndex == _recordingData.Length )
				{
					IsRecording = false;

					SaveRecording();
				}
			}
		}
	}

	public void StartRecording()
	{
		var app = App.Instance!;

		if ( app.Simulator.IsOnTrack )
		{
			app.Logger.WriteLine( "[RecordingManager] StartRecording >>>" );

			IsRecording = true;

			_trackPosition = 0;
			_recordingDataIndex = 0;

			app.Logger.WriteLine( "[RecordingManager] <<< StartRecording" );
		}
	}

	public void SaveRecording()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[RecordingManager] SaveRecording >>>" );

		var fileName = $"{app.Simulator.CarScreenName} @ {app.Simulator.TrackDisplayName} - {app.Simulator.TrackConfigName} ({_trackPosition}%)";

		var filePath = Path.Combine( CurrentRecordingsDirectory, $"{fileName}.csv" );

		using var writer = new StreamWriter( filePath );

		writer.WriteLine( fileName );

		using var csv = new CsvWriter( writer, CultureInfo.InvariantCulture );

		csv.WriteRecords( _recordingData );

		writer.Close();

		LoadRecording( filePath );

		MainWindow._racingWheelPage.UpdatePreviewRecordingsOptions();

		var settings = DataContext.DataContext.Instance.Settings;

		settings.RacingWheelSelectedRecording = filePath;

		app.Logger.WriteLine( "[RecordingManager] <<< SaveRecording" );
	}
}
