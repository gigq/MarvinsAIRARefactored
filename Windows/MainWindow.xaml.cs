
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using ScrollEventArgs = System.Windows.Controls.Primitives.ScrollEventArgs;
using TabControl = System.Windows.Controls.TabControl;

using Simagic;

using MarvinsAIRARefactored.Classes;
using MarvinsAIRARefactored.Components;
using MarvinsAIRARefactored.Controls;
using MarvinsAIRARefactored.PInvoke;

namespace MarvinsAIRARefactored.Windows;

public partial class MainWindow : Window
{
	private const int UpdateInterval = 6;

	public nint WindowHandle { get; private set; } = 0;
	public bool SteeringEffectsTabItemIsVisible { get; private set; } = false;
	public bool GraphTabItemIsVisible { get; private set; } = false;
	public bool DebugTabItemIsVisible { get; private set; } = false;

	private string? _installerFilePath = null;

	private bool _initialized = false;

	private NotifyIcon? _notifyIcon = null;

	private int _updateCounter = UpdateInterval + 6;

	public MainWindow()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[MainWindow] Constructor >>>" );

		InitializeComponent();

		var version = Misc.GetVersion();

		app.Logger.WriteLine( $"[MainWindow] Version is {version}" );

		//FIX		Components.Localization.SetLanguageComboBoxItemsSource( App_Language_ComboBox );

		//FIX		Simulator_HeaderData_HeaderDataViewer.Initialize( Simulator_HeaderData_ScrollBar );
		//FIX		Simulator_SessionInfo_SessionInfoViewer.Initialize( Simulator_SessionInfo_ScrollBar );
		//FIX		Simulator_TelemetryData_TelemetryDataViewer.Initialize( Simulator_TelemetryData_ScrollBar );

#if ADMINBOXX

		var iconUri = new Uri( "pack://application:,,,/MarvinsAIRARefactored;component/Artwork/AppIcon/adminboxx.ico" );

		RacingWheel_TabItem.Visibility = Visibility.Collapsed;
		SteeringEffects_TabItem.Visibility = Visibility.Collapsed;
		Pedals_TabItem.Visibility = Visibility.Collapsed;
		Sounds_TabItem.Visibility = Visibility.Collapsed;
		SpeechToText_TabItem.Visibility = Visibility.Collapsed;
		Graph_TabItem.Visibility = Visibility.Collapsed;
		Simulator_TabItem.Visibility = Visibility.Collapsed;
		Contribute_TabItem.Visibility = Visibility.Collapsed;
		Donate_TabItem.Visibility = Visibility.Collapsed;
		Debug_TabItem.Visibility = Visibility.Collapsed;

		App_Language_GroupBox.Visibility = Visibility.Collapsed;
		App_CloudService_GroupBox.Visibility = Visibility.Collapsed;

		TabItemPositionHelper.SetIsFirst( AdminBoxx_TabItem, true );
		TabItemPositionHelper.SetIsLast( App_TabItem, true );

		TabControl.SelectedItem = AdminBoxx_TabItem;

#else

		var iconUri = new Uri("pack://application:,,,/MarvinsAIRARefactored;component/Artwork/AppIcon/maira-universal.ico");

#endif

		Icon = BitmapFrame.Create( iconUri );

#if !CODER

		//FIX Debug_TabItem.Visibility = Visibility.Collapsed;

		//FIX TabItemPositionHelper.SetIsLast( Donate_TabItem, true );

#endif

		app.Logger.WriteLine( "[MainWindow] <<< Constructor" );
	}

	public void Initialize()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[MainWindow] Initialize >>>" );

		var value = UXTheme.ShouldSystemUseDarkMode() ? 1 : 0;

		DWMAPI.DwmSetWindowAttribute( WindowHandle, (uint) DWMAPI.cbAttribute.DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, (uint) System.Runtime.InteropServices.Marshal.SizeOf( value ) );

		UpdateRacingWheelPowerButton();
		UpdateRacingWheelForceFeedbackButtons();

		RefreshWindow();

		Misc.ForcePropertySetters( MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings );

		var settings = MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings;

		if ( settings.AppRememberWindowPositionAndSize )
		{
			var rectangle = settings.AppWindowPositionAndSize;

			if ( Misc.IsWindowBoundsVisible( rectangle ) )
			{
				Left = rectangle.Location.X;
				Top = rectangle.Location.Y;
				Width = rectangle.Size.Width;
				Height = rectangle.Size.Height;

				WindowStartupLocation = WindowStartupLocation.Manual;
			}
		}

		_initialized = true;

		app.Logger.WriteLine( "[MainWindow] <<< Initialize" );
	}

	public void RefreshWindow()
	{
		Dispatcher.Invoke( () =>
		{
			var app = App.Instance!;

#if ADMINBOXX

			Title = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization[ "AdminBoxx" ] + " " + Misc.GetVersion();

#else

			Title = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization[ "AppTitle" ] + " " + Misc.GetVersion();

			app.DirectInput.SetMairaComboBoxItemsSource( RacingWheel_SteeringDevice_ComboBox );

			//FIX app.LFE.SetMairaComboBoxItemsSource( RacingWheel_LFERecordingDevice_ComboBox );

			//FIX app.RecordingManager.SetMairaComboBoxItemsSource( RacingWheel_PreviewRecordings_ComboBox );

			//FIX Graph.SetMairaComboBoxItemsSource( Graph_Statistics_ComboBox );

			//FIX RacingWheel.SetMairaComboBoxItemsSource( RacingWheel_Algorithm_ComboBox );

			//FIX Pedals.SetMairaComboBoxItemsSource( Pedals_ClutchEffect1_ComboBox );
			//FIX Pedals.SetMairaComboBoxItemsSource( Pedals_ClutchEffect2_ComboBox );
			//FIX Pedals.SetMairaComboBoxItemsSource( Pedals_ClutchEffect3_ComboBox );
			//FIX Pedals.SetMairaComboBoxItemsSource( Pedals_BrakeEffect1_ComboBox );
			//FIX Pedals.SetMairaComboBoxItemsSource( Pedals_BrakeEffect2_ComboBox );
			//FIX Pedals.SetMairaComboBoxItemsSource( Pedals_BrakeEffect3_ComboBox );
			//FIX Pedals.SetMairaComboBoxItemsSource( Pedals_ThrottleEffect1_ComboBox );
			//FIX Pedals.SetMairaComboBoxItemsSource( Pedals_ThrottleEffect2_ComboBox );
			//FIX Pedals.SetMairaComboBoxItemsSource( Pedals_ThrottleEffect3_ComboBox );

			SteeringEffects.SetCalibrationFileNameMairaComboBoxItemsSource();

			//FIX SteeringEffects.SetVibrationPatternMairaComboBoxItemsSource( SteeringEffects_UndersteerWheelVibrationPattern_ComboBox );
			//FIX SteeringEffects.SetVibrationPatternMairaComboBoxItemsSource( SteeringEffects_OversteerWheelVibrationPattern_ComboBox );

			//FIX SteeringEffects.SetConstantForceDirectionMairaComboBoxItemsSource( SteeringEffects_UndersteerWheelConstantForceDirection_ComboBox );
			//FIX SteeringEffects.SetConstantForceDirectionMairaComboBoxItemsSource( SteeringEffects_OversteerWheelConstantForceDirection_ComboBox );

			//FIX SpeechToText.SetMairaComboBoxItemsSource( SpeechToText_Language_ComboBox );

			app.SpeechToText.UpdateStrings();

#endif

			UpdateStatus();
			UpdatePedalsDevice();
			UpdateNotifyIcon();
		} );
	}

	public void UpdateStatus()
	{
		Dispatcher.Invoke( () =>
		{
			var app = App.Instance!;

			var localization = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization;

			var backgroundColor = Brushes.Black;

			var panel1Message = string.Empty;
			var panel2Message = string.Empty;
			var panel3Message = string.Empty;
			var panel4Message = string.Empty;

			if ( app.CloudService.CheckingForUpdate )
			{
				backgroundColor = Brushes.DarkOrange;

				panel1Message = localization[ "CheckingForUpdate" ];
			}
			else if ( app.CloudService.DownloadingUpdate )
			{
				backgroundColor = Brushes.DarkOrange;

				panel1Message = localization[ "DownloadingUpdate" ];
			}
			else if ( app.AdminBoxx.IsUpdating )
			{
				backgroundColor = Brushes.DarkOrange;

				panel1Message = localization[ "AdminBoxxIsUpdating" ];
			}
			else if ( app.Simulator.IsConnected )
			{
				backgroundColor = new SolidColorBrush( System.Windows.Media.Color.FromScRgb( 1f, 0.1f, 0.1f, 0.1f ) );

				panel1Message = app.Simulator.CarScreenName == string.Empty ? localization[ "Default" ] : app.Simulator.CarScreenName;
				panel2Message = app.Simulator.TrackDisplayName == string.Empty ? localization[ "Default" ] : app.Simulator.TrackDisplayName;
				panel3Message = app.Simulator.TrackConfigName == string.Empty ? localization[ "Default" ] : app.Simulator.TrackConfigName;
				panel4Message = localization[ app.Simulator.WeatherDeclaredWet ? "Wet" : "Dry" ];
			}
			else
			{
				backgroundColor = new SolidColorBrush( System.Windows.Media.Color.FromScRgb( 1f, 0.3f, 0f, 0f ) );

				panel1Message = localization[ "SimulatorNotRunning" ];
			}

			//FIX Status_Border.Background = backgroundColor;

			if ( panel1Message == string.Empty )
			{
				//FIX Status_Panel1_Label.Visibility = Visibility.Collapsed;
			}
			else
			{
				//FIX Status_Panel1_Label.Content = panel1Message;
				//FIX Status_Panel1_Label.Visibility = Visibility.Visible;
			}

			if ( panel2Message == string.Empty )
			{
				//FIX Status_Panel2_Label.Visibility = Visibility.Collapsed;
			}
			else
			{
				//FIX Status_Panel2_Label.Content = panel2Message;
				//FIX Status_Panel2_Label.Visibility = Visibility.Visible;
			}

			if ( panel3Message == string.Empty )
			{
				//FIX Status_Panel3_Label.Visibility = Visibility.Collapsed;
			}
			else
			{
				//FIX Status_Panel3_Label.Content = panel3Message;
				//FIX Status_Panel3_Label.Visibility = Visibility.Visible;
			}

			if ( panel4Message == string.Empty )
			{
				//FIX Status_Panel4_Label.Visibility = Visibility.Collapsed;
			}
			else
			{
				//FIX Status_Panel4_Label.Content = panel4Message;
				//FIX Status_Panel4_Label.Visibility = Visibility.Visible;
			}
		} );
	}

	public void UpdateRacingWheelPowerButton()
	{
		var app = App.Instance!;

		Dispatcher.Invoke( () =>
		{
			ImageSource? imageSource;

			if ( !MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings.RacingWheelEnableForceFeedback )
			{
				imageSource = new ImageSourceConverter().ConvertFromString( "pack://application:,,,/MarvinsAIRARefactored;component/Artwork/Buttons/power-large-red.png" ) as ImageSource;
			}
			else if ( app.RacingWheel.SuspendForceFeedback || !app.DirectInput.ForceFeedbackInitialized )
			{
				if ( app.Simulator.IsConnected )
				{
					imageSource = new ImageSourceConverter().ConvertFromString( "pack://application:,,,/MarvinsAIRARefactored;component/Artwork/Buttons/power-large-yellow.png" ) as ImageSource;
				}
				else
				{
					imageSource = new ImageSourceConverter().ConvertFromString( "pack://application:,,,/MarvinsAIRARefactored;component/Artwork/Buttons/power-large-orange.png" ) as ImageSource;
				}
			}
			else
			{
				imageSource = new ImageSourceConverter().ConvertFromString( "pack://application:,,,/MarvinsAIRARefactored;component/Artwork/Buttons/power-large-green.png" ) as ImageSource;
			}

			if ( imageSource != null )
			{
				RacingWheel_Power_MairaMappableButton.Icon = imageSource;
			}
		} );
	}

	public void UpdateRacingWheelForceFeedbackButtons()
	{
		var app = App.Instance!;

		Dispatcher.Invoke( () =>
		{
			var isEnabled = app.DirectInput.ForceFeedbackInitialized;

			RacingWheel_Test_MairaMappableButton.IsEnabled = isEnabled;
			RacingWheel_Reset_MairaMappableButton.IsEnabled = isEnabled;
			//FIX RacingWheel_Set_MairaMappableButton.IsEnabled = isEnabled;
			//FIX RacingWheel_Clear_MairaMappableButton.IsEnabled = isEnabled;
		} );
	}

	public void UpdateRacingWheelAlgorithmControls()
	{
		Dispatcher.Invoke( () =>
		{
			var racingWheelDetailBoostKnobControlVisibility = Visibility.Hidden;
			var racingWheelDeltaLimitKnobControlVisibility = Visibility.Hidden;
			var racingWheelDetailBoostBiasKnobControlVisibility = Visibility.Hidden;
			var racingWheelDeltaLimiterBiasKnobControlVisibility = Visibility.Hidden;
			var racingWheelSlewCompressionThresholdVisibility = Visibility.Hidden;
			var racingWheelSlewCompressionRateVisibility = Visibility.Hidden;
			var racingWheelTotalCompressionThresholdVisibility = Visibility.Hidden;
			var racingWheelTotalCompressionRateVisibility = Visibility.Hidden;

			var racingWheelAlgorithmRowTwoGridVisibility = Visibility.Collapsed;
			var racingWheelCurbProtectionGroupBoxVisibility = Visibility.Collapsed;

			switch ( MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings.RacingWheelAlgorithm )
			{
				case RacingWheel.Algorithm.DetailBooster:
				case RacingWheel.Algorithm.DetailBoosterOn60Hz:
					racingWheelDetailBoostKnobControlVisibility = Visibility.Visible;
					racingWheelDetailBoostBiasKnobControlVisibility = Visibility.Visible;
					racingWheelCurbProtectionGroupBoxVisibility = Visibility.Visible;
					break;

				case RacingWheel.Algorithm.DeltaLimiter:
				case RacingWheel.Algorithm.DeltaLimiterOn60Hz:
					racingWheelDeltaLimitKnobControlVisibility = Visibility.Visible;
					racingWheelDeltaLimiterBiasKnobControlVisibility = Visibility.Visible;
					racingWheelCurbProtectionGroupBoxVisibility = Visibility.Visible;
					break;

				case RacingWheel.Algorithm.ZeAlanLeTwist:
					racingWheelSlewCompressionThresholdVisibility = Visibility.Visible;
					racingWheelSlewCompressionRateVisibility = Visibility.Visible;
					racingWheelTotalCompressionThresholdVisibility = Visibility.Visible;
					racingWheelTotalCompressionRateVisibility = Visibility.Visible;

					racingWheelAlgorithmRowTwoGridVisibility = Visibility.Visible;
					racingWheelCurbProtectionGroupBoxVisibility = Visibility.Visible;
					break;
			}

			//FIX RacingWheel_DetailBoost_KnobControl.Visibility = racingWheelDetailBoostKnobControlVisibility;
			//FIX RacingWheel_DeltaLimit_KnobControl.Visibility = racingWheelDeltaLimitKnobControlVisibility;
			//FIX RacingWheel_DetailBoostBias_KnobControl.Visibility = racingWheelDetailBoostBiasKnobControlVisibility;
			//FIX RacingWheel_DeltaLimiterBias_KnobControl.Visibility = racingWheelDeltaLimiterBiasKnobControlVisibility;
			//FIX RacingWheel_SlewCompressionThreshold.Visibility = racingWheelSlewCompressionThresholdVisibility;
			//FIX RacingWheel_SlewCompressionRate.Visibility = racingWheelSlewCompressionRateVisibility;
			//FIX RacingWheel_TotalCompressionThreshold.Visibility = racingWheelTotalCompressionThresholdVisibility;
			//FIX RacingWheel_TotalCompressionRate.Visibility = racingWheelTotalCompressionRateVisibility;

			//FIX RacingWheel_AlgorithmRowTwo_Grid.Visibility = racingWheelAlgorithmRowTwoGridVisibility;
			//FIX RacingWheel_CurbProtection_GroupBox.Visibility = racingWheelCurbProtectionGroupBoxVisibility;
		} );
	}

	public void UpdateRacingWheelSimpleMode()
	{
		Dispatcher.Invoke( () =>
		{
			var settings = MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings;

			Misc.ApplyToTaggedElements( MainGrid, "Complex", element => element.Visibility = settings.RacingWheelSimpleModeEnabled ? Visibility.Collapsed : Visibility.Visible );
		} );
	}

	public void UpdatePedalsDevice()
	{
		var app = App.Instance!;

		Dispatcher.Invoke( () =>
		{
			var localization = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization;

			switch ( app.Pedals.PedalsDevice )
			{
				case HPR.PedalsDevice.None:
					//FIX app.MainWindow.Pedals_Device_Label.Content = localization[ "PedalsNone" ];
					break;

				case HPR.PedalsDevice.P1000:
					//FIX app.MainWindow.Pedals_Device_Label.Content = localization[ "PedalsP1000" ];
					break;

				case HPR.PedalsDevice.P2000:
					//FIX app.MainWindow.Pedals_Device_Label.Content = localization[ "PedalsP2000" ];
					break;
			}
		} );
	}

	public void UpdateNotifyIcon()
	{
		var app = App.Instance!;

		Dispatcher.Invoke( () =>
		{
			var localization = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization;

			if ( _notifyIcon != null )
			{
				_notifyIcon.Visible = false;

				_notifyIcon.Dispose();
			}

			if ( MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings.AppMinimizeToSystemTray )
			{
				var resourceStream = Application.GetResourceStream( new Uri( "pack://application:,,,/MarvinsAIRARefactored;component/Artwork/AppIcon/maira-universal.ico" ) ).Stream;

				_notifyIcon = new()
				{
					Icon = new Icon( resourceStream ),
					Visible = true,
					Text = localization[ "AppTitle" ],
					ContextMenuStrip = new ContextMenuStrip()
				};

				_notifyIcon.ContextMenuStrip.Items.Add( localization[ "ShowWindow" ], null, ( s, e ) => MakeWindowVisible() );
				_notifyIcon.ContextMenuStrip.Items.Add( localization[ "ExitApp" ], null, ( s, e ) => ExitApp() );

				_notifyIcon.MouseClick += ( s, e ) =>
				{
					if ( e.Button == MouseButtons.Left )
					{
						MakeWindowVisible();
					}
					else if ( e.Button == MouseButtons.Right )
					{
						_notifyIcon.ContextMenuStrip?.Show( System.Windows.Forms.Cursor.Position );
					}
				};
			}
		} );
	}

	public void MakeWindowVisible()
	{
		Show();

		WindowState = WindowState.Normal;

		Activate();

		if ( !MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings.AppTopmostWindowEnabled )
		{
			Topmost = true;
			Topmost = false;
		}

		Focus();
	}

	private void ExitApp()
	{
		if ( _notifyIcon != null )
		{
			_notifyIcon.Visible = false;

			_notifyIcon.Dispose();

			Close();
		}
	}

	private void UpdateTabItemIsVisible()
	{
		if ( WindowState == WindowState.Minimized )
		{
			SteeringEffectsTabItemIsVisible = false;
			GraphTabItemIsVisible = false;
			DebugTabItemIsVisible = false;
		}
		else if ( TabControl.SelectedItem is TabItem selectedTab )
		{
			//FIX SteeringEffectsTabItemIsVisible = ( selectedTab == SteeringEffects_TabItem );
			//FIX GraphTabItemIsVisible = ( selectedTab == Graph_TabItem );
			//FIX DebugTabItemIsVisible = ( selectedTab == Debug_TabItem );
		}
	}

	public void CloseAndLaunchInstaller( string installerFilePath )
	{
		_installerFilePath = installerFilePath;

		Close();
	}

	private void Window_ContentRendered( object sender, EventArgs e )
	{
		if ( WindowHandle == 0 )
		{
			WindowHandle = new WindowInteropHelper( this ).Handle;

			App.Instance!.GripOMeterWindow.Owner = this;
		}
	}

	private void Window_LocationChanged( object sender, EventArgs e )
	{
		if ( _initialized )
		{
			if ( IsVisible && ( WindowState == WindowState.Normal ) )
			{
				var settings = MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings;

				var rectangle = settings.AppWindowPositionAndSize;

				rectangle.Location = new System.Drawing.Point( (int) RestoreBounds.Left, (int) RestoreBounds.Top );

				settings.AppWindowPositionAndSize = rectangle;
			}
		}
	}

	private void Window_SizeChanged( object sender, SizeChangedEventArgs e )
	{
		if ( _initialized )
		{
			if ( IsVisible && ( WindowState == WindowState.Normal ) )
			{
				var settings = MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings;

				var rectangle = settings.AppWindowPositionAndSize;

				rectangle.Size = new System.Drawing.Size( (int) RestoreBounds.Width, (int) RestoreBounds.Height );

				settings.AppWindowPositionAndSize = rectangle;
			}
		}
	}

	private void Window_StateChanged( object sender, EventArgs e )
	{
		if ( MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings.AppMinimizeToSystemTray )
		{
			if ( WindowState == WindowState.Minimized )
			{
				Hide();
			}
		}

		UpdateTabItemIsVisible();
	}

	private void Window_Closing( object sender, CancelEventArgs e )
	{
		if ( _notifyIcon != null )
		{
			_notifyIcon.Visible = false;

			_notifyIcon.Dispose();
		}
	}

	private void Window_Closed( object sender, EventArgs e )
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[MainWindow] Window closed" );

		if ( _installerFilePath != null )
		{
			var processStartInfo = new ProcessStartInfo( _installerFilePath )
			{
				UseShellExecute = true
			};

			Process.Start( processStartInfo );
		}
	}

	private void Window_Loaded( object sender, RoutedEventArgs e )
	{
#if !ADMINBOXX

		var tabPanel = Misc.FindTabPanel( TabControl );

		if ( tabPanel != null )
		{
			//FIX Logo_Image.Width = tabPanel.ActualWidth - 10;
			//FIX Logo_Image.Visibility = Visibility.Visible;

			tabPanel.SizeChanged += ( s, args ) =>
			{
				//FIX Logo_Image.Width = tabPanel.ActualWidth - 10;
			};
		}

#endif
	}

	private void TabControl_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		if ( e.Source is TabControl )
		{
			UpdateTabItemIsVisible();
		}
	}

	private void RacingWheel_Power_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings.RacingWheelEnableForceFeedback = !MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings.RacingWheelEnableForceFeedback;
	}

	private void RacingWheel_Test_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.RacingWheel.PlayTestSignal = true;
	}

	private void RacingWheel_Reset_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.RacingWheel.ResetForceFeedback = true;
	}

	private void RacingWheel_Set_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.RacingWheel.AutoSetMaxForce = true;
	}

	private void RacingWheel_Clear_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.RacingWheel.ClearPeakTorque = true;
	}

	private void RacingWheel_Preview_ScrollViewer_PreviewMouseWheel( object sender, MouseWheelEventArgs e )
	{
		e.Handled = true;

		var eventArg = new MouseWheelEventArgs( e.MouseDevice, e.Timestamp, e.Delta )
		{
			RoutedEvent = MouseWheelEvent,
			Source = sender
		};

		var parent = ( (ScrollViewer) sender ).Parent as UIElement;

		parent?.RaiseEvent( eventArg );
	}

	private void SteeringEffects_ResetGripOMeter_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.GripOMeterWindow.ResetWindow();
	}

	private void SteeringEffects_RunCalibration_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.SteeringEffects.RunCalibration();
	}

	private void SteeringEffects_StopCalibration_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.SteeringEffects.StopCalibration( false );
	}

	private void SteeringEffects_SteeringWheelLeft_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.VirtualJoystick.Steering = -1f;
	}

	private void SteeringEffects_SteeringWheelCenter_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.VirtualJoystick.Steering = 0f;
	}

	private void SteeringEffects_SteeringWheelRight_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.VirtualJoystick.Steering = 1f;
	}

	private void SteeringEffects_SteeringWheel90Left_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.VirtualJoystick.Steering = -( 90f / 450f );
	}

	private void SteeringEffects_MinThrottle_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.VirtualJoystick.Throttle = 0f;
	}

	private void SteeringEffects_MaxThrottle_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.VirtualJoystick.Throttle = 1f;
	}

	private void SteeringEffects_ShiftUp_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.VirtualJoystick.ShiftUp = true;
	}

	private void SteeringEffects_ShiftDown_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.VirtualJoystick.ShiftDown = true;
	}

	private void SteeringEffects_MinBrake_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.VirtualJoystick.Brake = 0f;
	}

	private void SteeringEffects_MaxBrake_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.VirtualJoystick.Brake = 1f;
	}

	private void Pedals_ClutchTest1_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 0, 0 );
	}

	private void Pedals_ClutchTest2_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 0, 1 );
	}

	private void Pedals_ClutchTest3_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 0, 2 );
	}

	private void Pedals_BrakeTest1_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 1, 0 );
	}

	private void Pedals_BrakeTest2_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 1, 1 );
	}

	private void Pedals_BrakeTest3_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 1, 2 );
	}

	private void Pedals_ThrottleTest1_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 2, 0 );
	}

	private void Pedals_ThrottleTest2_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 2, 1 );
	}

	private void Pedals_ThrottleTest3_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Pedals.StartTest( 2, 2 );
	}

	private void Sounds_ABSEngaged_Test_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Sounds.Test( Sounds.SoundEffectType.ABSEngaged );
	}

	private void Sounds_WheelLock_Test_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Sounds.Test( Sounds.SoundEffectType.WheelLock );
	}

	private void Sounds_WheelSpin_Test_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Sounds.Test( Sounds.SoundEffectType.WheelSpin );
	}

	private void Sounds_Understeer_Test_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Sounds.Test( Sounds.SoundEffectType.Understeer );
	}

	private void Sounds_Oversteer_Test_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.Sounds.Test( Sounds.SoundEffectType.Oversteer );
	}

	private void SpeechToText_ResetOverlayWindow_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.SpeechToTextWindow.ResetWindow();
	}

	private void AdminBoxx_ConnectToAdminBoxx_MairaSwitch_Toggled( object sender, EventArgs e )
	{
		var app = App.Instance!;

		//FIX if ( AdminBoxx_ConnectToAdminBoxx_MairaSwitch.IsOn )
		{
			if ( !app.AdminBoxx.IsConnected )
			{
				app.AdminBoxx.Connect();
			}
		}
		//FIX else
		{
			app.AdminBoxx.Disconnect();
		}
	}

	private void AdminBoxx_Brightness_ValueChanged( float newValue )
	{
		var app = App.Instance!;

		app.AdminBoxx.ResendAllLEDs();
	}

	private void AdminBoxx_Volume_ValueChanged( float newValue )
	{
		var app = App.Instance!;

		app.AudioManager.Play( "beep", newValue );
	}

	private void AdminBoxx_Test_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.AdminBoxx.StartTestCycle();
	}

	private void Graph_Target_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		//FIX if ( Graph_BottomPanel_StackPanel.Visibility == Visibility.Visible )
		{
			Misc.ApplyToTaggedElements( MainGrid, "HideWhenGraphIsSoloed", element => element.Visibility = Visibility.Collapsed );

			//FIX Graph_Main_StackPanel.Margin = new Thickness( 0 );
			//FIX Graph_Border.Margin = new Thickness( 0 );

			WindowStyle = WindowStyle.None;
			ResizeMode = ResizeMode.NoResize;
			SizeToContent = SizeToContent.Height;
		}
		//FIX else
		{
			Misc.ApplyToTaggedElements( MainGrid, "HideWhenGraphIsSoloed", element => element.Visibility = Visibility.Visible );

			//FIX Graph_Main_StackPanel.Margin = new Thickness( 10, 10, 10, 20 );
			//FIX Graph_Border.Margin = new Thickness( 0, 10, 0, 0 );

			WindowStyle = WindowStyle.SingleBorderWindow;
			ResizeMode = ResizeMode.CanResizeWithGrip;
			SizeToContent = SizeToContent.Manual;
		}
	}

	private void Simulator_HeaderData_HeaderDataViewer_MouseWheel( object sender, MouseWheelEventArgs e )
	{
		var delta = e.Delta / 30.0f;

		if ( delta > 0 )
		{
			delta = MathF.Max( 1, delta );
		}
		else
		{
			delta = MathF.Min( -1, delta );
		}

		//FIX Simulator_HeaderData_ScrollBar.Value -= delta;

		//FIX Simulator_HeaderData_HeaderDataViewer.ScrollIndex = (int) Simulator_HeaderData_ScrollBar.Value;
	}

	private void Simulator_HeaderData_ScrollBar_Scroll( object sender, ScrollEventArgs e )
	{
		//FIX Simulator_HeaderData_HeaderDataViewer.ScrollIndex = (int) e.NewValue;
	}

	private void Simulator_SessionInfo_SessionInfoViewer_MouseWheel( object sender, MouseWheelEventArgs e )
	{
		var delta = e.Delta / 30.0f;

		if ( delta > 0 )
		{
			delta = MathF.Max( 1, delta );
		}
		else
		{
			delta = MathF.Min( -1, delta );
		}

		//FIX Simulator_SessionInfo_ScrollBar.Value -= delta;

		//FIX Simulator_SessionInfo_SessionInfoViewer.ScrollIndex = (int) Simulator_SessionInfo_ScrollBar.Value;
	}

	private void Simulator_SessionInfo_ScrollBar_Scroll( object sender, ScrollEventArgs e )
	{
		//FIX Simulator_SessionInfo_SessionInfoViewer.ScrollIndex = (int) e.NewValue;
	}

	private void Simulator_TelemetryData_TelemetryDataViewer_MouseWheel( object sender, MouseWheelEventArgs e )
	{
		var delta = e.Delta / 30.0f;

		if ( delta > 0 )
		{
			delta = MathF.Max( 1, delta );
		}
		else
		{
			delta = MathF.Min( -1, delta );
		}

		//FIX Simulator_TelemetryData_ScrollBar.Value -= delta;

		//FIX Simulator_TelemetryData_TelemetryDataViewer.ScrollIndex = (int) Simulator_TelemetryData_ScrollBar.Value;
	}

	private void Simulator_TelemetryData_ScrollBar_Scroll( object sender, ScrollEventArgs e )
	{
		//FIX Simulator_TelemetryData_TelemetryDataViewer.ScrollIndex = (int) e.NewValue;
	}

	private async void App_CheckNow_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		await app.CloudService.CheckForUpdates( true );
	}

	private void Hyperlink_RequestNavigate( object sender, System.Windows.Navigation.RequestNavigateEventArgs e )
	{
		Process.Start( new ProcessStartInfo( e.Uri.AbsoluteUri ) { UseShellExecute = true } );

		e.Handled = true;
	}

	private void Debug_ResetRecording_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.RecordingManager.ResetRecording();
	}

	private void Debug_SaveRecording_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.RecordingManager.SaveRecording();
	}

	public void Tick( App app )
	{
		_updateCounter--;

		if ( _updateCounter == 0 )
		{
			_updateCounter = UpdateInterval;

			//FIX Simulator_HeaderData_HeaderDataViewer.InvalidateVisual();
			//FIX Simulator_SessionInfo_SessionInfoViewer.InvalidateVisual();
			//FIX Simulator_TelemetryData_TelemetryDataViewer.InvalidateVisual();
		}
	}
}
