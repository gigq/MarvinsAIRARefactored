
using System.Runtime.CompilerServices;

using MarvinsAIRARefactored.Controls;
using MarvinsAIRARefactored.Windows;

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

			if ( MairaAppMenuPopup.CurrentAppPage == MainWindow.AppPage.Debug )
			{
				MainWindow._debugPage.Debug_TextBlock_1.Text = Label_1;
				MainWindow._debugPage.Debug_TextBlock_2.Text = Label_2;
				MainWindow._debugPage.Debug_TextBlock_3.Text = Label_3;
				MainWindow._debugPage.Debug_TextBlock_4.Text = Label_4;
				MainWindow._debugPage.Debug_TextBlock_5.Text = Label_5;
				MainWindow._debugPage.Debug_TextBlock_6.Text = Label_6;
				MainWindow._debugPage.Debug_TextBlock_7.Text = Label_7;
				MainWindow._debugPage.Debug_TextBlock_8.Text = Label_8;
				MainWindow._debugPage.Debug_TextBlock_9.Text = Label_9;
				MainWindow._debugPage.Debug_TextBlock_10.Text = Label_10;
			}
		}
	}
}
