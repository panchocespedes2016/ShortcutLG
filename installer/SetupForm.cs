using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShortcutLGSetup
{
    internal sealed class SetupForm : Form
    {
        private readonly bool uninstallMode;
        private CheckBox desktopCheck;
        private CheckBox startupCheck;
        private CheckBox runCheck;
        private CheckBox configCheck;
        private Button actionButton;
        private Button cancelButton;
        private Button uninstallButton;
        private Label statusLabel;
        private Label pathLabel;
        private Label descriptionLabel;
        private Label titleLabel;
        private Label subtitleLabel;
        private ProgressBar progress;
        private bool busy;
        private bool actionStarted;
        private bool uninstallRequested;

        private static readonly Color HeaderBack = Color.FromArgb(11, 23, 40);
        private static readonly Color HeaderText = Color.FromArgb(160, 205, 235);
        private static readonly Color ErrorColor = Color.FromArgb(180, 30, 30);
        private static readonly Color SuccessColor = Color.FromArgb(0, 120, 0);
        private static readonly Color WarningColor = Color.FromArgb(150, 100, 0);

        public SetupForm(bool uninstallMode)
        {
            this.uninstallMode = uninstallMode;
            InitializeUi();
            ConfigureMode();
            Shown += delegate { actionButton.Focus(); };
        }

        private void InitializeUi()
        {
            Text = "ShortcutLG";
            ClientSize = new Size(600, 430);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;

            Icon setupIcon = null;
            try
            {
                setupIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
            if (setupIcon != null)
            {
                Icon = setupIcon;
                ShowIcon = true;
            }

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 92;
            header.BackColor = HeaderBack;
            Controls.Add(header);

            PictureBox picture = new PictureBox();
            picture.Location = new Point(20, 16);
            picture.Size = new Size(60, 60);
            picture.SizeMode = PictureBoxSizeMode.Zoom;
            if (setupIcon != null) picture.Image = setupIcon.ToBitmap();
            header.Controls.Add(picture);

            titleLabel = new Label();
            titleLabel.AutoSize = true;
            titleLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            titleLabel.ForeColor = Color.White;
            titleLabel.Location = new Point(94, 14);
            header.Controls.Add(titleLabel);

            subtitleLabel = new Label();
            subtitleLabel.AutoSize = true;
            subtitleLabel.Font = new Font("Segoe UI", 9.5F);
            subtitleLabel.ForeColor = HeaderText;
            subtitleLabel.Location = new Point(97, 51);
            header.Controls.Add(subtitleLabel);

            descriptionLabel = new Label();
            descriptionLabel.Location = new Point(22, 108);
            descriptionLabel.Size = new Size(556, 44);
            descriptionLabel.AutoSize = false;
            Controls.Add(descriptionLabel);

            pathLabel = new Label();
            pathLabel.Location = new Point(22, 156);
            pathLabel.Size = new Size(556, 34);
            pathLabel.AutoEllipsis = true;
            pathLabel.ForeColor = SystemColors.GrayText;
            Controls.Add(pathLabel);

            desktopCheck = new CheckBox();
            desktopCheck.Text = "Crear acceso directo en el escritorio";
            desktopCheck.Location = new Point(24, 198);
            desktopCheck.AutoSize = true;
            Controls.Add(desktopCheck);

            startupCheck = new CheckBox();
            startupCheck.Text = "Iniciar ShortcutLG con Windows";
            startupCheck.Location = new Point(24, 226);
            startupCheck.AutoSize = true;
            Controls.Add(startupCheck);

            runCheck = new CheckBox();
            runCheck.Text = "Abrir ShortcutLG al terminar";
            runCheck.Location = new Point(24, 254);
            runCheck.AutoSize = true;
            runCheck.Checked = true;
            Controls.Add(runCheck);

            configCheck = new CheckBox();
            configCheck.Text = "Eliminar también la configuración y la autorización de la TV";
            configCheck.Location = new Point(24, 198);
            configCheck.AutoSize = true;
            configCheck.Visible = false;
            Controls.Add(configCheck);

            statusLabel = new Label();
            statusLabel.Location = new Point(22, 302);
            statusLabel.Size = new Size(556, 44);
            statusLabel.AutoSize = false;
            Controls.Add(statusLabel);

            progress = new ProgressBar();
            progress.Location = new Point(22, 350);
            progress.Size = new Size(556, 14);
            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 0;
            progress.Visible = false;
            Controls.Add(progress);

            actionButton = new Button();
            actionButton.Location = new Point(344, 380);
            actionButton.Size = new Size(120, 34);
            actionButton.Click += ActionButtonClick;
            Controls.Add(actionButton);

            cancelButton = new Button();
            cancelButton.Text = "Cancelar";
            cancelButton.Location = new Point(474, 380);
            cancelButton.Size = new Size(104, 34);
            cancelButton.Click += delegate { Close(); };
            Controls.Add(cancelButton);

            uninstallButton = new Button();
            uninstallButton.Text = "Desinstalar";
            uninstallButton.Location = new Point(22, 380);
            uninstallButton.Size = new Size(120, 34);
            uninstallButton.Click += UninstallButtonClick;
            uninstallButton.Visible = false;
            Controls.Add(uninstallButton);

            AcceptButton = actionButton;
            CancelButton = cancelButton;
        }

        private static string GetInstalledVersion()
        {
            try
            {
                if (File.Exists(AppPaths.AppPath))
                {
                    FileVersionInfo info = FileVersionInfo.GetVersionInfo(AppPaths.AppPath);
                    if (!string.IsNullOrWhiteSpace(info.FileVersion))
                        return info.FileVersion;
                }
            }
            catch { }
            try
            {
                using (Microsoft.Win32.RegistryKey key =
                    Microsoft.Win32.Registry.CurrentUser.OpenSubKey(AppPaths.UninstallKeyPath, false))
                {
                    if (key != null)
                    {
                        string version = key.GetValue("DisplayVersion") as string;
                        if (!string.IsNullOrWhiteSpace(version)) return version;
                    }
                }
            }
            catch { }
            return null;
        }

        private void ConfigureMode()
        {
            string installedVersion = GetInstalledVersion();
            if (uninstallMode)
            {
                Text = "Desinstalar ShortcutLG " + AppPaths.Version;
                titleLabel.Text = "Desinstalar ShortcutLG";
                subtitleLabel.Text = installedVersion != null
                    ? "Versión instalada: " + installedVersion
                    : "Desinstalador · Versión " + AppPaths.Version;
                descriptionLabel.Text =
                    "Se quitarán el programa, los accesos directos y el inicio automático." +
                    " La configuración de la TV se conserva salvo que marques la casilla.";
                pathLabel.Text = "Instalación: " + AppPaths.GetUninstallDir();
                desktopCheck.Visible = false;
                startupCheck.Visible = false;
                runCheck.Visible = false;
                configCheck.Visible = true;
                actionButton.Text = "Desinstalar";
                SetStatus("Pulsa Desinstalar para confirmar.", SystemColors.GrayText);
            }
            else
            {
                bool installed = File.Exists(AppPaths.AppPath);
                Text = installed
                    ? "Actualizar ShortcutLG " + AppPaths.Version
                    : "Instalar ShortcutLG " + AppPaths.Version;
                titleLabel.Text = "ShortcutLG " + AppPaths.Version;
                subtitleLabel.Text = installed
                    ? (installedVersion != null
                        ? "Actualización · Instalada: " + installedVersion
                        : "Actualizar instalación")
                    : "Instalador · Versión " + AppPaths.Version;
                descriptionLabel.Text = installed
                    ? "Se actualizará la instalación existente. La configuración de la TV se conserva."
                    : "Instala ShortcutLG en tu usuario. No requiere permisos de administrador.";
                pathLabel.Text = "Destino: " + AppPaths.InstallDir;
                desktopCheck.Checked = !installed || File.Exists(AppPaths.DesktopShortcut);
                startupCheck.Checked = !installed || InstallOperations.IsStartupEnabled();
                actionButton.Text = installed ? "Actualizar" : "Instalar";
                uninstallButton.Visible = installed;
                SetStatus("Pulsa " + actionButton.Text + " para comenzar.", SystemColors.GrayText);
            }
        }

        private void ActionButtonClick(object sender, EventArgs e)
        {
            if (busy || actionStarted) return;
            actionStarted = true;

            if (uninstallMode)
            {
                DialogResult answer = MessageBox.Show(this,
                    "¿Desinstalar ShortcutLG de este equipo?",
                    "Desinstalar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes)
                {
                    actionStarted = false;
                    return;
                }
                RunUninstall(configCheck.Checked);
            }
            else
            {
                if (!EnsureAppStopped())
                {
                    actionStarted = false;
                    return;
                }

                bool desktop = desktopCheck.Checked;
                bool startup = startupCheck.Checked;
                bool run = runCheck.Checked;
                SetBusy(true, "Instalando...");
                Task.Run(() => InstallOperations.Install(desktop, startup))
                    .ContinueWith(delegate(Task<string> task) { Finish(task, run); });
            }
        }

        private void UninstallButtonClick(object sender, EventArgs e)
        {
            if (busy || actionStarted) return;

            // Una sola pregunta: Sí desinstala y borra también la
            // configuración y la autorización de la TV; No desinstala
            // conservando la configuración; Cancelar no hace nada.
            DialogResult answer = MessageBox.Show(this,
                "¿Desinstalar ShortcutLG de este equipo?\n\n" +
                "Sí = desinstalar y eliminar también la configuración y la autorización de la TV.\n" +
                "No = desinstalar conservando la configuración.\n" +
                "Cancelar = no hacer nada.",
                "Desinstalar", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (answer == DialogResult.Cancel) return;

            actionStarted = true;
            uninstallRequested = true;
            RunUninstall(answer == DialogResult.Yes);
        }

        private void RunUninstall(bool deleteConfig)
        {
            if (!EnsureAppStopped())
            {
                actionStarted = false;
                uninstallRequested = false;
                return;
            }

            SetBusy(true, "Desinstalando...");
            Task.Run(() => InstallOperations.Uninstall(deleteConfig))
                .ContinueWith(delegate(Task<string> task) { Finish(task, false); });
        }

        private bool EnsureAppStopped()
        {
            if (!InstallOperations.IsAppRunning()) return true;

            DialogResult answer = MessageBox.Show(this,
                "ShortcutLG está en ejecución. Se cerrará para continuar.\n\n¿Continuar?",
                "ShortcutLG", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return false;

            string error = InstallOperations.StopApp();
            if (!string.IsNullOrEmpty(error))
            {
                MessageBox.Show(this, "No se pudo cerrar ShortcutLG:\n" + error,
                    "ShortcutLG", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        private void Finish(Task<string> task, bool launch)
        {
            string error = null;
            try
            {
                error = task.Result;
            }
            catch (AggregateException ex)
            {
                error = ex.GetBaseException().Message;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            try
            {
                BeginInvoke(new Action(delegate
                {
                    SetBusy(false, "");

                    if (uninstallMode || uninstallRequested)
                    {
                        if (!string.IsNullOrEmpty(error))
                        {
                            SetStatus("Desinstalación con avisos.", WarningColor);
                            MessageBox.Show(this,
                                "ShortcutLG se ha desinstalado, pero hubo un aviso:\n\n" + error +
                                "\n\nSi queda alguna carpeta, puedes borrarla manualmente.",
                                "Desinstalación completada", MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                        else
                        {
                            SetStatus("Desinstalado correctamente.", SuccessColor);
                            MessageBox.Show(this, "ShortcutLG se ha desinstalado correctamente.",
                                "Desinstalación completada", MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }

                        try
                        {
                            InstallOperations.ScheduleDeleteInstallDir();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this,
                                "No se pudo programar el borrado de la carpeta de instalación:\n\n" +
                                ex.Message + "\n\nPuedes borrarla manualmente.",
                                "Desinstalación completada", MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                        Close();
                        return;
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        SetStatus("Error: " + error, ErrorColor);
                        MessageBox.Show(this, "No se pudo instalar ShortcutLG:\n\n" + error,
                            "Error de instalación", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        actionStarted = false;
                        return;
                    }

                    SetStatus("Instalado correctamente.", SuccessColor);
                    string access = desktopCheck.Checked
                        ? "Puedes abrirlo desde el menú Inicio o desde el acceso directo del escritorio."
                        : "Puedes abrirlo desde el menú Inicio.";
                    MessageBox.Show(this,
                        "ShortcutLG se ha instalado correctamente.\n\n" + access,
                        "Instalación completada", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    if (launch)
                    {
                        try
                        {
                            Process.Start(AppPaths.AppPath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, "No se pudo abrir ShortcutLG:\n" + ex.Message,
                                "ShortcutLG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    Close();
                }));
            }
            catch { }
        }

        private void SetBusy(bool value, string message)
        {
            busy = value;
            desktopCheck.Enabled = !value;
            startupCheck.Enabled = !value;
            runCheck.Enabled = !value;
            configCheck.Enabled = !value;
            actionButton.Enabled = !value;
            cancelButton.Enabled = !value;
            if (uninstallButton != null) uninstallButton.Enabled = !value;
            progress.Visible = value;
            progress.MarqueeAnimationSpeed = value ? 30 : 0;
            UseWaitCursor = value;
            if (!string.IsNullOrEmpty(message))
                SetStatus(message, SystemColors.ControlText);
        }

        private void SetStatus(string text, Color color)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = color;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (busy)
            {
                e.Cancel = true;
                SetStatus("Espera a que termine la operación...", WarningColor);
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
