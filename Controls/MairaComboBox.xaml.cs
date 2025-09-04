
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using UserControl = System.Windows.Controls.UserControl;

using MarvinsAIRARefactored.DataContext;
using MarvinsAIRARefactored.Windows;

namespace MarvinsAIRARefactored.Controls;

public partial class MairaComboBox : UserControl
{
	private readonly SolidColorBrush _normalSelectedValueBrush = new( (Color) ColorConverter.ConvertFromString( "#ff5b2e" ) );
	private readonly SolidColorBrush _offSelectedValueBrush = new( (Color) ColorConverter.ConvertFromString( "#eeeeee" ) );

	public MairaComboBox()
	{
		InitializeComponent();

		_normalSelectedValueBrush.Freeze();
		_offSelectedValueBrush.Freeze();

		UpdateSelectedValueVisuals();
	}

	#region User Control Events

	private void TextBlock_PreviewMouseRightButtonDown( object sender, MouseButtonEventArgs e )
	{
		var app = App.Instance!;

		e.Handled = true;

		if ( ContextSwitches != null )
		{
			app.Logger.WriteLine( "[MairaComboBox] Showing update context switches window" );

			var updateContextSwitchesWindow = new UpdateContextSwitchesWindow( ContextSwitches )
			{
				Owner = app.MainWindow
			};

			updateContextSwitchesWindow.ShowDialog();
		}
	}

	#endregion

	#region Dependency Properties

	public static readonly DependencyProperty LabelProperty = DependencyProperty.Register( nameof( Label ), typeof( string ), typeof( MairaComboBox ), new PropertyMetadata( string.Empty ) );

	public string Label
	{
		get => (string) GetValue( LabelProperty );
		set => SetValue( LabelProperty, value );
	}

	public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register( nameof( ItemsSource ), typeof( object ), typeof( MairaComboBox ) );

	public object ItemsSource
	{
		get => GetValue( ItemsSourceProperty );
		set => SetValue( ItemsSourceProperty, value );
	}

	public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register( nameof( SelectedValue ), typeof( object ), typeof( MairaComboBox ), new PropertyMetadata( null, SelectedValueChanged ) );

	public object SelectedValue
	{
		get => GetValue( SelectedValueProperty );
		set => SetValue( SelectedValueProperty, value );
	}

	public static readonly DependencyProperty OffValueProperty = DependencyProperty.Register( nameof( OffValue ), typeof( object ), typeof( MairaComboBox ), new PropertyMetadata( null, OffValueChanged ) );

	public object OffValue
	{
		get => GetValue( OffValueProperty );
		set => SetValue( OffValueProperty, value );
	}

	public static readonly DependencyProperty ContextSwitchesProperty = DependencyProperty.Register( nameof( ContextSwitches ), typeof( ContextSwitches ), typeof( MairaComboBox ), new PropertyMetadata( null ) );

	public ContextSwitches ContextSwitches
	{
		get => (ContextSwitches) GetValue( ContextSwitchesProperty );
		set => SetValue( ContextSwitchesProperty, value );
	}

	#endregion

	#region Dependency Property Changed Events

	private static void SelectedValueChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
	{
		var mairaComboBox = (MairaComboBox) d;

		mairaComboBox.UpdateSelectedValueVisuals();
	}

	private static void OffValueChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
	{
		var mairaComboBox = (MairaComboBox) d;

		mairaComboBox.UpdateSelectedValueVisuals();
	}

	#endregion

	#region Logic

	private void UpdateSelectedValueVisuals()
	{
		if ( SelectedValue?.ToString() == OffValue?.ToString() )
		{
			ComboBox.Foreground = _offSelectedValueBrush;
		}
		else
		{
			ComboBox.Foreground = _normalSelectedValueBrush;
		}
	}

	#endregion
}
