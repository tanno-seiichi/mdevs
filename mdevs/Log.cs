// Log.cs
//
// mdevs（mdew の Visual Studio 版）の一部。
// 動作の記録を、Visual Studio の「出力」ウィンドウの「mdevs」に書く（不具合の調査用）。

using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace mdevs
{
    /// <summary>
    /// 動作の記録を「出力」ウィンドウ（表示→出力）の「mdevs」に書く。
    /// </summary>
    internal static class Log
    {
        /// <summary>「出力」ウィンドウの「mdevs」の GUID。</summary>
        private static readonly Guid s_paneGuid = new Guid("8b4c8589-e3ba-4bce-b3b9-e082097605c9");
        /// <summary>「出力」ウィンドウの「mdevs」（初めて書く時に作る）。</summary>
        private static IVsOutputWindowPane s_pane;

        /// <summary>1行書く（先頭に時刻を付ける）。UI スレッド以外から呼ばれた時は、UI スレッドで書く。</summary>
        /// <param name="a_message">書く内容。</param>
        public static void Write(string a_message)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff} {a_message}{Environment.NewLine}";
            System.Diagnostics.Debug.Write("[mdevs] " + line);
#pragma warning disable VSTHRD110, VSTHRD001
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                try
                {
                    if (null == s_pane)
                    {
                        var output = ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                        if (null == output)
                        {
                            return;
                        }
                        Guid guid = s_paneGuid;
                        output.CreatePane(ref guid, "mdevs", 1, 1);
                        output.GetPane(ref guid, out s_pane);
                    }
                    s_pane?.OutputStringThreadSafe(line);
                }
                catch (Exception)
                {
                    // 記録できなくても動作には影響しない
                }
            });
#pragma warning restore VSTHRD110, VSTHRD001
        }

        /// <summary>例外を書く。</summary>
        /// <param name="a_message">何をしていた時か。</param>
        /// <param name="a_exception">例外。</param>
        public static void Write(string a_message, Exception a_exception)
        {
            Write($"{a_message}: {a_exception}");
        }
    }
}
