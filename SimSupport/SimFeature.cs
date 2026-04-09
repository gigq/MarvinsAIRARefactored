namespace MarvinsALMUARefactored.SimSupport;

[Flags]
public enum SimFeature
{
	None = 0,
	TelemetryBackend = 1 << 0,
	SimulatorDiagnostics = 1 << 1,
	TradingPaints = 1 << 2
}
