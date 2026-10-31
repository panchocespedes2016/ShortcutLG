using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShortcutLGSetup
{
    internal static class InstallOperations
    {
        public static bool IsAppRunning()
        {
            return Process.GetProcessesByName(AppPaths.AppName).Length > 0;
        }

        public static string StopApp()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(AppPaths.AppName);
                foreach (Process process in processes)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit(5000);
                        }
                    }
                    catch (Exception ex)
                    {
                        return ex.Message;
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static string Install(bool createDesktopShortcut, bool enableStartup)
        {
            string installDir = AppPaths.InstallDir;
            string appPath = AppPaths.AppPath;
            Directory.CreateDirectory(installDir);

            ExtractResource("ShortcutLG.exe", appPath);
            ExtractResource("LEEME.txt", Path.Combine(installDir, "LEEME.txt"));

            string uninstallerPath = AppPaths.UninstallerPath;
            if (!string.Equals(
                Path.GetFullPath(Application.ExecutablePath),
                Path.GetFullPath(uninstallerPath),
                StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(Application.ExecutablePath, uninstallerPath, true);
            }

            Shortcut.Create(AppPaths.StartMenuShortcut, appPath, installDir,
                "ShortcutLG - Cambia la entrada HDMI de la TV LG");
            if (createDesktopShortcut)
            {
                Shortcut.Create(AppPaths.DesktopShortcut, appPath, installDir,
                    "ShortcutLG - Cambia la entrada HDMI de la TV LG");
            }
            else
            {
                Shortcut.Delete(AppPaths.DesktopShortcut);
            }

            SetStartup(enableStartup, appPath);
            RegisterUninstaller(installDir, appPath, uninstallerPath);
            return null;
        }

        public static string Uninstall(bool deleteConfig)
        {
            string error = null;

            Shortcut.Delete(AppPaths.DesktopShortcut);
            Shortcut.Delete(AppPaths.StartMenuShortcut);
            RemoveStartup();

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(AppPaths.UninstallKeyPath, false);
            }
            catch (Exception ex)
            {
                error = "No se pudo borrar la entrada de desinstalacion: " + ex.Message;
            }

            if (deleteConfig)
            {
                try
                {
                    if (Directory.Exists(AppPaths.ConfigDir))
                        Directory.Delete(AppPaths.ConfigDir, true);
                }
                catch (Exception ex)
                {
                    error = "No se pudo borrar la configuracion: " + ex.Message;
                }
            }

            return error;
        }

        public static void ScheduleDeleteInstallDir()
        {
            ScheduleDelete(AppPaths.InstallDir);
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppPaths.RunKeyPath, false))
                {
                    return key != null && key.GetValue(AppPaths.RunValueName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void ExtractResource(string resourceName, string destination)
        {
            using (Stream stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception("Falta el recurso interno " + resourceName + ".");

                if (File.Exists(destination))
                    File.SetAttributes(destination, FileAttributes.Normal);

                using (FileStream file = new FileStream(destination, FileMode.Create,
                    FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(file);
                }
            }
        }

        private static void SetStartup(bool enabled, string appPath)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(AppPaths.RunKeyPath))
            {
                if (key == null)
                    throw new Exception("No se pudo abrir el Registro de Windows.");
                if (enabled)
                {
                    key.SetValue(AppPaths.RunValueName, "\"" + appPath + "\"",
                        RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(AppPaths.RunValueName, false);
                }
            }
        }

        private static void RemoveStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppPaths.RunKeyPath, true))
                {
                    if (key != null) key.DeleteValue(AppPaths.RunValueName, false);
                }
            }
            catch { }
        }

        private static void RegisterUninstaller(string installDir, string appPath,
            string uninstallerPath)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(AppPaths.UninstallKeyPath))
            {
                if (key == null)
                    throw new Exception("No se pudo registrar el desinstalador.");

                key.SetValue("DisplayName", "ShortcutLG");
                key.SetValue("DisplayVersion", AppPaths.Version);
                key.SetValue("Publisher", "ShortcutLG");
                key.SetValue("DisplayIcon", appPath + ",0");
                key.SetValue("InstallLocation", installDir);
                key.SetValue("UninstallString", "\"" + uninstallerPath + "\" --uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"),
                    RegistryValueKind.String);

                long sizeKb = new FileInfo(appPath).Length / 1024;
                if (sizeKb < 1) sizeKb = 1;
                if (sizeKb > int.MaxValue) sizeKb = int.MaxValue;
                key.SetValue("EstimatedSize", (int)sizeKb, RegistryValueKind.DWord);
            }
        }

        private static void ScheduleDelete(string installDir)
        {
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe",
                "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"" + installDir + "\"");
            info.CreateNoWindow = true;
            info.UseShellExecute = false;
            info.WindowStyle = ProcessWindowStyle.Hidden;
            info.WorkingDirectory = Path.GetTempPath();
            Process.Start(info);
        }
    }
}
