namespace MarvinsALMUARefactored.Components.SimBackends;

internal sealed class UnsupportedSimTelemetryBackend : ISimTelemetryBackend
{
	public bool IsStarted => false;

	public bool IsConnected => false;

	public void Initialize()
	{
	}

	public void Start()
	{
	}

	public void Tick()
	{
	}

	public void Stop()
	{
	}

	public void Shutdown()
	{
	}
}
