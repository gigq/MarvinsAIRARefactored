
using System.Runtime.InteropServices;

namespace MarvinsALMUARefactored.PInvoke;

public static partial class UXTheme
{
	[LibraryImport( "UXTheme.dll", EntryPoint = "#138", SetLastError = true)]
	[return: MarshalAs( UnmanagedType.Bool )]
	public static partial bool ShouldSystemUseDarkMode();
}
