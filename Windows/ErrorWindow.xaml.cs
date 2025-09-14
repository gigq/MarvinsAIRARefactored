
using System.Windows;

using Clipboard = System.Windows.Clipboard;

namespace MarvinsAIRARefactored.Windows;

public partial class ErrorWindow : Window
{
	private readonly Exception? _exception;

	public ErrorWindow( string message, Exception? exception = null )
	{
		InitializeComponent();

		_exception = exception;

		Message_TextBlock.Text = message;

		Details_TextBlock.Text = exception?.ToString() ?? string.Empty;
	}

	public static void ShowModal( string message, Exception? exception = null )
	{
		var dialog = new ErrorWindow( message, exception );

		dialog.ShowDialog();
	}

	private void CopyDetails_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		try
		{
			var textToCopy = $"{Message_TextBlock.Text}\r\n\r\n{_exception}\r\n";

			Clipboard.SetText( textToCopy );
		}
		catch
		{
			// Swallow – clipboard can throw if unavailable
		}
	}

	private void Exit_MairaButton_Click( object sender, RoutedEventArgs e )
	{
		DialogResult = true;

		Close();
	}
}
