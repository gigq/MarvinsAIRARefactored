
using System.Windows;

using UserControl = System.Windows.Controls.UserControl;

namespace MarvinsAIRARefactored.Pages;

public partial class DebugPage : UserControl
{
	public DebugPage()
	{
		InitializeComponent();
	}

	#region User Control Events

	private void ResetRecording_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.RecordingManager.ResetRecording();
	}

	private void SaveRecording_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.RecordingManager.SaveRecording();
	}

	#endregion
}
