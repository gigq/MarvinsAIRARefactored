
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FlowDirection = System.Windows.FlowDirection;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

using MarvinsAIRARefactored.Classes;

namespace MarvinsAIRARefactored.Components;

public class SteeringEffects
{
	public static string CalibrationDirectory { get; private set; } = Path.Combine( App.DocumentsFolder, "Calibration" );

	public float UndersteerEffect { get; private set; } = 0f;
	public float OversteerEffect { get; private set; } = 0f;
	public float SkidSlip { get; private set; } = 0f;

	private enum CalibrationPhase
	{
		NotCalibrating,
		ResetCalibration,
		Calibrating,
		Stop
	}

	private const int CalibrationFileVersion = 2;

	private const int MaxSteeringWheelAngleInDegrees = 450;
	private const int MaxNumSteeringWheelAngles = MaxSteeringWheelAngleInDegrees * 2 + 1;

	private const float CalibrationSpeedInKPH = 15f;

	private const int CalibrationGraphWidth = MaxSteeringWheelAngleInDegrees * 2;
	private const int CalibrationGraphHeight = 300;

	private CalibrationPhase _calibrationPhase = CalibrationPhase.NotCalibrating;
	private float _calibrationProgress = 0f;

	private int _framesToSkip = 0;

	private float _lastFrameSpeed = 0f;

	private int _targetSteeringWheelAngleInDegrees = 0;
	private float _targetSpeedInKPH = 0f;

	private float _robotSettleTimer = 0f;
	private float _robotThrottle = 0f;

	private int _yawRateInDPSRunningAverageCount = 0;
	private float _yawRateInDPSRunningAverage = 0f;

	private int _numSteeringWheelAnglesRecorded = 0;

	private readonly float[] _steeringWheelAnglesInDegrees = new float[ MaxNumSteeringWheelAngles ];
	private readonly float[] _yawRateDataInDegreesPerSecond = new float[ MaxNumSteeringWheelAngles ];

	private string? _currentlyActiveCarScreenName = null;
	private string _currentlyActiveCalibrationFileName = string.Empty;

	private readonly float[] _expectedYawRateInDegreesPerSecond = new float[ MaxNumSteeringWheelAngles ];
	private bool _calibrationIsValid = false;

	private readonly RenderTargetBitmap _calibrationGraphRenderTargetBitmap = new( CalibrationGraphWidth, CalibrationGraphHeight, 96.0, 96.0, PixelFormats.Pbgra32 );

	private readonly Pen _calibrationGraphPen = new( Brushes.Lime, 1.0 )
	{
		StartLineCap = PenLineCap.Round,
		EndLineCap = PenLineCap.Round
	};

	public SteeringEffects()
	{
		_calibrationGraphPen.Freeze();
	}

	public void SetMairaComboBoxItemsSource( bool forceRefresh = false )
	{
#if !ADMINBOXX

		var app = App.Instance!;

		app.Logger.WriteLine( "[SteeringEffects] SetMairaComboBoxItemsSource >>>" );

		if ( forceRefresh || ( _currentlyActiveCarScreenName == null ) || ( app.Simulator.CarScreenName != _currentlyActiveCarScreenName ) )
		{
			_currentlyActiveCarScreenName = app.Simulator.CarScreenName;

			var settings = DataContext.DataContext.Instance.Settings;
			var localization = DataContext.DataContext.Instance.Localization;

			var dictionary = new Dictionary<string, string>()
			{
				{ string.Empty, localization["CalibrationFileNotSelected"] }
			};

			foreach ( var filePath in Directory.GetFiles( CalibrationDirectory, $"{app.Simulator.CarScreenName} - *.csv" ) )
			{
				var option = Path.GetFileNameWithoutExtension( filePath );

				dictionary.Add( option, option );
			}

			if ( !dictionary.ContainsKey( settings.SteeringEffectsCalibrationFileName ) )
			{
				settings.SteeringEffectsCalibrationFileName = string.Empty;
			}

			app.Dispatcher.BeginInvoke( () =>
			{
				app.MainWindow.SteeringEffects_CalibrationFileName_ComboBox.ItemsSource = dictionary;
				app.MainWindow.SteeringEffects_CalibrationFileName_ComboBox.SelectedValue = settings.SteeringEffectsCalibrationFileName;
				app.MainWindow.SteeringEffects_CalibrationFileName_ComboBox.Visibility = Visibility.Visible;
			} );
		}

		app.Logger.WriteLine( "[SteeringEffects] <<< SetMairaComboBoxItemsSource" );

#endif
	}

	public void Update( App app, float deltaSeconds )
	{
		if ( _calibrationPhase == CalibrationPhase.NotCalibrating )
		{
			if ( _calibrationIsValid )
			{
				UpdateEffects( app );
			}
		}
		else
		{
			switch ( _calibrationPhase )
			{
				case CalibrationPhase.ResetCalibration:
					DoResetCalibration( app, deltaSeconds );
					break;

				case CalibrationPhase.Calibrating:
					DoCalibration( app, deltaSeconds );
					break;

				case CalibrationPhase.Stop:
					DoStop( app, deltaSeconds );
					break;
			}
		}
	}

	private void UpdateEffects( App app )
	{
		var settings = DataContext.DataContext.Instance.Settings;

		// get current steering wheel angle in degrees

		var steeringWheelAngleInDegrees = app.Simulator.SteeringWheelAngle * MathZ.RadiansToDegrees;

		// get current speed (minimum 1 kph to avoid divide by 0)

		var speedInKPH = MathF.Max( app.Simulator.Speed * MathZ.MPSToKPH, 1f );

		// get current normalized yaw rate

		var yawRateInDegreesPerSecond = MathF.Abs( app.Simulator.YawRate * MathZ.RadiansToDegrees ) / speedInKPH;

		// compute the float angle index (into the _expectedYawRateInDPS array)

		var angleIndex = Math.Clamp( steeringWheelAngleInDegrees + MaxSteeringWheelAngleInDegrees, 0f, _expectedYawRateInDegreesPerSecond.Length - 1f );

		// get the nearest integer angle indices

		var angleIndexLower = (int) MathF.Floor( angleIndex );
		var angleIndexUpper = (int) MathF.Ceiling( angleIndex );

		// if both indices are the same (at exact int or at boundary), no need to blend

		float expectedYawRateInDegreesPerSecond;

		if ( angleIndexLower == angleIndexUpper )
		{
			expectedYawRateInDegreesPerSecond = _expectedYawRateInDegreesPerSecond[ angleIndexLower ];
		}
		else
		{
			// linearly interpolate between lower and upper values

			var t = angleIndex - angleIndexLower; // (0..1)

			expectedYawRateInDegreesPerSecond = MathZ.Lerp( _expectedYawRateInDegreesPerSecond[ angleIndexLower ], _expectedYawRateInDegreesPerSecond[ angleIndexUpper ], t );
		}

		// calculate absolute deviation from expected; sign encodes under/over

		var deviation = yawRateInDegreesPerSecond - expectedYawRateInDegreesPerSecond;  // + => oversteer, - => understeer

		var absDeviation = MathF.Abs( deviation );

		// fade effects out at low speed

		var speedFade = MathZ.Smoothstep( 1f, 20f, speedInKPH );

		// deviation < 0 = understeer

		var understeerEffect = 0f;

		if ( ( deviation < 0f ) && ( absDeviation >= settings.SteeringEffectsUndersteerMinimumThreshold ) )
		{
			understeerEffect = MathZ.Saturate( MathZ.InverseLerp( settings.SteeringEffectsUndersteerMinimumThreshold, settings.SteeringEffectsUndersteerMaximumThreshold, absDeviation ) );
		}

		UndersteerEffect = speedFade * understeerEffect;

		// deviation > 0 = oversteer

		var oversteerEffect = 0f;

		if ( ( deviation > 0f ) && ( absDeviation >= settings.SteeringEffectsOversteerMinimumThreshold ) )
		{
			oversteerEffect = MathZ.Saturate( MathZ.InverseLerp( settings.SteeringEffectsOversteerMinimumThreshold, settings.SteeringEffectsOversteerMaximumThreshold, absDeviation ) );
		}

		OversteerEffect = speedFade * oversteerEffect;

		// calculate skid / slip indicator position and align to turning direction

		var skidSlip = 0f;

		if ( deviation < 0f )
		{
			skidSlip = -MathZ.Saturate( MathZ.InverseLerp( 0f, settings.SteeringEffectsUndersteerMaximumThreshold, absDeviation ) );
		}
		else if ( deviation > 0f )
		{
			skidSlip = MathZ.Saturate( MathZ.InverseLerp( 0f, settings.SteeringEffectsOversteerMaximumThreshold, absDeviation ) );
		}

		skidSlip = speedFade * skidSlip * -MathF.Sign( steeringWheelAngleInDegrees );

		SkidSlip = Math.Clamp( skidSlip, -1f, 1f );
	}

	public void RunCalibration()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[SteeringEffects] RunCalibration >>>" );

		// start at the very beginning

		_calibrationPhase = CalibrationPhase.ResetCalibration;

		app.Logger.WriteLine( "[SteeringEffects] <<< RunCalibration" );
	}

	public void StopCalibration( bool saveCalibration )
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[SteeringEffects] StopCalibration >>>" );

		// whoa, nelly!

		_calibrationPhase = CalibrationPhase.Stop;
		_calibrationProgress = 1f;

		// save the calibration

		if ( saveCalibration )
		{
			// save the calibration data

			SaveCalibration();

			// clear the calibration data

			ClearCalibration();

			// reload the calibration data

			LoadCalibration();
		}

		app.Logger.WriteLine( "[SteeringEffects] <<< StopCalibration" );
	}

	private void UpdateRobot( App app, float deltaSeconds )
	{
		// let the car settle before driving

		if ( _robotSettleTimer > 0f )
		{
			_robotSettleTimer = Math.Max( _robotSettleTimer - deltaSeconds, 0f );

			_robotThrottle = 0f;
		}
		else
		{
			// always be in first gear

			if ( app.Simulator.Gear != 1 )
			{
				if ( app.Simulator.Gear > 1 )
				{
					app.VirtualJoystick.ShiftDown = true;
				}
				else
				{
					app.VirtualJoystick.ShiftUp = true;
				}

				_robotSettleTimer = 0.5f;
			}
			else
			{
				// update throttle

				var deltaToTarget = _targetSpeedInKPH - app.Simulator.Speed * MathZ.MPSToKPH;

				var targetAcceleration = Math.Clamp( deltaToTarget, -1f, 1f );

				var currentAcceleration = ( app.Simulator.Speed - _lastFrameSpeed ) * MathZ.MPSToKPH / deltaSeconds;

				var deltaAcceleration = targetAcceleration - currentAcceleration;

				if ( MathF.Sign( deltaToTarget ) == MathF.Sign( deltaAcceleration ) )
				{
					_robotThrottle += Math.Clamp( deltaAcceleration, -deltaSeconds / 30f, deltaSeconds / 30f );

					_robotThrottle = MathZ.Saturate( _robotThrottle );
				}
			}
		}

		// update virtual joystick

		app.VirtualJoystick.Steering = ( _targetSteeringWheelAngleInDegrees + app.Simulator.SteeringOffset ) / 450f;
		app.VirtualJoystick.Brake = 0f;
		app.VirtualJoystick.Throttle = _robotThrottle;

		// remember last frame speed

		_lastFrameSpeed = app.Simulator.Speed;
	}

	private void DoResetCalibration( App app, float deltaSeconds )
	{
		// reset calibration progress

		_calibrationProgress = 0f;

		// reset misc stuff

		_framesToSkip = 0;
		_lastFrameSpeed = 0f;

		// reset targets

		_targetSteeringWheelAngleInDegrees = (int) ( MathF.Max( -MaxSteeringWheelAngleInDegrees, app.Simulator.SteeringWheelAngleMax * MathZ.RadiansToDegrees / -2f ) );
		_targetSpeedInKPH = CalibrationSpeedInKPH;

		// reset robot

		_robotSettleTimer = 1f;
		_robotThrottle = 0f;

		// reset yaw rate running average

		_yawRateInDPSRunningAverageCount = 0;
		_yawRateInDPSRunningAverage = 0f;

		// clear out our old calibration data

		_numSteeringWheelAnglesRecorded = 0;

		Array.Clear( _steeringWheelAnglesInDegrees );
		Array.Clear( _yawRateDataInDegreesPerSecond );

		// next phase

		_calibrationPhase = CalibrationPhase.Calibrating;
	}

	private void DoCalibration( App app, float deltaSeconds )
	{
		// update robot

		UpdateRobot( app, deltaSeconds );

		// don't do anything if we need to skip some frames (let steering wheel adjustment settle)

		if ( _framesToSkip > 0 )
		{
			_framesToSkip--;

			return;
		}

		// don't do anything if robot is still settling

		if ( _robotSettleTimer != 0f )
		{
			return;
		}

		// stop here if we aren't near our speed target yet

		if ( ( ( app.Simulator.Speed * MathZ.MPSToKPH ) - _targetSpeedInKPH ) < -1f )
		{
			return;
		}

		// update the yaw rate running average

		var yawRateInDPS = MathF.Abs( app.Simulator.YawRate * MathZ.RadiansToDegrees ) / ( app.Simulator.Speed * MathZ.MPSToKPH );

		if ( _yawRateInDPSRunningAverageCount == 0 )
		{
			_yawRateInDPSRunningAverage = yawRateInDPS;
		}
		else
		{
			_yawRateInDPSRunningAverage = MathZ.Lerp( _yawRateInDPSRunningAverage, yawRateInDPS, 0.2f );
		}

		_yawRateInDPSRunningAverageCount++;

		// stop when we have averaged the yaw rate over 1/6 of a second (10 frames)

		if ( _yawRateInDPSRunningAverageCount == 10 )
		{
			// save the yaw rate

			_steeringWheelAnglesInDegrees[ _numSteeringWheelAnglesRecorded ] = app.Simulator.SteeringWheelAngle * MathZ.RadiansToDegrees + app.Simulator.SteeringOffset;
			_yawRateDataInDegreesPerSecond[ _numSteeringWheelAnglesRecorded ] = _yawRateInDPSRunningAverage;

			// update calibration graph

			if ( _numSteeringWheelAnglesRecorded >= 1 )
			{
				app.Dispatcher.Invoke( () =>
				{
					var drawingVisual = new DrawingVisual();

					using var drawingContext = drawingVisual.RenderOpen();

					var p1 = new Point( MaxSteeringWheelAngleInDegrees + _steeringWheelAnglesInDegrees[ _numSteeringWheelAnglesRecorded - 1 ] + 0.5, CalibrationGraphHeight - _yawRateDataInDegreesPerSecond[ _numSteeringWheelAnglesRecorded - 1 ] * 100.0 + 0.5 );
					var p2 = new Point( MaxSteeringWheelAngleInDegrees + _steeringWheelAnglesInDegrees[ _numSteeringWheelAnglesRecorded - 0 ] + 0.5, CalibrationGraphHeight - _yawRateDataInDegreesPerSecond[ _numSteeringWheelAnglesRecorded - 0 ] * 100.0 + 0.5 );

					drawingContext.DrawLine( _calibrationGraphPen, p1, p2 );
					drawingContext.Close();

					_calibrationGraphRenderTargetBitmap.Render( drawingVisual );
				} );
			}

			// next!

			_numSteeringWheelAnglesRecorded++;

			// reset the yaw rate running average

			_yawRateInDPSRunningAverageCount = 0;
			_yawRateInDPSRunningAverage = 0f;

			// move to the next target steering wheel angle

			_targetSteeringWheelAngleInDegrees++;

			// skip some frames to let the steering wheel angle adjustment settle

			_framesToSkip = 5;

			// check if we are done calibrating

			var maxSteeringWheelAngleInDegrees = Math.Min( MaxSteeringWheelAngleInDegrees, (int) ( app.Simulator.SteeringWheelAngleMax * MathZ.RadiansToDegrees / 2f ) );

			if ( _targetSteeringWheelAngleInDegrees > maxSteeringWheelAngleInDegrees )
			{
				StopCalibration( true );

				return;
			}

			// update the calibration progress

			_calibrationProgress = (float) _numSteeringWheelAnglesRecorded / (float) ( maxSteeringWheelAngleInDegrees * 2 + 1 );
		}
	}

	private void DoStop( App app, float deltaSeconds )
	{
		app.VirtualJoystick.Steering = 0f;
		app.VirtualJoystick.Throttle = 0f;
		app.VirtualJoystick.Brake = 1f;

		if ( app.Simulator.Speed <= 0.005f )
		{
			app.VirtualJoystick.Brake = 0f;

			_calibrationPhase = CalibrationPhase.NotCalibrating;
		}
	}

	private void DrawCalibrationGraphGrid()
	{
		var app = App.Instance!;

		app.Dispatcher.Invoke( () =>
		{
			// start drawing

			var drawingVisual = new DrawingVisual();

			using var drawingContext = drawingVisual.RenderOpen();

			// label drawing

			var labelTypeface = new Typeface( "Segoe UI" );

			const double labelFontSize = 14.0;

			var labelBgBrush = new SolidColorBrush( Color.FromArgb( 255, 0, 0, 0 ) );
			var labelBorderBrush = new SolidColorBrush( Color.FromArgb( 255, 255, 255, 255 ) );
			var labelBorderPen = new Pen( labelBorderBrush, 1.0 );

			labelBgBrush.Freeze();
			labelBorderBrush.Freeze();
			labelBorderPen.Freeze();

			const double labelPaddingX = 5.0;
			const double labelPaddingY = 1.0;
			const double labelCornerRadius = 5.0;

			void DrawLabel( string text, int t, bool left )
			{
				var formatted = new FormattedText( text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, labelTypeface, labelFontSize, Brushes.White, 1.0 );

				double labelX, labelY;

				if ( left )
				{
					labelX = labelPaddingX + 10.0;
					labelY = t - ( formatted.Height / 2.0 );
				}
				else
				{
					labelX = t - ( formatted.Width / 2.0 );
					labelY = labelPaddingY + 10.0;
				}

				var rect = new Rect( labelX - labelPaddingX, labelY - labelPaddingY, formatted.Width + ( labelPaddingX * 2.0 ), formatted.Height + ( labelPaddingY * 2.0 ) );

				drawingContext.DrawRoundedRectangle( labelBgBrush, labelBorderPen, rect, labelCornerRadius, labelCornerRadius );
				drawingContext.DrawText( formatted, new Point( rect.X + labelPaddingX, rect.Y + labelPaddingY ) );
			}

			// transparent background

			drawingContext.DrawRectangle( Brushes.Transparent, null, new Rect( 0, 0, CalibrationGraphWidth, CalibrationGraphHeight ) );

			// pen brushes

			var thinPenBrush = new SolidColorBrush( Color.FromArgb( 32, 255, 255, 255 ) );
			var thickPenBrush = new SolidColorBrush( Color.FromArgb( 96, 255, 255, 255 ) );

			thinPenBrush.Freeze();
			thickPenBrush.Freeze();

			// pens

			var thinPen = new Pen( thinPenBrush, 1.0 );
			var thickPen = new Pen( thickPenBrush, 1.0 );

			thinPen.Freeze();
			thickPen.Freeze();

			// draw vertical lines

			for ( var x = 0; x <= CalibrationGraphWidth; x += 10 )
			{
				if ( ( x == 0 ) || ( x == CalibrationGraphWidth ) )
				{
					continue;
				}

				var pen = ( x % 50 == 0 ) ? thickPen : thinPen;

				drawingContext.DrawLine( pen, new Point( x + 0.5, 0 ), new Point( x + 0.5, CalibrationGraphHeight ) );
			}

			// draw horizontal lines

			for ( var y = 0; y <= CalibrationGraphHeight; y += 10 )
			{
				if ( ( y == 0 ) || ( y == CalibrationGraphHeight ) )
				{
					continue;
				}

				var pen = ( y % 50 == 0 ) ? thickPen : thinPen;

				drawingContext.DrawLine( pen, new Point( 0, y + 0.5 ), new Point( CalibrationGraphWidth, y + 0.5 ) );
			}

			// draw vertical text

			for ( var x = 0; x <= CalibrationGraphWidth; x += 10 )
			{
				if ( ( x == 0 ) || ( x == CalibrationGraphWidth ) )
				{
					continue;
				}

				if ( x % 50 == 0 )
				{
					var labelValue = Math.Abs( -CalibrationGraphWidth / 2 + ( x / 50 ) * 50 );
					var labelText = $"{labelValue}";

					DrawLabel( labelText, x, false );
				}
			}

			// draw horizontal text

			for ( var y = 0; y <= CalibrationGraphHeight; y += 10 )
			{
				if ( ( y == 0 ) || ( y == CalibrationGraphHeight ) )
				{
					continue;
				}

				if ( y % 50 == 0 )
				{
					var labelValue = ( CalibrationGraphHeight - y ) / 100f;
					var labelText = $"{labelValue:F1}";

					DrawLabel( labelText, y, true );
				}
			}

			// finish drawing

			drawingContext.Close();

			_calibrationGraphRenderTargetBitmap.Clear();
			_calibrationGraphRenderTargetBitmap.Render( drawingVisual );

			// set new image source

			app.MainWindow.SteeringEffects_Calibration_Image.Source = _calibrationGraphRenderTargetBitmap;
		} );
	}

	private void SaveCalibration()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[SteeringEffects] SaveCalibration >>>" );

		// create directory if it does not exist

		if ( !Directory.Exists( CalibrationDirectory ) )
		{
			Directory.CreateDirectory( CalibrationDirectory );
		}

		// open file

		var filePath = Path.Combine( CalibrationDirectory, $"{app.Simulator.CarScreenName} - {app.Simulator.CarSetupName} - {app.Simulator.CurrentTireCompoundType}.csv" );

		using var writer = new StreamWriter( filePath );

		// write version, car name, car setup, and tire compound type

		writer.WriteLine( $"{CalibrationFileVersion},{app.Simulator.CarScreenName},{app.Simulator.CarSetupName},{app.Simulator.CurrentTireCompoundType}" );

		// write header row

		writer.WriteLine( "Steering Wheel Angle,Yaw Rate" );

		// write data rows

		for ( var angleIndex = 0; angleIndex < _numSteeringWheelAnglesRecorded; angleIndex++ )
		{
			writer.WriteLine( $"{_steeringWheelAnglesInDegrees[ angleIndex ]:F6},{_yawRateDataInDegreesPerSecond[ angleIndex ]:F6}" );
		}

		// close the file

		writer.Close();

		// update setting to use this calibration file

		DataContext.DataContext.Instance.Settings.SteeringEffectsCalibrationFileName = Path.GetFileNameWithoutExtension( filePath );

		// update the combo box options

		SetMairaComboBoxItemsSource( true );

		//

		app.Logger.WriteLine( "[SteeringEffects] <<< SaveCalibration" );
	}

	private static float FilterWeight( float angleDeg, float innerDeg, float outerDeg )
	{
		var absAngle = MathF.Abs( angleDeg );

		if ( absAngle <= innerDeg ) return 0f;
		if ( absAngle >= outerDeg ) return 1f;

		var t = ( absAngle - innerDeg ) / ( outerDeg - innerDeg );

		return MathZ.Smoothstep( 0f, 1f, t );
	}

	public void ClearCalibration()
	{
		// clear out the data tables

		_numSteeringWheelAnglesRecorded = 0;

		Array.Clear( _steeringWheelAnglesInDegrees );
		Array.Clear( _yawRateDataInDegreesPerSecond );
		Array.Clear( _expectedYawRateInDegreesPerSecond );

		// clear out calibration

		_calibrationIsValid = false;

		// reset the currently active calibration file name

		_currentlyActiveCalibrationFileName = string.Empty;

		// clear the graph

		DrawCalibrationGraphGrid();
	}

	public void LoadCalibration()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[SteeringEffects] LoadCalibration >>>" );

		// don't load the calibration file if we are currently calibrating

		if ( _calibrationPhase != CalibrationPhase.NotCalibrating )
		{
			app.Logger.WriteLine( "[SteeringEffects] We are currently calibrating" );
		}
		else
		{
			// figure out which calibration file we need to load

			var settings = DataContext.DataContext.Instance.Settings;

			if ( settings.SteeringEffectsCalibrationFileName == null )
			{
				app.Logger.WriteLine( $"[SteeringEffects] Calibration file name property value is null (shouldn't be possible!)" );

				ClearCalibration();
			}
			else if ( settings.SteeringEffectsCalibrationFileName == string.Empty )
			{
				app.Logger.WriteLine( $"[SteeringEffects] No calibration file selected for this tire compound" );

				ClearCalibration();
			}
			else if ( settings.SteeringEffectsCalibrationFileName == _currentlyActiveCalibrationFileName )
			{
				app.Logger.WriteLine( $"[SteeringEffects] Calibration file is already loaded" );
			}
			else
			{
				// clear the current calibration

				ClearCalibration();

				// keep track of whether the file load was good or not

				var fileLoadWasSuccessful = true;

				// open file

				var filePath = Path.Combine( CalibrationDirectory, $"{settings.SteeringEffectsCalibrationFileName}.csv" );

				if ( !File.Exists( filePath ) )
				{
					app.Logger.WriteLine( $"[SteeringEffects] Calibration file not found: {filePath}" );

					fileLoadWasSuccessful = false;
				}
				else
				{
					using var reader = new StreamReader( filePath );

					// skip the first two lines

					var carInfoLine = reader.ReadLine();
					var headerLine = reader.ReadLine();

					// read header line and extract steering wheel angles

					while ( !reader.EndOfStream )
					{
						var line = reader.ReadLine();

						if ( string.IsNullOrWhiteSpace( line ) ) continue;

						var parts = line.Split( ',' );

						if ( !float.TryParse( parts[ 0 ], NumberStyles.Float, CultureInfo.InvariantCulture, out var steeringWheelAngleInDegrees ) ) continue;
						if ( !float.TryParse( parts[ 1 ], NumberStyles.Float, CultureInfo.InvariantCulture, out var yawRateInDegreesPerSecond ) ) continue;

						_steeringWheelAnglesInDegrees[ _numSteeringWheelAnglesRecorded ] = steeringWheelAngleInDegrees;
						_yawRateDataInDegreesPerSecond[ _numSteeringWheelAnglesRecorded ] = yawRateInDegreesPerSecond;

						_numSteeringWheelAnglesRecorded++;
					}
				}

				if ( fileLoadWasSuccessful )
				{
					var descComparer = Comparer<float>.Create( ( a, b ) => b.CompareTo( a ) ); // reverse order

					// Build fast lookup: angle (deg) -> expected yaw rate (DPS)

					for ( var angleIndex = 0; angleIndex < _expectedYawRateInDegreesPerSecond.Length; angleIndex++ )
					{
						var steeringWheelAngleInDegrees = angleIndex - MaxSteeringWheelAngleInDegrees;

						// Find exact or insertion index in the calibration angles

						var foundOrInsertion = Array.BinarySearch( _steeringWheelAnglesInDegrees, steeringWheelAngleInDegrees, descComparer );

						if ( foundOrInsertion >= 0 )
						{
							// Exact hit on a calibration angle

							_expectedYawRateInDegreesPerSecond[ angleIndex ] = _yawRateDataInDegreesPerSecond[ foundOrInsertion ];
						}
						else
						{
							// No exact hit; ~val is the insertion point of angleDegrees

							var nextIndex = ~foundOrInsertion;

							// Clamp outside the calibration domain

							if ( nextIndex <= 0 )
							{
								_expectedYawRateInDegreesPerSecond[ angleIndex ] = _yawRateDataInDegreesPerSecond[ 0 ];
							}
							else if ( nextIndex >= _steeringWheelAnglesInDegrees.Length )
							{
								_expectedYawRateInDegreesPerSecond[ angleIndex ] = _yawRateDataInDegreesPerSecond[ _numSteeringWheelAnglesRecorded - 1 ];
							}
							else
							{
								// Interpolate between prevIndex and nextIndex

								var prevIndex = nextIndex - 1;

								var x0 = _steeringWheelAnglesInDegrees[ prevIndex ];
								var x1 = _steeringWheelAnglesInDegrees[ nextIndex ];

								var y0 = _yawRateDataInDegreesPerSecond[ prevIndex ];
								var y1 = _yawRateDataInDegreesPerSecond[ nextIndex ];

								// Guard against duplicate x’s

								if ( x1 == x0 )
								{
									_expectedYawRateInDegreesPerSecond[ angleIndex ] = y0;
								}
								else
								{
									var t = ( steeringWheelAngleInDegrees - x0 ) / ( x1 - x0 );

									_expectedYawRateInDegreesPerSecond[ angleIndex ] = y0 + t * ( y1 - y0 );
								}
							}
						}
					}

					// apply zero-phase low-pass butterworth filter to get rid of suspension / steering noise pollution

					var filteredExpectedYawRateInDegreesPerSecond = Butterworth.FiltfiltLowpass( _expectedYawRateInDegreesPerSecond, 3, 0.07f );

					// blending - full raw inside ±20°, fade to full filtered by ±40°

					for ( var angleIndex = 0; angleIndex < _expectedYawRateInDegreesPerSecond.Length; angleIndex++ )
					{
						var weightFiltered = FilterWeight( angleIndex - MaxSteeringWheelAngleInDegrees, 20f, 40f );
						var weightRaw = 1f - weightFiltered;

						_expectedYawRateInDegreesPerSecond[ angleIndex ] = ( weightRaw * _expectedYawRateInDegreesPerSecond[ angleIndex ] ) + ( weightFiltered * filteredExpectedYawRateInDegreesPerSecond[ angleIndex ] );
					}

					// calibration is valid

					_calibrationIsValid = true;

					// update the graph

					app.Dispatcher.Invoke( () =>
					{
						var drawingVisual = new DrawingVisual();

						using var drawingContext = drawingVisual.RenderOpen();

						for ( var angleIndex = 1; angleIndex < _expectedYawRateInDegreesPerSecond.Length; angleIndex++ )
						{
							var p1 = new Point( angleIndex - 0.5, CalibrationGraphHeight - _expectedYawRateInDegreesPerSecond[ angleIndex - 1 ] * 100.0 + 0.5 );
							var p2 = new Point( angleIndex + 0.5, CalibrationGraphHeight - _expectedYawRateInDegreesPerSecond[ angleIndex - 0 ] * 100.0 + 0.5 );

							drawingContext.DrawLine( _calibrationGraphPen, p1, p2 );
						}

						drawingContext.Close();

						_calibrationGraphRenderTargetBitmap.Render( drawingVisual );
					} );
				}
			}
		}

		//

		app.Logger.WriteLine( "[SteeringEffects] <<< LoadCalibration" );
	}

	public void Tick( App app )
	{
		if ( app.MainWindow.SteeringEffectsTabItemIsVisible )
		{
			var localization = DataContext.DataContext.Instance.Localization;

			app.MainWindow.SteeringEffects_CalibrationProgress_Label.Text = $"{localization[ "Progress" ]} {_calibrationProgress * 100f:F0}{localization[ "Percent" ]}";
			app.MainWindow.SteeringEffects_CarSetupName_TextBlock.Text = $"{localization[ "CurrentCarSetup" ]} {app.Simulator.CarSetupName.ToUpper()}";

			var disableButtons = ( app.Simulator.TrackDisplayName != "Centripetal Circuit" );

			app.MainWindow.SteeringEffects_NotOnCentripetalCircuitTrack_TextBlock.Visibility = disableButtons ? Visibility.Visible : Visibility.Collapsed;

			if ( _calibrationPhase == CalibrationPhase.NotCalibrating )
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
