
using System.Runtime.CompilerServices;

namespace MarvinsAIRARefactored.Components;

public class Debug
{
	private const int UpdateInterval = 6;

	public string Label_1 { private get; set; } = string.Empty;
	public string Label_2 { private get; set; } = string.Empty;
	public string Label_3 { private get; set; } = string.Empty;
	public string Label_4 { private get; set; } = string.Empty;
	public string Label_5 { private get; set; } = string.Empty;
	public string Label_6 { private get; set; } = string.Empty;
	public string Label_7 { private get; set; } = string.Empty;
	public string Label_8 { private get; set; } = string.Empty;
	public string Label_9 { private get; set; } = string.Empty;
	public string Label_10 { private get; set; } = string.Empty;

	private int _updateCounter = UpdateInterval + 1;

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public void Tick( App app )
	{
		_updateCounter--;

		if ( _updateCounter == 0 )
		{
			_updateCounter = UpdateInterval;

			if ( app.MainWindow.DebugTabItemIsVisible )
			{
				//FIX app.MainWindow.Debug_Label_1.Content = Label_1;
				//FIX app.MainWindow.Debug_Label_2.Content = Label_2;
				//FIX app.MainWindow.Debug_Label_3.Content = Label_3;
				//FIX app.MainWindow.Debug_Label_4.Content = Label_4;
				//FIX app.MainWindow.Debug_Label_5.Content = Label_5;
				//FIX app.MainWindow.Debug_Label_6.Content = Label_6;
				//FIX app.MainWindow.Debug_Label_7.Content = Label_7;
				//FIX app.MainWindow.Debug_Label_8.Content = Label_8;
				//FIX app.MainWindow.Debug_Label_9.Content = Label_9;
				//FIX app.MainWindow.Debug_Label_10.Content = Label_10;
			}
		}
	}
}
