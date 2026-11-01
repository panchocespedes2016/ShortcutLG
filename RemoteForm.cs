using System;
using System.Drawing;
using System.Windows.Forms;

namespace LgHdmiTray
{
    // Mando flotante: la misma botonera del menú, despegada en una
    // ventanita que se queda siempre visible. Cerrar la ventana con la X
    // solo cierra el mando; la app sigue en la bandeja.
    internal sealed class RemoteForm : Form
    {
        private readonly Action<int> onAction;
        private readonly Label statusLabel;

        public RemoteForm(Action<int> onAction)
        {
            this.onAction = onAction;
            Button[] buttons = new Button[AppConfig.ActionCount];

            Text = "ShortcutLG - Mando";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(296, 326);
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;

            for (int i = 0; i < 4; i++)
                buttons[i] = AddButton(AppConfig.ActionNames[i], i, 12 + i * 69, 12, 65, 38);

            buttons[AppConfig.ActionVolumeDown] = AddButton(
                AppConfig.ActionNames[AppConfig.ActionVolumeDown],
                AppConfig.ActionVolumeDown, 12, 58, 134, 38);
            buttons[AppConfig.ActionVolumeUp] = AddButton(
                AppConfig.ActionNames[AppConfig.ActionVolumeUp],
                AppConfig.ActionVolumeUp, 150, 58, 134, 38);

            buttons[AppConfig.ActionPowerOff] = AddButton(
                AppConfig.ActionNames[AppConfig.ActionPowerOff],
                AppConfig.ActionPowerOff, 12, 104, 134, 38);
            buttons[AppConfig.ActionHome] = AddButton(
                AppConfig.ActionNames[AppConfig.ActionHome],
                AppConfig.ActionHome, 150, 104, 134, 38);

            buttons[AppConfig.ActionUp] = AddButton(
                "Arriba", AppConfig.ActionUp, 81, 158, 65, 36);
            buttons[AppConfig.ActionLeft] = AddButton(
                "Izquierda", AppConfig.ActionLeft, 12, 198, 65, 36);
            buttons[AppConfig.ActionOk] = AddButton(
                "OK", AppConfig.ActionOk, 81, 198, 65, 36);
            buttons[AppConfig.ActionRight] = AddButton(
                "Derecha", AppConfig.ActionRight, 150, 198, 65, 36);
            buttons[AppConfig.ActionDown] = AddButton(
                "Abajo", AppConfig.ActionDown, 81, 238, 65, 36);

            buttons[AppConfig.ActionExit] = AddButton(
                AppConfig.ActionNames[AppConfig.ActionExit],
                AppConfig.ActionExit, 219, 158, 65, 116);

            statusLabel = new Label();
            statusLabel.Location = new Point(12, 282);
            statusLabel.Size = new Size(272, 32);
            statusLabel.AutoEllipsis = true;
            statusLabel.ForeColor = SystemColors.GrayText;
            statusLabel.Text = "Listo.";
            Controls.Add(statusLabel);
        }

        public void SetStatus(string text)
        {
            if (statusLabel == null || statusLabel.IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(delegate { SetStatus(text); })); } catch { }
                return;
            }
            statusLabel.Text = string.IsNullOrWhiteSpace(text) ? "Listo." : text;
        }

        private Button AddButton(string text, int action, int x, int y, int w, int h)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, h);
            b.Tag = action;
            b.Click += ButtonClick;
            Controls.Add(b);
            return b;
        }

        private void ButtonClick(object sender, EventArgs e)
        {
            Button b = sender as Button;
            if (b == null || onAction == null) return;
            int action = (int)b.Tag;

            // La confirmación de Apagar es un MessageBox sin dueño: si el
            // mando sigue TopMost, el diálogo puede quedar detrás. Se baja
            // momentáneamente y se restaura al volver de la acción (la
            // confirmación ocurre dentro, antes de lanzar la orden).
            bool wasTop = TopMost;
            TopMost = false;
            try
            {
                SetStatus(AppConfig.ActionNames[action] + "...");
                onAction(action);
            }
            finally
            {
                TopMost = wasTop;
            }
        }
    }
}
