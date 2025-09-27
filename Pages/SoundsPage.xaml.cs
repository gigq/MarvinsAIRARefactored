
using System.Windows;

using UserControl = System.Windows.Controls.UserControl;

using MarvinsAIRARefactored.Components;

namespace MarvinsAIRARefactored.Pages;

public partial class SoundsPage : UserControl
{
	public SoundsPage()
	{
		InitializeComponent();
	}

	#region User Control Events

	private void ABSEngaged_Test_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Sounds.Test( Sounds.SoundEffectType.ABSEngaged );
	}

	private void WheelLock_Test_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Sounds.Test( Sounds.SoundEffectType.WheelLock );
	}

	private void WheelSpin_Test_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Sounds.Test( Sounds.SoundEffectType.WheelSpin );
	}

	private void Understeer_Test_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Sounds.Test( Sounds.SoundEffectType.Understeer );
	}

	private void Oversteer_Test_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Sounds.Test( Sounds.SoundEffectType.Oversteer );
	}

	private void SeatOfPants_Test_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Sounds.Test( Sounds.SoundEffectType.SeatOfPants );
	}

	#endregion
}
