
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

using Accord.Math.Optimization;

using MarvinsAIRARefactored.Classes;

namespace MarvinsAIRARefactored.Components;

public class SteeringEffects
{
	public bool IsUndersteering { get; private set; } = false;
	public float UndersteerEffectIntensity { get; private set; } = 0f;
	public float MaximumGrip { get; private set; } = 0f; // if == 0 then there is no max grip
	public float CurrentGrip { get; private set; } = 0f;

	private enum Phase
	{
		NotCalibrating,
		ResetCalibration,
		DriveToWallEdge,
		WarmUpTires,
		DriveToActiveResetPoint,
		TurningPasses,
		Stop
	}

	private enum RobotMode
	{
		DriveToTarget,
		FixedSteeringWheelAngle
	}

	private const float KPHToMPS = 5f / 18f;
	private const float MPSToKPH = 18f / 5f;
	private const float RadiansToDegrees = 180f / MathF.PI;
	private const float DegreesToRadians = MathF.PI / 180f;

	private const float MapScale = 1.225f;

	private const float CarHomePositionX = -0.8f;
	private const float CarHomePositionY = -5.2f;

	private const float WarmUpTiresDrivingRadius = 190f;
	private const int WarmUpLaps = 10;
	private const int WarmUpTiresSpeedInKPH = 120;

	private const float ActiveResetSavePointX = 0f;
	private const float ActiveResetSavePointY = 0f;

	private const int MaxCalibrationProgress = 1 + WarmUpLaps + 1 + 17 + 17;
	private const int MaxSteeringWheelAngleInDegrees = 180;
	private const int MaxSpeedInKPH = 250;
	private const int MaxNumSteeringWheelAngles = 72;
	private const int SteeringWheelAngleIncrement = 10;

	private readonly string _calibrationDirectory = Path.Combine( App.DocumentsFolder, "Calibration" );

	private Phase _currentPhase = Phase.NotCalibrating;
	private int _calibrationProgress = 0;

	private int _currentWarmUpLapNumber = 0;

	private float _targetPositionX = 0f;
	private float _targetPositionY = 0f;
	private int _targetSteeringWheelAngleInDegrees = 0;
	private int _targetVelocityInKPH = 0;
	private int _targetAccelerationInKPH = 0;
	private float _targetDistanceToStop = 0f;

	private RobotMode _robotMode = RobotMode.DriveToTarget;

	private float _robotSettleTimer = 0f;
	private float _robotSteeringWheelAngleInDegrees = 0f;
	private float _robotBrake = 0f;
	private float _robotThrottle = 0f;
	private float _robotLastFrameVelocityX = 0f;
	private float _robotGearShiftTimer = 0f;

	private int _numSteeringWheelAnglesRecorded = 0;

	private float _maxAbsYawRateInDegrees = 0f;

	private readonly int[] _steeringWheelAnglesInDegrees = new int[ MaxNumSteeringWheelAngles ];
	private readonly float[,] _yawRateDataInDegrees = new float[ MaxNumSteeringWheelAngles, MaxSpeedInKPH + 1 ];

	private float _carResetPositionX = CarHomePositionX;
	private float _carResetPositionY = CarHomePositionY;

	private float _carPositionX = 0f;
	private float _carPositionY = 0f;

	private float[]? _leftCoefficients;
	private float[]? _rightCoefficients;

	private float _scaleTop = 0f;
	private float _scaleBottom = 0f;

	private readonly Cobyla _cobyla = new( 2, CobylaObjective );

	public void RunCalibration()
	{
		// start at the very beginning

		_currentPhase = Phase.ResetCalibration;
	}

	public void StopCalibration()
	{
		// whoa, nelly!

		_currentPhase = Phase.Stop;

		// save the calibration data

		SaveCalibration();

		// load the calibration data

		LoadCalibration();
	}

	public void Update( App app, float deltaSeconds )
	{
		if ( _currentPhase == Phase.NotCalibrating )
		{
			var settings = DataContext.DataContext.Instance.Settings;

			var steeringWheelAngleInDegrees = app.Simulator.SteeringWheelAngle * RadiansToDegrees;
			var prediction = Predict( steeringWheelAngleInDegrees );

			if ( prediction == 0f )
			{
				MaximumGrip = 0f;
			}
			else
			{
				MaximumGrip = Misc.Lerp( 0.5f, 1.0f, ( prediction - _scaleBottom ) / ( _scaleTop - _scaleBottom ) );
			}

			var peak = prediction * settings.SteeringEffectsUndersteerThreshold;
			var warn = prediction * settings.SteeringEffectsUndersteerWarningThreshold;

			if ( ( app.Simulator.VelocityX > 1f ) && ( MathF.Sign( app.Simulator.SteeringWheelAngle ) == MathF.Sign( app.Simulator.YawRate ) ) )
			{
				var speedInKPH = app.Simulator.VelocityX * MPSToKPH;
				var yawRateInDegrees = app.Simulator.YawRate * RadiansToDegrees;
				var absYawRateInDegrees = MathF.Abs( yawRateInDegrees );
				var current = MathF.Log( speedInKPH / ( absYawRateInDegrees + 1f ) );

				if ( peak > 0f )
				{
					CurrentGrip = ( current / peak ) * MaximumGrip;
					IsUndersteering = ( current > peak );

					var range = peak - warn;

					if ( range > 0f )
					{
						var intensity = Math.Clamp( ( current - warn ) / range, 0f, 1f );

						UndersteerEffectIntensity = MathF.Pow( intensity, Misc.CurveToPower( settings.SteeringEffectsUndersteerCurve ) );
					}
					else
					{
						UndersteerEffectIntensity = IsUndersteering ? 1f : 0f;
					}
				}
				else
				{
					CurrentGrip = 0f;
					IsUndersteering = false;
					UndersteerEffectIntensity = 0f;
				}
			}
			else
			{
				CurrentGrip = 0f;
				IsUndersteering = false;
				UndersteerEffectIntensity = 0f;
			}
		}
		else
		{
			switch ( _currentPhase )
			{
				case Phase.ResetCalibration:
					DoResetCalibration( app, deltaSeconds );
					break;

				case Phase.DriveToWallEdge:
					DoDriveToWallEdge( app, deltaSeconds );
					break;

				case Phase.WarmUpTires:
					DoWarmUpTires( app, deltaSeconds );
					break;

				case Phase.DriveToActiveResetPoint:
					DoDriveActiveResetSavePoint( app, deltaSeconds );
					break;

				case Phase.TurningPasses:
					DoTurningPasses( app, deltaSeconds );
					break;

				case Phase.Stop:
					DoStop( app, deltaSeconds );
					break;
			}

			var worldVelocityX = app.Simulator.VelocityX * MathF.Sin( app.Simulator.YawNorth ) - app.Simulator.VelocityY * MathF.Cos( app.Simulator.YawNorth );
			var worldVelocityY = app.Simulator.VelocityX * MathF.Cos( app.Simulator.YawNorth ) + app.Simulator.VelocityY * MathF.Sin( app.Simulator.YawNorth );

			_carPositionX += worldVelocityX * deltaSeconds;
			_carPositionY += worldVelocityY * deltaSeconds;

			CurrentGrip = 0f;
			IsUndersteering = false;
			UndersteerEffectIntensity = 0f;
		}
	}

	private float PredictNearestDistanceToTarget( App app, float yawRate, float deltaSeconds )
	{
		var posX = _carPositionX;
		var posY = _carPositionY;

		var nearestDistance = float.MaxValue;

		var yawNorth = app.Simulator.YawNorth;

		var maxLoops = (int) ( 20f / deltaSeconds );

		for ( var i = 0; i < maxLoops; i++ )
		{
			yawNorth -= yawRate * deltaSeconds;

			posX += ( app.Simulator.VelocityX * MathF.Sin( yawNorth ) - app.Simulator.VelocityY * MathF.Cos( yawNorth ) ) * deltaSeconds;
			posY += ( app.Simulator.VelocityX * MathF.Cos( yawNorth ) + app.Simulator.VelocityY * MathF.Sin( yawNorth ) ) * deltaSeconds;

			var deltaX = _targetPositionX - posX;
			var deltaY = _targetPositionY - posY;

			var distance = MathF.Sqrt( deltaX * deltaX + deltaY * deltaY );

			if ( distance > nearestDistance )
			{
				break;
			}
			else
			{
				nearestDistance = distance;
			}
		}

		return nearestDistance;
	}

	private void ResetRobot()
	{
		_robotSettleTimer = 0f;
		_robotSteeringWheelAngleInDegrees = 0f;
		_robotBrake = 0f;
		_robotThrottle = 0f;
		_robotLastFrameVelocityX = 0f;
	}

	private void UpdateRobot( App app, float deltaSeconds )
	{
		var currentAccelerationInMPS = ( app.Simulator.VelocityX - _robotLastFrameVelocityX ) / deltaSeconds; // in m*s^2

		if ( _robotSettleTimer < 1f )
		{
			// let the car settle before driving

			_robotSettleTimer = Math.Min( _robotSettleTimer + deltaSeconds, 1f );

			_robotBrake = 0f;
			_robotThrottle = 0f;
			_robotSteeringWheelAngleInDegrees = 0f;

			_carPositionX = _carResetPositionX;
			_carPositionY = _carResetPositionY;
		}
		else
		{
			if ( app.Simulator.Gear < app.Simulator.NumForwardGears )
			{
				// shift until we get into top gear

				_robotGearShiftTimer += deltaSeconds;

				if ( _robotGearShiftTimer >= 1f )
				{
					_robotGearShiftTimer = 0f;

					app.VirtualJoystick.ShiftUp = true;
				}
			}
			else
			{

				// adjust steering wheel

				if ( _robotMode == RobotMode.DriveToTarget )
				{
					var nearestDistanceToTargetTurningLeft = PredictNearestDistanceToTarget( app, app.Simulator.YawRate - 0.005f, deltaSeconds );
					var nearestDistanceToTargetTurningRight = PredictNearestDistanceToTarget( app, app.Simulator.YawRate + 0.005f, deltaSeconds );

					var wheelTurnAmount = ( nearestDistanceToTargetTurningRight - nearestDistanceToTargetTurningLeft ) * 0.15f;

					_robotSteeringWheelAngleInDegrees += Math.Clamp( wheelTurnAmount, -0.25f, 0.25f ) * Math.Min( 1f, app.Simulator.VelocityX * 0.25f );
				}
				else
				{
					var deltaSteeringWheelAngleInDegrees = _targetSteeringWheelAngleInDegrees - _robotSteeringWheelAngleInDegrees;

					if ( MathF.Abs( deltaSteeringWheelAngleInDegrees ) < 0.5f )
					{
						_robotSteeringWheelAngleInDegrees = _targetSteeringWheelAngleInDegrees;
					}
					else
					{
						_robotSteeringWheelAngleInDegrees = Misc.Lerp( _robotSteeringWheelAngleInDegrees, _targetSteeringWheelAngleInDegrees, 0.15f );
					}
				}

				// adjust throttle and brake

				if ( ( _targetVelocityInKPH == 0 ) && ( _targetAccelerationInKPH == 0 ) )
				{
					_robotThrottle -= deltaSeconds; // go from 100% to 0% throttle in one second

					var currentDistanceToStop = -( ( app.Simulator.VelocityX * app.Simulator.VelocityX ) / ( 2f * currentAccelerationInMPS ) ); // how far will the car go before we come to a complete stop at the current acceleration?

					var deltaDistanceToStop = _targetDistanceToStop - currentDistanceToStop;

					if ( ( currentAccelerationInMPS > 0f ) || ( deltaDistanceToStop < 0f ) )
					{
						_robotBrake += MathF.Min( deltaSeconds / 2f, -deltaDistanceToStop * 0.01f ); // increase brake (take 2 seconds to go from 0% to 100% brake)
					}
					else
					{
						_robotBrake -= deltaSeconds / 2f; // ease off the brake (take 2 seconds to go from 100% to 0% brake)
					}
				}
				else if ( _targetAccelerationInKPH != 0 )
				{
					var deltaAccelerationInKPH = _targetAccelerationInKPH - currentAccelerationInMPS * MPSToKPH;

					_robotThrottle += Math.Clamp( deltaAccelerationInKPH * deltaSeconds, -deltaSeconds / 30f, deltaSeconds / 30f );
				}
				else if ( _targetVelocityInKPH != 0 )
				{
					var deltaTargetVelocityInKPH = _targetVelocityInKPH - app.Simulator.VelocityX * MPSToKPH;

					var targetAccelerationInKPH = Math.Clamp( deltaTargetVelocityInKPH, -0.1f, 1f ); // 1 KPH is the target acceleration

					var deltaAccelerationInKPH = targetAccelerationInKPH - currentAccelerationInMPS * MPSToKPH;

					_robotThrottle += Math.Clamp( deltaAccelerationInKPH * deltaSeconds, -deltaSeconds / 30f, deltaSeconds / 30f );
				}
			}
		}

		// update virtual joystick

		_robotSteeringWheelAngleInDegrees = Math.Clamp( _robotSteeringWheelAngleInDegrees, -450f, 450f );
		_robotBrake = Math.Clamp( _robotBrake, 0f, 1f );
		_robotThrottle = Math.Clamp( _robotThrottle, 0f, 1f );
		_robotLastFrameVelocityX = app.Simulator.VelocityX;

		app.VirtualJoystick.Steering = _robotSteeringWheelAngleInDegrees / 450f;
		app.VirtualJoystick.Brake = _robotBrake;
		app.VirtualJoystick.Throttle = _robotThrottle;

		// debug

		app.Debug.Label_1 = $"deltaSeconds: {deltaSeconds:F6}";

		app.Debug.Label_3 = $"velocityX: {app.Simulator.VelocityX * MPSToKPH:F2}";
		app.Debug.Label_4 = $"robotLastFrameVelocityX: {_robotLastFrameVelocityX * MPSToKPH:F2}";

		app.Debug.Label_6 = $"currentAcceleration: {currentAccelerationInMPS * MPSToKPH:F3}";

		app.Debug.Label_8 = $"robotThrottle: {_robotThrottle * 100f:F0}%";
		app.Debug.Label_9 = $"robotBrake: {_robotBrake * 100f:F0}%";
	}

	private void DoResetCalibration( App app, float deltaSeconds )
	{
		// reset calibration progress

		_calibrationProgress = 0;

		// clear out our old calibration data

		_numSteeringWheelAnglesRecorded = 0;

		Array.Clear( _steeringWheelAnglesInDegrees );
		Array.Clear( _yawRateDataInDegrees );

		// reset the position of the car

		_carResetPositionX = CarHomePositionX;
		_carResetPositionY = CarHomePositionY;

		// reset robot

		ResetRobot();

		// next phase

		_currentPhase = Phase.DriveToWallEdge;
	}

	private void DoDriveToWallEdge( App app, float deltaSeconds )
	{
		// set target position and velocity

		_targetPositionX = WarmUpTiresDrivingRadius;
		_targetPositionY = CarHomePositionY;
		_targetVelocityInKPH = 50;
		_targetAccelerationInKPH = 0;
		_targetDistanceToStop = 0f;

		// check if we're getting close and if so, go to the next phase

		if ( _carPositionX >= 50f )
		{
			_calibrationProgress++;

			_currentWarmUpLapNumber = 0;

			_currentPhase = Phase.WarmUpTires;
		}

		// update robot

		UpdateRobot( app, deltaSeconds );
	}

	private void DoWarmUpTires( App app, float deltaSeconds )
	{
		// remember our original target position

		var originalTargetPositionY = _targetPositionY;

		// set target position and velocity

		var distance = MathF.Sqrt( _carPositionX * _carPositionX + _carPositionY * _carPositionY );

		_targetPositionX = _carPositionX * WarmUpTiresDrivingRadius / distance;
		_targetPositionY = _carPositionY * WarmUpTiresDrivingRadius / distance;

		float radians = 20f * DegreesToRadians;

		float cosTheta = MathF.Cos( radians );
		float sinTheta = MathF.Sin( radians );

		float rotatedX = _targetPositionX * cosTheta - _targetPositionY * sinTheta;
		float rotatedY = _targetPositionX * sinTheta + _targetPositionY * cosTheta;

		_targetPositionX = rotatedX;
		_targetPositionY = rotatedY;

		_targetVelocityInKPH = WarmUpTiresSpeedInKPH;
		_targetAccelerationInKPH = 0;
		_targetDistanceToStop = 0f;

		// check if we are done running warm up laps

		if ( _carPositionX > 0 )
		{
			if ( MathF.Sign( _targetPositionY ) != MathF.Sign( originalTargetPositionY ) )
			{
				_calibrationProgress++;
				_currentWarmUpLapNumber++;

				if ( _currentWarmUpLapNumber > WarmUpLaps )
				{
					_currentPhase = Phase.DriveToActiveResetPoint;
				}
			}
		}

		// update robot

		UpdateRobot( app, deltaSeconds );
	}

	private void DoDriveActiveResetSavePoint( App app, float deltaSeconds )
	{
		// set target position

		_targetPositionX = ActiveResetSavePointX;
		_targetPositionY = ActiveResetSavePointY;

		// set target velocity and acceleration

		_targetVelocityInKPH = 0;
		_targetAccelerationInKPH = 0;

		// set target distance to stop

		var deltaX = _targetPositionX - _carPositionX;
		var deltaY = _targetPositionY - _carPositionY;

		_targetDistanceToStop = MathF.Sqrt( deltaX * deltaX + deltaY * deltaY );

		// when we have stopped, go to the next phase

		if ( app.Simulator.VelocityX < 0.005f )
		{
			// hit the active reset save button

			app.VirtualJoystick.ActiveResetSave = true;

			// save the reset position

			_carResetPositionX = _carPositionX;
			_carResetPositionY = _carPositionY;

			// prepare for the first pass

			_robotSettleTimer = 0f;
			_targetSteeringWheelAngleInDegrees = -MaxSteeringWheelAngleInDegrees;
			_steeringWheelAnglesInDegrees[ _numSteeringWheelAnglesRecorded ] = _targetSteeringWheelAngleInDegrees;
			_targetVelocityInKPH = 1;
			_targetAccelerationInKPH = 1;
			_targetDistanceToStop = 0f;
			_maxAbsYawRateInDegrees = 0f;

			// tell robot to use fixed steering wheel angle

			_robotMode = RobotMode.FixedSteeringWheelAngle;

			// and we're off

			_calibrationProgress++;

			_currentPhase = Phase.TurningPasses;
		}

		// update robot

		UpdateRobot( app, deltaSeconds );
	}

	private void DoTurningPasses( App app, float deltaSeconds )
	{
		if ( _robotSettleTimer >= 1f )
		{
			// get our current abs yaw rate in degrees

			var absYawRateInDegrees = MathF.Abs( app.Simulator.YawRate * RadiansToDegrees );

			// figure out if we have crashed (or is likely to crash)

			var crashed = app.Simulator.GForce >= 2f || ( MathF.Sqrt( _carPositionX * _carPositionX + _carPositionY * _carPositionY ) > 195f );

			// check if we've reached our target speed

			if ( !crashed && ( app.Simulator.VelocityX >= _targetVelocityInKPH * KPHToMPS ) )
			{
				// yes - save the data

				_yawRateDataInDegrees[ _numSteeringWheelAnglesRecorded, _targetVelocityInKPH ] = app.Simulator.YawRate * RadiansToDegrees;

				// update max abs yaw rate

				_maxAbsYawRateInDegrees = MathF.Max( _maxAbsYawRateInDegrees, absYawRateInDegrees );

				// bump up the target speed

				_targetVelocityInKPH++;
			}

			// check if we are done with this pass

			if ( crashed || ( _targetVelocityInKPH > MaxSpeedInKPH ) || ( MathF.Abs( app.Simulator.VelocityY ) > 1.5f ) || ( ( app.Simulator.VelocityX >= ( 40f * KPHToMPS ) ) && ( absYawRateInDegrees < ( _maxAbsYawRateInDegrees * 0.9f ) ) ) )
			{
				// update calibration progress

				_calibrationProgress++;

				// increase num steering wheel angles recorded

				_numSteeringWheelAnglesRecorded++;

				// hit the active reset run button

				app.VirtualJoystick.ActiveResetRun = true;

				// prepare for the next pass

				if ( _targetSteeringWheelAngleInDegrees < 0 )
				{
					_targetSteeringWheelAngleInDegrees = -_targetSteeringWheelAngleInDegrees;
				}
				else
				{
					_targetSteeringWheelAngleInDegrees = -_targetSteeringWheelAngleInDegrees + SteeringWheelAngleIncrement;
				}

				_steeringWheelAnglesInDegrees[ _numSteeringWheelAnglesRecorded ] = _targetSteeringWheelAngleInDegrees;
				_targetVelocityInKPH = 1;
				_targetAccelerationInKPH = 1;
				_targetDistanceToStop = 0f;
				_maxAbsYawRateInDegrees = 0f;

				// reset the robot

				ResetRobot();

				// detect if it's time to stop

				if ( _targetSteeringWheelAngleInDegrees == -10 )
				{
					StopCalibration();
				}
			}
		}

		// update robot

		UpdateRobot( app, deltaSeconds );
	}

	private void DoStop( App app, float deltaSeconds )
	{
		app.VirtualJoystick.Steering = 0f;
		app.VirtualJoystick.Throttle = 0f;
		app.VirtualJoystick.Brake = 1f;

		if ( app.Simulator.VelocityX <= 0.005f )
		{
			app.VirtualJoystick.Brake = 0f;

			_currentPhase = Phase.NotCalibrating;
		}
	}

	private string GetPreferredCalibrationFileName()
	{
		// TODO update this code so the user can override the calibration file name

		var app = App.Instance!;

		var carSetupName = Path.GetFileNameWithoutExtension( app.Simulator.CarSetupName );

		var filePath = Path.Combine( _calibrationDirectory, $"{app.Simulator.CarScreenName} - {app.Simulator.CarSetupLoadTypeName} - {carSetupName} - {app.Simulator.CarSetupTireType}.csv" );

		return filePath;
	}

	private void SaveCalibration()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[SteeringEffects] SaveCalibration >>>" );

		// create directory if it does not exist

		if ( !Directory.Exists( _calibrationDirectory ) )
		{
			Directory.CreateDirectory( _calibrationDirectory );
		}

		// open file

		var filePath = GetPreferredCalibrationFileName();

		using var writer = new StreamWriter( filePath );

		// write car name and calibration

		writer.WriteLine( $"{app.Simulator.CarScreenName},{app.Simulator.CarSetupLoadTypeName},{app.Simulator.CarSetupName},{app.Simulator.CarSetupTireType}" );

		// write header row

		var headerString = "Speed (KPH)";

		for ( var angleIndex = 0; angleIndex < _numSteeringWheelAnglesRecorded; angleIndex++ )
		{
			headerString += $",{_steeringWheelAnglesInDegrees[ angleIndex ]}";
		}

		writer.WriteLine( headerString );

		// write data rows

		for ( var speed = 0; speed <= MaxSpeedInKPH; speed++ )
		{
			var dataString = $"{speed}";

			for ( var angleIndex = 0; angleIndex < _numSteeringWheelAnglesRecorded; angleIndex++ )
			{
				if ( _yawRateDataInDegrees[ angleIndex, speed ] == 0f )
				{
					dataString += $",";
				}
				else
				{
					dataString += "," + _yawRateDataInDegrees[ angleIndex, speed ].ToString( "F6", CultureInfo.InvariantCulture );
				}
			}

			writer.WriteLine( dataString );
		}

		app.Logger.WriteLine( "[SteeringEffects] <<< SaveCalibration" );
	}

	public void LoadCalibration()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[SteeringEffects] LoadCalibration >>>" );

		if ( _currentPhase == Phase.NotCalibrating )
		{
			// clear out the data tables

			_numSteeringWheelAnglesRecorded = 0;

			Array.Clear( _steeringWheelAnglesInDegrees );
			Array.Clear( _yawRateDataInDegrees );

			// keep track of whether the file load was good or not

			var fileLoadWasSuccessful = true;

			// open file

			var filePath = GetPreferredCalibrationFileName();

			if ( !File.Exists( filePath ) )
			{
				app.Logger.WriteLine( $"[SteeringEffects] Calibration file not found: {filePath}" );

				fileLoadWasSuccessful = false;
			}
			else
			{
				using var reader = new StreamReader( filePath );

				// skip the first line

				var carInfoLine = reader.ReadLine();

				// read header line and extract steering wheel angles

				var headerLine = reader.ReadLine();

				if ( string.IsNullOrWhiteSpace( headerLine ) )
				{
					app.Logger.WriteLine( "[SteeringEffects] Missing header line." );

					fileLoadWasSuccessful = false;
				}
				else
				{
					var headerParts = headerLine.Split( ',' );

					if ( headerParts.Length < 2 )
					{
						app.Logger.WriteLine( "[SteeringEffects] Invalid header line." );

						fileLoadWasSuccessful = false;
					}
					else
					{
						var angleLabels = headerParts.Skip( 1 ).ToList();

						_numSteeringWheelAnglesRecorded = angleLabels.Count;

						for ( var angleIndex = 0; angleIndex < _numSteeringWheelAnglesRecorded; angleIndex++ )
						{
							if ( !int.TryParse( angleLabels[ angleIndex ].Trim(), out _steeringWheelAnglesInDegrees[ angleIndex ] ) )
							{
								app.Logger.WriteLine( "[SteeringEffects] Failed to parse initial steering wheel angle." );

								fileLoadWasSuccessful = false;

								break;
							}
						}

						// read yaw rate data

						if ( fileLoadWasSuccessful )
						{
							while ( !reader.EndOfStream )
							{
								var line = reader.ReadLine();

								if ( string.IsNullOrWhiteSpace( line ) ) continue;

								var parts = line.Split( ',' );

								if ( !int.TryParse( parts[ 0 ], out var speedInKPH ) ) continue;

								if ( speedInKPH > MaxSpeedInKPH )
								{
									app.Logger.WriteLine( "[SteeringEffects] Invalid speed." );

									fileLoadWasSuccessful = false;

									break;
								}

								for ( var angleIndex = 0; angleIndex < _numSteeringWheelAnglesRecorded; angleIndex++ )
								{
									var partIndex = angleIndex + 1;

									if ( partIndex >= parts.Length ) break;

									if ( float.TryParse( parts[ partIndex ], NumberStyles.Float, CultureInfo.InvariantCulture, out var yawRate ) )
									{
										_yawRateDataInDegrees[ angleIndex, speedInKPH ] = yawRate;
									}
									else
									{
										_yawRateDataInDegrees[ angleIndex, speedInKPH ] = 0f;
									}
								}
							}
						}
					}
				}
			}

			if ( fileLoadWasSuccessful )
			{
				// find the best coefficients to use for predicting the peak yaw rates and corresponding speeds

				var yawRateModel = new YawRateModel( _steeringWheelAnglesInDegrees, _yawRateDataInDegrees, MaxSpeedInKPH );

				var (leftYawCoefficients, leftSpeedCoefficients) = yawRateModel.FitWithProgressiveRefinement( true );
				var (rightYawCoefficients, rightSpeedCoefficients) = yawRateModel.FitWithProgressiveRefinement( false );

				// allocate data arrays for curve fitting

				var numAngles = 160;

				var leftAngles = new double[ numAngles ];
				var rightAngles = new double[ numAngles ];

				var leftValues = new double[ numAngles ];
				var rightValues = new double[ numAngles ];

				// fill out the data arrays

				for ( var angleIndex = 0; angleIndex < numAngles; angleIndex++ )
				{
					var angle = angleIndex + 20;

					leftAngles[ angleIndex ] = angle;
					rightAngles[ angleIndex ] = angle;

					var maxYawRate = YawRateModel.Predict( leftYawCoefficients, -angle );
					var correspondingSpeed = YawRateModel.Predict( leftSpeedCoefficients, -angle );

					leftValues[ angleIndex ] = correspondingSpeed / ( MathF.Abs( maxYawRate ) + 1f );

					maxYawRate = YawRateModel.Predict( rightYawCoefficients, angle );
					correspondingSpeed = YawRateModel.Predict( rightSpeedCoefficients, angle );

					rightValues[ angleIndex ] = correspondingSpeed / ( MathF.Abs( maxYawRate ) + 1f );
				}

				//

				float[] calculateCoefficients( int numAngles, double[] inAngles, double[] inValues )
				{
					// trim the data arrays

					double[] angles = [ .. inAngles.Take( numAngles ) ];
					double[] values = [ .. inValues.Take( numAngles ) ];

					double[] logValues = [ .. values.Select( v => Math.Log( v ) ) ];

					// prep Cobyla objective function

					_cobylaAngles = angles;
					_cobylaValues = logValues;

					// initial guess

					double[] coefficients = [ 150, -5 ];

					// run optimizer (modifies initial guess in place)

					var success = _cobyla.Minimize( coefficients );

					return [ (float) coefficients[ 0 ], (float) coefficients[ 1 ] ];
				}

				// get our coefficients

				_leftCoefficients = calculateCoefficients( numAngles, leftAngles, leftValues );
				_rightCoefficients = calculateCoefficients( numAngles, rightAngles, rightValues );

				// figure out a good scale to use

				var leftPrediction = Predict( MathF.Min( 0, _leftCoefficients[ 1 ] ) - 1f );
				var rightPrediction = Predict( 1f - MathF.Min( 0, _rightCoefficients[ 1 ] ) );

				_scaleTop = MathF.Max( leftPrediction, rightPrediction );

				leftPrediction = Predict( -180f );
				rightPrediction = Predict( 180f );

				_scaleBottom = MathF.Min( leftPrediction, rightPrediction );
			}
		}

		//

		app.Logger.WriteLine( "[SteeringEffects] <<< LoadCalibration" );
	}

	private static double[] _cobylaAngles = [];
	private static double[] _cobylaValues = [];

	private static double CobylaObjective( double[] parameters )
	{
		var a = parameters[ 0 ];
		var b = parameters[ 1 ];

		var error = 0.0;

		for ( var angleIndex = 0; angleIndex < _cobylaAngles.Length; angleIndex++ )
		{
			double angle = _cobylaAngles[ angleIndex ];
			double prediction = Math.Log( a ) - Math.Log( angle + b ); // log-space model converges better
			double residual = prediction - _cobylaValues[ angleIndex ];

			error += residual * residual;
		}

		return error;
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private float Predict( float steeringWheelAngle )
	{
		if ( ( _leftCoefficients != null ) && ( _rightCoefficients != null ) )
		{
			var absSteeringWheelAngle = MathF.Abs( steeringWheelAngle );

			if ( steeringWheelAngle < 0f )
			{
				var denominator = absSteeringWheelAngle + _leftCoefficients[ 1 ];

				if ( denominator > 0 )
				{
					return MathF.Log( _leftCoefficients[ 0 ] ) - MathF.Log( denominator );
				}
			}
			else
			{
				var denominator = absSteeringWheelAngle + _rightCoefficients[ 1 ];

				if ( denominator > 0 )
				{
					return MathF.Log( _rightCoefficients[ 0 ] ) - MathF.Log( denominator );
				}
			}
		}

		return 0f;
	}

	public void Tick( App app )
	{
		var localization = DataContext.DataContext.Instance.Localization;

		if ( app.MainWindow.SteeringEffectsTabItemIsVisible )
		{
			app.MainWindow.SteeringEffects_Calibration_Phase_Label.Content = $"{localization[ "Phase:" ]} {localization[ _currentPhase.ToString() ]}";
			app.MainWindow.SteeringEffects_Calibration_Progress_Label.Content = $"{localization[ "Progress:" ]} {_calibrationProgress * 100f / MaxCalibrationProgress:F0}{localization[ "Percent" ]}";

			app.MainWindow.SteeringEffects_Calibration_Brake_Label.Content = $"{localization[ "Brake:" ]} {_robotBrake * 100f:F0}{localization[ "Percent" ]}";
			app.MainWindow.SteeringEffects_Calibration_Throttle_Label.Content = $"{localization[ "Throttle:" ]} {_robotThrottle * 100f:F0}{localization[ "Percent" ]}";
			app.MainWindow.SteeringEffects_Calibration_SteeringWheel_Label.Content = $"{localization[ "SteeringWheel:" ]} {_robotSteeringWheelAngleInDegrees:F0}{localization[ "Degrees" ]}";

			app.MainWindow.SteeringEffects_Calibration_YawRate_Label.Content = $"{localization[ "YawRate:" ]} {app.Simulator.YawRate * RadiansToDegrees,6:F1}";

			app.MainWindow.SteeringEffects_Calibration_CarPositionX_Label.Content = $"{localization[ "CarPositionX:" ]} {_carPositionX,6:F1}";
			app.MainWindow.SteeringEffects_Calibration_CarPositionY_Label.Content = $"{localization[ "CarPositionY:" ]} {_carPositionY,6:F1}";

			app.MainWindow.SteeringEffects_Calibration_Heading_Label.Content = $"{localization[ "Heading:" ]} {app.Simulator.YawNorth * RadiansToDegrees,6:F1}";

			app.MainWindow.SteeringEffects_Calibration_VelocityX_Label.Content = $"{localization[ "VelocityX:" ]} {app.Simulator.VelocityX * MPSToKPH,6:F1}";
			app.MainWindow.SteeringEffects_Calibration_VelocityY_Label.Content = $"{localization[ "VelocityY:" ]} {app.Simulator.VelocityY * MPSToKPH,6:F1}";

			var transformGroup = new TransformGroup
			{
				Children =
				[
					new RotateTransform( app.Simulator.YawNorth * RadiansToDegrees ),
					new TranslateTransform( _carPositionX * MapScale, _carPositionY * -MapScale )
				]
			};

			app.MainWindow.SteeringEffects_RaceCar_Image.RenderTransform = transformGroup;

			if ( _robotMode == RobotMode.DriveToTarget )
			{
				app.MainWindow.SteeringEffects_TargetPosition_Image.RenderTransform = new TranslateTransform( _targetPositionX * MapScale, _targetPositionY * -MapScale );
				app.MainWindow.SteeringEffects_TargetPosition_Image.Visibility = Visibility.Visible;
			}
			else
			{
				app.MainWindow.SteeringEffects_TargetPosition_Image.Visibility = Visibility.Hidden;
			}

			var disableButtons = ( app.Simulator.TrackDisplayName != "Centripetal Circuit" );

			if ( _currentPhase == Phase.NotCalibrating )
			{
				app.MainWindow.SteeringEffects_RunCalibration_MairaButton.Disabled = false;
				app.MainWindow.SteeringEffects_StopCalibration_MairaButton.Disabled = !disableButtons;
			}
			else
			{
				app.MainWindow.SteeringEffects_RunCalibration_MairaButton.Disabled = !disableButtons;
				app.MainWindow.SteeringEffects_StopCalibration_MairaButton.Disabled = false;
			}
		}
	}
}
