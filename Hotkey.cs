using System;
using System.Text;
using System.Windows.Forms;

namespace LgHdmiTray
{
    internal static class NativeMethods
    {
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;
        public const int WM_HOTKEY = 0x0312;
        public const int VK_LWIN = 0x5B;
        public const int VK_RWIN = 0x5C;

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
    }

    internal sealed class Hotkey
    {
        public uint Modifiers;
        public Keys Key;

        public Hotkey(uint modifiers, Keys key)
        {
            Modifiers = modifiers & (NativeMethods.MOD_ALT | NativeMethods.MOD_CONTROL |
                                     NativeMethods.MOD_SHIFT | NativeMethods.MOD_WIN);
            Key = key;
        }

        public bool IsValid
        {
            get
            {
                return Key != Keys.None &&
                       (Modifiers & (NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_WIN)) != 0;
            }
        }

        public uint RegisterModifiers
        {
            get { return Modifiers | NativeMethods.MOD_NOREPEAT; }
        }

        public Hotkey Clone()
        {
            return new Hotkey(Modifiers, Key);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if ((Modifiers & NativeMethods.MOD_WIN) != 0) AddPart(sb, "Win");
            if ((Modifiers & NativeMethods.MOD_CONTROL) != 0) AddPart(sb, "Ctrl");
            if ((Modifiers & NativeMethods.MOD_ALT) != 0) AddPart(sb, "Alt");
            if ((Modifiers & NativeMethods.MOD_SHIFT) != 0) AddPart(sb, "Shift");
            AddPart(sb, KeyName(Key));
            return sb.ToString();
        }

        private static void AddPart(StringBuilder sb, string text)
        {
            if (sb.Length > 0) sb.Append("+");
            sb.Append(text);
        }

        public static string KeyName(Keys key)
        {
            int k = (int)key;
            if (k >= (int)Keys.D0 && k <= (int)Keys.D9)
                return ((char)('0' + (k - (int)Keys.D0))).ToString();
            if (k >= (int)Keys.A && k <= (int)Keys.Z)
                return ((char)('A' + (k - (int)Keys.A))).ToString();
            if (k >= (int)Keys.F1 && k <= (int)Keys.F24)
                return "F" + (k - (int)Keys.F1 + 1).ToString();
            if (key == Keys.Return) return "Enter";
            if (key == Keys.Escape) return "Esc";
            if (key == Keys.Back) return "Backspace";
            return key.ToString();
        }

        public static bool TryParse(string text, out Hotkey hotkey)
        {
            hotkey = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string[] parts = text.Split('+');
            uint mods = 0;
            string keyPart = null;
            foreach (string raw in parts)
            {
                string p = raw.Trim();
                if (p.Length == 0) return false;
                if (p.Equals("win", StringComparison.OrdinalIgnoreCase) || p.Equals("windows", StringComparison.OrdinalIgnoreCase))
                    mods |= NativeMethods.MOD_WIN;
                else if (p.Equals("ctrl", StringComparison.OrdinalIgnoreCase) || p.Equals("control", StringComparison.OrdinalIgnoreCase))
                    mods |= NativeMethods.MOD_CONTROL;
                else if (p.Equals("alt", StringComparison.OrdinalIgnoreCase))
                    mods |= NativeMethods.MOD_ALT;
                else if (p.Equals("shift", StringComparison.OrdinalIgnoreCase))
                    mods |= NativeMethods.MOD_SHIFT;
                else if (keyPart == null)
                    keyPart = p;
                else
                    return false;
            }
            if (keyPart == null) return false;

            Keys key;
            if (keyPart.Length == 1 && keyPart[0] >= '0' && keyPart[0] <= '9')
                key = (Keys)((int)Keys.D0 + (keyPart[0] - '0'));
            else if (keyPart.Length == 1 && char.IsLetter(keyPart[0]))
            {
                if (!Enum.TryParse<Keys>(keyPart.ToUpperInvariant(), true, out key)) return false;
            }
            else if (keyPart.Length >= 2 && (keyPart[0] == 'F' || keyPart[0] == 'f'))
            {
                int n;
                if (!int.TryParse(keyPart.Substring(1), out n) || n < 1 || n > 24) return false;
                key = (Keys)((int)Keys.F1 + n - 1);
            }
            else if (!Enum.TryParse<Keys>(keyPart, true, out key))
                return false;

            hotkey = new Hotkey(mods, key);
            return true;
        }

        public static bool IsWinKeyDown()
        {
            return (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 ||
                   (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;
        }
    }
}
