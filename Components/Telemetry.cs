
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace MarvinsAIRARefactored.Components;

public class Telemetry
{
	private const string MemoryMappedFileName = "Local\\MAIRARefactoredTelemetry";
	private const int MaxStringLengthInBytes = 256;

	[StructLayout( LayoutKind.Sequential, Pack = 4 )]
	public unsafe struct DataBufferStruct
	{
		public int tickCount;

		public float racingWheelStrength;
		public float racingWheelMaxForce;

		public float racingWheelAutoTorque;

		public int racingWheelAlgorithm;
		public fixed byte racingWheelAlgorithmName[ MaxStringLengthInBytes ];

		public bool racingWheelAlgorithmSoftLimiterIsEnabled;
		public fixed byte racingWheelAlgorithmSoftLimiterName[ MaxStringLengthInBytes ];
		public fixed byte racingWheelAlgorithmSoftLimiterValue[ MaxStringLengthInBytes ];

		public fixed float racingWheelAlgorithmSettings[ 4 ];
		public fixed byte racingWheelAlgorithmSettingNames[ 4 * MaxStringLengthInBytes ];
		public fixed byte racingWheelAlgorithmSettingValues[ 4 * MaxStringLengthInBytes ];

		public float racingWheelOutputTorque;
		public bool racingWheelOutputTorqueIsClipping;

		public bool racingWheelCrashProtectionIsActive;
		public bool racingWheelCurbProtectionIsActive;

		public bool racingWheelFadingIsActive;

		public float steeringEffectsUndersteerEffect;
		public float steeringEffectsOversteerEffect;
		public float steeringEffectsSeatOfPantsEffect;
		public float steeringEffectsSkidSlip;

		public float pedalsClutchFrequency;
		public float pedalsClutchAmplitude;

		public float pedalsBrakeFrequency;
		public float pedalsBrakeAmplitude;

		public float pedalsThrottleFrequency;
		public float pedalsThrottleAmplitude;

		public void SetRacingWheelAlgorithmName( string? value )
		{
			fixed ( byte* bytePtr = racingWheelAlgorithmName )
			{
				WriteString( bytePtr, 0, MaxStringLengthInBytes, value );
			}
		}

		public void SetRacingWheelAlgorithmSoftLimiterName( string? value )
		{
			fixed ( byte* bytePtr = racingWheelAlgorithmSoftLimiterName )
			{
				WriteString( bytePtr, 0, MaxStringLengthInBytes, value );
			}
		}

		public void SetRacingWheelAlgorithmSoftLimiterValue( string? value )
		{
			fixed ( byte* bytePtr = racingWheelAlgorithmSoftLimiterValue )
			{
				WriteString( bytePtr, 0, MaxStringLengthInBytes, value );
			}
		}

		public void SetRacingWheelAlgorithmSettingName( int index, string? value )
		{
			if ( index < 0 || index >= 5 ) return;

			fixed ( byte* bytePtr = racingWheelAlgorithmSettingNames )
			{
				WriteString( bytePtr, index, MaxStringLengthInBytes, value );
			}
		}

		public void SetRacingWheelAlgorithmSettingValue( int index, string? value )
		{
			if ( index < 0 || index >= 5 ) return;

			fixed ( byte* bytePtr = racingWheelAlgorithmSettingValues )
			{
				WriteString( bytePtr, index, MaxStringLengthInBytes, value );
			}
		}

		public static unsafe void WriteString( byte* bytePtr, int index, int capacity, string? value )
		{
			if ( bytePtr == null || capacity <= 0 ) return;

			var offset = index * capacity;

			if ( string.IsNullOrEmpty( value ) )
			{
				bytePtr[ offset ] = 0;
				return;
			}

			var bytes = Encoding.UTF8.GetBytes( value );

			var length = Math.Min( bytes.Length, capacity - 1 );

			Marshal.Copy( bytes, 0, (IntPtr) bytePtr + offset, length );

			bytePtr[ offset + length ] = 0;
		}
	}

	[StructLayout( LayoutKind.Sequential, Pack = 4 )]
	public unsafe struct DataStruct
	{
		public int version;
		public int bufferIndex;

		public DataBufferStruct buffer0;
		public DataBufferStruct buffer1;
		public DataBufferStruct buffer2;

		public static ref DataBufferStruct GetDataBuffer( ref DataStruct dataStruct, int index )
		{
			switch ( index )
			{
				case 0: return ref dataStruct.buffer0;
				case 1: return ref dataStruct.buffer1;
				default: return ref dataStruct.buffer2;
			}
		}
	}

	private DataStruct _data = new();
	private int _currentBufferIndex = 0;

	private MemoryMappedFile? _memoryMappedFile = null;
	private MemoryMappedViewAccessor? _memoryMappedFileViewAccessor = null;

	public void Initialize()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[Telemetry] Initialize >>>" );

		var sizeOfTelemetryData = Marshal.SizeOf<DataStruct>();

		_memoryMappedFile = MemoryMappedFile.CreateOrOpen( MemoryMappedFileName, sizeOfTelemetryData );
		_memoryMappedFileViewAccessor = _memoryMappedFile.CreateViewAccessor();

		app.Logger.WriteLine( "[Telemetry] <<< Initialize" );
	}

	public void Shutdown()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[Telemetry] Shutdown >>>" );

		_memoryMappedFileViewAccessor = null;
		_memoryMappedFile = null;

		app.Logger.WriteLine( "[Telemetry] <<< Shutdown" );
	}

	public void Tick( App app )
	{
		var localization = DataContext.DataContext.Instance.Localization;
		var settings = DataContext.DataContext.Instance.Settings;

		// get the buffer to write to

		_currentBufferIndex = ( _currentBufferIndex + 1 ) % 3;

		ref var dataBuffer = ref DataStruct.GetDataBuffer( ref _data, _currentBufferIndex );

		// update the buffer

		dataBuffer.tickCount++;

		// racing wheel

		dataBuffer.racingWheelStrength = settings.RacingWheelStrength;
		dataBuffer.racingWheelMaxForce = settings.RacingWheelMaxForce;

		dataBuffer.racingWheelAutoTorque = app.RacingWheel.AutoTorque;

		dataBuffer.racingWheelAlgorithm = (int) settings.RacingWheelAlgorithm;
		dataBuffer.SetRacingWheelAlgorithmName( localization[ settings.RacingWheelAlgorithm.ToString() ] );

		dataBuffer.racingWheelAlgorithmSoftLimiterIsEnabled = settings.RacingWheelEnableSoftLimiter;
		dataBuffer.SetRacingWheelAlgorithmSoftLimiterName( localization[ "SoftClipping" ] );
		dataBuffer.SetRacingWheelAlgorithmSoftLimiterValue( settings.RacingWheelEnableSoftLimiter ? localization[ "ON" ] : localization[ "OFF" ] );

		unsafe
		{
			for ( var index = 0; index < 4; index++ )
			{
				dataBuffer.racingWheelAlgorithmSettings[ index ] = 0f;

				dataBuffer.SetRacingWheelAlgorithmSettingName( index, null );
				dataBuffer.SetRacingWheelAlgorithmSettingValue( index, null );
			}

			switch ( settings.RacingWheelAlgorithm )
			{
				case RacingWheel.Algorithm.DetailBooster:
				case RacingWheel.Algorithm.DetailBoosterOn60Hz:

					dataBuffer.racingWheelAlgorithmSettings[ 0 ] = settings.RacingWheelDetailBoost;
					dataBuffer.racingWheelAlgorithmSettings[ 1 ] = settings.RacingWheelDetailBoostBias;

					dataBuffer.SetRacingWheelAlgorithmSettingName( 0, localization[ "DetailBoost" ] );
					dataBuffer.SetRacingWheelAlgorithmSettingName( 1, localization[ "DetailBoostBias" ] );

					dataBuffer.SetRacingWheelAlgorithmSettingValue( 0, settings.RacingWheelDetailBoostString );
					dataBuffer.SetRacingWheelAlgorithmSettingValue( 1, settings.RacingWheelDetailBoostBiasString );

					break;

				case RacingWheel.Algorithm.DeltaLimiter:
				case RacingWheel.Algorithm.DeltaLimiterOn60Hz:

					dataBuffer.racingWheelAlgorithmSettings[ 0 ] = settings.RacingWheelDeltaLimit;
					dataBuffer.racingWheelAlgorithmSettings[ 1 ] = settings.RacingWheelDeltaLimiterBias;

					dataBuffer.SetRacingWheelAlgorithmSettingName( 0, localization[ "DeltaLimit" ] );
					dataBuffer.SetRacingWheelAlgorithmSettingName( 1, localization[ "DeltaLimiterBias" ] );

					dataBuffer.SetRacingWheelAlgorithmSettingValue( 0, settings.RacingWheelDeltaLimitString );
					dataBuffer.SetRacingWheelAlgorithmSettingValue( 1, settings.RacingWheelDeltaLimiterBiasString );

					break;

				case RacingWheel.Algorithm.SlewAndTotalCompression:

					dataBuffer.racingWheelAlgorithmSettings[ 0 ] = settings.RacingWheelSlewCompressionThreshold;
					dataBuffer.racingWheelAlgorithmSettings[ 1 ] = settings.RacingWheelSlewCompressionRate;
					dataBuffer.racingWheelAlgorithmSettings[ 2 ] = settings.RacingWheelTotalCompressionThreshold;
					dataBuffer.racingWheelAlgorithmSettings[ 3 ] = settings.RacingWheelTotalCompressionRate;

					dataBuffer.SetRacingWheelAlgorithmSettingName( 0, localization[ "SlewCompressionThreshold" ] );
					dataBuffer.SetRacingWheelAlgorithmSettingName( 1, localization[ "SlewCompressionRate" ] );
					dataBuffer.SetRacingWheelAlgorithmSettingName( 2, localization[ "TotalCompressionThreshold" ] );
					dataBuffer.SetRacingWheelAlgorithmSettingName( 3, localization[ "TotalCompressionRate" ] );

					dataBuffer.SetRacingWheelAlgorithmSettingValue( 0, settings.RacingWheelSlewCompressionThresholdString );
					dataBuffer.SetRacingWheelAlgorithmSettingValue( 1, settings.RacingWheelSlewCompressionRateString );
					dataBuffer.SetRacingWheelAlgorithmSettingValue( 2, settings.RacingWheelTotalCompressionThresholdString );
					dataBuffer.SetRacingWheelAlgorithmSettingValue( 3, settings.RacingWheelTotalCompressionRateString );

					break;

				case RacingWheel.Algorithm.MultiAdjustmentToolkit:

					dataBuffer.racingWheelAlgorithmSettings[ 0 ] = settings.RacingWheelMultiTorqueCompression;
					dataBuffer.racingWheelAlgorithmSettings[ 1 ] = settings.RacingWheelMultiSlewRateReduction;
					dataBuffer.racingWheelAlgorithmSettings[ 2 ] = settings.RacingWheelMultiDetailGain;
					dataBuffer.racingWheelAlgorithmSettings[ 3 ] = settings.RacingWheelMultiOutputSmoothing;

					dataBuffer.SetRacingWheelAlgorithmSettingName( 0, localization[ "TorqueCompression" ] );
					dataBuffer.SetRacingWheelAlgorithmSettingName( 1, localization[ "SlewRateReduction" ] );
					dataBuffer.SetRacingWheelAlgorithmSettingName( 2, localization[ "DetailGain" ] );
					dataBuffer.SetRacingWheelAlgorithmSettingName( 3, localization[ "OutputSmoothing" ] );

					dataBuffer.SetRacingWheelAlgorithmSettingValue( 0, settings.RacingWheelMultiTorqueCompressionString );
					dataBuffer.SetRacingWheelAlgorithmSettingValue( 1, settings.RacingWheelMultiSlewRateReductionString );
					dataBuffer.SetRacingWheelAlgorithmSettingValue( 2, settings.RacingWheelMultiDetailGainString );
					dataBuffer.SetRacingWheelAlgorithmSettingValue( 3, settings.RacingWheelMultiOutputSmoothingString );

					break;
			}
		}

		dataBuffer.racingWheelOutputTorque = app.RacingWheel.OutputTorque;
		dataBuffer.racingWheelOutputTorqueIsClipping = ( app.RacingWheel.OutputTorque < -1f ) || ( app.RacingWheel.OutputTorque > 1f );

		dataBuffer.racingWheelCrashProtectionIsActive = app.RacingWheel.CrashProtectionIsActive;
		dataBuffer.racingWheelCurbProtectionIsActive = app.RacingWheel.CurbProtectionIsActive;

		dataBuffer.racingWheelFadingIsActive = app.RacingWheel.FadingIsActive;

		// pedals

		dataBuffer.pedalsClutchFrequency = app.Pedals.ClutchFrequency;
		dataBuffer.pedalsClutchAmplitude = app.Pedals.ClutchAmplitude;

		dataBuffer.pedalsBrakeFrequency = app.Pedals.BrakeFrequency;
		dataBuffer.pedalsBrakeAmplitude = app.Pedals.BrakeAmplitude;

		dataBuffer.pedalsThrottleFrequency = app.Pedals.ThrottleFrequency;
		dataBuffer.pedalsThrottleAmplitude = app.Pedals.ThrottleAmplitude;

		// steering effects

		dataBuffer.steeringEffectsUndersteerEffect = app.SteeringEffects.UndersteerEffect;
		dataBuffer.steeringEffectsOversteerEffect = app.SteeringEffects.OversteerEffect;
		dataBuffer.steeringEffectsSeatOfPantsEffect = app.SteeringEffects.SeatOfPantsEffect;
		dataBuffer.steeringEffectsSkidSlip = app.SteeringEffects.SkidSlip;

		// let SimHub know this buffer is ready for reading

		_data.version = 4;
		_data.bufferIndex = _currentBufferIndex;

		_memoryMappedFileViewAccessor?.Write( 0, ref _data );
	}
}
