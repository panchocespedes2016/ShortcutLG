using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LgHdmiTray
{
    internal sealed class TvSettingsForm : Form
    {
        private readonly AppConfig config;
        private TextBox ipBox;
        private Label statusLabel;
        private Button searchButton;
        private Button pairButton;
        private Button saveButton;
        private Button closeButton;
        private ProgressBar progress;
        private Label monitorLabel;
        private bool busy;

        public TvSettingsForm(AppConfig config)
        {
            this.config = config;
            InitializeUi();

            ipBox.Text = config.TvIp;
            if (string.IsNullOrWhiteSpace(config.TvIp))
                SetStatus("Escribe la IP de la TV o pulsa Buscar.", SystemColors.ControlText);
            else if (string.IsNullOrWhiteSpace(config.ClientKey))
                SetStatus("IP configurada. Pulsa Emparejar TV para autorizarla.", SystemColors.ControlText);
            else
                SetStatus("TV configurada. Puedes cambiar la IP o volver a emparejar.", SystemColors.ControlText);

            Shown += OnShown;
        }

        private void InitializeUi()
        {
            Text = "Configurar TV LG";
            ClientSize = new Size(500, 372);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ShowIcon = false;

            Label title = new Label();
            title.Text = "TV LG (webOS)";
            title.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(20, 16);
            Controls.Add(title);

            Label description = new Label();
            description.Text = "Introduce la IP de la TV o búscala en la red local.";
            description.AutoSize = true;
            description.Location = new Point(22, 52);
            description.ForeColor = SystemColors.GrayText;
            Controls.Add(description);

            Label ipLabel = new Label();
            ipLabel.Text = "IP:";
            ipLabel.AutoSize = true;
            ipLabel.Location = new Point(22, 86);
            Controls.Add(ipLabel);

            ipBox = new TextBox();
            ipBox.Location = new Point(55, 82);
            ipBox.Size = new Size(300, 25);
            ipBox.KeyDown += IpBoxKeyDown;
            Controls.Add(ipBox);

            searchButton = new Button();
            searchButton.Text = "Buscar TV";
            searchButton.Location = new Point(365, 80);
            searchButton.Size = new Size(112, 28);
            searchButton.Click += SearchButtonClick;
            Controls.Add(searchButton);

            progress = new ProgressBar();
            progress.Location = new Point(22, 118);
            progress.Size = new Size(455, 16);
            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 0;
            progress.Visible = false;
            Controls.Add(progress);

            statusLabel = new Label();
            statusLabel.Location = new Point(22, 140);
            statusLabel.Size = new Size(455, 60);
            statusLabel.AutoSize = false;
            Controls.Add(statusLabel);

            pairButton = new Button();
            pairButton.Text = "Emparejar TV";
            pairButton.Location = new Point(22, 210);
            pairButton.Size = new Size(140, 34);
            pairButton.Click += PairButtonClick;
            Controls.Add(pairButton);

            saveButton = new Button();
            saveButton.Text = "Guardar";
            saveButton.Location = new Point(172, 210);
            saveButton.Size = new Size(100, 34);
            saveButton.Click += SaveButtonClick;
            Controls.Add(saveButton);

            closeButton = new Button();
            closeButton.Text = "Cerrar";
            closeButton.Location = new Point(282, 210);
            closeButton.Size = new Size(100, 34);
            closeButton.Click += CloseButtonClick;
            Controls.Add(closeButton);

            Label hint = new Label();
            hint.Text = "La primera vez, acepta el aviso que aparece en la TV con el mando.";
            hint.AutoSize = true;
            hint.Location = new Point(22, 256);
            hint.ForeColor = SystemColors.GrayText;
            Controls.Add(hint);

            monitorLabel = new Label();
            monitorLabel.Location = new Point(22, 282);
            monitorLabel.Size = new Size(455, 76);
            monitorLabel.AutoSize = false;
            monitorLabel.ForeColor = SystemColors.GrayText;
            monitorLabel.Text = "Buscando monitor LG por HDMI...";
            Controls.Add(monitorLabel);
        }

        private void OnShown(object sender, EventArgs e)
        {
            LoadMonitorInfoAsync();
            if (string.IsNullOrWhiteSpace(ipBox.Text))
                DiscoverAsync();
            else
                ipBox.Focus();
        }

        private void LoadMonitorInfoAsync()
        {
            Task.Run(() => MonitorInfo.GetLgMonitorSummary()).ContinueWith(task =>
            {
                string summary = "";
                try
                {
                    summary = task.Result;
                }
                catch (Exception ex)
                {
                    Log.Write("No se pudo obtener el monitor por HDMI: " + ex.Message);
                }

                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (string.IsNullOrWhiteSpace(summary))
                        {
                            monitorLabel.ForeColor = SystemColors.GrayText;
                            monitorLabel.Text =
                                "Monitor por HDMI: no se detecto un LG. No es necesario para configurar la TV.";
                        }
                        else
                        {
                            monitorLabel.ForeColor = SuccessColor;
                            monitorLabel.Text =
                                "Monitor LG detectado por HDMI: " + summary + ". El HDMI identifica el " +
                                "monitor, pero la IP y el control se obtienen por la red.";
                        }
                    }));
                }
                catch { }
            });
        }

        private void IpBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                PairTv();
            }
        }

        private void SearchButtonClick(object sender, EventArgs e)
        {
            DiscoverAsync();
        }

        private void DiscoverAsync()
        {
            if (busy) return;
            SetBusy(true, "Buscando TVs LG en la red...");
            Task.Run(() => LgTvClient.Discover(3500)).ContinueWith(task =>
            {
                List<string> found = new List<string>();
                try
                {
                    if (task.Result != null) found = task.Result;
                }
                catch (Exception ex)
                {
                    Log.Write("Busqueda de TV fallida: " + ex.Message);
                }

                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        SetBusy(false, "");
                        if (found.Count == 1)
                        {
                            ipBox.Text = found[0];
                            SetStatus("TV encontrada: " + found[0] + ". Pulsa Emparejar TV.", SuccessColor);
                        }
                        else if (found.Count > 1)
                        {
                            ipBox.Text = found[0];
                            SetStatus("Varias TVs encontradas: " + string.Join(", ", found.ToArray()) +
                                      ". Escribe la IP de la que quieras usar.", WarningColor);
                        }
                        else
                        {
                            SetStatus("No se encontro ninguna TV automaticamente. Escribe la IP manualmente.", WarningColor);
                        }
                    }));
                }
                catch { }
            });
        }

        private void PairButtonClick(object sender, EventArgs e)
        {
            PairTv();
        }

        private void PairTv()
        {
            if (busy) return;
            string ip = ipBox.Text.Trim();
            if (ip.Length == 0)
            {
                SetStatus("Escribe la IP de la TV.", ErrorColor);
                ipBox.Focus();
                return;
            }

            if (!string.IsNullOrWhiteSpace(config.ClientKey))
            {
                DialogResult answer = MessageBox.Show(this,
                    "Se pedira permiso de nuevo en la TV y se reemplazara la autorizacion actual.\n\n¿Continuar?",
                    "Emparejar TV", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;
            }

            SetBusy(true, "Conectando con " + ip + "...\nAcepta el aviso que aparezca en la TV con el mando.");
            Task.Run(() => LgTvClient.Pair(ip, 60)).ContinueWith(task =>
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
                }

                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        SetBusy(false, "");
                        if (result.Ok)
                        {
                            config.TvIp = ip;
                            config.ClientKey = result.ClientKey;
                            try
                            {
                                config.Save();
                            }
                            catch (Exception ex)
                            {
                                SetStatus("Emparejada, pero no se pudo guardar: " + ex.Message, ErrorColor);
                                return;
                            }
                            ipBox.Text = ip;
                            SetStatus("TV emparejada correctamente. Ya puedes usar los atajos.", SuccessColor);
                        }
                        else
                        {
                            SetStatus(string.IsNullOrWhiteSpace(result.Error)
                                ? "No se pudo emparejar la TV." : result.Error, ErrorColor);
                        }
                    }));
                }
                catch { }
            });
        }

        private void SaveButtonClick(object sender, EventArgs e)
        {
            if (busy) return;
            string ip = ipBox.Text.Trim();
            if (ip.Length == 0)
            {
                SetStatus("Escribe la IP de la TV.", ErrorColor);
                ipBox.Focus();
                return;
            }

            bool ipChanged = !string.Equals(ip, config.TvIp, StringComparison.OrdinalIgnoreCase);
            if (ipChanged && !string.IsNullOrWhiteSpace(config.ClientKey))
            {
                DialogResult answer = MessageBox.Show(this,
                    "La IP ha cambiado. Se borrara la autorizacion actual y tendras que emparejar la TV de nuevo.\n\n¿Guardar?",
                    "Cambiar IP", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;
                config.ClientKey = "";
            }

            config.TvIp = ip;
            try
            {
                config.Save();
            }
            catch (Exception ex)
            {
                SetStatus("No se pudo guardar la configuracion: " + ex.Message, ErrorColor);
                return;
            }

            if (string.IsNullOrWhiteSpace(config.ClientKey))
                SetStatus("IP guardada. Pulsa Emparejar TV para autorizar esta TV.", WarningColor);
            else
                SetStatus("Configuracion guardada.", SuccessColor);
        }

        private void CloseButtonClick(object sender, EventArgs e)
        {
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (busy)
            {
                e.Cancel = true;
                SetStatus("Espera a que termine la operacion...", WarningColor);
                return;
            }
            base.OnFormClosing(e);
        }

        private void SetBusy(bool value, string message)
        {
            busy = value;
            ipBox.Enabled = !value;
            searchButton.Enabled = !value;
            pairButton.Enabled = !value;
            saveButton.Enabled = !value;
            closeButton.Enabled = !value;
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

        private static Color SuccessColor
        {
            get { return Color.FromArgb(0, 120, 0); }
        }

        private static Color ErrorColor
        {
            get { return Color.FromArgb(180, 30, 30); }
        }

        private static Color WarningColor
        {
            get { return Color.FromArgb(150, 100, 0); }
        }
    }
}
