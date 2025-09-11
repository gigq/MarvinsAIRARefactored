
using System.Collections.ObjectModel;
using System.Windows;

using static MarvinsAIRARefactored.Windows.MainWindow;

using UserControl = System.Windows.Controls.UserControl;

namespace MarvinsAIRARefactored.Controls
{
	public partial class MairaAppMenuPopup : UserControl
	{
		public sealed class AppMenuItem
		{
			public AppPage AppPage { get; init; }
			public UserControl PageUserControl { get; init; } = new UserControl();
			public string DisplayName { get; set; } = string.Empty;
		}

		public ObservableCollection<AppMenuItem> AppMenuItems { get; } = [];

		public MairaAppMenuPopup()
		{
			InitializeComponent();

#if !ADMINBOXX

			AppMenuItems.Add( new AppMenuItem
			{
				AppPage = AppPage.RacingWheel,
				PageUserControl = _racingWheelPage
			} );

			AppMenuItems.Add( new AppMenuItem
			{
				AppPage = AppPage.SteeringEffects,
				PageUserControl = _steeringEffectsPage
			} );

			AppMenuItems.Add( new AppMenuItem
			{
				AppPage = AppPage.Pedals,
				PageUserControl = _pedalsPage
			} );

			AppMenuItems.Add( new AppMenuItem
			{
				AppPage = AppPage.Sounds,
				PageUserControl = _soundsPage
			} );

			AppMenuItems.Add( new AppMenuItem
			{
				AppPage = AppPage.SpeechToText,
				PageUserControl = _speechToTextPage
			} );

			AppMenuItems.Add( new AppMenuItem
			{
				AppPage = AppPage.Graph,
				PageUserControl = _graphPage
			} );

			AppMenuItems.Add( new AppMenuItem
			{
				AppPage = AppPage.Simulator,
				PageUserControl = _simulatorPage
			} );

#endif

#if ADMINBOXX

			AppMenuItems.Add( new AppMenuItem
			{
				AppPage = AppPage.AdminBoxx,
				PageUserControl = _adminBoxxPage
			} );

#endif

			AppMenuItems.Add( new AppMenuItem
			{
				AppPage = AppPage.Application,
				PageUserControl = _applicationPage
			} );

#if !ADMINBOXX

			AppMenuItems.Add( new AppMenuItem
			{
				AppPage = AppPage.Contribute,
				PageUserControl = _contributePage
			} );

			AppMenuItems.Add( new AppMenuItem
			{
				AppPage = AppPage.Donate,
				PageUserControl = _donatePage
			} );

#endif

#if DEBUG

			AppMenuItems.Add( new AppMenuItem
			{
				AppPage = AppPage.Debug,
				PageUserControl = _debugPage
			} );

#endif

			RelocalizeAppMenuItems();

			SelectedAppPage = AppPage.RacingWheel;
			SelectedAppPageUserControl = _racingWheelPage;
			SelectedAppMenuItem = AppMenuItems.FirstOrDefault( appMenuItem => appMenuItem.AppPage == SelectedAppPage );

			UpdateSelectedAppPageText();
		}

		#region User Control Events

		private void Scrim_MouseDown( object sender, System.Windows.Input.MouseButtonEventArgs e )
		{
			IsMenuOpen = false;
		}

		#endregion

		#region Dependency Properties

		public static readonly DependencyProperty SelectedAppPageProperty = DependencyProperty.Register( nameof( SelectedAppPage ), typeof( AppPage ), typeof( MairaAppMenuPopup ), new FrameworkPropertyMetadata( default( AppPage ), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedAppPageChanged ) );

		public AppPage SelectedAppPage
		{
			get => (AppPage) GetValue( SelectedAppPageProperty );
			set => SetValue( SelectedAppPageProperty, value );
		}

		public static readonly DependencyProperty SelectedAppPageTextProperty = DependencyProperty.Register( nameof( SelectedAppPageText ), typeof( string ), typeof( MairaAppMenuPopup ), new PropertyMetadata( string.Empty ) );

		public string SelectedAppPageText
		{
			get => (string) GetValue( SelectedAppPageTextProperty );
			set => SetValue( SelectedAppPageTextProperty, value );
		}

		public static readonly DependencyProperty SelectedAppPageUserControlProperty = DependencyProperty.Register( nameof( SelectedAppPageUserControl ), typeof( UserControl ), typeof( MairaAppMenuPopup ), new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault ) );

		public object SelectedAppPageUserControl
		{
			get => GetValue( SelectedAppPageUserControlProperty );
			set => SetValue( SelectedAppPageUserControlProperty, value );
		}

		public static readonly DependencyProperty SelectedAppMenuItemProperty = DependencyProperty.Register( nameof( SelectedAppMenuItem ), typeof( AppMenuItem ), typeof( MairaAppMenuPopup ), new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedMenuItemChanged ) );

		public AppMenuItem? SelectedAppMenuItem
		{
			get => (AppMenuItem?) GetValue( SelectedAppMenuItemProperty );
			set => SetValue( SelectedAppMenuItemProperty, value );
		}

		public static readonly DependencyProperty IsMenuOpenProperty = DependencyProperty.Register( nameof( IsMenuOpen ), typeof( bool ), typeof( MairaAppMenuPopup ), new FrameworkPropertyMetadata( false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault ) );

		public bool IsMenuOpen
		{
			get => (bool) GetValue( IsMenuOpenProperty );
			set => SetValue( IsMenuOpenProperty, value );
		}

		#endregion

		#region Dependency Property Changed Events

		private static void OnSelectedMenuItemChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
		{
			if ( d is MairaAppMenuPopup mairaAppMenuPopup )
			{
				if ( e.NewValue is AppMenuItem appMenuItem )
				{
					mairaAppMenuPopup.SelectedAppPage = appMenuItem.AppPage;
					mairaAppMenuPopup.SelectedAppPageUserControl = appMenuItem.PageUserControl;

					mairaAppMenuPopup.IsMenuOpen = false;

					mairaAppMenuPopup.UpdateSelectedAppPageText();
				}
			}
		}

		private static void OnSelectedAppPageChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
		{
			if ( d is MairaAppMenuPopup mairaAppMenuPopup )
			{
				var appPage = (AppPage) e.NewValue;

				var match = mairaAppMenuPopup.AppMenuItems.FirstOrDefault( appMenuItem => appMenuItem.AppPage == appPage );

				if ( ( match != null ) && !Equals( mairaAppMenuPopup.SelectedAppMenuItem, match ) )
				{
					mairaAppMenuPopup.SelectedAppMenuItem = match;
				}
			}
		}

		#endregion

		#region Logic

		public void RelocalizeAppMenuItems()
		{
			var localization = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization;

			foreach ( var menuItem in AppMenuItems )
			{
				switch ( menuItem.AppPage )
				{
					case AppPage.RacingWheel:
						menuItem.DisplayName = localization[ "RacingWheel" ];
						break;

					case AppPage.SteeringEffects:
						menuItem.DisplayName = localization[ "SteeringEffects" ];
						break;

					case AppPage.Pedals:
						menuItem.DisplayName = localization[ "Pedals" ];
						break;

					case AppPage.Sounds:
						menuItem.DisplayName = localization[ "Sounds" ];
						break;

					case AppPage.SpeechToText:
						menuItem.DisplayName = localization[ "SpeechToText" ];
						break;

					case AppPage.Graph:
						menuItem.DisplayName = localization[ "Graph" ];
						break;

					case AppPage.Simulator:
						menuItem.DisplayName = localization[ "Simulator" ];
						break;

					case AppPage.AdminBoxx:
						menuItem.DisplayName = localization[ "AdminBoxx" ];
						break;

					case AppPage.Application:
						menuItem.DisplayName = localization[ "App" ];
						break;

					case AppPage.Contribute:
						menuItem.DisplayName = localization[ "Contribute" ];
						break;

					case AppPage.Donate:
						menuItem.DisplayName = localization[ "Donate" ];
						break;

					case AppPage.Debug:
						menuItem.DisplayName = "Debug";
						break;
				}
			}

			UpdateSelectedAppPageText();
		}

		public void UpdateSelectedAppPageText()
		{
			var localization = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization;

			switch ( SelectedAppPage )
			{
				case AppPage.RacingWheel:
					SelectedAppPageText = localization[ "RacingWheel_UC" ];
					break;

				case AppPage.SteeringEffects:
					SelectedAppPageText = localization[ "SteeringEffects_UC" ];
					break;

				case AppPage.Pedals:
					SelectedAppPageText = localization[ "Pedals_UC" ];
					break;

				case AppPage.Sounds:
					SelectedAppPageText = localization[ "Sounds_UC" ];
					break;

				case AppPage.SpeechToText:
					SelectedAppPageText = localization[ "SpeechToText_UC" ];
					break;

				case AppPage.Graph:
					SelectedAppPageText = localization[ "Graph_UC" ];
					break;

				case AppPage.Simulator:
					SelectedAppPageText = localization[ "Simulator_UC" ];
					break;

				case AppPage.AdminBoxx:
					SelectedAppPageText = localization[ "AdminBoxx_UC" ];
					break;

				case AppPage.Application:
					SelectedAppPageText = localization[ "App_UC" ];
					break;

				case AppPage.Contribute:
					SelectedAppPageText = localization[ "Contribute_UC" ];
					break;

				case AppPage.Donate:
					SelectedAppPageText = localization[ "Donate_UC" ];
					break;

				case AppPage.Debug:
					SelectedAppPageText = "DEBUG";
					break;
			}
		}

		#endregion
	}
}
