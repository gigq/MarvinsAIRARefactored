
using System.Windows;

using UserControl = System.Windows.Controls.UserControl;

namespace MarvinsAIRARefactored.Controls;

public partial class MairaGroupHeader : UserControl
{
	public MairaGroupHeader()
	{
		InitializeComponent();
	}

	#region Dependency Properties

	public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register( nameof( HeaderText ), typeof( string ), typeof( MairaGroupHeader ), new PropertyMetadata( string.Empty ) );

	public string HeaderText
	{
		get => (string) GetValue( HeaderTextProperty );
		set => SetValue( HeaderTextProperty, value );
	}

	#endregion
}
