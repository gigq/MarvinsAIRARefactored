
using System.Globalization;
using System.IO;

using CsvHelper;

namespace MarvinsAIRARefactored.Components;

public class SteeringEffects
{
	private class RecordingData
	{
		public float SteeringWheelAngle { get; set; }
		public float SpeedInKPH { get; set; }
		public float YawRate { get; set; }
	}

	private const float KPHToMetersPerSecond = 1f / 3.6f;

	private readonly string _recordingsDirectory = Path.Combine( App.DocumentsFolder, "Recordings" );

	private bool _isCalibrating = false;
	private bool _isStopping = false;
	private int _targetSpeedInKPH = 0;
	private float _lastFrameVelocityX = 0f;

	private readonly RecordingData[] _recordingData = new RecordingData[ 300 ];

	public void RunCalibration()
	{
		var app = App.Instance!;

		// shortcut to settings

		var settings = DataContext.DataContext.Instance.Settings;

		// round the target steering wheel angle to the nearest degree

		var steeringWheelAngle = MathF.Round( settings.SteeringEffectsSteeringWheelAngle );

		// set initial virtual joystick parameters

		app.VirtualJoystick.SteeringWheelAngle = steeringWheelAngle / 450f;
		app.VirtualJoystick.Throttle = 0f;
		app.VirtualJoystick.Brake = 0f;

		// set target speed

		_targetSpeedInKPH = 1;
		_lastFrameVelocityX = 0f;

		// start the calibration process

		_isCalibrating = true;
	}

	public void StopCalibration()
	{
		// whoa, nelly!

		_isCalibrating = false;
		_isStopping = true;

		// save the data

		SaveRecording();
	}

	public void Tick( App app )
	{
		// shortcut to settings

		var settings = DataContext.DataContext.Instance.Settings;

		float speedDelta = 0f;

		if ( _isCalibrating )
		{
			// if we aren't gaining speed, increase the throttle a hair

			speedDelta = app.Simulator.VelocityX - _lastFrameVelocityX;

			_lastFrameVelocityX = app.Simulator.VelocityX;

			if ( speedDelta <= 0.01f )
			{
				app.VirtualJoystick.Throttle += 0.0005f;
			}

			// shift up if we are in neutral or near shift RPM

			if ( ( app.Simulator.Gear == 0 ) || ( app.Simulator.RPM >= app.Simulator.ShiftLightsShiftRPM * 0.75f ) )
			{
				app.VirtualJoystick.ShiftUp = true;
			}

			// check if we've reached our target speed

			var targetSpeedInMPS = _targetSpeedInKPH * KPHToMetersPerSecond;

			if ( app.Simulator.VelocityX >= targetSpeedInMPS )
			{
				// yes - save the data

				_recordingData[ (int) _targetSpeedInKPH ] = new RecordingData()
				{
					SteeringWheelAngle = MathF.Round( settings.SteeringEffectsSteeringWheelAngle ),
					SpeedInKPH = _targetSpeedInKPH,
					YawRate = app.Simulator.YawRate
				};

				// bump up the target speed

				_targetSpeedInKPH++;
			}
		}
		else if ( _isStopping )
		{
			app.VirtualJoystick.Brake = 1f;

			if ( app.Simulator.Gear != 0 )
			{
				app.VirtualJoystick.ShiftDown = true;
			}

			if ( app.Simulator.VelocityX <= 0.001f )
			{
				_isStopping = false;
			}
		}

		app.Debug.Label_1 = $"isCalibrating = {_isCalibrating}";
		app.Debug.Label_2 = $"isStopping = {_isStopping}";
		app.Debug.Label_3 = $"SteeringWheelAngle = {app.VirtualJoystick.SteeringWheelAngle}";
		app.Debug.Label_4 = $"Throttle = {app.VirtualJoystick.Throttle}";
		app.Debug.Label_5 = $"Brake = {app.VirtualJoystick.Brake}";
		app.Debug.Label_6 = $"Gear = {app.Simulator.Gear}";
		app.Debug.Label_7 = $"YawRate = {app.Simulator.YawRate}";
		app.Debug.Label_8 = $"speedDelta = {speedDelta}";
		app.Debug.Label_9 = $"targetSpeedInKPH = {_targetSpeedInKPH}";
	}

	public void SaveRecording()
	{
		// shortcut to settings

		var settings = DataContext.DataContext.Instance.Settings;

		var app = App.Instance!;

		app.Logger.WriteLine( "[SteeringEffects] SaveRecording >>>" );

		var filePath = Path.Combine( _recordingsDirectory, $"{app.Simulator.CarScreenName} {MathF.Round( settings.SteeringEffectsSteeringWheelAngle ):F0}.csv" );

		using var writer = new StreamWriter( filePath );

		writer.WriteLine( "New recording" );

		using var csv = new CsvWriter( writer, CultureInfo.InvariantCulture );

		csv.WriteRecords( _recordingData );

		app.Logger.WriteLine( "[SteeringEffects] <<< SaveRecording" );
	}
}
