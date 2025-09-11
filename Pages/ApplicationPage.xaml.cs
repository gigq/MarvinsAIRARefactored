
using System.Windows;

using UserControl = System.Windows.Controls.UserControl;

namespace MarvinsAIRARefactored.Pages;

public partial class ApplicationPage : UserControl
{
	public ApplicationPage()
	{
		InitializeComponent();
	}

	#region User Control Events

	private async void CheckNow_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		await app.CloudService.CheckForUpdates( true );
	}

	#endregion
}
