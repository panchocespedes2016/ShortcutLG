using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LgHdmiTray
{
    internal sealed class AppConfig
    {
        public string TvIp;
        public string ClientKey;
        public Hotkey[] Hotkeys;

        public AppConfig()
        {
            TvIp = "";
            ClientKey = "";
            Hotkeys = DefaultHotkeys();
        }

        public static Hotkey[] DefaultHotkeys()
        {
            return new Hotkey[]
            {
                new Hotkey(NativeMethods.MOD_WIN | NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT, Keys.D1),
                new Hotkey(NativeMethods.MOD_WIN | NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT, Keys.D2),
                new Hotkey(NativeMethods.MOD_WIN | NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT, Keys.D3),
                new Hotkey(NativeMethods.MOD_WIN | NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT, Keys.D4)
            };
        }

        public bool IsConfigured
        {
            get { return !string.IsNullOrWhiteSpace(TvIp) && !string.IsNullOrWhiteSpace(ClientKey); }
        }

        public static string DataDirectory
        {
            get
            {
                string custom = Environment.GetEnvironmentVariable("LGHDMI_CONFIG_DIR");
                if (!string.IsNullOrWhiteSpace(custom)) return custom;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShortcutLG");
            }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(DataDirectory, "tray.ini"); }
        }

        public static AppConfig Load()
        {
            AppConfig cfg = new AppConfig();
            try
            {
                if (!File.Exists(ConfigPath)) return cfg;
                Dictionary<string, string> values =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(ConfigPath))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("#") || trimmed.StartsWith(";")) continue;
                    int eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;
                    values[trimmed.Substring(0, eq).Trim()] = trimmed.Substring(eq + 1).Trim();
                }
                string value;
                if (values.TryGetValue("TvIp", out value)) cfg.TvIp = value;
                if (values.TryGetValue("ClientKey", out value)) cfg.ClientKey = value;
                for (int i = 0; i < 4; i++)
                {
                    Hotkey hk;
                    if (values.TryGetValue("Hotkey" + (i + 1), out value) &&
                        Hotkey.TryParse(value, out hk) && hk.IsValid)
                        cfg.Hotkeys[i] = hk;
                }
            }
            catch (Exception ex)
            {
                Log.Write("Error leyendo configuracion: " + ex.Message);
            }
            return cfg;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                List<string> lines = new List<string>();
                lines.Add("# Configuracion de ShortcutLG");
                lines.Add("TvIp=" + (TvIp ?? ""));
                lines.Add("ClientKey=" + (ClientKey ?? ""));
                for (int i = 0; i < 4; i++)
                    lines.Add("Hotkey" + (i + 1) + "=" + (Hotkeys[i] != null ? Hotkeys[i].ToString() : ""));
                File.WriteAllLines(ConfigPath, lines.ToArray(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log.Write("Error guardando configuracion: " + ex.Message);
                throw;
            }
        }
    }

    internal static class Log
    {
        private static readonly object Sync = new object();

        public static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(AppConfig.DataDirectory);
                    string path = Path.Combine(AppConfig.DataDirectory, "tray.log");
                    FileInfo info = new FileInfo(path);
                    if (info.Exists && info.Length > 512 * 1024)
                    {
                        try
                        {
                            string old = path + ".old";
                            if (File.Exists(old)) File.Delete(old);
                            File.Move(path, old);
                        }
                        catch { }
                    }
                    File.AppendAllText(path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine,
                        new UTF8Encoding(false));
                }
            }
            catch { }
        }
    }
}
