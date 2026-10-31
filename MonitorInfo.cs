using System;
using System.Collections.Generic;
using System.Management;
using System.Text;

namespace LgHdmiTray
{
    internal static class MonitorInfo
    {
        public static string GetLgMonitorSummary()
        {
            List<string> entries = new List<string>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "root\\wmi",
                    "SELECT ManufacturerName, UserFriendlyName, YearOfManufacture FROM WmiMonitorID"))
                {
                    foreach (ManagementBaseObject item in searcher.Get())
                    {
                        try
                        {
                            string manufacturer = Decode(item["ManufacturerName"]);
                            string name = Decode(item["UserFriendlyName"]);
                            object year = item["YearOfManufacture"];

                            bool isLg =
                                manufacturer.IndexOf("LG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("LG", StringComparison.OrdinalIgnoreCase) >= 0;
                            if (!isLg) continue;

                            string text = (manufacturer + " " + name).Trim();
                            if (year != null) text += " (" + year + ")";
                            if (!entries.Contains(text)) entries.Add(text);
                        }
                        finally
                        {
                            item.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("No se pudo leer la informacion EDID del monitor: " + ex.Message);
            }

            if (entries.Count == 0) return "";
            return string.Join("; ", entries.ToArray());
        }

        private static string Decode(object value)
        {
            Array array = value as Array;
            if (array == null) return "";
            StringBuilder builder = new StringBuilder();
            foreach (object item in array)
            {
                int code = Convert.ToInt32(item);
                if (code == 0) break;
                builder.Append((char)code);
            }
            return builder.ToString().Trim();
        }
    }
}
