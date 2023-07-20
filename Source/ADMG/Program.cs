using ASFW;
using ASFW.Extension.Text;
using ASFW.Platform.Desktop;

namespace ADMG;

internal static class Program
{
	private static void Main()
	{
		Asfw.EnableExtension<TextAsfwExtension>();
		Asfw.Init<DesktopASFWPlatform>();

		using (var dmg = new DMG(@"E:\roms\GB\Dr. Mario (World).gb"))
			dmg.Start();

		Asfw.Quit();
	}
}
