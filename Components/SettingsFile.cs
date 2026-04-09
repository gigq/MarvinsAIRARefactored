
using System.IO;

using MarvinsALMUARefactored.Classes;
using MarvinsALMUARefactored.DataContext;

namespace MarvinsALMUARefactored.Components;

public class SettingsFile
{
	private static string SettingsFilePath { get; } = Path.Combine( App.DocumentsFolder, "Settings.xml" );

	private bool _pauseSerialization = false;
	public bool PauseSerialization
	{
		get => _pauseSerialization;

		set
		{
			if ( value != _pauseSerialization )
			{
				_pauseSerialization = value;

				var app = App.Instance!;

				if ( value )
				{
					app.Logger.WriteLine( "[SettingsFile] Pausing serialization" );
				}
				else
				{
					app.Logger.WriteLine( "[SettingsFile] Un-pausing serialization" );
				}
			}
		}
	}

	private bool _queueForSerialization = false;
	public bool QueueForSerialization
	{
		private get => _queueForSerialization;

		set
		{
			if ( value != _queueForSerialization )
			{
				if ( !value || !PauseSerialization )
				{
					_queueForSerialization = value;
				}
			}
		}
	}

	private int _serializationCounter = 0;

	public void Initialize()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[SettingsFile] Initialize >>>" );

		PauseSerialization = true;

		Settings.SuppressUpdatingOfContextSettings = true;

		if ( File.Exists( SettingsFilePath ) )
		{
			DataContext.DataContext.Instance.Settings = (Settings) Serializer.Load<Settings>( SettingsFilePath );
		}
		else
		{
			app.Logger.WriteLine( "[SettingsFile] Settings file does not exist - we will create a new one" );

			DataContext.DataContext.Instance.Settings.AppCurrentLanguageCode = DataContext.DataContext.Instance.Localization.ChooseInitialLanguage();
		}

		Settings.SuppressUpdatingOfContextSettings = false;

		PauseSerialization = false;

		app.Logger.WriteLine( "[SettingsFile] <<< Initialize" );
	}

	public void Tick( App app )
	{
		if ( QueueForSerialization )
		{
			if ( _serializationCounter == 0 )
			{
				app.Logger.WriteLine( "[SettingsFile] Queued for serialization" );
			}

			_serializationCounter = 60;

			QueueForSerialization = false;
		}

		if ( _serializationCounter > 0 )
		{
			_serializationCounter--;

			if ( _serializationCounter == 0 )
			{
				Serializer.Save( SettingsFilePath, DataContext.DataContext.Instance.Settings );

				app.Logger.WriteLine( "[SettingsFile] Settings.xml file updated" );
			}
		}
	}
}
