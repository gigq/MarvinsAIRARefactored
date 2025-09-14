
using System.Windows;
using UserControl = System.Windows.Controls.UserControl;

namespace MarvinsAIRARefactored.Pages;

public partial class PedalsPage : UserControl
{
	public PedalsPage()
	{
		InitializeComponent();
	}

	#region User Control Events

	private void ClutchTest1_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 0, 0 );
	}

	private void ClutchTest2_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 0, 1 );
	}

	private void ClutchTest3_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 0, 2 );
	}

	private void BrakeTest1_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 1, 0 );
	}

	private void BrakeTest2_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 1, 1 );
	}

	private void BrakeTest3_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 1, 2 );
	}

	private void ThrottleTest1_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 2, 0 );
	}

	private void ThrottleTest2_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 2, 1 );
	}

	private void ThrottleTest3_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 2, 2 );
	}

	#endregion
}
