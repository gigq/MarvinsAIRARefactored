
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

	public static readonly DependencyProperty LabelProperty = DependencyProperty.Register( nameof( Label ), typeof( string ), typeof( MairaGroupHeader ), new PropertyMetadata( string.Empty ) );

	public string Label
	{
		get => (string) GetValue( LabelProperty );
		set => SetValue( LabelProperty, value );
	}

	public static readonly DependencyProperty SubLabelProperty = DependencyProperty.Register( nameof( SubLabel ), typeof( string ), typeof( MairaGroupHeader ), new PropertyMetadata( string.Empty ) );

	public string SubLabel
	{
		get => (string) GetValue( SubLabelProperty );
		set => SetValue( SubLabelProperty, value );
	}

	#endregion
}
