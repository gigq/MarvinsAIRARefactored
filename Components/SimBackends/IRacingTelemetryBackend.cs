using IRSDKSharper;

namespace MarvinsAIRARefactored.Components.SimBackends;

internal sealed class IRacingTelemetryBackend(
	IRacingSdk irsdk,
	Action<Exception> onException,
	Action onConnected,
	Action onDisconnected,
	Action onSessionInfo,
	Action onTelemetryData,
	Action<string> onDebugLog ) : ISimTelemetryBackend
{
	private bool _initialized = false;

	public bool IsStarted => irsdk.IsStarted;

	public bool IsConnected => irsdk.IsConnected;

	public void Initialize()
	{
		if ( _initialized )
		{
			return;
		}

		irsdk.OnException += onException;
		irsdk.OnConnected += onConnected;
		irsdk.OnDisconnected += onDisconnected;
		irsdk.OnSessionInfo += onSessionInfo;
		irsdk.OnTelemetryData += onTelemetryData;
		irsdk.OnDebugLog += onDebugLog;

		_initialized = true;
	}

	public void Start()
	{
		if ( !irsdk.IsStarted )
		{
			irsdk.Start();
		}
	}

	public void Stop()
	{
		if ( irsdk.IsStarted )
		{
			irsdk.Stop();
		}
	}

	public void Shutdown()
	{
		Stop();

		while ( irsdk.IsStarted )
		{
			Thread.Sleep( 50 );
		}
	}

	public void Tick()
	{
	}
}
