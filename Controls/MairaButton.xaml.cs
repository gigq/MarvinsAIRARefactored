
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

using UserControl = System.Windows.Controls.UserControl;

namespace MarvinsAIRARefactored.Controls;

public partial class MairaButton : UserControl
{
	private DispatcherTimer? _timer = null;
	private bool _blink = false;

	public MairaButton()
	{
		InitializeComponent();
	}

	public static readonly DependencyProperty TitleProperty = DependencyProperty.Register( nameof( Title ), typeof( string ), typeof( MairaButton ), new PropertyMetadata( "" ) );

	public string Title
	{
		get => (string) GetValue( TitleProperty );
		set => SetValue( TitleProperty, value );
	}

	public static readonly DependencyProperty TitleOnRightProperty = DependencyProperty.Register( nameof( TitleOnRight ), typeof( bool ), typeof( MairaButton ), new PropertyMetadata( false ) );

	public bool TitleOnRight
	{
		get => (bool) GetValue( TitleOnRightProperty );
		set => SetValue( TitleOnRightProperty, value );
	}

	public static readonly DependencyProperty BehindIconProperty = DependencyProperty.Register( nameof( BehindIcon ), typeof( ImageSource ), typeof( MairaButton ), new PropertyMetadata( null ) );

	public ImageSource BehindIcon
	{
		get => (ImageSource) GetValue( BehindIconProperty );
		set => SetValue( BehindIconProperty, value );
	}

	public static readonly DependencyProperty ButtonIconProperty = DependencyProperty.Register( nameof( ButtonIcon ), typeof( ImageSource ), typeof( MairaButton ), new PropertyMetadata( null ) );

	public ImageSource ButtonIcon
	{
		get => (ImageSource) GetValue( ButtonIconProperty );
		set => SetValue( ButtonIconProperty, value );
	}

	public static readonly DependencyProperty IsMappedProperty = DependencyProperty.Register( nameof( IsMapped ), typeof( bool ), typeof( MairaButton ), new PropertyMetadata( false, OnIsMappedChanged ) );

	public bool IsMapped
	{
		get => (bool) GetValue( IsMappedProperty );
		set => SetValue( IsMappedProperty, value );
	}

	private static void OnIsMappedChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
	{
		if ( d is MairaButton mairaButton )
		{
			if ( !mairaButton.Disabled )
			{
				if ( mairaButton.IsMapped )
				{
					mairaButton.ButtonBorder_Default_Image.Visibility = Visibility.Hidden;
					mairaButton.ButtonBorder_Mapped_Image.Visibility = Visibility.Visible;
				}
				else
				{
					mairaButton.ButtonBorder_Default_Image.Visibility = Visibility.Visible;
					mairaButton.ButtonBorder_Mapped_Image.Visibility = Visibility.Hidden;
				}
			}
		}
	}

	public static readonly DependencyProperty BlinkProperty = DependencyProperty.Register( nameof( Blink ), typeof( bool ), typeof( MairaButton ), new PropertyMetadata( false, OnBlinkChanged ) );

	public bool Blink
	{
		get => (bool) GetValue( BlinkProperty );
		set => SetValue( BlinkProperty, value );
	}

	private static void OnBlinkChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
	{
		if ( d is MairaButton mairaButton )
		{
			mairaButton.UpdateBlink();
		}
	}

	public static readonly DependencyProperty SmallProperty = DependencyProperty.Register( nameof( Small ), typeof( bool ), typeof( MairaButton ), new PropertyMetadata( false, OnSmallChanged ) );

	public bool Small
	{
		get => (bool) GetValue( SmallProperty );
		set => SetValue( SmallProperty, value );
	}

	private static void OnSmallChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
	{
		if ( d is MairaButton mairaButton )
		{
			if ( mairaButton.Small )
			{
				mairaButton.ButtonBorder_Default_Image.Width = 24;
				mairaButton.ButtonBorder_Default_Image.Height = 24;

				mairaButton.ButtonBorder_Mapped_Image.Width = 24;
				mairaButton.ButtonBorder_Mapped_Image.Height = 24;

				mairaButton.ButtonFace_Default_Image.Width = 24;
				mairaButton.ButtonFace_Default_Image.Height = 24;

				mairaButton.ButtonFace_Disabled_Image.Width = 24;
				mairaButton.ButtonFace_Disabled_Image.Height = 24;

				mairaButton.ButtonFace_Hover_Image.Width = 24;
				mairaButton.ButtonFace_Hover_Image.Height = 24;

				mairaButton.ButtonFace_Pressed_Image.Width = 24;
				mairaButton.ButtonFace_Pressed_Image.Height = 24;

				mairaButton.BehindIcon_Image.Width = 24;
				mairaButton.BehindIcon_Image.Height = 24;

				mairaButton.ButtonIcon_Image.Width = 24;
				mairaButton.ButtonIcon_Image.Height = 24;
			}
			else
			{
				mairaButton.ButtonBorder_Default_Image.Width = 48;
				mairaButton.ButtonBorder_Default_Image.Height = 48;

				mairaButton.ButtonBorder_Mapped_Image.Width = 48;
				mairaButton.ButtonBorder_Mapped_Image.Height = 48;

				mairaButton.ButtonFace_Default_Image.Width = 48;
				mairaButton.ButtonFace_Default_Image.Height = 48;

				mairaButton.ButtonFace_Disabled_Image.Width = 48;
				mairaButton.ButtonFace_Disabled_Image.Height = 48;

				mairaButton.ButtonFace_Hover_Image.Width = 48;
				mairaButton.ButtonFace_Hover_Image.Height = 48;

				mairaButton.ButtonFace_Pressed_Image.Width = 48;
				mairaButton.ButtonFace_Pressed_Image.Height = 48;

				mairaButton.BehindIcon_Image.Width = 48;
				mairaButton.BehindIcon_Image.Height = 48;

				mairaButton.ButtonIcon_Image.Width = 48;
				mairaButton.ButtonIcon_Image.Height = 48;
			}
		}
	}

	public static readonly DependencyProperty DisabledProperty = DependencyProperty.Register( nameof( Disabled ), typeof( bool ), typeof( MairaButton ), new PropertyMetadata( false, OnDisabledChanged ) );

	public bool Disabled
	{
		get => (bool) GetValue( DisabledProperty );
		set => SetValue( DisabledProperty, value );
	}

	private static void OnDisabledChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
	{
		if ( d is MairaButton mairaButton )
		{
			if ( mairaButton.Disabled )
			{
				mairaButton.ButtonBorder_Default_Image.Visibility = Visibility.Visible;
				mairaButton.ButtonBorder_Mapped_Image.Visibility = Visibility.Hidden;

				mairaButton.ButtonFace_Default_Image.Visibility = Visibility.Hidden;
				mairaButton.ButtonFace_Disabled_Image.Visibility = Visibility.Visible;
				mairaButton.ButtonFace_Hover_Image.Visibility = Visibility.Hidden;
				mairaButton.ButtonFace_Pressed_Image.Visibility = Visibility.Hidden;
			}
			else
			{
				if ( mairaButton.IsMapped )
				{
					mairaButton.ButtonBorder_Default_Image.Visibility = Visibility.Hidden;
					mairaButton.ButtonBorder_Mapped_Image.Visibility = Visibility.Visible;
				}
				else
				{
					mairaButton.ButtonBorder_Default_Image.Visibility = Visibility.Visible;
					mairaButton.ButtonBorder_Mapped_Image.Visibility = Visibility.Hidden;
				}

				mairaButton.ButtonFace_Default_Image.Visibility = Visibility.Visible;
				mairaButton.ButtonFace_Disabled_Image.Visibility = Visibility.Hidden;
				mairaButton.ButtonFace_Hover_Image.Visibility = Visibility.Hidden;
				mairaButton.ButtonFace_Pressed_Image.Visibility = Visibility.Hidden;
			}
		}
	}

	private void UpdateBlink()
	{
		if ( Blink )
		{
			if ( _timer == null )
			{
				_timer = new()
				{
					Interval = TimeSpan.FromSeconds( 1 )
				};

				_timer.Tick += OnTimer;

				_blink = true;

				ButtonIcon_Image.Visibility = Visibility.Visible;

				_timer.Start();
			}
		}
		else
		{
			if ( _timer != null )
			{
				_timer.Stop();

				_timer = null;

				ButtonIcon_Image.Visibility = Visibility.Visible;
			}
		}
	}

	private void OnTimer( object? sender, EventArgs e )
	{
		ButtonIcon_Image.Visibility = _blink ? Visibility.Hidden : Visibility.Visible;

		_blink = !_blink;
	}

	public event RoutedEventHandler? Click;

	private void Button_Click( object sender, RoutedEventArgs e )
	{
		if ( !Disabled )
		{
			Click?.Invoke( this, e );
		}
	}

	private void Button_MouseEnter( object sender, RoutedEventArgs e )
	{
		if ( !Disabled )
		{
			ButtonFace_Hover_Image.Visibility = Visibility.Visible;
		}
	}

	private void Button_MouseLeave( object sender, RoutedEventArgs e )
	{
		ButtonFace_Hover_Image.Visibility = Visibility.Hidden;
	}

	private void Button_PreviewMouseDown( object sender, RoutedEventArgs e )
	{
		if ( !Disabled )
		{
			ButtonFace_Pressed_Image.Visibility = Visibility.Visible;
		}
	}

	private void Button_PreviewMouseUp( object sender, RoutedEventArgs e )
	{
		ButtonFace_Pressed_Image.Visibility = Visibility.Hidden;
	}
}
