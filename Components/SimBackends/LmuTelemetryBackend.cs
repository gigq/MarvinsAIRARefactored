using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using IRSDKSharper;

namespace MarvinsAIRARefactored.Components.SimBackends;

internal sealed class LmuTelemetryBackend( Simulator simulator ) : ISimTelemetryBackend
{
	private const string SharedMemoryMapName = "LMU_Data";
	private static readonly TimeSpan DisconnectGracePeriod = TimeSpan.FromSeconds( 1 );
	private const int GenericFfbTorqueOffset = 68;
	private const float SteeringTorqueScale = 1.5f;
	private const float ParkingTorqueStartSpeed = 0.5f;
	private const float ParkingTorqueFullSpeed = 3f;

	// Offsets below come from the local LMU shared-memory headers (pack=4) via tools/lmu_layout_dump.cpp.
	private const int SharedMemoryTelemetryOffset = 128464;
	private const int TelemetryPlayerVehicleIndexOffset = SharedMemoryTelemetryOffset + 1;
	private const int TelemetryPlayerHasVehicleOffset = SharedMemoryTelemetryOffset + 2;
	private const int TelemetryVehicleArrayOffset = SharedMemoryTelemetryOffset + 4;
	private const int TelemetryVehicleSize = 1888;

	private const int VehicleDeltaTimeOffset = 4;
	private const int VehicleElapsedTimeOffset = 12;
	private const int VehicleLapNumberOffset = 20;
	private const int VehicleNameOffset = 32;
	private const int VehicleTrackNameOffset = 96;
	private const int VehicleLocalVelocityOffset = 184;
	private const int VehicleLocalAccelerationOffset = 208;
	private const int VehicleLocalRotationOffset = 304;
	private const int VehicleGearOffset = 352;
	private const int VehicleEngineRpmOffset = 356;
	private const int VehicleThrottleOffset = 388;
	private const int VehicleBrakeOffset = 396;
	private const int VehicleSteeringOffset = 404;
	private const int VehicleClutchOffset = 412;
	private const int VehicleSteeringShaftTorqueOffset = 452;
	private const int VehicleEngineMaxRpmOffset = 532;
	private const int VehicleMaxGearsOffset = 605;
	private const int VehiclePhysicalSteeringWheelRangeOffset = 692;
	private const int VehicleAbsActiveOffset = 746;
	private const int VehicleTcActiveOffset = 747;

	private bool _started = false;
	private bool _openLogged = false;
	private bool _connected = false;
	private DateTime _lastSuccessfulReadUtc = DateTime.MinValue;
	private DateTime _nextDiagnosticsLogUtc = DateTime.MinValue;
	private DateTime _nextTickDiagnosticsLogUtc = DateTime.MinValue;
	private double _lastSessionTimeSeconds = double.NaN;
	private DateTime _lastSessionTimeAdvanceUtc = DateTime.MinValue;

	public bool IsStarted => _started;

	public bool IsConnected => _connected;

	public void Initialize()
	{
	}

	public void Start()
	{
		_started = true;
	}

	public void Tick()
	{
		var nowUtc = DateTime.UtcNow;

		if ( nowUtc >= _nextTickDiagnosticsLogUtc )
		{
			_nextTickDiagnosticsLogUtc = nowUtc.AddSeconds( 1 );
			App.Instance!.Logger.WriteLine( $"[LMU-TICK] started={_started} connected={_connected}" );
		}

		try
		{
			if ( !TryBuildSnapshot( out var snapshot ) )
			{
				HandleReadFailure();
				return;
			}

			_lastSuccessfulReadUtc = DateTime.UtcNow;

			if ( !_connected )
			{
				_connected = true;
				simulator.HandleBackendConnected();
			}

			if ( Math.Abs( snapshot.SessionTime - _lastSessionTimeSeconds ) < 0.0001 )
			{
				if ( ( _lastSessionTimeAdvanceUtc != DateTime.MinValue ) && ( nowUtc - _lastSessionTimeAdvanceUtc >= TimeSpan.FromMilliseconds( 500 ) ) )
				{
					App.Instance!.Logger.WriteLine( "[LMU] Session time stalled; polling with a fresh shared memory view" );
					_lastSessionTimeAdvanceUtc = nowUtc;

					return;
				}
			}
			else
			{
				_lastSessionTimeSeconds = snapshot.SessionTime;
				_lastSessionTimeAdvanceUtc = nowUtc;
			}

			simulator.ApplyLmuTelemetry( snapshot );
		}
		catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException )
		{
			App.Instance!.Logger.WriteLine( $"[LMU] Native shared-memory read failed: {exception.Message.Trim()}" );
			HandleReadFailure();
		}
	}

	public void Stop()
	{
		_started = false;

		if ( _connected )
		{
			_connected = false;
			simulator.HandleBackendDisconnected();
		}

		_nextDiagnosticsLogUtc = DateTime.MinValue;
		_nextTickDiagnosticsLogUtc = DateTime.MinValue;
		_lastSessionTimeSeconds = double.NaN;
		_lastSessionTimeAdvanceUtc = DateTime.MinValue;
		_openLogged = false;
	}

	public void Shutdown()
	{
		Stop();
	}

	private void HandleReadFailure()
	{
		if ( _connected && ( DateTime.UtcNow - _lastSuccessfulReadUtc >= DisconnectGracePeriod ) )
		{
			_connected = false;
			simulator.HandleBackendDisconnected();
		}
	}

	private bool TryBuildSnapshot( out LmuTelemetrySnapshot snapshot )
	{
		snapshot = default;

		if ( !_started )
		{
			return false;
		}

		using var sharedMemory = MemoryMappedFile.OpenExisting( SharedMemoryMapName, MemoryMappedFileRights.Read );
		using var view = sharedMemory.CreateViewAccessor( 0, 0, MemoryMappedFileAccess.Read );

		if ( !_openLogged )
		{
			App.Instance!.Logger.WriteLine( "[LMU] Opened native shared memory map" );
			_openLogged = true;
		}

		if ( !view.ReadBoolean( TelemetryPlayerHasVehicleOffset ) )
		{
			return false;
		}

		var playerVehicleIndex = view.ReadByte( TelemetryPlayerVehicleIndexOffset );

		if ( playerVehicleIndex >= 104 )
		{
			return false;
		}

		var vehicleOffset = TelemetryVehicleArrayOffset + ( playerVehicleIndex * TelemetryVehicleSize );

		var deltaTime = (float) view.ReadDouble( vehicleOffset + VehicleDeltaTimeOffset );
		if ( deltaTime <= 0f )
		{
			deltaTime = 1f / 400f;
		}

		var sessionTime = view.ReadDouble( vehicleOffset + VehicleElapsedTimeOffset );
		var lap = view.ReadInt32( vehicleOffset + VehicleLapNumberOffset );
		var vehicleName = ReadFixedString( view, vehicleOffset + VehicleNameOffset, 64 );
		var trackName = ReadFixedString( view, vehicleOffset + VehicleTrackNameOffset, 64 );

		var localVelocityX = view.ReadDouble( vehicleOffset + VehicleLocalVelocityOffset );
		var localVelocityY = view.ReadDouble( vehicleOffset + VehicleLocalVelocityOffset + 8 );
		var localVelocityZ = view.ReadDouble( vehicleOffset + VehicleLocalVelocityOffset + 16 );

		var localAccelerationX = view.ReadDouble( vehicleOffset + VehicleLocalAccelerationOffset );
		var localAccelerationY = view.ReadDouble( vehicleOffset + VehicleLocalAccelerationOffset + 8 );
		var localAccelerationZ = view.ReadDouble( vehicleOffset + VehicleLocalAccelerationOffset + 16 );

		var localRotationY = view.ReadDouble( vehicleOffset + VehicleLocalRotationOffset + 8 );

		var gear = view.ReadInt32( vehicleOffset + VehicleGearOffset );
		var rpm = (float) view.ReadDouble( vehicleOffset + VehicleEngineRpmOffset );
		var throttle = (float) view.ReadDouble( vehicleOffset + VehicleThrottleOffset );
		var brake = (float) view.ReadDouble( vehicleOffset + VehicleBrakeOffset );
		var steeringInput = (float) view.ReadDouble( vehicleOffset + VehicleSteeringOffset );
		var clutch = (float) view.ReadDouble( vehicleOffset + VehicleClutchOffset );
		// LMU reports shaft torque in its own coordinate system. MAIRA's wheel pipeline follows the
		// same sign convention as iRacing, so we invert here before handing it to the shared algorithm stack.
		var steeringTorque = (float) -view.ReadDouble( vehicleOffset + VehicleSteeringShaftTorqueOffset ) * SteeringTorqueScale;
		var genericFfbTorque = view.ReadSingle( GenericFfbTorqueOffset );
		var engineMaxRpm = (float) view.ReadDouble( vehicleOffset + VehicleEngineMaxRpmOffset );
		var maxGears = view.ReadByte( vehicleOffset + VehicleMaxGearsOffset );
		var physicalSteeringWheelRange = view.ReadSingle( vehicleOffset + VehiclePhysicalSteeringWheelRangeOffset );
		var absActive = view.ReadBoolean( vehicleOffset + VehicleAbsActiveOffset );
		var tcActive = view.ReadBoolean( vehicleOffset + VehicleTcActiveOffset );

		var velocityX = (float) -localVelocityZ;
		var velocityY = (float) -localVelocityX;
		var forwardSpeed = MathF.Sqrt( velocityX * velocityX + velocityY * velocityY );
		var steeringRangeRadians = physicalSteeringWheelRange > 0f ? physicalSteeringWheelRange * MathF.PI / 180f : MathF.PI * 2.5f;
		var steeringAngle = steeringInput * steeringRangeRadians * 0.5f;

		// Standing-still shaft torque can pin the wheel against the stop in LMU, especially with the
		// steering already loaded against static friction. Fade the source in only once the car is rolling.
		var parkingFade = Math.Clamp( ( forwardSpeed - ParkingTorqueStartSpeed ) / ( ParkingTorqueFullSpeed - ParkingTorqueStartSpeed ), 0f, 1f );
		steeringTorque *= parkingFade;

		LogSnapshotDiagnostics( sessionTime, playerVehicleIndex, steeringInput, genericFfbTorque, steeringTorque, forwardSpeed, throttle, brake, gear );

		snapshot = new LmuTelemetrySnapshot(
			deltaTime,
			vehicleName,
			trackName,
			string.Empty,
			true,
			true,
			lap,
			0f,
			0f,
			0f,
			gear,
			maxGears,
			rpm,
			engineMaxRpm * 0.85f,
			engineMaxRpm * 0.95f,
			throttle,
			brake,
			clutch,
			forwardSpeed,
			velocityX,
			velocityY,
			(float) -localAccelerationZ,
			(float) -localAccelerationX,
			(float) localAccelerationY,
			(float) localRotationY,
			steeringAngle,
			steeringRangeRadians,
			steeringTorque,
			(float) sessionTime,
			0,
			false,
			absActive,
			tcActive,
			IRacingSdkEnum.TrkLoc.OnTrack,
			IRacingSdkEnum.TrkSurf.Asphalt1Material );

		return true;
	}

	private void LogSnapshotDiagnostics( double sessionTime, int playerVehicleIndex, float steeringInput, float genericFfbTorque, float steeringTorque, float forwardSpeed, float throttle, float brake, int gear )
	{
		var nowUtc = DateTime.UtcNow;

		if ( nowUtc < _nextDiagnosticsLogUtc )
		{
			return;
		}

		_nextDiagnosticsLogUtc = nowUtc.AddSeconds( 1 );

		App.Instance!.Logger.WriteLine(
			$"[LMU] elapsed={sessionTime:F3}s idx={playerVehicleIndex} speed={forwardSpeed:F2}m/s steer={steeringInput:F3} shaft={steeringTorque:F3}Nm generic={genericFfbTorque:F3} throttle={throttle:F2} brake={brake:F2} gear={gear}" );
	}

	private static string ReadFixedString( MemoryMappedViewAccessor view, int offset, int byteCount )
	{
		var bytes = new byte[ byteCount ];
		view.ReadArray( offset, bytes, 0, byteCount );

		var terminatorIndex = Array.IndexOf( bytes, (byte) 0 );
		var length = terminatorIndex >= 0 ? terminatorIndex : bytes.Length;

		if ( length <= 0 )
		{
			return string.Empty;
		}

		return Encoding.ASCII.GetString( bytes, 0, length ).Trim();
	}

}

internal readonly record struct LmuTelemetrySnapshot(
	float DeltaSeconds,
	string CarScreenName,
	string TrackDisplayName,
	string UserName,
	bool IsDrivingSession,
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
	bool ABSactive,
	bool TCactive,
	IRacingSdkEnum.TrkLoc PlayerTrackSurface,
	IRacingSdkEnum.TrkSurf PlayerTrackSurfaceMaterial );
