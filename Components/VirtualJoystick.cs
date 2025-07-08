
using MarvinsAIRARefactored.Classes;

using vJoyInterfaceWrap;

namespace MarvinsAIRARefactored.Components;

public class VirtualJoystick
{
	public uint JoystickId { get; set; } = 1;
	public float SteeringWheelAngle { get; set; } = 0f;
	public float Throttle { get; set; } = 0f;
	public float Brake { get; set; } = 0f;
	public bool ShiftUp { get; set; } = false;
	public bool ShiftDown { get; set; } = false;
	public bool ActiveResetSave { get; set; } = false;
	public bool ActiveResetRun { get; set; } = false;

	private long _minimumX = 0;
	private long _maximumX = 0;

	private long _minimumY = 0;
	private long _maximumY = 0;

	private long _minimumZ = 0;
	private long _maximumZ = 0;

	private readonly vJoy _vJoy = new();

	private vJoy.JoystickState _joystickState;

	private bool _initialized = false;

	public VirtualJoystick()
	{
		var app = App.Instance!;

		if ( !_vJoy.vJoyEnabled() )
		{
			app.Logger.WriteLine( "[VirtualJoystick] Driver is not enabled" );
		}
		else
		{
			app.Logger.WriteLine( $"[VirtualJoystick] Vendor is {_vJoy.GetvJoyManufacturerString()}" );
			app.Logger.WriteLine( $"[VirtualJoystick] Vendor is {_vJoy.GetvJoyProductString()}" );
			app.Logger.WriteLine( $"[VirtualJoystick] Vendor is {_vJoy.GetvJoySerialNumberString()}" );

			UInt32 dllVer = 0, drvVer = 0;

			if ( !_vJoy.DriverMatch( ref dllVer, ref drvVer ) )
			{
				app.Logger.WriteLine( $"[VirtualJoystick] DLL version ({dllVer}) does not match driver version ({drvVer})" );
			}
			else
			{
				app.Logger.WriteLine( "[VirtualJoystick] DLL version is correct" );
			}

			var vjdStatus = _vJoy.GetVJDStatus( JoystickId );

			if ( ( vjdStatus != VjdStat.VJD_STAT_OWN ) && ( vjdStatus != VjdStat.VJD_STAT_FREE ) )
			{
				app.Logger.WriteLine( $"[VirtualJoystick] Joystick {JoystickId} is not owned or free" );
			}
			else
			{
				if ( !_vJoy.AcquireVJD( JoystickId ) )
				{
					app.Logger.WriteLine( $"[VirtualJoystick] Joystick {JoystickId} could not be acquired" );
				}
				else
				{
					_vJoy.ResetVJD( JoystickId );

					_vJoy.GetVJDAxisMin( JoystickId, HID_USAGES.HID_USAGE_X, ref _minimumX );
					_vJoy.GetVJDAxisMax( JoystickId, HID_USAGES.HID_USAGE_X, ref _maximumX );

					_vJoy.GetVJDAxisMin( JoystickId, HID_USAGES.HID_USAGE_Y, ref _minimumY );
					_vJoy.GetVJDAxisMax( JoystickId, HID_USAGES.HID_USAGE_Y, ref _maximumY );

					_vJoy.GetVJDAxisMin( JoystickId, HID_USAGES.HID_USAGE_Z, ref _minimumZ );
					_vJoy.GetVJDAxisMax( JoystickId, HID_USAGES.HID_USAGE_Z, ref _maximumZ );

					_initialized = true;
				}
			}
		}
	}

	public void Tick( App app )
	{
		if ( _initialized )
		{
			_joystickState.bDevice = (byte) JoystickId;

			_joystickState.AxisX = (int) MathF.Round( Misc.Lerp( _minimumX, _maximumX, SteeringWheelAngle * 0.5f + 0.5f ) );
			_joystickState.AxisY = (int) MathF.Round( Misc.Lerp( _minimumY, _maximumY, Throttle ) );
			_joystickState.AxisZ = (int) MathF.Round( Misc.Lerp( _minimumZ, _maximumZ, Brake ) );

			uint shiftUp = ShiftUp ? (uint) 0x00000001 : 0;
			uint shiftDown = ShiftDown ? (uint) 0x00000002 : 0;
			uint activeResetSave = ActiveResetSave ? (uint) 0x00000004 : 0;
			uint activeResetRun = ActiveResetRun ? (uint) 0x00000008 : 0;

			ShiftUp = false;
			ShiftDown = false;
			ActiveResetSave = false;
			ActiveResetRun = false;

			_joystickState.Buttons = shiftUp | shiftDown | activeResetSave | activeResetRun;

			if ( !_vJoy.UpdateVJD( JoystickId, ref _joystickState ) )
			{
				if ( !_vJoy.AcquireVJD( JoystickId ) )
				{
					app.Logger.WriteLine( $"[VirtualJoystick] Joystick {JoystickId} could not be re-acquired" );

					_initialized = false;
				}
			}
		}
	}
}
