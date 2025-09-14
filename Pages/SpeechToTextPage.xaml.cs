
using System.Windows;

using UserControl = System.Windows.Controls.UserControl;

namespace MarvinsAIRARefactored.Pages;

public partial class SpeechToTextPage : UserControl
{
	public SpeechToTextPage()
	{
		InitializeComponent();
	}

	#region User Control Events

	private void ResetOverlayWindow_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.SpeechToTextWindow.ResetWindow();
	}

	#endregion
}
