using System.IO;

using MarvinsAIRARefactored.Windows;

namespace MarvinsAIRARefactored.SimSupport;

public static class SimRegistry
{
	private static readonly IReadOnlyDictionary<SimId, SimDefinition> _definitions = new Dictionary<SimId, SimDefinition>
	{
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
		var definition = GetDefinition( simId );

		return definition.SupportLevel switch
		{
			SimSupportLevel.Supported => definition.DisplayName,
			_ => $"{definition.DisplayName} (scaffold)"
		};
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
