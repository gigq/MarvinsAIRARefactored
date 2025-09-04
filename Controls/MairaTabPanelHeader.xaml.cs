
using System.Windows;
using System.Windows.Media;

using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using UserControl = System.Windows.Controls.UserControl;

namespace MarvinsAIRARefactored.Controls;

public partial class MairaTabPanelHeader : UserControl
{
	public enum StatusStyleEnum
	{
		Normal,
		Warning,
		Error
	};

	private readonly SolidColorBrush _normalStatusStyleStrokeBrush = new( (Color) ColorConverter.ConvertFromString( "#666666" ) );
	private readonly SolidColorBrush _normalStatusStyleTextBrush = new( (Color) ColorConverter.ConvertFromString( "#eeeeee" ) );

	private readonly SolidColorBrush _warningStatusStyleStrokeBrush = new( (Color) ColorConverter.ConvertFromString( "#997330" ) );
	private readonly SolidColorBrush _warningStatusStyleTextBrush = new( (Color) ColorConverter.ConvertFromString( "#ffb22e" ) );

	private readonly SolidColorBrush _errorStatusStyleStrokeBrush = new( (Color) ColorConverter.ConvertFromString( "#993030" ) );
	private readonly SolidColorBrush _errorStatusStyleTextBrush = new( (Color) ColorConverter.ConvertFromString( "#ff2e2e" ) );

	public MairaTabPanelHeader()
	{
		InitializeComponent();

		_normalStatusStyleStrokeBrush.Freeze();
		_normalStatusStyleTextBrush.Freeze();

		_warningStatusStyleStrokeBrush.Freeze();
		_warningStatusStyleTextBrush.Freeze();

		_errorStatusStyleStrokeBrush.Freeze();
		_errorStatusStyleTextBrush.Freeze();

		UpdateStatusStyle();
	}

	#region Dependency Properties

	public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register( nameof( HeaderText ), typeof( string ), typeof( MairaTabPanelHeader ), new PropertyMetadata( string.Empty ) );

	public string HeaderText
	{
		get => (string) GetValue( HeaderTextProperty );
		set => SetValue( HeaderTextProperty, value );
	}

	public static readonly DependencyProperty StatusText1Property = DependencyProperty.Register( nameof( StatusText1 ), typeof( string ), typeof( MairaTabPanelHeader ), new PropertyMetadata( string.Empty ) );

	public string StatusText1
	{
		get => (string) GetValue( StatusText1Property );
		set => SetValue( StatusText1Property, value );
	}

	public static readonly DependencyProperty StatusText2Property = DependencyProperty.Register( nameof( StatusText2 ), typeof( string ), typeof( MairaTabPanelHeader ), new PropertyMetadata( string.Empty ) );

	public string StatusText2
	{
		get => (string) GetValue( StatusText2Property );
		set => SetValue( StatusText2Property, value );
	}

	public static readonly DependencyProperty StatusText3Property = DependencyProperty.Register( nameof( StatusText3 ), typeof( string ), typeof( MairaTabPanelHeader ), new PropertyMetadata( string.Empty ) );

	public string StatusText3
	{
		get => (string) GetValue( StatusText3Property );
		set => SetValue( StatusText3Property, value );
	}

	public static readonly DependencyProperty StatusText4Property = DependencyProperty.Register( nameof( StatusText4 ), typeof( string ), typeof( MairaTabPanelHeader ), new PropertyMetadata( string.Empty ) );

	public string StatusText4
	{
		get => (string) GetValue( StatusText4Property );
		set => SetValue( StatusText4Property, value );
	}

	public static readonly DependencyProperty StatusStyleProperty = DependencyProperty.Register( nameof( StatusStyle ), typeof( StatusStyleEnum ), typeof( MairaTabPanelHeader ), new PropertyMetadata( StatusStyleEnum.Normal, OnStatusStyleChanged ) );

	public StatusStyleEnum StatusStyle
	{
		get => (StatusStyleEnum) GetValue( StatusStyleProperty );
		set => SetValue( StatusStyleProperty, value );
	}

	private static void OnStatusStyleChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
	{
		var mairaTabPanelHeader = (MairaTabPanelHeader) d;

		mairaTabPanelHeader.UpdateStatusStyle();
	}

	#endregion

	#region Logic

	private void UpdateStatusStyle()
	{
		SolidColorBrush strokeBrush;
		SolidColorBrush textBrush;

		switch ( StatusStyle )
		{
			default:
				strokeBrush = _normalStatusStyleStrokeBrush;
				textBrush = _normalStatusStyleTextBrush;
				break;

			case StatusStyleEnum.Warning:
				strokeBrush = _warningStatusStyleStrokeBrush;
				textBrush = _warningStatusStyleTextBrush;
				break;

			case StatusStyleEnum.Error:
				strokeBrush = _errorStatusStyleStrokeBrush;
				textBrush = _errorStatusStyleTextBrush;
				break;
		}

		Status_Border.BorderBrush = strokeBrush;
		Status_TextBlock_1.Foreground = textBrush;
		Status_Divider_1.BorderBrush = strokeBrush;
		Status_TextBlock_2.Foreground = textBrush;
		Status_Divider_2.BorderBrush = strokeBrush;
		Status_TextBlock_3.Foreground = textBrush;
		Status_Divider_3.BorderBrush = strokeBrush;
		Status_TextBlock_4.Foreground = textBrush;
	}

	#endregion
}
