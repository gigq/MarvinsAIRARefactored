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

		app.Logger.WriteLine( $"[Simulator] ApplySelectedSimulator >>> {CurrentSimDefinition.DisplayName}" );

		if ( SupportsSelectedSimulatorBackend )
		{
			if ( !_irsdk.IsStarted )
			{
				_irsdk.Start();
			}
		}
		else if ( _irsdk.IsStarted )
		{
			_irsdk.Stop();
		}

		app.Logger.WriteLine( "[Simulator] <<< ApplySelectedSimulator" );
	}
}
