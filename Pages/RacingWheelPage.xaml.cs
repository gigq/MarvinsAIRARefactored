
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using UserControl = System.Windows.Controls.UserControl;

using MarvinsAIRARefactored.Components;
using MarvinsAIRARefactored.SimSupport;

namespace MarvinsAIRARefactored.Pages;

public partial class RacingWheelPage : UserControl
{
	private const double PreviewZoomSize = 256.0;
	private const double PreviewZoomFactor = 6.0;
	private const double PreviewZoomPopupOffset = 32.0;

	public RacingWheelPage()
	{
		InitializeComponent();
	}

	#region User Control Events

	private void Power_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings.RacingWheelEnableForceFeedback = !MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings.RacingWheelEnableForceFeedback;
	}

	private void Test_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.RacingWheel.PlayTestSignal = true;
	}

	private void Reset_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.RacingWheel.ResetForceFeedback = true;
	}

	private void Set_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.RacingWheel.AutoSetMaxForce = true;
	}

	private void Clear_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.RacingWheel.ClearPeakTorque = true;
	}

	private void Preview_ScrollViewer_PreviewMouseWheel( object sender, MouseWheelEventArgs e )
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

	private void Preview_ScrollViewer_Loaded( object sender, RoutedEventArgs e )
	{
		var scrollViewer = (ScrollViewer) sender;
		var half = scrollViewer.ScrollableWidth / 2;

		scrollViewer.ScrollToHorizontalOffset( half );
	}

	private void AlgorithmPreview_Image_MouseEnter( object sender, MouseEventArgs e )
	{
		if ( AlgorithmPreview_Image.Source == null )
		{
			return;
		}

		var cursorPosition = e.GetPosition( AlgorithmPreview_Image );

		UpdatePreviewZoom( cursorPosition );
		UpdatePreviewPopupPosition( cursorPosition );

		PreviewZoom_Popup.IsOpen = true;
	}

	private void AlgorithmPreview_Image_MouseLeave( object sender, MouseEventArgs e )
	{
		PreviewZoom_Popup.IsOpen = false;
	}

	private void AlgorithmPreview_Image_MouseMove( object sender, MouseEventArgs e )
	{
		if ( !PreviewZoom_Popup.IsOpen )
		{
			return;
		}

		var cursorPosition = e.GetPosition( AlgorithmPreview_Image );

		UpdatePreviewZoom( cursorPosition );
		UpdatePreviewPopupPosition( cursorPosition );
	}

	private void UpdatePreviewZoom( Point position )
	{
		var imageWidth = AlgorithmPreview_Image.ActualWidth;
		var imageHeight = AlgorithmPreview_Image.ActualHeight;

		if ( imageWidth <= 0d || imageHeight <= 0d )
		{
			return;
		}

		var regionWidth = PreviewZoomSize / PreviewZoomFactor;
		var regionHeight = PreviewZoomSize / PreviewZoomFactor;

		var halfRegionWidth = regionWidth / 2d;
		var halfRegionHeight = regionHeight / 2d;

		var left = position.X - halfRegionWidth;
		var top = position.Y - halfRegionHeight;

		if ( left < 0d )
		{
			left = 0d;
		}

		if ( top < 0d )
		{
			top = 0d;
		}

		if ( left + regionWidth > imageWidth )
		{
			left = imageWidth - regionWidth;
		}

		if ( top + regionHeight > imageHeight )
		{
			top = imageHeight - regionHeight;
		}

		var xNorm = left / imageWidth;
		var yNorm = top / imageHeight;
		var wNorm = regionWidth / imageWidth;
		var hNorm = regionHeight / imageHeight;

		PreviewZoom_Brush.Viewbox = new Rect( xNorm, yNorm, wNorm, hNorm );
	}

	private void UpdatePreviewPopupPosition( Point cursorPosition )
	{
		PreviewZoom_Popup.HorizontalOffset = cursorPosition.X + PreviewZoomPopupOffset;
		PreviewZoom_Popup.VerticalOffset = cursorPosition.Y + PreviewZoomPopupOffset;
	}

	private void StartRecording_MairaMappableButton_Click( object sender, RoutedEventArgs e )
	{
		var app = App.Instance!;

		app.RecordingManager.StartRecording();
	}

	#endregion

	#region Logic

	private static string GetSimulatorAwareAlgorithmLabel( SimId selectedSimId, RacingWheel.Algorithm algorithm, Components.Localization localization )
	{
		if ( selectedSimId != SimId.LeMansUltimate )
		{
			return algorithm switch
			{
				RacingWheel.Algorithm.Native60Hz => localization[ "Native60Hz" ],
				RacingWheel.Algorithm.Native360Hz => localization[ "Native360Hz" ],
				RacingWheel.Algorithm.DetailBooster => localization[ "DetailBooster" ],
				RacingWheel.Algorithm.DeltaLimiter => localization[ "DeltaLimiter" ],
				RacingWheel.Algorithm.DetailBoosterOn60Hz => localization[ "DetailBoosterOn60Hz" ],
				RacingWheel.Algorithm.DeltaLimiterOn60Hz => localization[ "DeltaLimiterOn60Hz" ],
				RacingWheel.Algorithm.SlewAndTotalCompression => localization[ "SlewAndTotalCompression" ],
				RacingWheel.Algorithm.MultiAdjustmentToolkit => localization[ "MultiAdjustmentToolkit" ],
				_ => algorithm.ToString()
			};
		}

		return algorithm switch
		{
			RacingWheel.Algorithm.Native60Hz => "Latest LMU sample (hold-latest, ~500 Hz)",
			RacingWheel.Algorithm.Native360Hz => "Interpolated LMU stream (~500 Hz)",
			RacingWheel.Algorithm.DetailBooster => "Detail Booster on interpolated LMU stream (~500 Hz)",
			RacingWheel.Algorithm.DeltaLimiter => "Delta Limiter on interpolated LMU stream (~500 Hz)",
			RacingWheel.Algorithm.DetailBoosterOn60Hz => "Detail Booster on latest LMU sample (~500 Hz)",
			RacingWheel.Algorithm.DeltaLimiterOn60Hz => "Delta Limiter on latest LMU sample (~500 Hz)",
			RacingWheel.Algorithm.SlewAndTotalCompression => "Slew + Total Compression on interpolated LMU stream (~500 Hz)",
			RacingWheel.Algorithm.MultiAdjustmentToolkit => "Multi Adjustment Toolkit (LMU stream aware, ~500 Hz)",
			_ => algorithm.ToString()
		};
	}

	private static string GetSimulatorAwareMultiFfbSourceLabel( SimId selectedSimId, RacingWheel.MultiFFBSourceOptions option, Components.Localization localization )
	{
		if ( selectedSimId != SimId.LeMansUltimate )
		{
			return option switch
			{
				RacingWheel.MultiFFBSourceOptions.Native60Hz => localization[ "Native60Hz" ],
				RacingWheel.MultiFFBSourceOptions.HybridVariable30 => localization[ "HybridVariable30" ],
				RacingWheel.MultiFFBSourceOptions.Hybrid10 => localization[ "Hybrid10" ],
				RacingWheel.MultiFFBSourceOptions.Native360Hz => localization[ "Native360Hz" ],
				RacingWheel.MultiFFBSourceOptions.DefaultsNative60Hz => localization[ "DefaultsNative60Hz" ],
				RacingWheel.MultiFFBSourceOptions.DefaultsHybridVariable30 => localization[ "DefaultsHybridVariable30" ],
				RacingWheel.MultiFFBSourceOptions.DefaultsHybrid10 => localization[ "DefaultsHybrid10" ],
				RacingWheel.MultiFFBSourceOptions.DefaultsNative360Hz => localization[ "DefaultsNative360Hz" ],
				RacingWheel.MultiFFBSourceOptions.PresetBasicFFB => localization[ "PresetBasicFFB" ],
				RacingWheel.MultiFFBSourceOptions.PresetBalancedFFB => localization[ "PresetBalancedFFB" ],
				RacingWheel.MultiFFBSourceOptions.PresetSmoothVariableBlend => localization[ "PresetSmoothVariableBlend" ],
				RacingWheel.MultiFFBSourceOptions.PresetBoostDetail => localization[ "PresetBoostDetail" ],
				RacingWheel.MultiFFBSourceOptions.PresetReduceDetail => localization[ "PresetReduceDetail" ],
				RacingWheel.MultiFFBSourceOptions.PresetReduceBigBumps => localization[ "PresetReduceBigBumps" ],
				RacingWheel.MultiFFBSourceOptions._Dummy1_ => "_",
				RacingWheel.MultiFFBSourceOptions._Dummy2_ => "_",
				_ => option.ToString()
			};
		}

		return option switch
		{
			RacingWheel.MultiFFBSourceOptions.Native60Hz => "Latest LMU sample (hold-latest, ~500 Hz)",
			RacingWheel.MultiFFBSourceOptions.HybridVariable30 => "Interpolated on latest LMU sample (variable blend, ~500 Hz)",
			RacingWheel.MultiFFBSourceOptions.Hybrid10 => "Interpolated on latest LMU sample (10% blend, ~500 Hz)",
			RacingWheel.MultiFFBSourceOptions.Native360Hz => "Interpolated LMU stream (~500 Hz)",
			RacingWheel.MultiFFBSourceOptions.DefaultsNative60Hz => "Defaults: latest LMU sample (~500 Hz)",
			RacingWheel.MultiFFBSourceOptions.DefaultsHybridVariable30 => "Defaults: interpolated on latest LMU sample (variable, ~500 Hz)",
			RacingWheel.MultiFFBSourceOptions.DefaultsHybrid10 => "Defaults: interpolated on latest LMU sample (10%, ~500 Hz)",
			RacingWheel.MultiFFBSourceOptions.DefaultsNative360Hz => "Defaults: interpolated LMU stream (~500 Hz)",
			RacingWheel.MultiFFBSourceOptions.PresetBasicFFB => localization[ "PresetBasicFFB" ],
			RacingWheel.MultiFFBSourceOptions.PresetBalancedFFB => localization[ "PresetBalancedFFB" ],
			RacingWheel.MultiFFBSourceOptions.PresetSmoothVariableBlend => localization[ "PresetSmoothVariableBlend" ],
			RacingWheel.MultiFFBSourceOptions.PresetBoostDetail => localization[ "PresetBoostDetail" ],
			RacingWheel.MultiFFBSourceOptions.PresetReduceDetail => localization[ "PresetReduceDetail" ],
			RacingWheel.MultiFFBSourceOptions.PresetReduceBigBumps => localization[ "PresetReduceBigBumps" ],
			RacingWheel.MultiFFBSourceOptions._Dummy1_ => "_",
			RacingWheel.MultiFFBSourceOptions._Dummy2_ => "_",
			_ => option.ToString()
		};
	}

	private static string GetSimulatorAwareMultiDetailLabel( SimId selectedSimId, Components.Localization localization )
	{
		if ( selectedSimId != SimId.LeMansUltimate )
		{
			return localization[ "Multi360HzDetail" ];
		}

		return "Surface Detail";
	}

	public void UpdateSteeringDeviceOptions()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[RacingWheelPage] UpdateSteeringDeviceOptions >>>" );

		var localization = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization;
		var settings = MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings;

		var dictionary = new Dictionary<Guid, string>();

		if ( app.DirectInput.ForceFeedbackDeviceList.Count == 0 )
		{
			dictionary.Add( Guid.Empty, localization[ "NoFFBDevicesFound" ] );
		}
		else
		{
			dictionary.Add( Guid.Empty, localization[ "FFBDeviceNotSelected" ] );
		}

		app.DirectInput.ForceFeedbackDeviceList.ToList().ForEach( keyValuePair => dictionary[ keyValuePair.Key ] = keyValuePair.Value );

		if ( !dictionary.ContainsKey( settings.RacingWheelSteeringDeviceGuid ) )
		{
			dictionary.Add( settings.RacingWheelSteeringDeviceGuid, $"{localization[ "DeviceNotFound" ]} [{settings.RacingWheelSteeringDeviceGuid}]" );
		}

		app.Dispatcher.Invoke( () =>
		{
			SteeringDevice_MairaComboBox.ItemsSource = dictionary.OrderBy( keyValuePair => keyValuePair.Value ).ToList();
			SteeringDevice_MairaComboBox.SelectedValue = settings.RacingWheelSteeringDeviceGuid;
			SteeringDevice_MairaComboBox.OffValue = Guid.Empty;
		} );

		app.Logger.WriteLine( "[RacingWheelPage] <<< UpdateSteeringDeviceOptions" );
	}

	public void UpdateAlgorithmOptions()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[RacingWheelPage] UpdateAlgorithmOptions >>>" );

		var localization = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization;
		var settings = MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings;
		var selectedSimId = app.Simulator.SelectedSimId;

		var dictionary = new Dictionary<RacingWheel.Algorithm, string>
		{
			{ RacingWheel.Algorithm.Native60Hz, GetSimulatorAwareAlgorithmLabel( selectedSimId, RacingWheel.Algorithm.Native60Hz, localization ) },
			{ RacingWheel.Algorithm.Native360Hz, GetSimulatorAwareAlgorithmLabel( selectedSimId, RacingWheel.Algorithm.Native360Hz, localization ) },
			{ RacingWheel.Algorithm.DetailBooster, GetSimulatorAwareAlgorithmLabel( selectedSimId, RacingWheel.Algorithm.DetailBooster, localization ) },
			{ RacingWheel.Algorithm.DeltaLimiter, GetSimulatorAwareAlgorithmLabel( selectedSimId, RacingWheel.Algorithm.DeltaLimiter, localization ) },
			{ RacingWheel.Algorithm.DetailBoosterOn60Hz, GetSimulatorAwareAlgorithmLabel( selectedSimId, RacingWheel.Algorithm.DetailBoosterOn60Hz, localization ) },
			{ RacingWheel.Algorithm.DeltaLimiterOn60Hz, GetSimulatorAwareAlgorithmLabel( selectedSimId, RacingWheel.Algorithm.DeltaLimiterOn60Hz, localization ) },
			{ RacingWheel.Algorithm.SlewAndTotalCompression, GetSimulatorAwareAlgorithmLabel( selectedSimId, RacingWheel.Algorithm.SlewAndTotalCompression, localization ) },
			{ RacingWheel.Algorithm.MultiAdjustmentToolkit, GetSimulatorAwareAlgorithmLabel( selectedSimId, RacingWheel.Algorithm.MultiAdjustmentToolkit, localization ) }
		};

		app.Dispatcher.Invoke( () =>
		{
			Algorithm_MairaComboBox.ItemsSource = dictionary.ToList();
			Algorithm_MairaComboBox.SelectedValue = settings.RacingWheelAlgorithm;
		} );

		app.Logger.WriteLine( "[RacingWheelPage] <<< UpdateAlgorithmOptions" );
	}

	public void UpdateMultiFFBSourceOptions()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[RacingWheelPage] UpdateFFBSourceOptions >>>" );

		var localization = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization;
		var settings = MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings;
		var selectedSimId = app.Simulator.SelectedSimId;

		var dictionary = new Dictionary<RacingWheel.MultiFFBSourceOptions, string>
		{
			{ RacingWheel.MultiFFBSourceOptions.Native60Hz, GetSimulatorAwareMultiFfbSourceLabel( selectedSimId, RacingWheel.MultiFFBSourceOptions.Native60Hz, localization ) },
			{ RacingWheel.MultiFFBSourceOptions.HybridVariable30, GetSimulatorAwareMultiFfbSourceLabel( selectedSimId, RacingWheel.MultiFFBSourceOptions.HybridVariable30, localization ) },
			{ RacingWheel.MultiFFBSourceOptions.Hybrid10, GetSimulatorAwareMultiFfbSourceLabel( selectedSimId, RacingWheel.MultiFFBSourceOptions.Hybrid10, localization ) },
			{ RacingWheel.MultiFFBSourceOptions.Native360Hz, GetSimulatorAwareMultiFfbSourceLabel( selectedSimId, RacingWheel.MultiFFBSourceOptions.Native360Hz, localization ) },
			{ RacingWheel.MultiFFBSourceOptions._Dummy1_, "_" },
			{ RacingWheel.MultiFFBSourceOptions.DefaultsNative60Hz, GetSimulatorAwareMultiFfbSourceLabel( selectedSimId, RacingWheel.MultiFFBSourceOptions.DefaultsNative60Hz, localization ) },
			{ RacingWheel.MultiFFBSourceOptions.DefaultsHybridVariable30, GetSimulatorAwareMultiFfbSourceLabel( selectedSimId, RacingWheel.MultiFFBSourceOptions.DefaultsHybridVariable30, localization ) },
			{ RacingWheel.MultiFFBSourceOptions.DefaultsHybrid10, GetSimulatorAwareMultiFfbSourceLabel( selectedSimId, RacingWheel.MultiFFBSourceOptions.DefaultsHybrid10, localization ) },
			{ RacingWheel.MultiFFBSourceOptions.DefaultsNative360Hz, GetSimulatorAwareMultiFfbSourceLabel( selectedSimId, RacingWheel.MultiFFBSourceOptions.DefaultsNative360Hz, localization ) },
			{ RacingWheel.MultiFFBSourceOptions._Dummy2_, "_" },
			{ RacingWheel.MultiFFBSourceOptions.PresetBasicFFB, GetSimulatorAwareMultiFfbSourceLabel( selectedSimId, RacingWheel.MultiFFBSourceOptions.PresetBasicFFB, localization ) },
			{ RacingWheel.MultiFFBSourceOptions.PresetBalancedFFB, GetSimulatorAwareMultiFfbSourceLabel( selectedSimId, RacingWheel.MultiFFBSourceOptions.PresetBalancedFFB, localization ) },
			{ RacingWheel.MultiFFBSourceOptions.PresetSmoothVariableBlend, GetSimulatorAwareMultiFfbSourceLabel( selectedSimId, RacingWheel.MultiFFBSourceOptions.PresetSmoothVariableBlend, localization ) },
			{ RacingWheel.MultiFFBSourceOptions.PresetBoostDetail, GetSimulatorAwareMultiFfbSourceLabel( selectedSimId, RacingWheel.MultiFFBSourceOptions.PresetBoostDetail, localization ) },
			{ RacingWheel.MultiFFBSourceOptions.PresetReduceDetail, GetSimulatorAwareMultiFfbSourceLabel( selectedSimId, RacingWheel.MultiFFBSourceOptions.PresetReduceDetail, localization ) },
			{ RacingWheel.MultiFFBSourceOptions.PresetReduceBigBumps, GetSimulatorAwareMultiFfbSourceLabel( selectedSimId, RacingWheel.MultiFFBSourceOptions.PresetReduceBigBumps, localization ) }
		};

		app.Dispatcher.Invoke( () =>
		{
			MultiFFBSource_MairaComboBox.ItemsSource = dictionary;
			MultiFFBSource_MairaComboBox.SelectedValue = settings.RacingWheelMultiFFBSourceSelection;
			Multi360HzDetail_MairaKnob.Label = GetSimulatorAwareMultiDetailLabel( selectedSimId, localization );
		} );

		app.Logger.WriteLine( "[RacingWheelPage] <<< UpdateFFBSourceOptions" );
	}

	public void UpdatePredictionModeOptions()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[RacingWheelPage] UpdatePredictionModeOptions >>>" );

		var localization = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization;
		var settings = MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings;

		var dictionary = new Dictionary<RacingWheel.PredictionMode, string>
		{
			{ RacingWheel.PredictionMode.Disabled, localization[ "Disabled" ] },
			{ RacingWheel.PredictionMode.PredictK1, localization[ "PredictK1" ] },
			{ RacingWheel.PredictionMode.PredictK2, localization[ "PredictK2" ] }
		};

		app.Dispatcher.Invoke( () =>
		{
			PredictionMode_MairaComboBox.ItemsSource = dictionary.ToList();
			PredictionMode_MairaComboBox.SelectedValue = settings.RacingWheelPredictionMode;
		} );

		app.Logger.WriteLine( "[RacingWheelPage] <<< UpdatePredictionModeOptions" );
	}

	public void UpdatePreviewRecordingsOptions()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[RacingWheelPage] UpdatePreviewRecordingsOptions >>>" );

		var localization = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization;

		var dictionary = new Dictionary<string, string>();

		if ( app.RecordingManager.Recordings.Count == 0 )
		{
			dictionary.Add( string.Empty, localization[ "NoRecordingsFound" ] );
		}

		foreach ( var recording in app.RecordingManager.Recordings )
		{
			dictionary.Add( recording.Key, recording.Value.Description! );
		}

		app.Dispatcher.Invoke( () =>
		{
			PreviewRecordings_MairaComboBox.ItemsSource = dictionary.OrderBy( keyValuePair => keyValuePair.Value ).ToList();
		} );

		app.Logger.WriteLine( "[RacingWheelPage] <<< UpdatePreviewRecordingsOptions" );
	}

	public void UpdateLFERecordingDeviceOptions()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[RacingWheelPage] UpdateLFERecordingDeviceOptions >>>" );

		var localization = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization;
		var settings = MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings;

		var dictionary = new Dictionary<Guid, string>();

		app.LFE.CaptureDeviceList.ToList().ForEach( keyValuePair => dictionary[ keyValuePair.Key ] = keyValuePair.Value );

		if ( !dictionary.ContainsKey( settings.RacingWheelLFERecordingDeviceGuid ) )
		{
			dictionary.Add( settings.RacingWheelLFERecordingDeviceGuid, $"{localization[ "DeviceNotFound" ]} [{settings.RacingWheelLFERecordingDeviceGuid}]" );
		}

		app.Dispatcher.Invoke( () =>
		{
			LFERecordingDevice_MairaComboBox.ItemsSource = dictionary.OrderBy( keyValuePair => keyValuePair.Value ).ToList();
			LFERecordingDevice_MairaComboBox.SelectedValue = settings.RacingWheelLFERecordingDeviceGuid;
			LFERecordingDevice_MairaComboBox.OffValue = Guid.Empty;
		} );

		app.Logger.WriteLine( "[RacingWheelPage] <<< UpdateLFERecordingDeviceOptions" );
	}

	public void UpdateSteeringDeviceSection()
	{
		var app = App.Instance!;

		var settings = MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings;

		app.Dispatcher.Invoke( () =>
		{
			// update power button

			ImageSource? imageSource;

			var blink = false;

			if ( !settings.RacingWheelEnableForceFeedback )
			{
				imageSource = new ImageSourceConverter().ConvertFromString( "pack://application:,,,/MarvinsAIRARefactored;component/Artwork/Buttons/power-red.png" ) as ImageSource;

				blink = true;
			}
			else if ( !app.Simulator.IsConnected )
			{
				imageSource = new ImageSourceConverter().ConvertFromString( "pack://application:,,,/MarvinsAIRARefactored;component/Artwork/Buttons/power-blue.png" ) as ImageSource;
			}
			else if ( !app.DirectInput.ForceFeedbackInitialized )
			{
				imageSource = new ImageSourceConverter().ConvertFromString( "pack://application:,,,/MarvinsAIRARefactored;component/Artwork/Buttons/power-yellow.png" ) as ImageSource;
			}
			else
			{
				imageSource = new ImageSourceConverter().ConvertFromString( "pack://application:,,,/MarvinsAIRARefactored;component/Artwork/Buttons/power-green.png" ) as ImageSource;
			}

			if ( imageSource != null )
			{
				Power_MairaMappableButton.Icon = imageSource;
				Power_MairaMappableButton.Blink = blink;
			}

			// update test, reset, set, and clear buttons

			var disabled = !app.DirectInput.ForceFeedbackInitialized;

			Test_MairaMappableButton.Disabled = disabled;
			Reset_MairaMappableButton.Disabled = disabled;
			Set_MairaMappableButton.Disabled = disabled;
			Clear_MairaMappableButton.Disabled = disabled;

			// update steering device error message

			if ( app.DirectInput.ForceFeedbackInitialized )
			{
				SteeringDeviceFaultReason_TextBlock.Visibility = Visibility.Collapsed;
			}
			else
			{
				if ( !settings.RacingWheelEnableForceFeedback )
				{
					SteeringDeviceFaultReason_TextBlock.Text = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization[ "FFBIsDisabled" ];
				}
				else if ( !app.Simulator.IsConnected )
				{
					SteeringDeviceFaultReason_TextBlock.Text = SimRegistry.GetNotRunningStatusText( app.Simulator.SelectedSimId );
				}
				else if ( ( app.Simulator.SelectedSimId == SimId.IRacing ) && ( app.Simulator.SimMode != "full" ) )
				{
					SteeringDeviceFaultReason_TextBlock.Text = SimRegistry.GetReplayModeStatusText( app.Simulator.SelectedSimId );
				}
				else if ( app.RacingWheel.SuspendForceFeedback )
				{
					if ( app.SteeringEffects.IsCalibrating )
					{
						SteeringDeviceFaultReason_TextBlock.Text = MarvinsAIRARefactored.DataContext.DataContext.Instance.Localization[ "CalibrationIsRunning" ];
					}
					else if ( ( app.Simulator.SelectedSimId == SimId.IRacing ) && app.Simulator.SteeringFFBEnabled && !settings.RacingWheelAlwaysEnableFFB )
					{
						SteeringDeviceFaultReason_TextBlock.Text = SimRegistry.GetSimulatorForceFeedbackStatusText( app.Simulator.SelectedSimId );
					}
					else if ( app.RacingWheel.WaitingForForceFeedbackResume )
					{
						SteeringDeviceFaultReason_TextBlock.Text = SimRegistry.GetForceFeedbackWaitingStatusText();
					}
					else
					{
						SteeringDeviceFaultReason_TextBlock.Text = SimRegistry.GetForceFeedbackSuspendedStatusText();
					}
				}
				else
				{
					SteeringDeviceFaultReason_TextBlock.Text = app.DirectInput.ForceFeedbackErrorMessage;
				}

				SteeringDeviceFaultReason_TextBlock.Visibility = Visibility.Visible;
			}
		} );
	}

	#endregion
}
