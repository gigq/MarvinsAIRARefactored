
using System.Diagnostics;
using System.IO;
using System.Management;

namespace MarvinsAIRARefactored.Classes;

public static class ChromeLauncher
{
	public static Process? LaunchChromeTo( string url )
	{
		var exe = FindChromeOrEdge();

		if ( exe == null )
		{
			return null;
		}

		var processStartInfo = new ProcessStartInfo( exe )
		{
			Arguments = $"--app=\"{url}\" --disable-translate --disable-infobars --no-first-run --window-size=640,400",
			UseShellExecute = false,
			CreateNoWindow = true
		};

		return Process.Start( processStartInfo );
	}

	public static bool IsChromeOrEdgeInstalled()
	{
		return FindChromeOrEdge() != null;
	}

	private static string? FindChromeOrEdge()
	{
		string[] candidates =
		{
            // Chrome
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Google\Chrome\Application\chrome.exe"),
			Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"),
			Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Google\Chrome\Application\chrome.exe"),

            // Edge
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"),
			Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"),
			Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Microsoft\Edge\Application\msedge.exe")
		};

		foreach ( var path in candidates )
		{
			if ( File.Exists( path ) )
			{
				return path;
			}
		}

		return null;
	}

	public static Process? FindExistingBridgeProcess( int port )
	{
		using var searcher = new ManagementObjectSearcher( "SELECT ProcessId, Name, CommandLine FROM Win32_Process WHERE " + "(Name='chrome.exe' OR Name='msedge.exe')" );

		foreach ( ManagementObject proc in searcher.Get().Cast<ManagementObject>() )
		{
			try
			{
				var cmd = ( proc[ "CommandLine" ] as string ) ?? "";
				var pid = Convert.ToInt32( proc[ "ProcessId" ] );

				if ( cmd.Contains( $"--app=\"http://localhost:{port}/\"", StringComparison.OrdinalIgnoreCase ) )
				{
					return Process.GetProcessById( pid );
				}
			}
			catch
			{
			}
		}

		return null;
	}
}
