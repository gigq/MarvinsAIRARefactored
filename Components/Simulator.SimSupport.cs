using MarvinsAIRARefactored.SimSupport;

namespace MarvinsAIRARefactored.Components;

public partial class Simulator
{
	public SimId SelectedSimId => DataContext.DataContext.Instance.Settings.AppSelectedSimulator;

	public SimDefinition CurrentSimDefinition => SimRegistry.GetDefinition( SelectedSimId );

	public bool SupportsSelectedSimulatorBackend => CurrentSimDefinition.Supports( SimFeature.TelemetryBackend );

	public void ApplySelectedSimulator()
	{
		var app = App.Instance!;
		var selectedBackend = GetSelectedTelemetryBackend();

		app.Logger.WriteLine( $"[Simulator] ApplySelectedSimulator >>> {CurrentSimDefinition.DisplayName}" );

		foreach ( var backend in _telemetryBackends.Values )
		{
			if ( ReferenceEquals( backend, selectedBackend ) && SupportsSelectedSimulatorBackend )
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
}
