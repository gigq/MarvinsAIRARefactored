using System.Diagnostics;

using MarvinsALMUARefactored.SimSupport;

namespace MarvinsALMUARefactored.Components;

public partial class Simulator
{
	public SimId ConfiguredSimId => App.RuntimeSimulatorOverride ?? DataContext.DataContext.Instance.Settings.AppSelectedSimulator;

	public bool IsAutoSelecting => ConfiguredSimId == SimId.Auto;

	public SimId SelectedSimId => IsAutoSelecting ? ( _autoDetectedSimId ?? SimId.Auto ) : ConfiguredSimId;

	public SimId ContextSimId => SelectedSimId != SimId.Auto ? SelectedSimId : _contextSimId;

	public SimDefinition CurrentSimDefinition => SimRegistry.GetDefinition( SelectedSimId );

	public bool SupportsSelectedSimulatorBackend => CurrentSimDefinition.Supports( SimFeature.TelemetryBackend );

	public void ApplySelectedSimulator()
	{
		if ( IsAutoSelecting )
		{
			UpdateAutoDetectedSimulator( true );
		}
		else
		{
			_autoDetectedSimId = null;

			if ( ConfiguredSimId != SimId.Auto )
			{
				_contextSimId = ConfiguredSimId;
			}
		}

		ApplySelectedSimulatorCore();
	}

	private void ApplySelectedSimulatorCore()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( $"[Simulator] ApplySelectedSimulator >>> {CurrentSimDefinition.DisplayName}" );

		foreach ( var backend in _telemetryBackends.Values )
		{
			if ( ( SelectedSimId != SimId.Auto ) && ReferenceEquals( backend, GetSelectedTelemetryBackend() ) && SupportsSelectedSimulatorBackend )
			{
				backend.Start();
			}
			else
			{
				backend.Stop();
			}
		}

		app.Logger.WriteLine( "[Simulator] <<< ApplySelectedSimulator" );
	}

	private void UpdateAutoDetectedSimulator( bool force )
	{
		if ( !IsAutoSelecting )
		{
			return;
		}

		var nowUtc = DateTime.UtcNow;

		if ( !force && ( nowUtc < _nextAutoDetectionUtc ) )
		{
			return;
		}

		_nextAutoDetectionUtc = nowUtc.AddMilliseconds( 500 );

		var detectedSimId = DetectRunningSimulator();

		if ( detectedSimId == _autoDetectedSimId )
		{
			return;
		}

		var previousSimId = _autoDetectedSimId;
		_autoDetectedSimId = detectedSimId;

		if ( detectedSimId != null )
		{
			_contextSimId = detectedSimId.Value;
		}

		var app = App.Instance;

		app?.Logger.WriteLine( $"[Simulator] Auto-detected simulator changed from {previousSimId?.ToString() ?? "None"} to {detectedSimId?.ToString() ?? "None"}" );

		if ( app?.Ready == true )
		{
			DataContext.DataContext.Instance.Settings.UpdateSettings( false );
			app.SteeringEffects.RefreshCalibrationDirectory();
			app.RecordingManager.ReloadForCurrentSimulator();
			ApplySelectedSimulatorCore();
			app.MainWindow.RefreshWindow();
		}
	}

	private SimId? DetectRunningSimulator()
	{
		if ( _autoDetectedSimId != null && IsSimulatorProcessRunning( _autoDetectedSimId.Value ) )
		{
			return _autoDetectedSimId;
		}

		if ( IsSimulatorProcessRunning( SimId.IRacing ) )
		{
			return SimId.IRacing;
		}

		if ( IsSimulatorProcessRunning( SimId.LeMansUltimate ) )
		{
			return SimId.LeMansUltimate;
		}

		return null;
	}

	private static bool IsSimulatorProcessRunning( SimId simId )
	{
		string[] processNames = simId switch
		{
			SimId.IRacing => [ "iRacingSim64DX11" ],
			SimId.LeMansUltimate => [ "Le Mans Ultimate" ],
			_ => Array.Empty<string>()
		};

		foreach ( var processName in processNames )
		{
			try
			{
				if ( Process.GetProcessesByName( processName ).Length > 0 )
				{
					return true;
				}
			}
			catch
			{
			}
		}

		return false;
	}
}
