using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace LgHdmiTray
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\ShortcutLG", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("ShortcutLG ya esta en ejecucion.", "ShortcutLG",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayContext());
            }
        }
    }

    internal sealed class HotkeyWindow : Form
    {
        public event EventHandler<HotkeyEventArgs> HotkeyPressed;

        public HotkeyWindow()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-3000, -3000);
            Size = new Size(1, 1);
            IntPtr created = Handle;
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.WM_HOTKEY)
            {
                EventHandler<HotkeyEventArgs> handler = HotkeyPressed;
                if (handler != null)
                    handler(this, new HotkeyEventArgs(message.WParam.ToInt32()));
                return;
            }
            base.WndProc(ref message);
        }
    }

    internal sealed class HotkeyEventArgs : EventArgs
    {
        public readonly int Id;

        public HotkeyEventArgs(int id)
        {
            Id = id;
        }
    }

    internal sealed class HotkeyManager : IDisposable
    {
        private readonly IntPtr handle;

        public HotkeyManager(IntPtr handle)
        {
            this.handle = handle;
        }

        public string TryRegisterAll(Hotkey[] hotkeys)
        {
            UnregisterAll();
            if (hotkeys == null) return "No hay atajos configurados.";

            for (int i = 0; i < hotkeys.Length; i++)
            {
                Hotkey hotkey = hotkeys[i];
                if (hotkey == null || !hotkey.IsValid)
                {
                    UnregisterAll();
                    string name = i >= 0 && i < AppConfig.ActionNames.Length
                        ? AppConfig.ActionNames[i] : "accion " + (i + 1);
                    return "El atajo de " + name + " no es valido.";
                }
                if (!NativeMethods.RegisterHotKey(handle, i + 1, hotkey.RegisterModifiers,
                    (uint)hotkey.Key))
                {
                    int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    UnregisterAll();
                    return "No se pudo registrar " + hotkey + " (error " + error + ").\n" +
                           "Puede que otra aplicacion lo este usando; prueba otra combinacion.";
                }
            }
            return null;
        }

        public void UnregisterAll()
        {
            for (int i = 1; i <= AppConfig.ActionCount; i++)
                NativeMethods.UnregisterHotKey(handle, i);
        }

        public void Dispose()
        {
            UnregisterAll();
        }
    }

    internal static class Startup
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "ShortcutLG";

        public static bool IsEnabled()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key =
                    Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    return key != null && key.GetValue(ValueName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void Set(bool enabled)
        {
            using (Microsoft.Win32.RegistryKey key =
                Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key == null)
                    throw new Exception("No se pudo abrir el Registro de Windows.");
                if (enabled)
                    key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                else
                    key.DeleteValue(ValueName, false);
            }
        }

        public static void Repair()
        {
            if (IsEnabled()) Set(true);
        }
    }
}
