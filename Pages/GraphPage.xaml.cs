
using System.Windows;

using UserControl = System.Windows.Controls.UserControl;

using MarvinsAIRARefactored.Classes;

namespace MarvinsAIRARefactored.Pages;

public partial class GraphPage : UserControl
{
	public GraphPage()
	{
		InitializeComponent();
	}

	#region User Control Events

	private void Target_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		if ( BottomPanel_StackPanel.Visibility == Visibility.Visible )
		{
			Misc.ApplyToTaggedElements( Root, "HideWhenGraphIsSoloed", element => element.Visibility = Visibility.Collapsed );

			Border.Margin = new Thickness( 0 );

			app.MainWindow.WindowStyle = WindowStyle.None;
			app.MainWindow.ResizeMode = ResizeMode.NoResize;
			app.MainWindow.SizeToContent = SizeToContent.Height;
		}
		else
		{
			Misc.ApplyToTaggedElements( Root, "HideWhenGraphIsSoloed", element => element.Visibility = Visibility.Visible );

			Border.Margin = new Thickness( 0, 10, 0, 0 );

			app.MainWindow.WindowStyle = WindowStyle.SingleBorderWindow;
			app.MainWindow.ResizeMode = ResizeMode.CanResizeWithGrip;
			app.MainWindow.SizeToContent = SizeToContent.Manual;
		}
	}

	#endregion
}
