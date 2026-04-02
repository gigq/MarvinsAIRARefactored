
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using PInvoke;
using IRSDKSharper;

using MarvinsAIRARefactored.Classes;
using MarvinsAIRARefactored.Components.SimBackends;
using MarvinsAIRARefactored.SimSupport;
using MarvinsAIRARefactored.Windows;

using static MarvinsAIRARefactored.Windows.MainWindow;

namespace MarvinsAIRARefactored.Components;

public partial class Simulator
{
	public const int SamplesPerFrame360Hz = 6;
	private const int UpdateInterval = 6;
	private const int MaxNumGears = 10;

	private readonly IRacingSdk _irsdk = new();
	private readonly IReadOnlyDictionary<SimId, ISimTelemetryBackend> _telemetryBackends;
	private SimId? _autoDetectedSimId = null;
	private SimId _contextSimId = SimId.IRacing;
	private DateTime _nextAutoDetectionUtc = DateTime.MinValue;
	private DateTime _nextBackendDiagnosticsLogUtc = DateTime.MinValue;
	private int _backendTickMutex = 0;

	public IRacingSdk IRSDK { get => _irsdk; }

	public IntPtr? WindowHandle { get; private set; } = null;

	public List<IRacingSdkSessionInfo.DriverInfoModel.DriverTireModel>? AvailableTires = null;
	public bool BrakeABSactive { get; private set; } = false;
	public float Brake { get; private set; } = 0f;
	public int[] CarIdxLap { get; private set; } = new int[ IRacingSdkConst.MaxNumCars ];
	public float[] CarIdxLapDistPct { get; private set; } = new float[ IRacingSdkConst.MaxNumCars ];
	public bool[] CarIdxOnPitRoad { get; private set; } = new bool[ IRacingSdkConst.MaxNumCars ];
	public int[] CarIdxPosition { get; private set; } = new int[ IRacingSdkConst.MaxNumCars ];
	public string CarScreenName { get; private set; } = string.Empty;
	public string CarContextName { get; private set; } = string.Empty;
	public string CarSetupName { get; private set; } = string.Empty;
	public float[] CFShockVel_ST { get; private set; } = new float[ SamplesPerFrame360Hz ];
	public float Clutch { get; private set; } = 0f;
	public float[] CRShockVel_ST { get; private set; } = new float[ SamplesPerFrame360Hz ];
	public float CurrentRpmSpeedRatio { get; private set; } = 0f;
	public int CurrentTireIndex { get; private set; } = -1;
	public string CurrentTireCompoundType { get; private set; } = string.Empty;
	public int DisplayUnits { get; private set; } = 0;
	public float FrameRate { get; private set; } = 0;
	public int Gear { get; private set; } = 0;
	public float GpuUsage { get; private set; } = 0f;
	public float LongitudinalGForce { get; private set; } = 0f;
	public float LateralGForce { get; private set; } = 0f;
	public bool IsConnected => ( SelectedSimId != SimId.Auto ) && GetSelectedTelemetryBackend().IsConnected;
	public bool IsOnTrack { get; private set; } = false;
	public bool IsReplayPlaying { get; private set; } = false;
	public int Lap { get; private set; } = 0;
	public float LapDist { get; private set; } = 0;
	public float LapDistPct { get; private set; } = 0f;
	public int LastRadioTransmitCarIdx { get; private set; } = -1;
	public float LatAccel { get; private set; } = 0f;
	public int LeagueID { get; private set; } = 0;
	public float[] LFShockVel_ST { get; private set; } = new float[ SamplesPerFrame360Hz ];
	public bool LoadNumTextures { get; private set; } = false;
	public float LongAccel { get; private set; } = 0f;
	public float[] LRShockVel_ST { get; private set; } = new float[ SamplesPerFrame360Hz ];
	public float FrontAxleLateralPatchVelocity { get; private set; } = 0f;
	public float RearAxleLateralPatchVelocity { get; private set; } = 0f;
	public float FrontAxleLongitudinalPatchVelocity { get; private set; } = 0f;
	public float RearAxleLongitudinalPatchVelocity { get; private set; } = 0f;
	public float FrontAxleWheelRotation { get; private set; } = 0f;
	public float RearAxleWheelRotation { get; private set; } = 0f;
	public int NumForwardGears { get; private set; } = 0;
	public IRacingSdkEnum.PaceMode PaceMode { get; private set; } = IRacingSdkEnum.PaceMode.NotPacing;
	public int PlayerCarIdx { get; private set; } = 0;
	public IRacingSdkEnum.TrkLoc PlayerTrackSurface { get; private set; } = IRacingSdkEnum.TrkLoc.NotInWorld;
	public IRacingSdkEnum.TrkSurf PlayerTrackSurfaceMaterial { get; private set; } = IRacingSdkEnum.TrkSurf.SurfaceNotInWorld;
	public int RadioTransmitCarIdx { get; private set; } = -1;
	public int ReplayFrameNumEnd { get; private set; } = 1;
	public bool ReplayPlaySlowMotion { get; private set; } = false;
	public int ReplayPlaySpeed { get; private set; } = 1;
	public float[] RFShockVel_ST { get; private set; } = new float[ SamplesPerFrame360Hz ];
	public float RPM { get; private set; } = 0f;
	public float[] RPMSpeedRatios { get; private set; } = new float[ MaxNumGears ];
	public float[] RRShockVel_ST { get; private set; } = new float[ SamplesPerFrame360Hz ];
	public int SeriesID { get; private set; } = 0;
	public IRacingSdkEnum.Flags SessionFlags { get; private set; } = 0;
	public int SessionID { get; private set; } = 0;
	public int SessionNum { get; private set; } = 0;
	public double SessionTime { get; private set; } = 0f;
	public float ShiftLightsFirstRPM { get; private set; } = 0f;
	public float ShiftLightsShiftRPM { get; private set; } = 0f;
	public string SimMode { get; private set; } = string.Empty;
	public float Speed { get; private set; } = 0f;
	public bool SteeringFFBEnabled { get; private set; } = false;
	public float SteeringOffsetInDegrees { get; private set; } = 0f;
	public float SteeringRatio { get; private set; } = 10f;
	public float SteeringWheelAngle { get; private set; } = 0f;
	public float SteeringWheelAngleMax { get; private set; } = 0f;
	public float[] SteeringWheelTorque_ST { get; private set; } = new float[ SamplesPerFrame360Hz ];
	public float Throttle { get; private set; } = 0f;
	public string TimeOfDay { get; private set; } = string.Empty;
	public string TrackDisplayName { get; private set; } = string.Empty;
	public string TrackConfigName { get; private set; } = string.Empty;
	public float TrackLength { get; private set; } = 0f;
	public string UserName { get; private set; } = string.Empty;
	public float Velocity { get; private set; } = 0f;
	public float VelocityX { get; private set; } = 0f;
	public float VelocityY { get; private set; } = 0f;
	public float VertAccel { get; private set; } = 0f;
	public bool WasOnTrack { get; private set; } = false;
	public bool WeatherDeclaredWet { get; private set; } = false;
	public float YawNorth { get; private set; } = 0f;
	public float YawRate { get; private set; } = 0f;

	private bool _telemetryDataInitialized = false;
	private bool _waitingForFirstSessionInfo = false;

	private int? _tickCountLastFrame = null;
	private bool? _weatherDeclaredWetLastFrame = null;
	private bool? _isReplayPlayingLastFrame = null;
	private IRacingSdkEnum.Flags? _sessionFlagsLastFrame = null;
	private int? _currentTireIndexLastFrame = null;

	private IRacingSdkDatum? _brakeABSactiveDatum = null;
	private IRacingSdkDatum? _brakeDatum = null;
	private IRacingSdkDatum? _carIdxLapDatum = null;
	private IRacingSdkDatum? _carIdxLapDistPctDatum = null;
	private IRacingSdkDatum? _carIdxPositionDatum = null;
	private IRacingSdkDatum? _carIdxOnPitRoadDatum = null;
	private IRacingSdkDatum? _carIdxTireCompoundDatum = null;
	private IRacingSdkDatum? _cfShockVel_STDatum = null;
	private IRacingSdkDatum? _clutchDatum = null;
	private IRacingSdkDatum? _crShockVel_STDatum = null;
	private IRacingSdkDatum? _displayUnitsDatum = null;
	private IRacingSdkDatum? _frameRateDatum = null;
	private IRacingSdkDatum? _gearDatum = null;
	private IRacingSdkDatum? _gpuUsageDatum = null;
	private IRacingSdkDatum? _isOnTrackDatum = null;
	private IRacingSdkDatum? _isReplayPlayingDatum = null;
	private IRacingSdkDatum? _lapDatum = null;
	private IRacingSdkDatum? _lapDistDatum = null;
	private IRacingSdkDatum? _lapDistPctDatum = null;
	private IRacingSdkDatum? _latAccelDatum = null;
	private IRacingSdkDatum? _lfShockVel_STDatum = null;
	private IRacingSdkDatum? _loadNumTexturesDatum = null;
	private IRacingSdkDatum? _longAccelDatum = null;
	private IRacingSdkDatum? _lrShockVel_STDatum = null;
	private IRacingSdkDatum? _paceModeDatum = null;
	private IRacingSdkDatum? _playerCarIdxDatum = null;
	private IRacingSdkDatum? _playerTrackSurfaceDatum = null;
	private IRacingSdkDatum? _playerTrackSurfaceMaterialDatum = null;
	private IRacingSdkDatum? _radioTransmitCarIdxDatum = null;
	private IRacingSdkDatum? _replayFrameNumEndDatum = null;
	private IRacingSdkDatum? _replayPlaySlowMotionDatum = null;
	private IRacingSdkDatum? _replayPlaySpeedDatum = null;
	private IRacingSdkDatum? _rfShockVel_STDatum = null;
	private IRacingSdkDatum? _rpmDatum = null;
	private IRacingSdkDatum? _rrShockVel_STDatum = null;
	private IRacingSdkDatum? _sessionFlagsDatum = null;
	private IRacingSdkDatum? _sessionNumDatum = null;
	private IRacingSdkDatum? _sessionTimeDatum = null;
	private IRacingSdkDatum? _speedDatum = null;
	private IRacingSdkDatum? _steeringFFBEnabledDatum = null;
	private IRacingSdkDatum? _steeringWheelAngleDatum = null;
	private IRacingSdkDatum? _steeringWheelAngleMaxDatum = null;
	private IRacingSdkDatum? _steeringWheelTorque_STDatum = null;
	private IRacingSdkDatum? _throttleDatum = null;
	private IRacingSdkDatum? _velocityXDatum = null;
	private IRacingSdkDatum? _velocityYDatum = null;
	private IRacingSdkDatum? _vertAccelDatum = null;
	private IRacingSdkDatum? _weatherDeclaredWetDatum = null;
	private IRacingSdkDatum? _yawNorthDatum = null;
	private IRacingSdkDatum? _yawRateDatum = null;

#if DEBUG

	private float _minMaxLogAccumulator = 0f;
	private float _minFrameRate = float.MaxValue;
	private float _maxFrameRate = float.MinValue;
	private float _minGpuUsage = float.MaxValue;
	private float _maxGpuUsage = float.MinValue;

#endif

	private readonly float[] _rpmSpeedRatioAccumulator = new float[ MaxNumGears ];
	private readonly int[] _rpmSpeedRatioSampleCount = new int[ MaxNumGears ];
	private const int RpmSpeedRatioMinSamples = 20;
	private readonly DataContext.ContextSwitches _fullContextSwitches = new( true, true, true, true, true );
	private DataContext.Context? _activeSettingsContextLastFrame = null;
	private string _carScreenNameLastFrame = string.Empty;
	private string _trackDisplayNameLastFrame = string.Empty;
	private string _trackConfigNameLastFrame = string.Empty;

	private int _updateCounter = UpdateInterval + 5;

	public Simulator()
	{
		_telemetryBackends = new Dictionary<SimId, ISimTelemetryBackend>
		{
			[ SimId.IRacing ] = new IRacingTelemetryBackend( _irsdk, OnException, OnConnected, OnDisconnected, OnSessionInfo, OnTelemetryData, OnDebugLog ),
			[ SimId.LeMansUltimate ] = new LmuTelemetryBackend( this )
		};
	}

	public void Initialize()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[Simulator] Initialize >>>" );

		foreach ( var backend in _telemetryBackends.Values )
		{
			backend.Initialize();
		}

		app.Logger.WriteLine( "[Simulator] <<< Initialize" );
	}

	public void Shutdown()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[Simulator] Shutdown >>>" );

		foreach ( var backend in _telemetryBackends.Values )
		{
			backend.Shutdown();
		}

		app.Logger.WriteLine( "[Simulator] <<< Shutdown" );
	}

	public void Start()
	{
		ApplySelectedSimulator();
	}

	public IRacingSdkSessionInfo.DriverInfoModel.DriverModel? GetDriver( int carIdx )
	{
		if ( SelectedSimId != SimId.IRacing )
		{
			return null;
		}

		var sessionInfo = _irsdk.Data.SessionInfo;

		if ( ( sessionInfo != null ) && ( sessionInfo.DriverInfo != null ) && ( sessionInfo.DriverInfo.Drivers != null ) )
		{
			foreach ( var driver in sessionInfo.DriverInfo.Drivers )
			{
				if ( driver.CarIdx == carIdx )
				{
					return driver;
				}
			}
		}

		return null;
	}

	private void OnException( Exception exception )
	{
		App.Instance!.ShowFatalError( null, exception );
	}

	private void OnConnected()
	{
		if ( SelectedSimId != SimId.IRacing )
		{
			return;
		}

		HandleBackendConnected( true );
	}

	private void OnDisconnected()
	{
		HandleBackendDisconnected();
	}

	private void OnSessionInfo()
	{
		if ( SelectedSimId != SimId.IRacing )
		{
			return;
		}

		var app = App.Instance!;

		var sessionInfo = _irsdk.Data.SessionInfo;

		CarSetupName = Path.GetFileNameWithoutExtension( sessionInfo.DriverInfo.DriverSetupName ).ToLower();

		NumForwardGears = sessionInfo.DriverInfo.DriverCarGearNumForward;

		ShiftLightsFirstRPM = sessionInfo.DriverInfo.DriverCarSLFirstRPM;
		ShiftLightsShiftRPM = sessionInfo.DriverInfo.DriverCarSLShiftRPM;

		if ( ShiftLightsShiftRPM <= ShiftLightsFirstRPM )
		{
			ShiftLightsShiftRPM = sessionInfo.DriverInfo.DriverCarSLBlinkRPM;
		}

		SimMode = sessionInfo.WeekendInfo.SimMode;

		foreach ( var driver in sessionInfo.DriverInfo.Drivers )
		{
			if ( driver.CarIdx == sessionInfo.DriverInfo.DriverCarIdx )
			{
				CarScreenName = driver.CarScreenName ?? string.Empty;
				CarContextName = CarScreenName;
				UserName = driver.UserName ?? string.Empty;
				break;
			}
		}

		TrackDisplayName = sessionInfo.WeekendInfo.TrackDisplayName ?? string.Empty;
		TrackConfigName = sessionInfo.WeekendInfo.TrackConfigName ?? string.Empty;

		if ( sessionInfo.CarSetup?.Chassis?.Front?.SteeringOffset != null )
		{
			var numericPart = SteeringOffsetRegex().Replace( sessionInfo.CarSetup.Chassis.Front.SteeringOffset, "" ).Trim();

			if ( float.TryParse( numericPart, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result ) )
			{
				SteeringOffsetInDegrees = result;
			}
			else
			{
				SteeringOffsetInDegrees = 0f;
			}
		}
		else
		{
			SteeringOffsetInDegrees = 0f;
		}

		if ( sessionInfo.CarSetup?.Chassis?.Front?.SteeringRatio != null )
		{
			var numericPart = SteeringRatioRegex().Replace( sessionInfo.CarSetup.Chassis.Front.SteeringRatio, "" ).Trim();

			if ( float.TryParse( numericPart, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result ) )
			{
				SteeringRatio = result;
			}
			else
			{
				SteeringRatio = 10f;
			}
		}
		else
		{
			SteeringRatio = 10f;
		}

		SeriesID = sessionInfo.WeekendInfo.SeriesID;
		LeagueID = sessionInfo.WeekendInfo.LeagueID;
		TimeOfDay = sessionInfo.WeekendInfo.WeekendOptions.TimeOfDay;

		var match = TrackLengthRegex().Match( sessionInfo.WeekendInfo.TrackLength );

		if ( match.Success )
		{
			TrackLength = float.Parse( match.Groups[ 1 ].Value, CultureInfo.InvariantCulture.NumberFormat );
		}
		else
		{
			TrackLength = 0f;
		}

		app.Drivers.Update( sessionInfo );
		app.TimingMarkers.UpdateTrackLength();

		app.MainWindow.UpdateStatus();

		if ( _waitingForFirstSessionInfo )
		{
			DataContext.DataContext.Instance.Settings.UpdateSettings( false );

			UpdateTireProperties();

#if !ADMINBOXX

			MainWindow._steeringEffectsPage.UpdateCalibrationFileNameOptions();

#endif

			_waitingForFirstSessionInfo = false;
		}

		if ( SessionID != sessionInfo.WeekendInfo.SessionID )
		{
			SessionID = sessionInfo.WeekendInfo.SessionID;

			app.TradingPaints.Reset();
		}

		app.TradingPaints.Update();

#if DEBUG

		// Write out SessionInfo.yaml file

		var sessionInfoYaml = _irsdk.Data.SessionInfoYaml;

		var diagnosticsDirectory = App.GetSimulatorContentDirectory( SelectedSimId, "Diagnostics" );

		Directory.CreateDirectory( diagnosticsDirectory );

		var filePath = Path.Combine( diagnosticsDirectory, "SessionInfo.yaml" );

		File.WriteAllText( filePath, sessionInfoYaml );

		// Write out TelemetryData.yaml file

		filePath = Path.Combine( diagnosticsDirectory, "TelemetryData.yaml" );

		var serializer = new SerializerBuilder().WithNamingConvention( CamelCaseNamingConvention.Instance ).Build();

		var yaml = serializer.Serialize( _irsdk.Data.TelemetryDataProperties );

		File.WriteAllText( filePath, yaml );

#endif
	}

	private void OnTelemetryData()
	{
		if ( SelectedSimId != SimId.IRacing )
		{
			return;
		}

		var app = App.Instance!;

		// initialize telemetry data properties

		if ( !_telemetryDataInitialized )
		{
			_brakeABSactiveDatum = _irsdk.Data.TelemetryDataProperties[ "BrakeABSactive" ];
			_brakeDatum = _irsdk.Data.TelemetryDataProperties[ "Brake" ];
			_carIdxLapDatum = _irsdk.Data.TelemetryDataProperties[ "CarIdxLap" ];
			_carIdxLapDistPctDatum = _irsdk.Data.TelemetryDataProperties[ "CarIdxLapDistPct" ];
			_carIdxPositionDatum = _irsdk.Data.TelemetryDataProperties[ "CarIdxPosition" ];
			_carIdxOnPitRoadDatum = _irsdk.Data.TelemetryDataProperties[ "CarIdxOnPitRoad" ];
			_carIdxTireCompoundDatum = _irsdk.Data.TelemetryDataProperties[ "CarIdxTireCompound" ];
			_clutchDatum = _irsdk.Data.TelemetryDataProperties[ "Clutch" ];
			_displayUnitsDatum = _irsdk.Data.TelemetryDataProperties[ "DisplayUnits" ];
			_frameRateDatum = _irsdk.Data.TelemetryDataProperties[ "FrameRate" ];
			_gearDatum = _irsdk.Data.TelemetryDataProperties[ "Gear" ];
			_gpuUsageDatum = _irsdk.Data.TelemetryDataProperties[ "GpuUsage" ];
			_isOnTrackDatum = _irsdk.Data.TelemetryDataProperties[ "IsOnTrack" ];
			_isReplayPlayingDatum = _irsdk.Data.TelemetryDataProperties[ "IsReplayPlaying" ];
			_lapDatum = _irsdk.Data.TelemetryDataProperties[ "Lap" ];
			_lapDistDatum = _irsdk.Data.TelemetryDataProperties[ "LapDist" ];
			_lapDistPctDatum = _irsdk.Data.TelemetryDataProperties[ "LapDistPct" ];
			_latAccelDatum = _irsdk.Data.TelemetryDataProperties[ "LatAccel" ];
			_loadNumTexturesDatum = _irsdk.Data.TelemetryDataProperties[ "LoadNumTextures" ];
			_longAccelDatum = _irsdk.Data.TelemetryDataProperties[ "LongAccel" ];
			_paceModeDatum = _irsdk.Data.TelemetryDataProperties[ "PaceMode" ];
			_playerCarIdxDatum = _irsdk.Data.TelemetryDataProperties[ "PlayerCarIdx" ];
			_playerTrackSurfaceDatum = _irsdk.Data.TelemetryDataProperties[ "PlayerTrackSurface" ];
			_playerTrackSurfaceMaterialDatum = _irsdk.Data.TelemetryDataProperties[ "PlayerTrackSurfaceMaterial" ];
			_radioTransmitCarIdxDatum = _irsdk.Data.TelemetryDataProperties[ "RadioTransmitCarIdx" ];
			_replayFrameNumEndDatum = _irsdk.Data.TelemetryDataProperties[ "ReplayFrameNumEnd" ];
			_replayPlaySlowMotionDatum = _irsdk.Data.TelemetryDataProperties[ "ReplayPlaySlowMotion" ];
			_replayPlaySpeedDatum = _irsdk.Data.TelemetryDataProperties[ "ReplayPlaySpeed" ];
			_rpmDatum = _irsdk.Data.TelemetryDataProperties[ "RPM" ];
			_sessionFlagsDatum = _irsdk.Data.TelemetryDataProperties[ "SessionFlags" ];
			_sessionNumDatum = _irsdk.Data.TelemetryDataProperties[ "SessionNum" ];
			_sessionTimeDatum = _irsdk.Data.TelemetryDataProperties[ "SessionTime" ];
			_speedDatum = _irsdk.Data.TelemetryDataProperties[ "Speed" ];
			_steeringFFBEnabledDatum = _irsdk.Data.TelemetryDataProperties[ "SteeringFFBEnabled" ];
			_steeringWheelAngleDatum = _irsdk.Data.TelemetryDataProperties[ "SteeringWheelAngle" ];
			_steeringWheelAngleMaxDatum = _irsdk.Data.TelemetryDataProperties[ "SteeringWheelAngleMax" ];
			_steeringWheelTorque_STDatum = _irsdk.Data.TelemetryDataProperties[ "SteeringWheelTorque_ST" ];
			_throttleDatum = _irsdk.Data.TelemetryDataProperties[ "Throttle" ];
			_velocityXDatum = _irsdk.Data.TelemetryDataProperties[ "VelocityX" ];
			_velocityYDatum = _irsdk.Data.TelemetryDataProperties[ "VelocityY" ];
			_vertAccelDatum = _irsdk.Data.TelemetryDataProperties[ "VertAccel" ];
			_weatherDeclaredWetDatum = _irsdk.Data.TelemetryDataProperties[ "WeatherDeclaredWet" ];
			_yawNorthDatum = _irsdk.Data.TelemetryDataProperties[ "YawNorth" ];
			_yawRateDatum = _irsdk.Data.TelemetryDataProperties[ "YawRate" ];

			_cfShockVel_STDatum = null;
			_crShockVel_STDatum = null;
			_lfShockVel_STDatum = null;
			_lrShockVel_STDatum = null;
			_rfShockVel_STDatum = null;
			_rrShockVel_STDatum = null;

			_irsdk.Data.TelemetryDataProperties.TryGetValue( "CFshockVel_ST", out _cfShockVel_STDatum );
			_irsdk.Data.TelemetryDataProperties.TryGetValue( "CRshockVel_ST", out _crShockVel_STDatum );
			_irsdk.Data.TelemetryDataProperties.TryGetValue( "LRshockVel_ST", out _lfShockVel_STDatum );
			_irsdk.Data.TelemetryDataProperties.TryGetValue( "LRshockVel_ST", out _lrShockVel_STDatum );
			_irsdk.Data.TelemetryDataProperties.TryGetValue( "RFshockVel_ST", out _rfShockVel_STDatum );
			_irsdk.Data.TelemetryDataProperties.TryGetValue( "RRshockVel_ST", out _rrShockVel_STDatum );

			_telemetryDataInitialized = true;
		}

		// shortcut to settings

		var settings = DataContext.DataContext.Instance.Settings;

		// set last frame tick count if its not been set yet

		_tickCountLastFrame ??= _irsdk.Data.TickCount - 1;

		// calculate delta time

		var deltaSeconds = (float) ( _irsdk.Data.TickCount - (int) _tickCountLastFrame ) / _irsdk.Data.TickRate;

		// update tick count last frame

		_tickCountLastFrame = _irsdk.Data.TickCount;

		// protect ourselves from zero or negative time just in case

		if ( deltaSeconds <= 0f )
		{
			return;
		}

		// poll directinput devices right before we process the algorithm (setting app.RacingWheel.UpdateSteeringWheelTorqueBuffer = true updates the prediction on the multimedia timer thread)

		// get next 360 Hz steering wheel torque samples

		_irsdk.Data.GetFloatArray( _steeringWheelTorque_STDatum, SteeringWheelTorque_ST, 0, SteeringWheelTorque_ST.Length );

		// save last frame values

		WasOnTrack = IsOnTrack;

		// update non-array telemetry data properties

		BrakeABSactive = _irsdk.Data.GetBool( _brakeABSactiveDatum );
		Brake = _irsdk.Data.GetFloat( _brakeDatum );
		Clutch = _irsdk.Data.GetFloat( _clutchDatum );
		DisplayUnits = _irsdk.Data.GetInt( _displayUnitsDatum );
		FrameRate = _irsdk.Data.GetFloat( _frameRateDatum );
		Gear = _irsdk.Data.GetInt( _gearDatum );
		GpuUsage = _irsdk.Data.GetFloat( _gpuUsageDatum );
		IsOnTrack = _irsdk.Data.GetBool( _isOnTrackDatum );
		IsReplayPlaying = _irsdk.Data.GetBool( _isReplayPlayingDatum );
		Lap = _irsdk.Data.GetInt( _lapDatum );
		LapDist = _irsdk.Data.GetFloat( _lapDistDatum );
		LapDistPct = _irsdk.Data.GetFloat( _lapDistPctDatum );
		LatAccel = _irsdk.Data.GetFloat( _latAccelDatum );
		LoadNumTextures = _irsdk.Data.GetBool( _loadNumTexturesDatum );
		LongAccel = _irsdk.Data.GetFloat( _longAccelDatum );
		PaceMode = (IRacingSdkEnum.PaceMode) _irsdk.Data.GetInt( _paceModeDatum );
		PlayerCarIdx = _irsdk.Data.GetInt( _playerCarIdxDatum );
		PlayerTrackSurface = (IRacingSdkEnum.TrkLoc) _irsdk.Data.GetInt( _playerTrackSurfaceDatum );
		PlayerTrackSurfaceMaterial = (IRacingSdkEnum.TrkSurf) _irsdk.Data.GetInt( _playerTrackSurfaceMaterialDatum );
		RadioTransmitCarIdx = _irsdk.Data.GetInt( _radioTransmitCarIdxDatum );
		ReplayFrameNumEnd = _irsdk.Data.GetInt( _replayFrameNumEndDatum );
		ReplayPlaySlowMotion = _irsdk.Data.GetBool( _replayPlaySlowMotionDatum );
		ReplayPlaySpeed = _irsdk.Data.GetInt( _replayPlaySpeedDatum );
		RPM = _irsdk.Data.GetFloat( _rpmDatum );
		SessionFlags = (IRacingSdkEnum.Flags) _irsdk.Data.GetBitField( _sessionFlagsDatum );
		SessionNum = _irsdk.Data.GetInt( _sessionNumDatum );
		SessionTime = _irsdk.Data.GetDouble( _sessionTimeDatum );
		Speed = _irsdk.Data.GetFloat( _speedDatum );
		SteeringFFBEnabled = _irsdk.Data.GetBool( _steeringFFBEnabledDatum );
		SteeringWheelAngle = _irsdk.Data.GetFloat( _steeringWheelAngleDatum );
		SteeringWheelAngleMax = _irsdk.Data.GetFloat( _steeringWheelAngleMaxDatum );
		Throttle = _irsdk.Data.GetFloat( _throttleDatum );
		VelocityX = _irsdk.Data.GetFloat( _velocityXDatum );
		VelocityY = _irsdk.Data.GetFloat( _velocityYDatum );
		VertAccel = _irsdk.Data.GetFloat( _vertAccelDatum );
		WeatherDeclaredWet = _irsdk.Data.GetBool( _weatherDeclaredWetDatum );
		YawNorth = _irsdk.Data.GetFloat( _yawNorthDatum );
		YawRate = _irsdk.Data.GetFloat( _yawRateDatum );

		// update min/max FrameRate and GpuUsage, log every second

#if DEBUG

		_minFrameRate = MathF.Min( _minFrameRate, FrameRate );
		_maxFrameRate = MathF.Max( _maxFrameRate, FrameRate );
		_minGpuUsage = MathF.Min( _minGpuUsage, GpuUsage );
		_maxGpuUsage = MathF.Max( _maxGpuUsage, GpuUsage );

		_minMaxLogAccumulator += deltaSeconds;

		if ( _minMaxLogAccumulator >= 1f )
		{
			app.Logger.WriteLine( $"[Simulator] FrameRate min={_minFrameRate:F1} max={_maxFrameRate:F1}, GpuUsage min={_minGpuUsage:F1} max={_maxGpuUsage:F1}" );

			_minMaxLogAccumulator -= 1f;

			_minFrameRate = float.MaxValue;
			_maxFrameRate = float.MinValue;
			_minGpuUsage = float.MaxValue;
			_maxGpuUsage = float.MinValue;
		}

#endif

		// update array telemetry data properties

		_irsdk.Data.GetIntArray( _carIdxLapDatum, CarIdxLap, 0, _carIdxLapDatum!.Count );
		_irsdk.Data.GetFloatArray( _carIdxLapDistPctDatum, CarIdxLapDistPct, 0, _carIdxLapDistPctDatum!.Count );
		_irsdk.Data.GetIntArray( _carIdxPositionDatum, CarIdxPosition, 0, _carIdxPositionDatum!.Count );
		_irsdk.Data.GetBoolArray( _carIdxOnPitRoadDatum, CarIdxOnPitRoad, 0, _carIdxOnPitRoadDatum!.Count );

		// get next 360 Hz shock velocity samples

		if ( _cfShockVel_STDatum != null )
		{
			_irsdk.Data.GetFloatArray( _cfShockVel_STDatum, CFShockVel_ST, 0, CFShockVel_ST.Length );
		}

		if ( _crShockVel_STDatum != null )
		{
			_irsdk.Data.GetFloatArray( _crShockVel_STDatum, CRShockVel_ST, 0, CRShockVel_ST.Length );
		}

		if ( _lfShockVel_STDatum != null )
		{
			_irsdk.Data.GetFloatArray( _lfShockVel_STDatum, LFShockVel_ST, 0, LFShockVel_ST.Length );
		}

		if ( _lrShockVel_STDatum != null )
		{
			_irsdk.Data.GetFloatArray( _lrShockVel_STDatum, LRShockVel_ST, 0, LRShockVel_ST.Length );
		}

		if ( _rfShockVel_STDatum != null )
		{
			_irsdk.Data.GetFloatArray( _rfShockVel_STDatum, RFShockVel_ST, 0, RFShockVel_ST.Length );
		}

		if ( _rrShockVel_STDatum != null )
		{
			_irsdk.Data.GetFloatArray( _rrShockVel_STDatum, RRShockVel_ST, 0, RRShockVel_ST.Length );
		}

		FinalizeTelemetryFrame( app, deltaSeconds );
	}

	private void UpdateTireProperties()
	{
		if ( SelectedSimId != SimId.IRacing )
		{
			CurrentTireCompoundType = "unknown";
			AvailableTires = null;
			return;
		}

		var tireFound = false;

		var sessionInfo = _irsdk.Data.SessionInfo;

		if ( sessionInfo != null )
		{
			if ( sessionInfo.DriverInfo != null )
			{
				if ( sessionInfo.DriverInfo.DriverTires != null )
				{
					AvailableTires = sessionInfo.DriverInfo.DriverTires;

					for ( var tireIndex = 0; tireIndex < sessionInfo.DriverInfo.DriverTires.Count; tireIndex++ )
					{
						if ( AvailableTires[ tireIndex ].TireIndex == CurrentTireIndex )
						{
							CurrentTireCompoundType = AvailableTires[ tireIndex ].TireCompoundType.ToLower();

							tireFound = true;

							break;
						}
					}
				}
			}
		}

		if ( !tireFound )
		{
			CurrentTireCompoundType = "unknown";
		}
	}

	private void OnDebugLog( string message )
	{
		var app = App.Instance!;

		app.Logger.WriteLine( $"[IRSDKSharper] {message}" );
	}

	public void Tick( App app )
	{
		UpdateAutoDetectedSimulator( false );

		var nowUtc = DateTime.UtcNow;

		if ( ( SelectedSimId != SimId.Auto ) && ( nowUtc >= _nextBackendDiagnosticsLogUtc ) )
		{
			_nextBackendDiagnosticsLogUtc = nowUtc.AddSeconds( 1 );
			app.Logger.WriteLine( $"[SimulatorTick] selected={SelectedSimId} backend={GetSelectedTelemetryBackend().GetType().Name}" );
		}

		if ( ( SelectedSimId != SimId.Auto ) && ( SelectedSimId != SimId.LeMansUltimate ) )
		{
			PollSelectedTelemetryBackend();
		}

		_updateCounter--;

		if ( _updateCounter <= 0 )
		{
			_updateCounter = UpdateInterval;

			_racingWheelPage.CurrentForce_TextBlock.Text = $"{MathF.Abs( SteeringWheelTorque_ST[ 5 ] ):F1} {DataContext.DataContext.Instance.Localization[ "TorqueUnits" ]}";
		}
	}

	[GeneratedRegex( @"\s*deg\s*$", RegexOptions.IgnoreCase, "en-US" )]
	private static partial Regex SteeringOffsetRegex();

	[GeneratedRegex( @"\s*:1\s*$", RegexOptions.IgnoreCase, "en-US" )]
	private static partial Regex SteeringRatioRegex();

	[GeneratedRegex( @"([-+]?[0-9]*\.?[0-9]+)", RegexOptions.IgnoreCase, "en-US" )]
	private static partial Regex TrackLengthRegex();

	private ISimTelemetryBackend GetTelemetryBackend( SimId simId )
	{
		return _telemetryBackends.TryGetValue( simId, out var backend ) ? backend : _telemetryBackends[ SimId.IRacing ];
	}

	internal void PollSelectedTelemetryBackend()
	{
		if ( SelectedSimId == SimId.Auto )
		{
			return;
		}

		if ( Interlocked.Exchange( ref _backendTickMutex, 1 ) != 0 )
		{
			return;
		}

		try
		{
			GetSelectedTelemetryBackend().Tick();
		}
		finally
		{
			Volatile.Write( ref _backendTickMutex, 0 );
		}
	}

	private ISimTelemetryBackend GetSelectedTelemetryBackend()
	{
		return GetTelemetryBackend( SelectedSimId );
	}

	internal void HandleBackendConnected()
	{
		HandleBackendConnected( false );
	}

	internal void HandleBackendDisconnected()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[Simulator] OnDisconnected >>>" );

		app.RacingWheel.UseSteeringWheelTorqueData = false;

		WindowHandle = null;

		_telemetryDataInitialized = false;
		_waitingForFirstSessionInfo = false;

		AvailableTires = null;
		BrakeABSactive = false;
		Brake = 0f;
		CarScreenName = string.Empty;
		CarContextName = string.Empty;
		CarSetupName = string.Empty;
		Clutch = 0f;
		CurrentRpmSpeedRatio = 0f;
		CurrentTireIndex = -1;
		CurrentTireCompoundType = string.Empty;
		DisplayUnits = 0;
		FrameRate = 0f;
		FrontAxleLateralPatchVelocity = 0f;
		RearAxleLateralPatchVelocity = 0f;
		FrontAxleLongitudinalPatchVelocity = 0f;
		RearAxleLongitudinalPatchVelocity = 0f;
		FrontAxleWheelRotation = 0f;
		RearAxleWheelRotation = 0f;
		Gear = 0;
		GpuUsage = 0f;
		LongitudinalGForce = 0f;
		LateralGForce = 0f;
		IsOnTrack = false;
		IsReplayPlaying = false;
		Lap = 0;
		LapDist = 0f;
		LapDistPct = 0f;
		LastRadioTransmitCarIdx = -1;
		LatAccel = 0f;
		LeagueID = 0;
		LoadNumTextures = false;
		LongAccel = 0f;
		NumForwardGears = 0;
		PaceMode = IRacingSdkEnum.PaceMode.NotPacing;
		PlayerCarIdx = -1;
		PlayerTrackSurface = IRacingSdkEnum.TrkLoc.NotInWorld;
		PlayerTrackSurfaceMaterial = IRacingSdkEnum.TrkSurf.SurfaceNotInWorld;
		RadioTransmitCarIdx = -1;
		ReplayFrameNumEnd = 1;
		ReplayPlaySlowMotion = false;
		ReplayPlaySpeed = 1;
		RPM = 0f;
		SessionFlags = 0;
		SessionID = 0;
		SessionNum = 0;
		SessionTime = 0;
		SeriesID = 0;
		Speed = 0f;
		ShiftLightsFirstRPM = 0f;
		ShiftLightsShiftRPM = 0f;
		SimMode = string.Empty;
		SteeringFFBEnabled = false;
		SteeringOffsetInDegrees = 0f;
		SteeringRatio = 10f;
		SteeringWheelAngle = 0f;
		SteeringWheelAngleMax = 0f;
		Throttle = 0f;
		TimeOfDay = string.Empty;
		TrackDisplayName = string.Empty;
		TrackConfigName = string.Empty;
		TrackLength = 0f;
		UserName = string.Empty;
		Velocity = 0f;
		VelocityX = 0f;
		VelocityY = 0f;
		VertAccel = 0f;
		WasOnTrack = false;
		WeatherDeclaredWet = false;
		YawNorth = 0f;
		YawRate = 0f;

		Array.Clear( CarIdxLap );
		Array.Clear( CarIdxLapDistPct );
		Array.Clear( CarIdxOnPitRoad );
		Array.Clear( CarIdxPosition );
		Array.Clear( CFShockVel_ST );
		Array.Clear( CRShockVel_ST );
		Array.Clear( LFShockVel_ST );
		Array.Clear( LRShockVel_ST );
		Array.Clear( RFShockVel_ST );
		Array.Clear( RRShockVel_ST );
		Array.Clear( SteeringWheelTorque_ST );
		Array.Clear( RPMSpeedRatios );
		Array.Clear( _rpmSpeedRatioAccumulator );
		Array.Clear( _rpmSpeedRatioSampleCount );

		_tickCountLastFrame = null;
		_weatherDeclaredWetLastFrame = null;
		_isReplayPlayingLastFrame = null;
		_sessionFlagsLastFrame = null;
		_currentTireIndexLastFrame = null;
		_activeSettingsContextLastFrame = null;
		_carScreenNameLastFrame = string.Empty;
		_trackDisplayNameLastFrame = string.Empty;
		_trackConfigNameLastFrame = string.Empty;

#if DEBUG

		_minMaxLogAccumulator = 0f;
		_minFrameRate = float.MaxValue;
		_maxFrameRate = float.MinValue;
		_minGpuUsage = float.MaxValue;
		_maxGpuUsage = float.MinValue;

#endif

		DataContext.DataContext.Instance.Settings.UpdateSettings( false );

		app.AdminBoxx.SimulatorDisconnected();

#if !ADMINBOXX

		app.SteeringEffects.SimulatorDisconnected();
		app.SpeechToText.SimulatorDisconnected();
		app.TimingMarkers.Reset();

		app.UpdateGripOMeterWindowVisibility();
		app.UpdateSpeechToTextWindowVisibility();
		app.UpdateGapMonitorWindowVisibility();

#endif

		app.MultimediaTimer.Suspend = true;

		app.MainWindow.UpdateStatus();

		_racingWheelPage.UpdateSteeringDeviceSection();

		app.Logger.WriteLine( "[Simulator] <<< OnDisconnected" );
	}

	internal void ApplyLmuTelemetry( LmuTelemetrySnapshot snapshot )
	{
		var app = App.Instance!;

		WasOnTrack = IsOnTrack;

		BrakeABSactive = snapshot.ABSactive;
		Brake = snapshot.Brake;
		CarScreenName = snapshot.CarScreenName;
		CarContextName = snapshot.CarContextName;
		CarSetupName = string.Empty;
		Clutch = snapshot.Clutch;
		DisplayUnits = 0;
		FrameRate = 0f;
		FrontAxleLateralPatchVelocity = snapshot.FrontAxleLateralPatchVelocity;
		RearAxleLateralPatchVelocity = snapshot.RearAxleLateralPatchVelocity;
		FrontAxleLongitudinalPatchVelocity = snapshot.FrontAxleLongitudinalPatchVelocity;
		RearAxleLongitudinalPatchVelocity = snapshot.RearAxleLongitudinalPatchVelocity;
		FrontAxleWheelRotation = snapshot.FrontAxleWheelRotation;
		RearAxleWheelRotation = snapshot.RearAxleWheelRotation;
		Gear = snapshot.Gear;
		GpuUsage = 0f;
		IsOnTrack = snapshot.IsOnTrack;
		IsReplayPlaying = false;
		Lap = snapshot.Lap;
		LapDist = snapshot.LapDist;
		LapDistPct = snapshot.LapDistPct;
		LatAccel = snapshot.LatAccel;
		LeagueID = 0;
		LoadNumTextures = false;
		LongAccel = snapshot.LongAccel;
		NumForwardGears = snapshot.NumForwardGears;
		PaceMode = IRacingSdkEnum.PaceMode.NotPacing;
		PlayerCarIdx = -1;
		PlayerTrackSurface = snapshot.PlayerTrackSurface;
		PlayerTrackSurfaceMaterial = snapshot.PlayerTrackSurfaceMaterial;
		RadioTransmitCarIdx = -1;
		ReplayFrameNumEnd = 1;
		ReplayPlaySlowMotion = false;
		ReplayPlaySpeed = 1;
		RPM = snapshot.RPM;
		SessionFlags = 0;
		SessionNum = snapshot.SessionNum;
		SessionTime = snapshot.SessionTime;
		ShiftLightsFirstRPM = snapshot.ShiftLightsFirstRPM;
		ShiftLightsShiftRPM = snapshot.ShiftLightsShiftRPM;
		SimMode = snapshot.IsDrivingSession ? "full" : string.Empty;
		Speed = snapshot.Speed;
		SteeringFFBEnabled = false;
		SteeringOffsetInDegrees = 0f;
		SteeringRatio = 10f;
		SteeringWheelAngle = snapshot.SteeringWheelAngle;
		SteeringWheelAngleMax = snapshot.SteeringWheelAngleMax;
		Throttle = snapshot.Throttle;
		TimeOfDay = string.Empty;
		TrackDisplayName = snapshot.TrackDisplayName;
		TrackConfigName = string.Empty;
		TrackLength = snapshot.TrackLength;
		UserName = snapshot.UserName;
		VelocityX = snapshot.VelocityX;
		VelocityY = snapshot.VelocityY;
		VertAccel = snapshot.VertAccel;
		WeatherDeclaredWet = snapshot.WeatherDeclaredWet;
		YawNorth = 0f;
		YawRate = snapshot.YawRate;

		for ( var i = 0; i < SteeringWheelTorque_ST.Length; i++ )
		{
			CFShockVel_ST[ i ] = snapshot.CFShockVelocity;
			CRShockVel_ST[ i ] = snapshot.CRShockVelocity;
			LFShockVel_ST[ i ] = snapshot.LFShockVelocity;
			LRShockVel_ST[ i ] = snapshot.LRShockVelocity;
			RFShockVel_ST[ i ] = snapshot.RFShockVelocity;
			RRShockVel_ST[ i ] = snapshot.RRShockVelocity;
			SteeringWheelTorque_ST[ i ] = snapshot.SteeringWheelTorque;
		}

		FinalizeTelemetryFrame( app, snapshot.DeltaSeconds );
	}

	private void HandleBackendConnected( bool waitForFirstSessionInfo )
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[Simulator] OnConnected >>>" );

		if ( CurrentSimDefinition.WindowTitle != null )
		{
			WindowHandle = User32.FindWindow( null, CurrentSimDefinition.WindowTitle );
		}
		else
		{
			WindowHandle = null;
		}

		app.MultimediaTimer.Suspend = false;

		_waitingForFirstSessionInfo = waitForFirstSessionInfo;

		app.RacingWheel.ResetForceFeedback = true;

		app.AdminBoxx.SimulatorConnected();

#if !ADMINBOXX

		app.SpeechToText.SimulatorConnected();

#endif

		Array.Clear( RPMSpeedRatios );
		Array.Clear( _rpmSpeedRatioAccumulator );
		Array.Clear( _rpmSpeedRatioSampleCount );

		app.MainWindow.UpdateStatus();
		_racingWheelPage.UpdateSteeringDeviceSection();

		app.Logger.WriteLine( "[Simulator] <<< OnConnected" );
	}

	private void FinalizeTelemetryFrame( App app, float deltaSeconds )
	{
		app.DirectInput.PollDevices( deltaSeconds );
		app.RacingWheel.UpdateSteeringWheelTorqueBuffer = true;
		app.RacingWheel.UseSteeringWheelTorqueData = ( SelectedSimId == SimId.LeMansUltimate ) ? IsConnected : IsOnTrack;

		if ( IsReplayPlaying != _isReplayPlayingLastFrame )
		{
			app.AdminBoxx.ReplayPlayingChanged();
		}

		_isReplayPlayingLastFrame = IsReplayPlaying;

		if ( SessionFlags != _sessionFlagsLastFrame )
		{
			app.AdminBoxx.SessionFlagsChanged();
		}

		_sessionFlagsLastFrame = SessionFlags;

		if ( RadioTransmitCarIdx != -1 )
		{
			LastRadioTransmitCarIdx = RadioTransmitCarIdx;
		}

		Velocity = MathF.Sqrt( VelocityX * VelocityX + VelocityY * VelocityY );

		LongitudinalGForce = MathF.Abs( LongAccel ) * MathZ.OneOverG;
		LateralGForce = MathF.Abs( LatAccel ) * MathZ.OneOverG;

		var settings = DataContext.DataContext.Instance.Settings;
		var activeSettingsContext = new DataContext.Context( _fullContextSwitches );

		if ( ( _activeSettingsContextLastFrame == null ) || ( activeSettingsContext.CompareTo( _activeSettingsContextLastFrame ) != 0 ) )
		{
			if ( !_waitingForFirstSessionInfo )
			{
				app.Logger.WriteLine( $"[Simulator] Active settings context changed to ({activeSettingsContext.SimulatorId}|{activeSettingsContext.WheelbaseGuid}|{activeSettingsContext.CarName}|{activeSettingsContext.TrackName}|{activeSettingsContext.TrackConfigurationName}|{activeSettingsContext.WetDryName})" );
				settings.UpdateSettings( false );
			}
		}

		_activeSettingsContextLastFrame = activeSettingsContext;
		_weatherDeclaredWetLastFrame = WeatherDeclaredWet;

		if ( ( CarScreenName != _carScreenNameLastFrame ) || ( TrackDisplayName != _trackDisplayNameLastFrame ) || ( TrackConfigName != _trackConfigNameLastFrame ) )
		{
			app.MainWindow.UpdateStatus();

			_carScreenNameLastFrame = CarScreenName;
			_trackDisplayNameLastFrame = TrackDisplayName;
			_trackConfigNameLastFrame = TrackConfigName;
		}

		if ( SelectedSimId == SimId.IRacing && ( _carIdxTireCompoundDatum != null ) && ( PlayerCarIdx >= 0 ) && ( PlayerCarIdx < _carIdxTireCompoundDatum.Count ) )
		{
			int[] carIdxTireCompounds = new int[ _carIdxTireCompoundDatum.Count ];

			_irsdk.Data.GetIntArray( _carIdxTireCompoundDatum, carIdxTireCompounds, 0, _carIdxTireCompoundDatum.Count );

			CurrentTireIndex = carIdxTireCompounds[ PlayerCarIdx ];

			if ( _currentTireIndexLastFrame != null )
			{
				if ( CurrentTireIndex != _currentTireIndexLastFrame )
				{
					UpdateTireProperties();
				}
			}

			_currentTireIndexLastFrame = CurrentTireIndex;
		}

		if ( IsOnTrack )
		{
			if ( ( settings.RacingWheelCrashProtectionDuration > 0f ) && ( settings.RacingWheelCrashProtectionForceReduction > 0f ) )
			{
				if ( ( settings.RacingWheelCrashProtectionLongitudalGForce < 20f ) && ( LongitudinalGForce >= settings.RacingWheelCrashProtectionLongitudalGForce ) )
				{
					app.RacingWheel.ActivateCrashProtection = true;
				}

				if ( ( settings.RacingWheelCrashProtectionLateralGForce < 20f ) && ( LateralGForce >= settings.RacingWheelCrashProtectionLateralGForce ) )
				{
					app.RacingWheel.ActivateCrashProtection = true;
				}
			}
		}

		if ( IsOnTrack )
		{
			if ( ( settings.RacingWheelCurbProtectionShockVelocity > 0f ) && ( settings.RacingWheelCurbProtectionDuration > 0f ) && ( settings.RacingWheelCurbProtectionForceReduction > 0f ) )
			{
				var maxShockVelocity = 0f;

				for ( var i = 0; i < SamplesPerFrame360Hz; i++ )
				{
					maxShockVelocity = MathF.Max( maxShockVelocity, MathF.Abs( CFShockVel_ST[ i ] ) );
					maxShockVelocity = MathF.Max( maxShockVelocity, MathF.Abs( CRShockVel_ST[ i ] ) );
					maxShockVelocity = MathF.Max( maxShockVelocity, MathF.Abs( LFShockVel_ST[ i ] ) );
					maxShockVelocity = MathF.Max( maxShockVelocity, MathF.Abs( LRShockVel_ST[ i ] ) );
					maxShockVelocity = MathF.Max( maxShockVelocity, MathF.Abs( RFShockVel_ST[ i ] ) );
					maxShockVelocity = MathF.Max( maxShockVelocity, MathF.Abs( RRShockVel_ST[ i ] ) );
				}

				if ( maxShockVelocity >= settings.RacingWheelCurbProtectionShockVelocity )
				{
					app.RacingWheel.ActivateCurbProtection = true;
				}
			}
		}

		if ( IsOnTrack && ( Gear > 0 ) && ( Clutch == 1f ) && ( RPM > 500f ) && ( VelocityX >= 10f * MathZ.MPHToMPS ) )
		{
			CurrentRpmSpeedRatio = VelocityX / RPM;

			if ( ( Brake == 0f ) && ( VelocityY < 0.1f ) && ( PlayerTrackSurface == IRacingSdkEnum.TrkLoc.OnTrack ) )
			{
				if ( RPMSpeedRatios[ Gear ] == 0f )
				{
					_rpmSpeedRatioAccumulator[ Gear ] += CurrentRpmSpeedRatio;
					_rpmSpeedRatioSampleCount[ Gear ]++;

					if ( _rpmSpeedRatioSampleCount[ Gear ] >= RpmSpeedRatioMinSamples )
					{
						RPMSpeedRatios[ Gear ] = _rpmSpeedRatioAccumulator[ Gear ] / _rpmSpeedRatioSampleCount[ Gear ];
						_rpmSpeedRatioAccumulator[ Gear ] = 0f;
						_rpmSpeedRatioSampleCount[ Gear ] = 0;
					}
				}
				else
				{
					var alpha = 1f - MathF.Exp( -deltaSeconds * 0.2f );
					RPMSpeedRatios[ Gear ] = MathZ.Lerp( RPMSpeedRatios[ Gear ], CurrentRpmSpeedRatio, alpha );
				}
			}
		}
		else
		{
			CurrentRpmSpeedRatio = 0f;
		}

		if ( IsOnTrack != WasOnTrack )
		{
			app.UpdateGripOMeterWindowVisibility();
			app.UpdateGapMonitorWindowVisibility();
		}

		app.SteeringEffects.Update( app, deltaSeconds );
		app.TriggerWorkerThread();
	}
}
