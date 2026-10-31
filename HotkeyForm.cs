using System;
using System.Drawing;
using System.Windows.Forms;

namespace LgHdmiTray
{
    internal sealed class HotkeyForm : Form
    {
        private readonly Func<Hotkey[], string> apply;
        private readonly Hotkey[] working;
        private readonly TextBox[] boxes;
        private Label hintLabel;

        public bool Applied { get; private set; }

        public HotkeyForm(Hotkey[] current, Func<Hotkey[], string> apply)
        {
            this.apply = apply;
            working = new Hotkey[4];
            boxes = new TextBox[4];
            Hotkey[] defaults = AppConfig.DefaultHotkeys();
            for (int i = 0; i < 4; i++)
            {
                Hotkey source = current != null && i < current.Length && current[i] != null
                    ? current[i] : defaults[i];
                working[i] = source.Clone();
            }
            InitializeUi();
            for (int i = 0; i < 4; i++)
                boxes[i].Text = working[i].ToString();
        }

        private void InitializeUi()
        {
            Text = "Cambiar combinaciones de teclas";
            ClientSize = new Size(470, 320);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ShowIcon = false;

            Label title = new Label();
            title.Text = "Cambiar combinaciones de teclas";
            title.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(20, 16);
            Controls.Add(title);

            Label description = new Label();
            description.Text = "Haz clic en una casilla y pulsa la combinación que quieras usar.";
            description.AutoSize = true;
            description.Location = new Point(22, 52);
            description.ForeColor = SystemColors.GrayText;
            Controls.Add(description);

            for (int i = 0; i < 4; i++)
            {
                Label label = new Label();
                label.Text = "HDMI " + (i + 1);
                label.AutoSize = true;
                label.Location = new Point(24, 92 + i * 36);
                Controls.Add(label);

                TextBox box = new TextBox();
                box.Location = new Point(110, 88 + i * 36);
                box.Size = new Size(320, 25);
                box.ReadOnly = true;
                box.TextAlign = HorizontalAlignment.Center;
                box.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                box.KeyDown += HotkeyBoxKeyDown;
                box.Enter += HotkeyBoxEnter;
                Controls.Add(box);
                boxes[i] = box;
            }

            hintLabel = new Label();
            hintLabel.Location = new Point(24, 232);
            hintLabel.Size = new Size(420, 34);
            hintLabel.ForeColor = SystemColors.GrayText;
            hintLabel.Text = "Debe incluir Ctrl, Alt o Win. Win+Alt+1..4 esta reservado por Windows.";
            Controls.Add(hintLabel);

            Button resetButton = new Button();
            resetButton.Text = "Restablecer";
            resetButton.Location = new Point(22, 274);
            resetButton.Size = new Size(110, 32);
            resetButton.Click += ResetButtonClick;
            Controls.Add(resetButton);

            Button saveButton = new Button();
            saveButton.Text = "Guardar";
            saveButton.Location = new Point(260, 274);
            saveButton.Size = new Size(90, 32);
            saveButton.Click += SaveButtonClick;
            Controls.Add(saveButton);

            Button cancelButton = new Button();
            cancelButton.Text = "Cancelar";
            cancelButton.Location = new Point(360, 274);
            cancelButton.Size = new Size(90, 32);
            cancelButton.Click += CancelButtonClick;
            Controls.Add(cancelButton);
        }

        private void HotkeyBoxEnter(object sender, EventArgs e)
        {
            TextBox box = sender as TextBox;
            if (box != null) box.SelectAll();
        }

        private void HotkeyBoxKeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;

            TextBox box = sender as TextBox;
            if (box == null) return;
            int index = Array.IndexOf(boxes, box);
            if (index < 0) return;

            Keys key = e.KeyCode;
            if (key == Keys.None || key == Keys.ControlKey || key == Keys.ShiftKey ||
                key == Keys.Menu || key == Keys.LWin || key == Keys.RWin)
                return;

            uint modifiers = 0;
            if (e.Control) modifiers |= NativeMethods.MOD_CONTROL;
            if (e.Alt) modifiers |= NativeMethods.MOD_ALT;
            if (e.Shift) modifiers |= NativeMethods.MOD_SHIFT;
            if (Hotkey.IsWinKeyDown()) modifiers |= NativeMethods.MOD_WIN;

            if ((modifiers & (NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_WIN)) == 0)
            {
                SetHint("Anade Ctrl, Alt o Win a la combinacion.", Color.FromArgb(180, 30, 30));
                return;
            }

            working[index] = new Hotkey(modifiers, key);
            box.Text = working[index].ToString();
            SetHint("Atajo asignado. Pulsa Guardar para aplicarlo.", SystemColors.ControlText);
        }

        private void ResetButtonClick(object sender, EventArgs e)
        {
            Hotkey[] defaults = AppConfig.DefaultHotkeys();
            for (int i = 0; i < 4; i++)
            {
                working[i] = defaults[i].Clone();
                boxes[i].Text = working[i].ToString();
            }
            SetHint("Atajos restablecidos. Pulsa Guardar para aplicarlos.", SystemColors.ControlText);
        }

        private void SaveButtonClick(object sender, EventArgs e)
        {
            Hotkey[] result = new Hotkey[4];
            for (int i = 0; i < 4; i++)
            {
                if (working[i] == null || !working[i].IsValid)
                {
                    MessageBox.Show(this, "El atajo de HDMI " + (i + 1) + " no es valido.",
                        "Atajos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    boxes[i].Focus();
                    return;
                }
                result[i] = working[i].Clone();
            }

            for (int i = 0; i < 4; i++)
            {
                for (int j = i + 1; j < 4; j++)
                {
                    if (result[i].Modifiers == result[j].Modifiers && result[i].Key == result[j].Key)
                    {
                        MessageBox.Show(this,
                            "HDMI " + (i + 1) + " y HDMI " + (j + 1) + " tienen el mismo atajo.",
                            "Atajos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }

            string error = apply(result);
            if (!string.IsNullOrEmpty(error))
            {
                MessageBox.Show(this, error, "No se pudo aplicar el atajo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Applied = true;
            Close();
        }

        private void CancelButtonClick(object sender, EventArgs e)
        {
            Close();
        }

        private void SetHint(string text, Color color)
        {
            hintLabel.Text = text;
            hintLabel.ForeColor = color;
        }
    }
}
