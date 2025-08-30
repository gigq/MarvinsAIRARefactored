
using System.Diagnostics;

using MarvinsAIRARefactored.Classes;
using MarvinsAIRARefactored.Controls;

namespace MarvinsAIRARefactored.Components;

public sealed class SpeechToText : IDisposable
{
	private ChromeSTTBridge? _chromeSTTBridge;
	private Process? _browserProcess;
	private string _language = "en-US";
	private bool _isEnabled = false;
	private bool _isStarted = false;

	public string Language
	{
		get => _language;

		set
		{
			_language = value ?? "en-US";

			_ = _chromeSTTBridge?.SendSetLanguageAsync( _language );
		}
	}

	public void Dispose() => _ = DisableAsync();

	public static void SetMairaComboBoxItemsSource( MairaComboBox mairaComboBox )
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[SpeechToText] SetMairaComboBoxItemsSource >>>" );

		var dictionary = new Dictionary<string, string>
		{
			// ---- European ----
			{ "en-US", "English (US)" },
			{ "en-GB", "English (UK)" },
			{ "fr-FR", "français" },
			{ "fr-CA", "français (Canada)" },
			{ "de-DE", "Deutsch" },
			{ "it-IT", "italiano" },
			{ "es-ES", "español (España)" },
			{ "es-MX", "español (México)" },
			{ "pt-PT", "português (Portugal)" },
			{ "pt-BR", "português (Brasil)" },
			{ "nl-NL", "Nederlands" },
			{ "nl-BE", "Nederlands (België)" },
			{ "pl-PL", "polski" },
			{ "cs-CZ", "čeština" },
			{ "sk-SK", "slovenčina" },
			{ "sl-SI", "slovenščina" },
			{ "hr-HR", "hrvatski" },
			{ "sr-RS", "српски" },
			{ "bs-BA", "bosanski" },
			{ "mk-MK", "македонски" },
			{ "bg-BG", "български" },
			{ "ro-RO", "română" },
			{ "hu-HU", "magyar" },
			{ "fi-FI", "suomi" },
			{ "sv-SE", "svenska" },
			{ "da-DK", "dansk" },
			{ "no-NO", "norsk bokmål" },
			{ "is-IS", "íslenska" },
			{ "et-EE", "eesti" },
			{ "lv-LV", "latviešu" },
			{ "lt-LT", "lietuvių" },
			{ "el-GR", "Ελληνικά" },
			{ "mt-MT", "Malti" },
			{ "ga-IE", "Gaeilge" },
			{ "cy-GB", "Cymraeg" },
			{ "ru-RU", "русский" },
			{ "uk-UA", "українська" },
			{ "be-BY", "беларуская" },
			{ "tr-TR", "Türkçe" },
			{ "sq-AL", "shqip" },

			// ---- Middle East / Central Asia ----
			{ "he-IL", "עברית" },
			{ "ar-SA", "العربية" },
			{ "fa-IR", "فارسی" },
			{ "ur-PK", "اردو" },
			{ "kk-KZ", "қазақ тілі" },
			{ "uz-UZ", "oʻzbekcha" },
			{ "az-AZ", "azərbaycan dili" },
			{ "hy-AM", "հայերեն" },
			{ "ka-GE", "ქართული" },

			// ---- South Asia ----
			{ "hi-IN", "हिन्दी" },
			{ "bn-BD", "বাংলা (বাংলাদেশ)" },
			{ "bn-IN", "বাংলা (ভারত)" },
			{ "ta-IN", "தமிழ்" },
			{ "te-IN", "తెలుగు" },
			{ "ml-IN", "മലയാളം" },
			{ "kn-IN", "ಕನ್ನಡ" },
			{ "gu-IN", "ગુજરાતી" },
			{ "pa-IN", "ਪੰਜਾਬੀ" },
			{ "si-LK", "සිංහල" },
			{ "ne-NP", "नेपाली" },

			// ---- East Asia ----
			{ "zh-CN", "中文 (简体)" },
			{ "zh-TW", "中文 (繁體)" },
			{ "zh-HK", "中文 (香港)" },
			{ "ja-JP", "日本語" },
			{ "ko-KR", "한국어" },

			// ---- Southeast Asia ----
			{ "th-TH", "ไทย" },
			{ "vi-VN", "Tiếng Việt" },
			{ "id-ID", "Bahasa Indonesia" },
			{ "ms-MY", "Bahasa Melayu" },
			{ "km-KH", "ភាសាខ្មែរ" },
			{ "lo-LA", "ລາວ" },
			{ "my-MM", "ဗမာ" },
			{ "fil-PH", "Filipino" },

			// ---- Other notable ----
			{ "af-ZA", "Afrikaans" },
			{ "sw-KE", "Kiswahili" },
			{ "zu-ZA", "isiZulu" }
		};

		mairaComboBox.ItemsSource = dictionary;
		mairaComboBox.SelectedValue = DataContext.DataContext.Instance.Settings.SpeechToTextLanguageCode;

		app.Logger.WriteLine( "[SpeechToText] <<< SetMairaComboBoxItemsSource" );
	}

	public async Task EnableAsync( int port = 18888 )
	{
		if ( _isEnabled )
		{
			return;
		}

		var app = App.Instance!;

		app.Logger.WriteLine( "[SpeechToText] >>> EnableAsync" );

		app.Logger.WriteLine( "[SpeechToText] Checking for Chrome/Edge" );

		if ( !ChromeLauncher.IsChromeOrEdgeInstalled() )
		{
			app.Logger.WriteLine( "[SpeechToText] Chrome/Edge not found. Please install Chrome or Edge to use STT." );

			app.SpeechToTextWindow.SetFinalText( "Chrome/Edge not found. Please install Chrome or Edge to use STT." );
		}
		else
		{

			app.Logger.WriteLine( "[SpeechToText] Enabling Chrome STT bridge" );

			_chromeSTTBridge = new ChromeSTTBridge( port );

			_chromeSTTBridge.PartialText += text =>
			{
				app.SpeechToTextWindow.SetPartialText( text );
			};

			_chromeSTTBridge.FinalText += text =>
			{
				app.SpeechToTextWindow.SetFinalText( text );
			};

			_chromeSTTBridge.ErrorText += text =>
			{
				app.SpeechToTextWindow.SetFinalText( text );
			};

			_chromeSTTBridge.Start();

			var existingProcess = ChromeLauncher.FindExistingBridgeProcess( port );

			if ( existingProcess != null )
			{
				_browserProcess = existingProcess;

				try
				{
					_browserProcess.EnableRaisingEvents = true;

					_browserProcess.Exited += ( s, e ) =>
					{
						app.Logger.WriteLine( "[SpeechToText] Existing bridge process exited." );

						_ = DisableAsync();
					};
				}
				catch
				{
				}

				var connectedExisting = await _chromeSTTBridge.WaitUntilConnectedAsync( TimeSpan.FromSeconds( 3 ) );

				if ( !connectedExisting )
				{
					app.Logger.WriteLine( "[SpeechToText] Existing bridge window found, waiting longer for WS connect…" );

					connectedExisting = await _chromeSTTBridge.WaitUntilConnectedAsync( TimeSpan.FromSeconds( 7 ) );
				}

				if ( connectedExisting )
				{
					await _chromeSTTBridge.SendSetLanguageAsync( _language );

					app.Logger.WriteLine( "[SpeechToText] Reused existing bridge window." );

					_isEnabled = true;

					return;
				}
				else
				{
					app.Logger.WriteLine( "[SpeechToText] Existing window didn’t connect — will launch a new one." );
				}
			}

			// Launch the minimal browser window to the hosted page

			_browserProcess = ChromeLauncher.LaunchChromeTo( _chromeSTTBridge.HostURL );

			if ( _browserProcess == null )
			{
				app.Logger.WriteLine( "[SpeechToText] Failed to launch Chrome/Edge." );

				app.SpeechToTextWindow.SetFinalText( "Failed to launch Chrome/Edge." );

				await DisableAsync();
			}
			else
			{
				// Wait for the page to connect its WebSocket (up to 10s)

				var connected = await _chromeSTTBridge.WaitUntilConnectedAsync( TimeSpan.FromSeconds( 10 ) );

				if ( !connected )
				{
					app.Logger.WriteLine( "[SpeechToText] Browser page did not connect within 10s." );

					app.SpeechToTextWindow.SetFinalText( "[SpeechToText] Browser page did not connect within 10s." );

					await DisableAsync();
				}
				else
				{

					// Set initial language

					await _chromeSTTBridge.SendSetLanguageAsync( _language );

					app.Logger.WriteLine( "[SpeechToText] STT ready. First run: user must click 'Enable Microphone' and allow access." );

					_isEnabled = true;
				}
			}
		}

		app.Logger.WriteLine( "[SpeechToText] << EnableAsync" );
	}

	public async Task DisableAsync()
	{
		if ( !_isEnabled && ( _chromeSTTBridge == null ) )
		{
			return;
		}

		var app = App.Instance!;

		app.Logger.WriteLine( "[SpeechToText] >>> DisableAsync" );

		try
		{
			if ( _chromeSTTBridge != null )
			{
				await _chromeSTTBridge.StopAsync();

				_chromeSTTBridge = null;
			}
		}
		catch
		{
			// ignore
		}

		try
		{
			if ( ( _browserProcess != null ) && !_browserProcess.HasExited )
			{
				_browserProcess.CloseMainWindow();
				_browserProcess.Dispose();
			}

			_browserProcess = null;
		}
		catch
		{
			// ignore
		}

		_isEnabled = false;

		app.Logger.WriteLine( "[SpeechToText] << DisableAsync" );
	}

	public void Toggle( bool enable )
	{
		if ( enable )
		{
			_ = EnableAsync();
		}
		else
		{
			_ = DisableAsync();
		}
	}

	public void Start()
	{
		if ( !_isStarted )
		{
			_isStarted = true;

			if ( _chromeSTTBridge != null )
			{
				if ( _chromeSTTBridge.IsClientConnected )
				{
					_ = _chromeSTTBridge.SendStartAsync();
				}
			}
		}
	}

	public void Stop()
	{
		if ( _isStarted )
		{
			_isStarted = false;

			if ( _chromeSTTBridge != null )
			{
				if ( _chromeSTTBridge.IsClientConnected )
				{
					_ = _chromeSTTBridge.SendStopAsync();
				}
			}
		}
	}

	public void UpdateStrings()
	{
		_chromeSTTBridge?.UpdateStrings();
	}
}
