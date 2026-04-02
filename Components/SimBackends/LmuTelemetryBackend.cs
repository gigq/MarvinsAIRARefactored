using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using rF2SharedMemoryNet;

using IRSDKSharper;

using RF2Scoring = rF2SharedMemoryNet.RF2Data.Structs.Scoring;
using RF2Telemetry = rF2SharedMemoryNet.RF2Data.Structs.Telemetry;
using RF2VehicleTelemetry = rF2SharedMemoryNet.RF2Data.Structs.VehicleTelemetry;

namespace MarvinsAIRARefactored.Components.SimBackends;

internal sealed class LmuTelemetryBackend( Simulator simulator ) : ISimTelemetryBackend
{
	private static readonly TimeSpan DisconnectGracePeriod = TimeSpan.FromSeconds( 1 );

	private RF2MemoryReader? _reader = null;
	private bool _connected = false;
	private DateTime _lastSuccessfulReadUtc = DateTime.MinValue;

	public bool IsStarted => _reader != null;

	public bool IsConnected => _connected;

	public void Initialize()
	{
	}

	public void Start()
	{
		if ( _reader != null )
		{
			return;
		}

		_reader = new RF2MemoryReader( NullLogger.Instance, false );
		_connected = false;
		_lastSuccessfulReadUtc = DateTime.MinValue;
	}

	public void Tick()
	{
		if ( _reader == null )
		{
			return;
		}

		var telemetry = _reader.GetTelemetry();
		var scoring = _reader.GetScoring();

		if ( !TryBuildSnapshot( telemetry, scoring, out var snapshot ) )
		{
			if ( _connected && ( DateTime.UtcNow - _lastSuccessfulReadUtc >= DisconnectGracePeriod ) )
			{
				_connected = false;
				simulator.HandleBackendDisconnected();
			}

			return;
		}

		_lastSuccessfulReadUtc = DateTime.UtcNow;

		if ( !_connected )
		{
			_connected = true;
			simulator.HandleBackendConnected();
		}

		simulator.ApplyLmuTelemetry( snapshot );
	}

	public void Stop()
	{
		if ( _reader == null )
		{
			return;
		}

		_reader.Dispose();
		_reader = null;

		if ( _connected )
		{
			_connected = false;
			simulator.HandleBackendDisconnected();
		}
	}

	public void Shutdown()
	{
		Stop();
	}

	private static bool TryBuildSnapshot( RF2Telemetry? telemetry, RF2Scoring? scoring, out LmuTelemetrySnapshot snapshot )
	{
		snapshot = default;

		if ( ( telemetry == null ) || ( scoring == null ) )
		{
			return false;
		}

		var telemetryValue = telemetry.Value;
		var scoringValue = scoring.Value;
		var telemetryVehicles = telemetryValue.Vehicles;
		var scoringVehicles = scoringValue.Vehicles;

		if ( ( telemetryVehicles == null ) || ( scoringVehicles == null ) )
		{
			return false;
		}

		var telemetryCount = Math.Min( telemetryValue.NumVehicles, telemetryVehicles.Length );
		var scoringCount = Math.Min( scoringValue.ScoringInfo.NumVehicles, scoringVehicles.Length );

		if ( ( telemetryCount <= 0 ) || ( scoringCount <= 0 ) )
		{
			return false;
		}

		var playerScoringIndex = -1;

		for ( var i = 0; i < scoringCount; i++ )
		{
			if ( scoringVehicles[ i ].IsPlayer != 0 )
			{
				playerScoringIndex = i;
				break;
			}
		}

		if ( playerScoringIndex < 0 )
		{
			return false;
		}

		var playerScoring = scoringVehicles[ playerScoringIndex ];

		RF2VehicleTelemetry? playerTelemetry = null;

		for ( var i = 0; i < telemetryCount; i++ )
		{
			if ( telemetryVehicles[ i ].ID == playerScoring.ID )
			{
				playerTelemetry = telemetryVehicles[ i ];
				break;
			}
		}

		if ( playerTelemetry == null )
		{
			if ( playerScoringIndex < telemetryCount )
			{
				playerTelemetry = telemetryVehicles[ playerScoringIndex ];
			}
			else
			{
				return false;
			}
		}

		var playerTelemetryValue = playerTelemetry.Value;
		var trackLength = (float) Math.Max( scoringValue.ScoringInfo.LapDist, 0.0 );
		var lapDist = (float) Math.Max( playerScoring.LapDist, 0.0 );
		var lapDistPct = trackLength > 0f ? Math.Clamp( lapDist / trackLength, 0f, 1f ) : 0f;
		var velocityX = (float) -playerTelemetryValue.LocalVelocity.Z;
		var velocityY = (float) -playerTelemetryValue.LocalVelocity.X;
		var forwardSpeed = MathF.Sqrt( velocityX * velocityX + velocityY * velocityY );
		var steeringRangeRadians = playerTelemetryValue.PhysicalSteeringWheelRange * MathF.PI / 180f;
		var steeringAngle = (float) playerTelemetryValue.UnfilteredSteering * steeringRangeRadians * 0.5f;
		var steeringTorque = (float) -playerTelemetryValue.SteeringShaftTorque;
		var shiftLightsFirstRpm = (float) ( playerTelemetryValue.EngineMaxRPM * 0.85 );
		var shiftLightsShiftRpm = (float) ( playerTelemetryValue.EngineMaxRPM * 0.95 );
		var isOnTrack = ( scoringValue.ScoringInfo.InRealtime != 0 ) && ( playerScoring.Control == 0 ) && ( playerScoring.InPits == 0 );

		snapshot = new LmuTelemetrySnapshot(
			Math.Max( (float) playerTelemetryValue.DeltaTime, 1f / 60f ),
			DecodeString( playerScoring.VehicleName, DecodeString( playerTelemetryValue.VehicleName ) ),
			DecodeString( scoringValue.ScoringInfo.TrackName, DecodeString( playerTelemetryValue.TrackName ) ),
			DecodeString( scoringValue.ScoringInfo.PlayerName, DecodeString( playerScoring.DriverName ) ),
			isOnTrack,
			(int) playerTelemetryValue.LapNumber,
			lapDist,
			lapDistPct,
			trackLength,
			(int) playerTelemetryValue.Gear,
			playerTelemetryValue.MaxGears,
			(float) playerTelemetryValue.EngineRPM,
			shiftLightsFirstRpm,
			shiftLightsShiftRpm,
			(float) playerTelemetryValue.UnfilteredThrottle,
			(float) playerTelemetryValue.UnfilteredBrake,
			(float) playerTelemetryValue.UnfilteredClutch,
			forwardSpeed,
			velocityX,
			velocityY,
			(float) -playerTelemetryValue.LocalAcceleration.Z,
			(float) -playerTelemetryValue.LocalAcceleration.X,
			(float) playerTelemetryValue.LocalAcceleration.Y,
			(float) playerTelemetryValue.LocalRotationalSpeed.Y,
			steeringAngle,
			steeringRangeRadians,
			steeringTorque,
			(float) scoringValue.ScoringInfo.CurrentET,
			scoringValue.ScoringInfo.Session,
			scoringValue.ScoringInfo.Raining > 0.05 || scoringValue.ScoringInfo.MaxPathWetness > 0.05,
			isOnTrack ? IRacingSdkEnum.TrkLoc.OnTrack : IRacingSdkEnum.TrkLoc.NotInWorld,
			isOnTrack ? IRacingSdkEnum.TrkSurf.Asphalt1Material : IRacingSdkEnum.TrkSurf.SurfaceNotInWorld );

		return true;
	}

	private static string DecodeString( byte[]? bytes, string fallback = "" )
	{
		if ( ( bytes == null ) || ( bytes.Length == 0 ) )
		{
			return fallback;
		}

		var terminatorIndex = Array.IndexOf( bytes, (byte) 0 );
		var length = terminatorIndex >= 0 ? terminatorIndex : bytes.Length;

		if ( length <= 0 )
		{
			return fallback;
		}

		var text = Encoding.UTF8.GetString( bytes, 0, length ).Trim();

		return text == string.Empty ? fallback : text;
	}
}

internal readonly record struct LmuTelemetrySnapshot(
	float DeltaSeconds,
	string CarScreenName,
	string TrackDisplayName,
	string UserName,
	bool IsOnTrack,
	int Lap,
	float LapDist,
	float LapDistPct,
	float TrackLength,
	int Gear,
	int NumForwardGears,
	float RPM,
	float ShiftLightsFirstRPM,
	float ShiftLightsShiftRPM,
	float Throttle,
	float Brake,
	float Clutch,
	float Speed,
	float VelocityX,
	float VelocityY,
	float LongAccel,
	float LatAccel,
	float VertAccel,
	float YawRate,
	float SteeringWheelAngle,
	float SteeringWheelAngleMax,
	float SteeringWheelTorque,
	double SessionTime,
	int SessionNum,
	bool WeatherDeclaredWet,
	IRacingSdkEnum.TrkLoc PlayerTrackSurface,
	IRacingSdkEnum.TrkSurf PlayerTrackSurfaceMaterial );
