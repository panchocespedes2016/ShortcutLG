using System;
using System.IO;
using Microsoft.Win32;

namespace ShortcutLGSetup
{
    internal static class AppPaths
    {
        public const string AppName = "ShortcutLG";
        public const string Version = "1.0.0";
        public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string RunValueName = "ShortcutLG";
        public const string UninstallKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ShortcutLG";

        public static string LocalAppData
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
        }

        public static string InstallDir
        {
            get { return Path.Combine(LocalAppData, @"Programs\ShortcutLG"); }
        }

        public static string AppPath
        {
            get { return Path.Combine(InstallDir, "ShortcutLG.exe"); }
        }

        public static string UninstallerPath
        {
            get { return Path.Combine(InstallDir, "Uninstall.exe"); }
        }

        public static string ConfigDir
        {
            get { return Path.Combine(LocalAppData, "ShortcutLG"); }
        }

        public static string StartMenuShortcut
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs\ShortcutLG.lnk");
            }
        }

        public static string DesktopShortcut
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "ShortcutLG.lnk");
            }
        }

        public static string GetUninstallDir()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath, false))
                {
                    if (key != null)
                    {
                        string location = key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrWhiteSpace(location)) return location;
                    }
                }
            }
            catch { }
            return InstallDir;
        }
    }
}
