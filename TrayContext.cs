using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LgHdmiTray
{
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly AppConfig config;
        private readonly NotifyIcon tray;
        private readonly HotkeyWindow window;
        private readonly HotkeyManager hotkeys;
        private ContextMenuStrip menu;
        private System.Windows.Forms.Timer firstRunTimer;
        private int switchBusy;
        private bool dialogOpen;
        private bool disposed;

        public TrayContext()
        {
            config = AppConfig.Load();
            Log.Write("Inicio. TV: " + (config.IsConfigured ? config.TvIp : "sin configurar"));

            window = new HotkeyWindow();
            window.HotkeyPressed += WindowHotkeyPressed;
            hotkeys = new HotkeyManager(window.Handle);

            tray = new NotifyIcon();
            Icon trayIcon = null;
            try
            {
                trayIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (Exception ex)
            {
                Log.Write("No se pudo cargar el icono: " + ex.Message);
            }
            tray.Icon = trayIcon != null ? trayIcon : SystemIcons.Application;
            tray.Text = "ShortcutLG";
            tray.Visible = true;
            tray.MouseClick += TrayMouseClick;
            RebuildMenu();

            string hotkeyError = hotkeys.TryRegisterAll(config.Hotkeys);
            if (hotkeyError != null)
            {
                Log.Write(hotkeyError);
                ShowBalloon("Algunos atajos no se pudieron registrar", hotkeyError,
                    ToolTipIcon.Warning, 6000);
            }
            else
            {
                Log.Write("Atajos registrados: " + HotkeyListText(config.Hotkeys));
            }

            try
            {
                Startup.Repair();
            }
            catch (Exception ex)
            {
                Log.Write("No se pudo reparar el inicio automatico: " + ex.Message);
            }

            if (!config.IsConfigured)
            {
                firstRunTimer = new System.Windows.Forms.Timer();
                firstRunTimer.Interval = 600;
                firstRunTimer.Tick += FirstRunTimerTick;
                firstRunTimer.Start();
                ShowBalloon("ShortcutLG", "Configura la TV para poder cambiar de entrada.",
                    ToolTipIcon.Info, 4000);
            }
        }

        private void RebuildMenu()
        {
            ContextMenuStrip oldMenu = menu;
            menu = new ContextMenuStrip();

            string status = string.IsNullOrWhiteSpace(config.TvIp)
                ? "Sin TV configurada"
                : "TV: " + config.TvIp;
            if (!config.IsConfigured && !string.IsNullOrWhiteSpace(config.TvIp))
                status += " (sin emparejar)";
            ToolStripMenuItem statusItem = new ToolStripMenuItem(status);
            statusItem.Enabled = false;
            menu.Items.Add(statusItem);
            menu.Items.Add(new ToolStripSeparator());

            for (int i = 0; i < AppConfig.ActionCount; i++)
            {
                if (i == AppConfig.ActionVolumeUp || i == AppConfig.ActionOk)
                    menu.Items.Add(new ToolStripSeparator());
                int action = i;
                ToolStripMenuItem item = new ToolStripMenuItem(AppConfig.ActionNames[i]);
                if (config.Hotkeys != null && i < config.Hotkeys.Length && config.Hotkeys[i] != null)
                    item.ShortcutKeyDisplayString = config.Hotkeys[i].ToString();
                item.Click += delegate { RunActionAsync(action); };
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem tvItem = new ToolStripMenuItem(
                string.IsNullOrWhiteSpace(config.TvIp) ? "Conectar a una TV..." : "Conectar a otra TV...");
            tvItem.Click += delegate { ShowTvSettings(); };
            menu.Items.Add(tvItem);

            ToolStripMenuItem hotkeyItem = new ToolStripMenuItem("Cambiar combinaciones de teclas...");
            hotkeyItem.Click += delegate { ShowHotkeySettings(); };
            menu.Items.Add(hotkeyItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem startupItem = new ToolStripMenuItem("Iniciar con Windows");
            startupItem.CheckOnClick = true;
            startupItem.Checked = Startup.IsEnabled();
            startupItem.Click += StartupItemClick;
            menu.Items.Add(startupItem);

            ToolStripMenuItem dataItem = new ToolStripMenuItem("Abrir carpeta de datos");
            dataItem.Click += delegate { OpenDataFolder(); };
            menu.Items.Add(dataItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Salir");
            exitItem.Click += delegate { ExitTray(); };
            menu.Items.Add(exitItem);

            tray.ContextMenuStrip = menu;
            if (oldMenu != null) oldMenu.Dispose();

            tray.Text = string.IsNullOrWhiteSpace(config.TvIp)
                ? "ShortcutLG - sin configurar"
                : "ShortcutLG - " + config.TvIp;
        }

        private void TrayMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && menu != null && !dialogOpen)
                menu.Show(Cursor.Position);
        }

        private void FirstRunTimerTick(object sender, EventArgs e)
        {
            if (firstRunTimer != null)
            {
                firstRunTimer.Stop();
                firstRunTimer.Dispose();
                firstRunTimer = null;
            }
            if (!config.IsConfigured) ShowTvSettings();
        }

        private void StartupItemClick(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null) return;
            try
            {
                Startup.Set(item.Checked);
                Log.Write("Inicio con Windows: " + item.Checked);
            }
            catch (Exception ex)
            {
                item.Checked = !item.Checked;
                Log.Write("Error con inicio automatico: " + ex.Message);
                ShowBalloon("No se pudo cambiar el inicio automatico", ex.Message,
                    ToolTipIcon.Error, 5000);
            }
        }

        private void OpenDataFolder()
        {
            try
            {
                Process.Start("explorer.exe", "\"" + AppConfig.DataDirectory + "\"");
            }
            catch (Exception ex)
            {
                Log.Write("No se pudo abrir la carpeta de datos: " + ex.Message);
            }
        }

        private void WindowHotkeyPressed(object sender, HotkeyEventArgs e)
        {
            if (dialogOpen || disposed) return;
            if (e.Id < 1 || e.Id > AppConfig.ActionCount) return;
            RunActionAsync(e.Id - 1);
        }

        private void RunActionAsync(int action)
        {
            if (disposed) return;
            if (action < 0 || action >= AppConfig.ActionCount) return;

            // Apagar pide confirmacion: un atajo accidental no debe apagar la TV.
            if (action == AppConfig.ActionPowerOff)
            {
                DialogResult confirm = MessageBox.Show(
                    "¿Apagar la TV?", "ShortcutLG",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;
            }

            if (Interlocked.CompareExchange(ref switchBusy, 1, 0) != 0) return;

            if (!config.IsConfigured)
            {
                Interlocked.Exchange(ref switchBusy, 0);
                ShowBalloon("ShortcutLG", "Configura la TV antes de usar los controles.",
                    ToolTipIcon.Warning, 4000);
                ShowTvSettings();
                return;
            }

            string ip = config.TvIp;
            string key = config.ClientKey;
            Log.Write(AppConfig.ActionNames[action] + " en " + ip);

            Task.Run(() => RunAction(ip, key, action)).ContinueWith(delegate(Task<LgResult> task)
            {
                LgResult result;
                try
                {
                    result = task.Result;
                }
                catch (Exception ex)
                {
                    result = new LgResult();
                    result.Error = "Error inesperado: " + ex.Message;
                    Log.Write(ex.ToString());
                }

                try
                {
                    window.BeginInvoke(new Action(delegate { FinishAction(action, ip, result); }));
                }
                catch
                {
                    Interlocked.Exchange(ref switchBusy, 0);
                }
            });
        }

        private static LgResult RunAction(string ip, string key, int action)
        {
            if (action >= 0 && action <= 3)
                return LgTvClient.Switch(ip, key, action + 1);
            if (action == AppConfig.ActionVolumeUp)
                return LgTvClient.Command(ip, key, "ssap://audio/volumeUp", null);
            if (action == AppConfig.ActionVolumeDown)
                return LgTvClient.Command(ip, key, "ssap://audio/volumeDown", null);
            if (action == AppConfig.ActionPowerOff)
                return LgTvClient.Command(ip, key, "ssap://system/turnOff", null);
            if (action == AppConfig.ActionHome)
                return LgTvClient.Home(ip, key);
            if (action == AppConfig.ActionOk)
                return LgTvClient.Button(ip, key, "ENTER");
            if (action == AppConfig.ActionUp)
                return LgTvClient.Button(ip, key, "UP");
            if (action == AppConfig.ActionDown)
                return LgTvClient.Button(ip, key, "DOWN");
            if (action == AppConfig.ActionLeft)
                return LgTvClient.Button(ip, key, "LEFT");
            if (action == AppConfig.ActionRight)
                return LgTvClient.Button(ip, key, "RIGHT");
            if (action == AppConfig.ActionExit)
                return LgTvClient.Button(ip, key, "EXIT");
            LgResult result = new LgResult();
            result.Error = "Accion desconocida.";
            return result;
        }

        private void FinishAction(int action, string ip, LgResult result)
        {
            Interlocked.Exchange(ref switchBusy, 0);
            string name = AppConfig.ActionNames[action];

            if (result == null)
            {
                ShowBalloon("No se pudo ejecutar " + name,
                    "Error desconocido.", ToolTipIcon.Error, 6000);
                return;
            }

            if (!string.IsNullOrEmpty(result.ClientKey) &&
                !string.Equals(result.ClientKey, config.ClientKey, StringComparison.Ordinal) &&
                string.Equals(ip, config.TvIp, StringComparison.OrdinalIgnoreCase))
            {
                config.ClientKey = result.ClientKey;
                try
                {
                    config.Save();
                }
                catch (Exception ex)
                {
                    Log.Write("No se pudo guardar la nueva clave: " + ex.Message);
                }
            }

            if (result.Ok)
            {
                if (action >= 0 && action <= 3)
                    Log.Write("HDMI " + (action + 1) + " activado.");
                else if (action == AppConfig.ActionPowerOff)
                    Log.Write("TV apagada.");
                else
                    Log.Write(name + ": OK.");
                return;
            }

            string error = string.IsNullOrWhiteSpace(result.Error)
                ? "No se pudo ejecutar la accion." : result.Error;
            Log.Write("Error " + name + ": " + error);
            ShowBalloon("No se pudo ejecutar " + name, error, ToolTipIcon.Error, 6000);
        }

        private void ShowTvSettings()
        {
            if (dialogOpen || disposed) return;
            dialogOpen = true;
            try
            {
                using (TvSettingsForm form = new TvSettingsForm(config))
                    form.ShowDialog();
            }
            catch (Exception ex)
            {
                Log.Write("Error en Configurar TV: " + ex);
                ShowBalloon("Error al configurar la TV", ex.Message, ToolTipIcon.Error, 5000);
            }
            finally
            {
                dialogOpen = false;
                RebuildMenu();
            }
        }

        private void ShowHotkeySettings()
        {
            if (dialogOpen || disposed) return;
            dialogOpen = true;
            bool applied = false;
            hotkeys.UnregisterAll();
            try
            {
                using (HotkeyForm form = new HotkeyForm(config.Hotkeys, ApplyHotkeys))
                {
                    form.ShowDialog();
                    applied = form.Applied;
                }
            }
            catch (Exception ex)
            {
                Log.Write("Error en Configurar atajos: " + ex);
                ShowBalloon("Error al configurar los atajos", ex.Message, ToolTipIcon.Error, 5000);
            }
            finally
            {
                if (!applied)
                {
                    string error = hotkeys.TryRegisterAll(config.Hotkeys);
                    if (error != null)
                    {
                        Log.Write(error);
                        ShowBalloon("Atajos desactivados", error, ToolTipIcon.Warning, 6000);
                    }
                }
                dialogOpen = false;
                RebuildMenu();
            }
        }

        private static string HotkeyListText(Hotkey[] hotkeys)
        {
            List<string> parts = new List<string>();
            if (hotkeys != null)
                foreach (Hotkey hk in hotkeys)
                    parts.Add(hk != null ? hk.ToString() : "?");
            return string.Join(", ", parts.ToArray());
        }

        private string ApplyHotkeys(Hotkey[] newHotkeys)
        {
            if (newHotkeys == null || newHotkeys.Length < AppConfig.ActionCount)
                return "Faltan atajos por configurar.";

            string error = hotkeys.TryRegisterAll(newHotkeys);
            if (error != null) return error;

            Hotkey[] previous = config.Hotkeys;
            Hotkey[] copy = new Hotkey[AppConfig.ActionCount];
            for (int i = 0; i < AppConfig.ActionCount; i++)
                copy[i] = newHotkeys[i].Clone();
            config.Hotkeys = copy;

            try
            {
                config.Save();
            }
            catch (Exception ex)
            {
                config.Hotkeys = previous;
                hotkeys.UnregisterAll();
                Log.Write("No se pudieron guardar los atajos: " + ex.Message);
                return "No se pudo guardar la configuracion: " + ex.Message;
            }

            Log.Write("Atajos guardados: " + HotkeyListText(copy));
            RebuildMenu();
            return null;
        }

        private void ShowBalloon(string title, string text, ToolTipIcon icon, int timeout)
        {
            if (disposed || tray == null) return;
            try
            {
                tray.BalloonTipTitle = title;
                tray.BalloonTipText = text;
                tray.BalloonTipIcon = icon;
                tray.ShowBalloonTip(timeout);
            }
            catch { }
        }

        private void ExitTray()
        {
            if (disposed) return;
            disposed = true;
            Log.Write("Salida.");
            try { hotkeys.Dispose(); } catch { }
            try { tray.Visible = false; tray.Dispose(); } catch { }
            try { window.Dispose(); } catch { }
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                if (firstRunTimer != null)
                {
                    firstRunTimer.Stop();
                    firstRunTimer.Dispose();
                    firstRunTimer = null;
                }
                try { hotkeys.Dispose(); } catch { }
                if (tray != null)
                {
                    try { tray.Visible = false; tray.Dispose(); } catch { }
                }
                if (window != null)
                {
                    try { window.Dispose(); } catch { }
                }
            }
            base.Dispose(disposing);
        }
    }
}
