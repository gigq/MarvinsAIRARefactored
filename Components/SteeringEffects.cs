
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace MarvinsAIRARefactored.Components;

public class SteeringEffects
{
	private enum Phase
	{
		NotRunning,
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
	private const float RadiansToDegrees = 180f / MathF.PI;
	private const float DegreesToRadians = MathF.PI / 180f;

	private const float DeltaTime = 1f / 60f;
	private const float MapScale = 1.225f;

	private const float CarHomePositionX = -1f;
	private const float CarHomePositionY = -5f;

	private const float WarmUpTiresDrivingRadius = 190f;
	private const float WarmUpLaps = 3;
	private const int WarmUpTiresSpeedInKPH = 120;

	private const float ActiveResetSavePointX = 0f;
	private const float ActiveResetSavePointY = 140f;

	private const int SteeringWheelAngleIntervalInDegrees = 10;
	private const int MaxSteeringWheelAngleInDegrees = 180;
	private const int MaxSpeedInKPH = 250;
	private const int NumSteeringWheelAngles = MaxSteeringWheelAngleInDegrees * 2 / SteeringWheelAngleIntervalInDegrees + 1;

	private readonly string _calibrationDirectory = Path.Combine( App.DocumentsFolder, "Calibration" );

	private Phase _currentPhase = Phase.NotRunning;

	private int _currentWarmUpLapNumber = 0;

	private float _targetPositionX = 0f;
	private float _targetPositionY = 0f;
	private int _targetSteeringWheelAngleInDegrees = 0;
	private int _targetVelocityInKPH = 0;

	private RobotMode _robotMode = RobotMode.DriveToTarget;

	private float _robotSettleTimer = 0f;
	private float _robotSteeringWheelAngleInDegrees = 0f;
	private float _robotClutch = 1f;
	private float _robotBrake = 0f;
	private float _robotThrottle = 0f;
	private float _robotLastFrameVelocityX = 0f;
	private float _robotGearShiftTimer = 0f;
	private bool _robotGearShifted = false;
	private bool _robotGearShiftUp = false;

	private int _initialSteeringWheelAngleInDegrees = 0;
	private int _numSteeringWheelAnglesRecorded = 0;

	private float _maxAbsYawRateInDegrees = 0f;

	private readonly float[,] _yawRateDataInDegrees = new float[ NumSteeringWheelAngles, MaxSpeedInKPH + 1 ];

	private float _carResetPositionX = CarHomePositionX;
	private float _carResetPositionY = CarHomePositionY;

	private float _carPositionX = 0f;
	private float _carPositionY = 0f;

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

		SaveRecording();
	}

	public void Update( App app, float deltaSeconds )
	{
		var worldVelocityX = app.Simulator.VelocityX * MathF.Sin( app.Simulator.YawNorth ) - app.Simulator.VelocityY * MathF.Cos( app.Simulator.YawNorth );
		var worldVelocityY = app.Simulator.VelocityX * MathF.Cos( app.Simulator.YawNorth ) + app.Simulator.VelocityY * MathF.Sin( app.Simulator.YawNorth );

		_carPositionX += worldVelocityX * deltaSeconds;
		_carPositionY += worldVelocityY * deltaSeconds;
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
		_robotClutch = 1f;
		_robotBrake = 0f;
		_robotThrottle = 0f;
		_robotLastFrameVelocityX = 0f;
		_robotGearShiftTimer = 0f;
		_robotGearShifted = false;
		_robotGearShiftUp = false;
	}

	private void UpdateRobot( App app )
	{
		if ( _robotSettleTimer < 1f )
		{
			// let the car settle before driving

			_robotSettleTimer = Math.Min( _robotSettleTimer + DeltaTime, 1f );

			_robotClutch = 1f;
			_robotBrake = 0.5f;
			_robotThrottle = 0f;
			_robotSteeringWheelAngleInDegrees = 0f;

			_carPositionX = _carResetPositionX;
			_carPositionY = _carResetPositionY;
		}
		else
		{
			// adjust gear

			if ( app.Simulator.Gear == 0 )
			{
				app.VirtualJoystick.ShiftUp = true;
			}
			else if ( _robotGearShiftTimer >= 2f )
			{
				if ( app.Simulator.RPM >= app.Simulator.ShiftLightsShiftRPM * 0.75f )
				{
					if ( app.Simulator.Gear < app.Simulator.NumForwardGears )
					{
						_robotGearShiftUp = true;
						_robotGearShifted = false;
						_robotGearShiftTimer = 0f;
					}
				}
				else if ( app.Simulator.RPM <= app.Simulator.ShiftLightsShiftRPM * 0.25f )
				{
					if ( app.Simulator.Gear > 1 )
					{
						_robotGearShiftUp = false;
						_robotGearShifted = false;
						_robotGearShiftTimer = 0f;
					}
				}
			}

			if ( _robotGearShiftTimer < 2f )
			{
				_robotGearShiftTimer = Math.Min( _robotGearShiftTimer + DeltaTime, 2f );

				if ( ( _robotGearShiftTimer >= 0.98f ) && !_robotGearShifted )
				{
					_robotGearShifted = true;

					if ( _robotGearShiftUp )
					{
						app.VirtualJoystick.ShiftUp = true;
					}
					else
					{
						app.VirtualJoystick.ShiftDown = true;
					}
				}
			}

			// adjust steering wheel

			if ( _robotMode == RobotMode.DriveToTarget )
			{
				var nearestDistanceToTargetTurningLeft = PredictNearestDistanceToTarget( app, app.Simulator.YawRate - 0.005f, 0.01f );
				var nearestDistanceToTargetTurningRight = PredictNearestDistanceToTarget( app, app.Simulator.YawRate + 0.005f, 0.01f );

				var wheelTurnAmount = ( nearestDistanceToTargetTurningRight - nearestDistanceToTargetTurningLeft ) * 0.05f;

				_robotSteeringWheelAngleInDegrees += Math.Clamp( wheelTurnAmount, -0.25f, 0.25f ) * Math.Min( 1f, app.Simulator.VelocityX );
			}
			else
			{
				_robotSteeringWheelAngleInDegrees = _targetSteeringWheelAngleInDegrees;
			}

			// should we be speeding up or slowing down?

			var deltaTargetVelocity = _targetVelocityInKPH * KPHToMPS - app.Simulator.VelocityX;

			// adjust clutch

			_robotClutch = MathF.Cos( _robotGearShiftTimer * 0.5f * MathF.Tau ) * 0.5f + 0.5f;

			// adjust brake

			if ( deltaTargetVelocity < 0 )
			{
				_robotBrake = Math.Min( _robotBrake - deltaTargetVelocity * 0.002f, 0.25f );
			}
			else
			{
				_robotBrake -= 0.01f;
			}

			// adjust throttle

			if ( _robotGearShiftTimer >= 2f )
			{
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
			else
			{
				_robotThrottle -= 0.00025f;
			}
		}

		// update virtual joystick

		_robotSteeringWheelAngleInDegrees = Math.Clamp( _robotSteeringWheelAngleInDegrees, -450f, 450f );
		_robotClutch = Math.Clamp( _robotClutch, 0f, 1f );
		_robotBrake = Math.Clamp( _robotBrake, 0f, 1f );
		_robotThrottle = Math.Clamp( _robotThrottle, 0f, 1f );

		app.VirtualJoystick.Steering = _robotSteeringWheelAngleInDegrees / 450f;
		app.VirtualJoystick.Clutch = _robotClutch;
		app.VirtualJoystick.Brake = _robotBrake;
		app.VirtualJoystick.Throttle = _robotThrottle * ( MathF.Cos( _robotGearShiftTimer * MathF.PI ) * 0.5f + 0.5f );
	}

	private void DoResetCalibration( App app )
	{
		// clear out our old yaw rate data

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

		if ( _carPositionX > 0f )
		{
			var deltaX = _targetPositionX - _carPositionX;
			var deltaY = _targetPositionY - _carPositionY;

			var distance = MathF.Sqrt( deltaX * deltaX + deltaY * deltaY );

			_targetVelocityInKPH = Math.Min( (int) MathF.Ceiling( distance * 0.5f ), WarmUpTiresSpeedInKPH );
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
			_initialSteeringWheelAngleInDegrees = (int) -MathF.Min( MaxSteeringWheelAngleInDegrees, MathF.Floor( app.Simulator.SteeringWheelAngleMax * RadiansToDegrees * 0.5f / 10 ) * 10 );
			_numSteeringWheelAnglesRecorded = 0;
			_targetSteeringWheelAngleInDegrees = _initialSteeringWheelAngleInDegrees;
			_targetVelocityInKPH = 1;
			_maxAbsYawRateInDegrees = 0f;

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

			// figure out if we have crashed

			var crashed = app.Simulator.GForce >= 2f;

			// check if we've reached our target speed

			if ( !crashed && ( app.Simulator.VelocityX >= _targetVelocityInKPH * KPHToMPS ) )
			{
				// yes - save the data

				_yawRateDataInDegrees[ _numSteeringWheelAnglesRecorded, _targetVelocityInKPH ] = app.Simulator.YawRate * RadiansToDegrees;

				// update max abs yaw rate

				if ( absYawRateInDegrees >= 10f )
				{
					_maxAbsYawRateInDegrees = MathF.Max( _maxAbsYawRateInDegrees, absYawRateInDegrees );
				}

				// bump up the target speed

				_targetVelocityInKPH++;
			}

			// check if we are done with this pass

			if ( crashed || ( _targetVelocityInKPH > MaxSpeedInKPH ) || ( MathF.Abs( app.Simulator.VelocityY ) > 3f ) || ( ( absYawRateInDegrees < ( _maxAbsYawRateInDegrees * 0.5f ) ) && ( _robotGearShiftTimer >= 2f ) ) )
			{
				// increase num steering wheel angles recorded

				_numSteeringWheelAnglesRecorded++;

				// prepare for the next pass

				_targetSteeringWheelAngleInDegrees += SteeringWheelAngleIntervalInDegrees;
				_targetVelocityInKPH = 1;
				_maxAbsYawRateInDegrees = 0f;

				// hit the active reset run button

				app.VirtualJoystick.ActiveResetRun = true;

				// reset the robot

				ResetRobot();

				// detect if it's time to move to the next phase

				if ( _targetSteeringWheelAngleInDegrees == 0 )
				{
					_currentPhase = Phase.UTurn;
				}
				else if ( _numSteeringWheelAnglesRecorded == NumSteeringWheelAngles )
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

			if ( app.Simulator.YawNorth <= 91f * DegreesToRadians )
			{
				// increase num steering wheel angles recorded

				_numSteeringWheelAnglesRecorded++;

				// prepare for the first right turning pass

				_targetSteeringWheelAngleInDegrees = SteeringWheelAngleIntervalInDegrees;
				_targetVelocityInKPH = 1;
				_maxAbsYawRateInDegrees = 0f;

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

			_currentPhase = Phase.NotRunning;
		}
	}

	public void Tick( App app )
	{
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
			app.MainWindow.SteeringEffects_Calibration_Phase.Content = $"Phase: {_currentPhase}";

			app.MainWindow.SteeringEffects_Calibration_ExtraInfo.Content = _currentPhase switch
			{
				Phase.WarmUpTires => $"Lap: {_currentWarmUpLapNumber} of {WarmUpLaps}",
				_ => "",
			};

			app.MainWindow.SteeringEffects_Calibration_Steering.Content = $"S: {_robotSteeringWheelAngleInDegrees,4:F0}";
			app.MainWindow.SteeringEffects_Calibration_Clutch.Content = $"C: {_robotClutch * 100f,3:F0}%";
			app.MainWindow.SteeringEffects_Calibration_Brake.Content = $"B: {_robotBrake * 100f,3:F0}%";
			app.MainWindow.SteeringEffects_Calibration_Throttle.Content = $"T: {_robotThrottle * 100f,3:F0}%";

			app.MainWindow.SteeringEffects_Calibration_CarPositionX.Content = $"X: {_carPositionX,6:F1}";
			app.MainWindow.SteeringEffects_Calibration_CarPositionY.Content = $"Y: {_carPositionY,6:F1}";

			app.MainWindow.SteeringEffects_Calibration_YawRate.Content = $"YR: {app.Simulator.YawRate * RadiansToDegrees,6:F1}";
			app.MainWindow.SteeringEffects_Calibration_YawNorth.Content = $"YN: {app.Simulator.YawNorth * RadiansToDegrees,6:F1}";
			app.MainWindow.SteeringEffects_Calibration_VelocityY.Content = $"VY: {app.Simulator.VelocityY,6:F1}";

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

	public void SaveRecording()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[SteeringEffects] SaveRecording >>>" );

		// create directory if it does not exist

		if ( !Directory.Exists( _calibrationDirectory ) )
		{
			Directory.CreateDirectory( _calibrationDirectory );
		}

		// open file

		var filePath = Path.Combine( _calibrationDirectory, $"{app.Simulator.CarScreenName}.csv" );

		using var writer = new StreamWriter( filePath );

		// write car name

		writer.WriteLine( app.Simulator.CarScreenName );

		// write header row

		var steeringWheelAngle = _initialSteeringWheelAngleInDegrees;

		var headerString = "Speed (KPH)";

		for ( var i = 0; i < _numSteeringWheelAnglesRecorded; i++ )
		{
			headerString += $",{steeringWheelAngle + i * SteeringWheelAngleIntervalInDegrees} Degrees";
		}

		writer.WriteLine( headerString );

		// write data rows

		for ( var j = 0; j <= MaxSpeedInKPH; j++ )
		{
			var dataString = $"{j}";

			for ( var i = 0; i < _numSteeringWheelAnglesRecorded; i++ )
			{
				if ( _yawRateDataInDegrees[ i, j ] == 0f )
				{
					dataString += $",";
				}
				else
				{
					dataString += $",{_yawRateDataInDegrees[ i, j ]:F3}";
				}
			}

			writer.WriteLine( dataString );
		}

		app.Logger.WriteLine( "[SteeringEffects] <<< SaveRecording" );
	}
}
