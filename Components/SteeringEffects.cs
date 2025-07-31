
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

using Accord.Math.Optimization;

namespace MarvinsAIRARefactored.Components;

public class SteeringEffects
{
	public float UndersteerEffectIntensity { get; private set; } = 0f;

	private enum Phase
	{
		NotCalibrating,
		ResetCalibration,
		DriveToWallEdge,
		WarmUpTires,
		DriveToActiveResetPoint,
		TurningPasses,
		UTurn,
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

	private const float DeltaTime = 1f / 60f;
	private const float MapScale = 1.225f;

	private const float CarHomePositionX = -1f;
	private const float CarHomePositionY = -5f;

	private const float WarmUpTiresDrivingRadius = 190f;
	private const float WarmUpLaps = 3;
	private const int WarmUpTiresSpeedInKPH = 150;

	private const float ActiveResetSavePointX = -25f;
	private const float ActiveResetSavePointY = 125f;

	private const int MaxSteeringWheelAngleInDegrees = 180;
	private const int MaxSpeedInKPH = 250;
	private const int MaxNumSteeringWheelAngles = 72;

	private const int StartingSteeringWheelAngle = 10;

	private readonly string _calibrationDirectory = Path.Combine( App.DocumentsFolder, "Calibration" );

	private Phase _currentPhase = Phase.NotCalibrating;

	private int _currentWarmUpLapNumber = 0;

	private float _targetPositionX = 0f;
	private float _targetPositionY = 0f;
	private int _targetSteeringWheelAngleInDegrees = 0;
	private int _targetVelocityInKPH = 0;

	private RobotMode _robotMode = RobotMode.DriveToTarget;

	private float _robotSettleTimer = 0f;
	private float _robotSteeringWheelAngleInDegrees = 0f;
	private float _robotBrake = 0f;
	private float _robotThrottle = 0f;
	private float _robotLastFrameVelocityX = 0f;
	private float _robotGearShiftTimer = 0f;

	private int _numSteeringWheelAnglesRecorded = 0;
	private int _steeringWheelDirection = 0;
	private int _currentSteeringWheelInterval = 0;

	private float _maxAbsYawRateInDegrees = 0f;

	private readonly int[] _steeringWheelAnglesInDegrees = new int[ MaxNumSteeringWheelAngles ];
	private readonly float[,] _yawRateDataInDegrees = new float[ MaxNumSteeringWheelAngles, MaxSpeedInKPH + 1 ];

	private float _carResetPositionX = CarHomePositionX;
	private float _carResetPositionY = CarHomePositionY;

	private float _carPositionX = 0f;
	private float _carPositionY = 0f;

	private double[]? _leftCoefficients;
	private double[]? _rightCoefficients;

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

		// save the data

		SaveCalibration();
	}

	public void Update( App app, float deltaSeconds )
	{
		if ( _currentPhase == Phase.NotCalibrating )
		{
			var steeringWheelAngleInDegrees = app.Simulator.SteeringWheelAngle * RadiansToDegrees;
			var yawRateInDegrees = app.Simulator.YawRate * RadiansToDegrees;
			var absYawRateInDegrees = MathF.Abs( yawRateInDegrees );
			var speedInKPH = app.Simulator.VelocityX * MPSToKPH;

			app.Debug.Label_1 = $"Steering Wheel Angle: {steeringWheelAngleInDegrees:F0} Degrees";
			app.Debug.Label_2 = $"Abs Yaw Rate: {absYawRateInDegrees:F1} Degrees";
			app.Debug.Label_3 = $"Speed: {speedInKPH:F0} KPH";

			var peak = Predict( steeringWheelAngleInDegrees );
			var warn = peak * 0.9f;

			var current = MathF.Log( speedInKPH / ( absYawRateInDegrees + 1f ) );

			app.Debug.Label_5 = $"Peak: {peak:F6}";
			app.Debug.Label_6 = $"Warn: {warn:F6}";
			app.Debug.Label_7 = $"Current: {current:F6}";

			if ( ( app.Simulator.VelocityX > 0f ) && ( warn > 0f ) && ( current > warn ) && ( MathF.Sign( steeringWheelAngleInDegrees ) == MathF.Sign( yawRateInDegrees ) ) )
			{
				UndersteerEffectIntensity = Math.Clamp( ( current - warn ) / ( peak - warn ), 0f, 1f );

				app.Debug.Label_9 = $"Effect Intensity: {UndersteerEffectIntensity * 100f:F0}%";
			}
			else
			{
				UndersteerEffectIntensity = 0f;

				app.Debug.Label_9 = $"Effect Intensity: ---";
			}
		}
		else
		{
			var worldVelocityX = app.Simulator.VelocityX * MathF.Sin( app.Simulator.YawNorth ) - app.Simulator.VelocityY * MathF.Cos( app.Simulator.YawNorth );
			var worldVelocityY = app.Simulator.VelocityX * MathF.Cos( app.Simulator.YawNorth ) + app.Simulator.VelocityY * MathF.Sin( app.Simulator.YawNorth );

			_carPositionX += worldVelocityX * deltaSeconds;
			_carPositionY += worldVelocityY * deltaSeconds;
		}
	}

	private float PredictNearestDistanceToTarget( App app, float yawRate, float timeStep )
	{
		var posX = _carPositionX;
		var posY = _carPositionY;

		var nearestDistance = float.MaxValue;

		var yawNorth = app.Simulator.YawNorth;

		while ( true )
		{
			yawNorth -= yawRate * timeStep;

			posX += ( app.Simulator.VelocityX * MathF.Sin( yawNorth ) - app.Simulator.VelocityY * MathF.Cos( yawNorth ) ) * timeStep;
			posY += ( app.Simulator.VelocityX * MathF.Cos( yawNorth ) + app.Simulator.VelocityY * MathF.Sin( yawNorth ) ) * timeStep;

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

	private void UpdateRobot( App app )
	{
		if ( _robotSettleTimer < 1f )
		{
			// let the car settle before driving

			_robotSettleTimer = Math.Min( _robotSettleTimer + DeltaTime, 1f );

			_robotBrake = 1f;
			_robotThrottle = 0f;
			_robotSteeringWheelAngleInDegrees = 0f;

			_carPositionX = _carResetPositionX;
			_carPositionY = _carResetPositionY;
		}
		else
		{
			// adjust gear

			if ( app.Simulator.Gear < app.Simulator.NumForwardGears )
			{
				_robotGearShiftTimer += DeltaTime;

				if ( _robotGearShiftTimer >= 1f )
				{
					_robotGearShiftTimer = 0f;

					app.VirtualJoystick.ShiftUp = true;
				}
			}

			// adjust steering wheel

			if ( _robotMode == RobotMode.DriveToTarget )
			{
				var nearestDistanceToTargetTurningLeft = PredictNearestDistanceToTarget( app, app.Simulator.YawRate - 0.005f, 0.01f );
				var nearestDistanceToTargetTurningRight = PredictNearestDistanceToTarget( app, app.Simulator.YawRate + 0.005f, 0.01f );

				var wheelTurnAmount = ( nearestDistanceToTargetTurningRight - nearestDistanceToTargetTurningLeft ) * 0.15f;

				_robotSteeringWheelAngleInDegrees += Math.Clamp( wheelTurnAmount, -0.25f, 0.25f ) * Math.Min( 1f, app.Simulator.VelocityX * 0.25f );
			}
			else
			{
				_robotSteeringWheelAngleInDegrees = _targetSteeringWheelAngleInDegrees;
			}

			if ( app.Simulator.Gear == app.Simulator.NumForwardGears )
			{
				// should we be speeding up or slowing down?

				var deltaTargetVelocity = _targetVelocityInKPH * KPHToMPS - app.Simulator.VelocityX;

				// adjust brake

				if ( deltaTargetVelocity < 0 )
				{
					_robotBrake = Math.Min( _robotBrake - deltaTargetVelocity * 0.0025f, 0.25f );
				}
				else
				{
					_robotBrake -= 0.01f;
				}

				// adjust throttle

				var currentAcceleration = app.Simulator.VelocityX - _robotLastFrameVelocityX;

				_robotLastFrameVelocityX = app.Simulator.VelocityX;

				if ( deltaTargetVelocity > 0f )
				{
					if ( currentAcceleration <= 0.01f )
					{
						if ( _robotBrake <= 0f )
						{
							_robotThrottle += 0.0005f;
						}
					}
				}
				else
				{
					_robotThrottle += deltaTargetVelocity * 0.0015f;
				}
			}
		}

		// update virtual joystick

		_robotSteeringWheelAngleInDegrees = Math.Clamp( _robotSteeringWheelAngleInDegrees, -450f, 450f );
		_robotBrake = Math.Clamp( _robotBrake, 0f, 1f );
		_robotThrottle = Math.Clamp( _robotThrottle, 0f, 1f );

		app.VirtualJoystick.Steering = _robotSteeringWheelAngleInDegrees / 450f;
		app.VirtualJoystick.Brake = _robotBrake;
		app.VirtualJoystick.Throttle = _robotThrottle;
	}

	private void DoResetCalibration( App app )
	{
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

	private void DoDriveToWallEdge( App app )
	{
		// set target position and velocity

		_targetPositionX = WarmUpTiresDrivingRadius;
		_targetPositionY = CarHomePositionY;
		_targetVelocityInKPH = 30;

		// check if we're getting close and if so, go to the next phase

		if ( _carPositionX >= 50f )
		{
			_currentWarmUpLapNumber = 0;

			_currentPhase = Phase.WarmUpTires;
		}

		// update robot

		UpdateRobot( app );
	}

	private void DoWarmUpTires( App app )
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

		// check if we are done running warm up laps

		if ( _carPositionX > 0 )
		{
			if ( MathF.Sign( _targetPositionY ) != MathF.Sign( originalTargetPositionY ) )
			{
				_currentWarmUpLapNumber++;

				if ( _currentWarmUpLapNumber > WarmUpLaps )
				{
					_currentPhase = Phase.DriveToActiveResetPoint;
				}
			}
		}

		// update robot

		UpdateRobot( app );
	}

	private void DoDriveActiveResetSavePoint( App app )
	{
		// set target position

		if ( _carPositionX < 50f )
		{
			_targetPositionY = _carPositionY;
		}
		else
		{
			_targetPositionY = ActiveResetSavePointY;
		}

		_targetPositionX = ActiveResetSavePointX;

		// set target velocity

		if ( _carPositionX > _targetPositionX )
		{
			var deltaX = _targetPositionX - _carPositionX;
			var deltaY = _targetPositionY - _carPositionY;

			var distance = MathF.Sqrt( deltaX * deltaX + deltaY * deltaY );

			_targetVelocityInKPH = Math.Min( (int) MathF.Ceiling( distance * 0.35f ), WarmUpTiresSpeedInKPH );
		}
		else
		{
			_targetVelocityInKPH = 0;
		}

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
			_steeringWheelDirection = -1;
			_targetSteeringWheelAngleInDegrees = StartingSteeringWheelAngle * _steeringWheelDirection;
			_steeringWheelAnglesInDegrees[ _numSteeringWheelAnglesRecorded ] = _targetSteeringWheelAngleInDegrees;
			_targetVelocityInKPH = 1;
			_maxAbsYawRateInDegrees = 0f;
			_currentSteeringWheelInterval = 2;

			// tell robot to use fixed steering wheel angle

			_robotMode = RobotMode.FixedSteeringWheelAngle;

			// and we're off

			_currentPhase = Phase.TurningPasses;
		}

		// update robot

		UpdateRobot( app );
	}

	private void DoTurningPasses( App app )
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
				// increase num steering wheel angles recorded

				_numSteeringWheelAnglesRecorded++;

				// if we've not crashed then increment the interval

				if ( !crashed )
				{
					_currentSteeringWheelInterval++;
				}

				// prepare for the next pass

				_targetSteeringWheelAngleInDegrees += _currentSteeringWheelInterval * _steeringWheelDirection;
				_steeringWheelAnglesInDegrees[ _numSteeringWheelAnglesRecorded ] = _targetSteeringWheelAngleInDegrees;
				_targetVelocityInKPH = 1;
				_maxAbsYawRateInDegrees = 0f;

				// hit the active reset run button

				app.VirtualJoystick.ActiveResetRun = true;

				// reset the robot

				ResetRobot();

				// detect if it's time to move to the next phase

				var maxSteeringWheelAngleInDegrees = Math.Min( app.Simulator.SteeringWheelAngleMax * RadiansToDegrees, MaxSteeringWheelAngleInDegrees );

				if ( _targetSteeringWheelAngleInDegrees < -maxSteeringWheelAngleInDegrees )
				{
					_currentPhase = Phase.UTurn;
				}
				else if ( _targetSteeringWheelAngleInDegrees > maxSteeringWheelAngleInDegrees )
				{
					StopCalibration();
				}
			}
		}

		// update robot

		UpdateRobot( app );
	}

	private void DoUTurn( App app )
	{
		if ( _robotSettleTimer >= 1f )
		{
			// set target steering wheel angle and velocity

			_targetSteeringWheelAngleInDegrees = -180;

			_targetVelocityInKPH = Math.Clamp( (int) MathF.Ceiling( ( app.Simulator.YawNorth - MathF.PI * 0.5f ) * 3f ), 1, 10 );

			// check if we've reached our desired orientation

			if ( app.Simulator.YawNorth <= 90f * DegreesToRadians )
			{
				// prepare for the first right turning pass

				_robotSettleTimer = 0f;
				_steeringWheelDirection = 1;
				_targetSteeringWheelAngleInDegrees = StartingSteeringWheelAngle * _steeringWheelDirection;
				_steeringWheelAnglesInDegrees[ _numSteeringWheelAnglesRecorded ] = _targetSteeringWheelAngleInDegrees;
				_targetVelocityInKPH = 1;
				_maxAbsYawRateInDegrees = 0f;
				_currentSteeringWheelInterval = 2;

				// hit the active reset save button

				app.VirtualJoystick.ActiveResetSave = true;

				// reset the robot

				ResetRobot();

				// save the reset position

				_carResetPositionX = _carPositionX;
				_carResetPositionY = _carPositionY;

				// and we're off

				_currentPhase = Phase.TurningPasses;
			}
		}

		// update robot

		UpdateRobot( app );
	}

	private void DoStop( App app )
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
					dataString += $",{_yawRateDataInDegrees[ angleIndex, speed ]:F6}";
				}
			}

			writer.WriteLine( dataString );
		}

		app.Logger.WriteLine( "[SteeringEffects] <<< SaveCalibration" );
	}

	private bool LoadCalibration()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[SteeringEffects] LoadCalibration >>>" );

		// clear out the data tables

		_numSteeringWheelAnglesRecorded = 0;

		Array.Clear( _steeringWheelAnglesInDegrees );
		Array.Clear( _yawRateDataInDegrees );

		// keep track of whether the file load was good or not

		var success = true;

		// open file

		var filePath = GetPreferredCalibrationFileName();

		if ( !File.Exists( filePath ) )
		{
			app.Logger.WriteLine( $"[SteeringEffects] Calibration file not found: {filePath}" );

			success = false;
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

				success = false;
			}
			else
			{
				var headerParts = headerLine.Split( ',' );

				if ( headerParts.Length < 2 )
				{
					app.Logger.WriteLine( "[SteeringEffects] Invalid header line." );

					success = false;
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

							success = false;

							break;
						}
					}

					// read yaw rate data

					if ( success )
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

								success = false;

								break;
							}

							for ( var angleIndex = 0; angleIndex < _numSteeringWheelAnglesRecorded; angleIndex++ )
							{
								var partIndex = angleIndex + 1;

								if ( partIndex >= parts.Length ) break;

								if ( float.TryParse( parts[ partIndex ], out var yawRate ) )
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

		//

		app.Logger.WriteLine( "[SteeringEffects] <<< LoadCalibration" );

		return success;
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

	public void UpdateCalibration()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[SteeringEffects] UpdateCalibration >>>" );

		// load the recording (only if we aren't currently calibrating)

		if ( ( _currentPhase == Phase.NotCalibrating ) && LoadCalibration() )
		{
			// allocate data arrays for curve fitting

			var numLeftAngles = 0;
			var numRightAngles = 0;

			var leftAngles = new double[ _numSteeringWheelAnglesRecorded ];
			var rightAngles = new double[ _numSteeringWheelAnglesRecorded ];

			var leftValues = new double[ _numSteeringWheelAnglesRecorded ];
			var rightValues = new double[ _numSteeringWheelAnglesRecorded ];

			// fill out the data arrays

			for ( var angleIndex = 0; angleIndex < _numSteeringWheelAnglesRecorded; angleIndex++ )
			{
				// find the peak yaw rate

				var increasingValueCount = 0;
				var lastAbsYawRateInDegrees = 0f;
				var maxAbsYawRateInDegrees = 0f;
				var speedAtMaxAbsYawRateInKPH = 0;

				for ( var speed = MaxSpeedInKPH; speed > 0; speed-- )
				{
					var yawRateInDegrees = _yawRateDataInDegrees[ angleIndex, speed ];

					if ( yawRateInDegrees == 0f )
					{
						continue;
					}

					var absYawRateInDegrees = MathF.Abs( yawRateInDegrees );

					if ( absYawRateInDegrees > lastAbsYawRateInDegrees )
					{
						increasingValueCount++;

						if ( increasingValueCount >= 5 )
						{
							maxAbsYawRateInDegrees = absYawRateInDegrees;

							speedAtMaxAbsYawRateInKPH = speed;
						}
					}
					else if ( increasingValueCount >= 5 )
					{
						break;
					}
					else
					{
						increasingValueCount = 0;
					}

					lastAbsYawRateInDegrees = absYawRateInDegrees;
				}

				if ( increasingValueCount >= 5 )
				{
					// store values as speed / ( yaw rate + 1 ) for each steering wheel angle

					if ( _steeringWheelAnglesInDegrees[ angleIndex ] < 0 )
					{
						leftAngles[ numLeftAngles ] = Math.Abs( _steeringWheelAnglesInDegrees[ angleIndex ] );
						leftValues[ numLeftAngles ] = speedAtMaxAbsYawRateInKPH / ( maxAbsYawRateInDegrees + 1f );

						numLeftAngles++;
					}
					else
					{
						rightAngles[ numRightAngles ] = Math.Abs( _steeringWheelAnglesInDegrees[ angleIndex ] );
						rightValues[ numRightAngles ] = speedAtMaxAbsYawRateInKPH / ( maxAbsYawRateInDegrees + 1f );

						numRightAngles++;
					}
				}
			}

			//

			double[] calculateCoefficients( int numAngles, double[] inAngles, double[] inValues )
			{
				// trim the data arrays

				double[] angles = [ .. inAngles.Take( numAngles ) ];
				double[] values = [ .. inValues.Take( numAngles ) ];

				double[] logValues = [ .. values.Select( v => Math.Log( v ) ) ];

				// prep Cobyla objective function

				_cobylaAngles = angles;
				_cobylaValues = logValues;

				// initial guess

				double[] coefficients = [ 1000, 1 ];

				// run optimizer (modifies initial guess in place)

				var success = _cobyla.Minimize( coefficients );

				return coefficients;
			}

			// get our coefficients

			_leftCoefficients = calculateCoefficients( numLeftAngles, leftAngles, leftValues );
			_rightCoefficients = calculateCoefficients( numRightAngles, rightAngles, rightValues );

			// write out debug csv file

			var filePath = Path.Combine( _calibrationDirectory, "debug.csv" );

			using var writer = new StreamWriter( filePath );

			var headerString = "SWA,Actual,Predicted";

			writer.WriteLine( headerString );

			for ( var angleIndex = 0; angleIndex < numLeftAngles; angleIndex++ )
			{
				var angle = leftAngles[ angleIndex ];
				var value = MathF.Log( (float) leftValues[ angleIndex ] );
				var predicted = Predict( (float) -angle );

				writer.WriteLine( $"{angle:F0},{value:F6},{predicted:F6}" );
			}

			filePath = Path.Combine( _calibrationDirectory, "debug2.csv" );

			using var writer2 = new StreamWriter( filePath );

			headerString = "SWA,Predicted";

			writer2.WriteLine( headerString );

			for ( var angle = 0.1f; angle < 90.0f; angle += 0.1f )
			{
				var predicted = Predict( (float) -angle );

				writer2.WriteLine( $"{angle:F1},{predicted:F6}" );
			}
		}

		//

		app.Logger.WriteLine( "[SteeringEffects] <<< UpdateCalibration" );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private float Predict( float steeringWheelAngle )
	{
		if ( ( _leftCoefficients != null ) && ( _rightCoefficients != null ) )
		{
			var absSteeringWheelAngle = MathF.Abs( steeringWheelAngle );

			if ( steeringWheelAngle < 0f )
			{
				var denominator = absSteeringWheelAngle + (float) _leftCoefficients[ 1 ];

				if ( denominator > 0 )
				{
					return (float) MathF.Log( (float) _leftCoefficients[ 0 ] ) - MathF.Log( denominator );
				}
			}
			else
			{
				var denominator = absSteeringWheelAngle + (float) _rightCoefficients[ 1 ];

				if ( denominator > 0 )
				{
					return (float) MathF.Log( (float) _rightCoefficients[ 0 ] ) - MathF.Log( denominator );
				}
			}
		}

		return 0f;
	}

	public void Tick( App app )
	{
		var localization = DataContext.DataContext.Instance.Localization;

		switch ( _currentPhase )
		{
			case Phase.ResetCalibration:
				DoResetCalibration( app );
				break;

			case Phase.DriveToWallEdge:
				DoDriveToWallEdge( app );
				break;

			case Phase.WarmUpTires:
				DoWarmUpTires( app );
				break;

			case Phase.DriveToActiveResetPoint:
				DoDriveActiveResetSavePoint( app );
				break;

			case Phase.TurningPasses:
				DoTurningPasses( app );
				break;

			case Phase.UTurn:
				DoUTurn( app );
				break;

			case Phase.Stop:
				DoStop( app );
				break;
		}

		if ( app.MainWindow.SteeringEffectsTabItemIsVisible )
		{
			app.MainWindow.SteeringEffects_Calibration_Phase_Label.Content = $"{localization[ "Phase:" ]} {localization[ _currentPhase.ToString() ]}";

			app.MainWindow.SteeringEffects_Calibration_Throttle_Label.Content = $"{localization[ "Throttle:" ]} {_robotThrottle * 100f:F0}{localization[ "Percent" ]}";
			app.MainWindow.SteeringEffects_Calibration_Brake_Label.Content = $"{localization[ "Brake:" ]} {_robotBrake * 100f:F0}{localization[ "Percent" ]}";

			app.MainWindow.SteeringEffects_Calibration_SteeringPosition_Label.Content = $"{localization[ "SteeringPosition:" ]} {_robotSteeringWheelAngleInDegrees:F0}{localization[ "Degrees" ]}";
			app.MainWindow.SteeringEffects_Calibration_SteeringIncrement_Label.Content = $"{localization[ "SteeringIncrement:" ]} {_currentSteeringWheelInterval:F0}{localization[ "Degrees" ]}";

			app.MainWindow.SteeringEffects_Calibration_YawRate_Label.Content = $"{localization[ "YawRate:" ]} {app.Simulator.YawRate * RadiansToDegrees,6:F1}";

			app.MainWindow.SteeringEffects_Calibration_CarPositionX_Label.Content = $"{localization[ "CarPositionX:" ]} {_carPositionX,6:F1}";
			app.MainWindow.SteeringEffects_Calibration_CarPositionY_Label.Content = $"{localization[ "CarPositionY:" ]} {_carPositionY,6:F1}";

			app.MainWindow.SteeringEffects_Calibration_VelocityX_Label.Content = $"{localization[ "VelocityX:" ]} {app.Simulator.VelocityX * MPSToKPH,6:F1}";
			app.MainWindow.SteeringEffects_Calibration_VelocityY_Label.Content = $"{localization[ "VelocityY:" ]} {app.Simulator.VelocityY * MPSToKPH,6:F1}";

			app.MainWindow.SteeringEffects_Calibration_Heading_Label.Content = $"{localization[ "Heading:" ]} {app.Simulator.YawNorth * RadiansToDegrees,6:F1}";

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
		}
	}
}
