// UiThread.cs
//
// mdevs（mdew の Visual Studio 版）の一部。
// UI スレッドで、今の処理が終わった後に実行する（後回しにする）処理。

using System;
using System.Windows.Threading;

namespace mdevs
{
    /// <summary>
    /// UI スレッドで、今の処理が終わった後に実行する。WebView2 のイベントの中で処理しない（WebView2 が再入に対応していない）、
    /// Visual Studio がファイルを開き終わってから処理する等、優先度を指定して後回しにしたい時に使う。
    /// </summary>
    internal static class UiThread
    {
        /// <summary>UI スレッドで、指定の優先度で後回しにして実行する（終わるのは待たない）。</summary>
        /// <param name="a_dispatcher">UI スレッドの Dispatcher。</param>
        /// <param name="a_action">実行する処理。</param>
        /// <param name="a_priority">優先度（Background：Visual Studio の処理が一段落してから）。</param>
        public static void Post(Dispatcher a_dispatcher, Action a_action, DispatcherPriority a_priority = DispatcherPriority.Normal)
        {
            // 優先度を指定するため、JoinableTaskFactory ではなく Dispatcher を直接使う。結果（終わるの）は待たない
#pragma warning disable VSTHRD001, VSTHRD110
            a_dispatcher.BeginInvoke(a_action, a_priority);
#pragma warning restore VSTHRD001, VSTHRD110
        }
    }
}
