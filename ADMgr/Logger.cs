using System;
using System.IO;
using System.Text;
using System.Threading;

namespace ADMgr
{
    public static class Logger
    {
        private static readonly object sync = new object();
        private static string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ADMgr.log");

        public static void Log(string message)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [T" + Thread.CurrentThread.ManagedThreadId + "] " + message + Environment.NewLine;
                lock (sync)
                {
                    File.AppendAllText(logPath, line, Encoding.UTF8);
                }
            }
            catch { }
        }

        public static void LogException(Exception ex, string context = null)
        {
            try
            {
                string header = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [T" + Thread.CurrentThread.ManagedThreadId + "] EXCEPTION" + (context != null ? " (" + context + ")" : "");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(header);
                sb.AppendLine(ex.ToString());
                sb.AppendLine();
                lock (sync)
                {
                    File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch { }
        }
    }
}
