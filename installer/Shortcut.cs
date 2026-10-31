using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ShortcutLGSetup
{
    internal static class Shortcut
    {
        public static void Create(string shortcutPath, string targetPath,
            string workingDirectory, string description)
        {
            string folder = Path.GetDirectoryName(shortcutPath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            if (File.Exists(shortcutPath)) File.Delete(shortcutPath);

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new Exception("Windows no tiene disponible WScript.Shell.");

            object shell = null;
            object link = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod,
                    null, shell, new object[] { shortcutPath });
                Type linkType = link.GetType();
                linkType.InvokeMember("TargetPath", BindingFlags.SetProperty,
                    null, link, new object[] { targetPath });
                linkType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty,
                    null, link, new object[] { workingDirectory });
                linkType.InvokeMember("Description", BindingFlags.SetProperty,
                    null, link, new object[] { description });
                linkType.InvokeMember("IconLocation", BindingFlags.SetProperty,
                    null, link, new object[] { targetPath });
                linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            }
            finally
            {
                if (link != null && Marshal.IsComObject(link)) Marshal.ReleaseComObject(link);
                if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
            }
        }

        public static void Delete(string shortcutPath)
        {
            try
            {
                if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
            }
            catch { }
        }
    }
}
