using System.Runtime.InteropServices;

namespace DynamicKeyPrompts;

static unsafe class Entry
{
    /// Called by the loader at the game's entry point (main thread, Arxan neutered, game CRT not yet
    /// initialised — do not touch game globals here).
    [UnmanagedCallersOnly(EntryPoint = "DKP_Init")]
    static int Init(LoaderApiNative* api)
    {
        try
        {
            if (!Loader.Attach(api)) return -1;
            Loader.Log($"core: DynamicKeyPrompts for DS3 {typeof(Entry).Assembly.GetName().Version} base={Loader.GameBase:X} arxan_detected={Loader.ArxanDetected}");

            if (!Loader.IsSupportedGame("DarkSoulsIII.exe", Rva.ExpectedTimeDateStamp)) return -2;

            Config.Load(Path.Combine(Loader.ModDir, "DynamicKeyPrompts.ini"));
            if (!Config.Enabled) return 0;
            if (Config.Style == LabelStyle.Icons) KeyIcons.Install();
            return Prompts.Install() ? 0 : -3;
        }
        catch (Exception e)
        {
            Loader.Log($"core: init failed: {e}");
            return -100;
        }
    }
}
