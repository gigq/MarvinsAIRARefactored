namespace MarvinsAIRARefactored.Components.SimBackends;

internal interface ISimTelemetryBackend
{
	bool IsStarted { get; }
	bool IsConnected { get; }

	void Initialize();
	void Start();
	void Tick();
	void Stop();
	void Shutdown();
}
