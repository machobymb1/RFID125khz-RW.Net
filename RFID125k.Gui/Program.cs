using RFID125k.Core;

namespace RFID125k.Gui;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Localization.LoadLanguage(Localization.LoadConfigLanguage());
        Application.Run(new MainForm());
    }
}