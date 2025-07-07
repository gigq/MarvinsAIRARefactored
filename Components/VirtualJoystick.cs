
using vJoyInterfaceWrap;

using MarvinsAIRARefactored.Classes;

namespace MarvinsAIRARefactored.Components;

public class VirtualJoystick
{
	public uint JoystickId { get; set; } = 1;
	public float SteeringWheelAngle { get; set; } = 0f;
	public float Throttle {  get; set; } = 0f;
	public float Brake {  get; set; } = 0f;
	public bool ShiftUp { get; set; } = false;
	public bool ShiftDown { get; set; } = false;

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

			_joystickState.AxisX = (int) Misc.Lerp( 0f, 65535f, SteeringWheelAngle * 0.5f + 0.5f );
			_joystickState.AxisY = (int) Misc.Lerp( 0f, 4095f, Throttle );
			_joystickState.AxisZ = (int) Misc.Lerp( 0f, 4095f, Brake );

			uint shiftUp = ShiftUp ? (uint) 0x00000001 : 0;
			uint shiftDown = ShiftDown ? (uint) 0x00000002 : 0;

			ShiftUp = false;
			ShiftDown = false;

			_joystickState.Buttons = shiftUp | shiftDown;

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
