using System.IO;

using MarvinsALMUARefactored.Windows;

namespace MarvinsALMUARefactored.SimSupport;

public static class SimRegistry
{
	private static readonly IReadOnlyDictionary<SimId, SimDefinition> _definitions = new Dictionary<SimId, SimDefinition>
	{
		[ SimId.Auto ] = new(
			SimId.Auto,
			"Auto detect",
			"Auto",
			SimSupportLevel.Supported,
			0,
			true,
			null,
			"Automatically detects a supported running simulator and switches MALMUA to it. If no supported simulator is running, MALMUA stays idle." ),
		[ SimId.IRacing ] = new(
			SimId.IRacing,
			"iRacing",
			"iRacing",
			SimSupportLevel.Supported,
			SimFeature.TelemetryBackend | SimFeature.SimulatorDiagnostics | SimFeature.TradingPaints,
			true,
			"iRacing.com Simulator",
			"Uses the existing IRSDK-backed implementation and preserves the legacy MAIRA folder layout for upstream compatibility." ),
		[ SimId.LeMansUltimate ] = new(
			SimId.LeMansUltimate,
			"Le Mans Ultimate",
			"LeMansUltimate",
			SimSupportLevel.Scaffolded,
			SimFeature.TelemetryBackend,
			false,
			"Le Mans Ultimate",
			"Experimental LMU telemetry is available through the shared-memory plugin path, while simulator diagnostics, Trading Paints, and other iRacing-specific features remain unsupported." )
	};

	public static IReadOnlyList<SimDefinition> Definitions { get; } = _definitions.Values.ToList().AsReadOnly();

	public static SimDefinition GetDefinition( SimId simId )
	{
		return _definitions.TryGetValue( simId, out var definition ) ? definition : _definitions[ SimId.IRacing ];
	}

	public static string GetOptionLabel( SimId simId )
	{
		return GetDefinition( simId ).DisplayName;
	}

	public static string GetNotRunningStatusText( SimId simId )
	{
		if ( simId == SimId.Auto )
		{
			return "No supported simulator is running";
		}

		var definition = GetDefinition( simId );

		return $"The {definition.DisplayName} simulator is not running";
	}

	public static string GetConnectedStatusText( SimId simId )
	{
		var definition = GetDefinition( simId );

		return $"Connected to {definition.DisplayName}";
	}

	public static string GetReplayModeStatusText( SimId simId )
	{
		var definition = GetDefinition( simId );

		return $"The {definition.DisplayName} simulator is not in an active driving session";
	}

	public static string GetSimulatorForceFeedbackStatusText( SimId simId )
	{
		var definition = GetDefinition( simId );

		return $"Force feedback is enabled in the {definition.DisplayName} simulator";
	}

	public static string GetForceFeedbackWaitingStatusText()
	{
		return "Force feedback is waiting to resume";
	}

	public static string GetForceFeedbackSuspendedStatusText()
	{
		return "Force feedback is currently suspended";
	}

	public static string GetDefaultTradingPaintsFolder( SimId simId )
	{
		return simId switch
		{
			SimId.IRacing => Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), "iRacing", "paint" ),
			_ => App.GetSimulatorContentDirectory( simId, "Trading Paints" )
		};
	}

	public static bool SupportsPage( SimId simId, MainWindow.AppPage appPage )
	{
		var definition = GetDefinition( simId );

		return appPage switch
		{
			MainWindow.AppPage.TradingPaints => definition.Supports( SimFeature.TradingPaints ),
			MainWindow.AppPage.Simulator => definition.Supports( SimFeature.SimulatorDiagnostics ),
			_ => true
		};
	}
}
