using System;
using System.IO;
using System.Windows.Forms;

namespace ShortcutLGSetup
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool uninstall = string.Equals(
                Path.GetFileName(Application.ExecutablePath),
                "Uninstall.exe", StringComparison.OrdinalIgnoreCase);
            if (args != null)
            {
                foreach (string arg in args)
                {
                    if (string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase))
                        uninstall = true;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm(uninstall));
        }
    }
}
