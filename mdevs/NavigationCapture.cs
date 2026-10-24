// NavigationCapture.cs
//
// mdevs（mdew の Visual Studio 版）の一部。
// Visual Studio の「検索結果」をダブルクリックして .md を開いた時に、ダブルクリックした箇所（ファイル・行・列）と、
// 検索した文字列を読み取り、mdevs の編集ペインでその箇所を表示する。
// Visual Studio は、検索結果の箇所をテキストエディターにしか伝えないため（mdevs には行・列が届かない）、ファイルを開く
// 直前（MarkdownEditorFactory.MapLogicalView）に、キーボードの入力先（ダブルクリックした検索結果の行）から読み取る。

using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace mdevs
{
    /// <summary>表示する箇所（「検索結果」でダブルクリックした箇所）と、その時の検索の条件。</summary>
    internal sealed class NavigationRequest
    {
        /// <summary>ファイルのフルパス（検索結果の行から読み取れない時は null。その時は、開いたファイルに対して表示する）。</summary>
        public string Path { get; set; }
        /// <summary>行（0 から数える）。</summary>
        public int Line { get; set; }
        /// <summary>列（0 から数える）。</summary>
        public int Column { get; set; }
        /// <summary>検索した文字列（分からなければ null）。</summary>
        public string Query { get; set; }
        /// <summary>大文字・小文字を区別したか。</summary>
        public bool MatchCase { get; set; }
        /// <summary>単語単位で探したか。</summary>
        public bool WholeWord { get; set; }
        /// <summary>正規表現を使ったか。</summary>
        public bool Regex { get; set; }
        /// <summary>読み取った時刻（古いものは使わない）。</summary>
        public DateTime Time { get; set; }
    }

    /// <summary>
    /// 「検索結果」でダブルクリックした箇所を読み取り、mdevs の編集ペインで表示する。
    /// </summary>
    internal static class NavigationCapture
    {
        /// <summary>まだ表示していない箇所（編集ペインができたら表示する）。</summary>
        private static NavigationRequest s_pending;
        /// <summary>最後に読み取った箇所（1回のダブルクリックで何回も呼ばれるため、同じ箇所を2回表示しないように使う）。</summary>
        private static NavigationRequest s_last;

        /// <summary>
        /// 「検索結果」等からファイルを開く直前に呼ばれる。キーボードの入力先（ダブルクリックした検索結果の行）から、
        /// ファイル・行・列を読み取って覚えておき、今の処理を抜けた後（ファイルが開いた後）に、その箇所を表示する。
        /// </summary>
        public static void CaptureFromFindResults()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                NavigationRequest request = ReadFocusedResult();
                if (null == request)
                {
                    return;
                }
                request.Time = DateTime.Now;
                if (null != s_last && s_last.Line == request.Line && s_last.Column == request.Column &&
                    string.Equals(s_last.Path, request.Path, StringComparison.OrdinalIgnoreCase) &&
                    (request.Time - s_last.Time).TotalSeconds < 2)
                {
                    return; // 同じダブルクリックで、もう読み取った
                }
                s_last = request;
                ReadLastFindOptions(request);
                s_pending = request;
                Log.Write($"検索結果の箇所：{request.Path} {request.Line + 1}行 {request.Column + 1}文字目 検索語「{request.Query}」");
                UiThread.Post(Dispatcher.CurrentDispatcher, ApplyPending, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Log.Write("検索結果の箇所を読み取れませんでした", ex);
            }
        }

        /// <summary>覚えておいた箇所を、そのファイルを開いている編集ペインで表示する（ファイルが分からない時は、今開いた
        /// （前面にある）編集ペインで表示する。まだ編集ペインが無ければ、編集ペインがファイルを読み込んだ時に Take で受け取る）。</summary>
        private static void ApplyPending()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            NavigationRequest request = s_pending;
            if (null == request)
            {
                return;
            }
            if (null != request.Path ? MarkdownEditorPane.TryNavigate(request) : TryNavigateActivePane(request))
            {
                s_pending = null;
            }
        }

        /// <summary>前面にある文書のウィンドウが mdevs の編集ペインなら、そこで箇所を表示する。</summary>
        /// <param name="a_request">表示する箇所。</param>
        /// <returns>表示したら true。</returns>
        private static bool TryNavigateActivePane(NavigationRequest a_request)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IVsWindowFrame frame = null;
            if (Package.GetGlobalService(typeof(SVsShellMonitorSelection)) is IVsMonitorSelection selection &&
                ErrorHandler.Succeeded(selection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out object frameObject)))
            {
                frame = frameObject as IVsWindowFrame;
            }
            if (null == frame ||
                ErrorHandler.Failed(frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out object docView)) ||
                !(docView is MarkdownEditorPane pane))
            {
                if (null != frame && ReopenInMdevs(frame, a_request))
                {
                    return true;
                }
                Log.Write("前面の文書が mdevs の編集ペインではないため、箇所を表示できませんでした。");
                return false;
            }
            pane.NavigateTo(a_request);
            return true;
        }

        /// <summary>
        /// Visual Studio が「検索結果」の .md のファイルを、mdevs ではなくテキストエディターで開いた時（まだ開いていなかった
        /// ファイルの時。Visual Studio の「検索結果」は、開いていないファイルをテキストエディターで開くため）、そのウィンドウを閉じて
        /// mdevs で開き直し、箇所を表示する（開き直した編集ペインが、ファイルを読み込む時に Take で受け取る）。
        /// テキストエディターで変更していた時は、開き直さない。
        /// </summary>
        /// <param name="a_frame">テキストエディターのウィンドウ。</param>
        /// <param name="a_request">表示する箇所。</param>
        /// <returns>開き直したら true。</returns>
        private static bool ReopenInMdevs(IVsWindowFrame a_frame, NavigationRequest a_request)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (ErrorHandler.Failed(a_frame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out object documentObject)) ||
                    !(documentObject is string path) || !IsMarkdownFile(path))
                {
                    return false;
                }
                if (ErrorHandler.Succeeded(a_frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out object docData)) &&
                    docData is IVsPersistDocData persist && ErrorHandler.Succeeded(persist.IsDocDataDirty(out int dirty)) && 0 != dirty)
                {
                    Log.Write($"テキストエディターで変更されているため、mdevs で開き直しませんでした：{path}");
                    return false;
                }
                Log.Write($"テキストエディターで開かれたため、mdevs で開き直します：{path}");
                ErrorHandler.ThrowOnFailure(a_frame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave));
                a_request.Path = path;
                a_request.Time = DateTime.Now;
                s_pending = a_request;
                IVsWindowFrame frame = VsShellUtilities.OpenDocumentWithSpecificEditor(ServiceProvider.GlobalProvider, path,
                    typeof(MarkdownEditorFactory).GUID, VSConstants.LOGVIEWID_Primary);
                frame?.Show();
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("mdevs で開き直せませんでした", ex);
                return false;
            }
        }

        /// <summary>Markdown のファイル（.md・.markdown）か。</summary>
        private static bool IsMarkdownFile(string a_path)
        {
            string extension = System.IO.Path.GetExtension(a_path);
            return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>そのファイルの、まだ表示していない箇所を受け取る（無い・古い時は null）。</summary>
        /// <param name="a_path">ファイルのパス。</param>
        /// <returns>表示する箇所。</returns>
        public static NavigationRequest Take(string a_path)
        {
            NavigationRequest request = s_pending;
            if (null == request || (DateTime.Now - request.Time).TotalSeconds > 5 ||
                (null != request.Path && !IsSamePath(request.Path, a_path)))
            {
                return null;
            }
            s_pending = null;
            return request;
        }

        /// <summary>2つのパスが同じファイルか（大文字・小文字を区別しない）。</summary>
        public static bool IsSamePath(string a_x, string a_y)
        {
            if (string.IsNullOrEmpty(a_x) || string.IsNullOrEmpty(a_y))
            {
                return false;
            }
            try
            {
                return string.Equals(System.IO.Path.GetFullPath(a_x), System.IO.Path.GetFullPath(a_y), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return string.Equals(a_x, a_y, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// キーボードの入力先の要素から親へたどり、検索結果の行のデータ（TryGetValue(名前, out 値) を持つもの。Visual Studio の
        /// 一覧の行のデータ）を探して、ファイル・行・列を読み取る。
        /// </summary>
        /// <returns>読み取った箇所（検索結果の行ではない時は null）。</returns>
        private static NavigationRequest ReadFocusedResult()
        {
            var element = Keyboard.FocusedElement as DependencyObject;
            Log.Write($"検索結果の読み取り：キーボードの入力先={element?.GetType().FullName ?? "なし"}");
            int depth = 0;
            for (DependencyObject node = element; null != node && depth < 60; node = GetParent(node), depth++)
            {
                if (!(node is FrameworkElement fe) || null == fe.DataContext)
                {
                    continue;
                }
                object data = fe.DataContext;
                MethodInfo tryGetValue = FindTryGetValue(data.GetType());
                if (null == tryGetValue)
                {
                    continue;
                }
                string path = null;
                foreach (string key in new[] { "documentName", "path", "filePath", "documentPath", "fileName", "file" })
                {
                    if (GetValue(data, tryGetValue, key) is string value && !string.IsNullOrEmpty(value))
                    {
                        path = value;
                        break;
                    }
                }
                object line = GetValue(data, tryGetValue, "line");
                object column = GetValue(data, tryGetValue, "column");
                Log.Write($"検索結果の行のデータ：{data.GetType().FullName} ファイル={path ?? "（不明）"} 行={line} 列={column}");
                if (null == line)
                {
                    continue;
                }
                return new NavigationRequest
                {
                    // ファイルが分からない（または見つからない）時は、開いたファイルに対して表示する（ApplyPending・Take）
                    Path = null != path && File.Exists(path) ? path : null,
                    Line = Convert.ToInt32(line),
                    Column = null == column ? 0 : Convert.ToInt32(column),
                };
            }
            return null;
        }

        /// <summary>TryGetValue(string, out object) のメソッドを探す（インターフェースの明示的な実装も対象）。</summary>
        private static MethodInfo FindTryGetValue(Type a_type)
        {
            Type[] parameters = { typeof(string), typeof(object).MakeByRefType() };
            MethodInfo method = a_type.GetMethod("TryGetValue", parameters);
            if (null != method)
            {
                return method;
            }
            foreach (Type iface in a_type.GetInterfaces())
            {
                method = iface.GetMethod("TryGetValue", parameters);
                if (null != method)
                {
                    return method;
                }
            }
            return null;
        }

        /// <summary>行のデータから、名前の値を取り出す（無ければ null）。</summary>
        private static object GetValue(object a_data, MethodInfo a_tryGetValue, string a_key)
        {
            object[] args = { a_key, null };
            try
            {
                return true.Equals(a_tryGetValue.Invoke(a_data, args)) ? args[1] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>親の要素（画面の要素は VisualTreeHelper、それ以外は LogicalTreeHelper でたどる）。</summary>
        private static DependencyObject GetParent(DependencyObject a_node)
        {
            if (a_node is Visual || a_node is System.Windows.Media.Media3D.Visual3D)
            {
                return VisualTreeHelper.GetParent(a_node) ?? LogicalTreeHelper.GetParent(a_node);
            }
            return LogicalTreeHelper.GetParent(a_node);
        }

        /// <summary>
        /// 直前に Visual Studio で検索した文字列と条件を読み取る（見つかった箇所すべてに色を付けるため）。
        /// 検索結果のウィンドウのタイトル（「"excel" の検索」等）の引用符の中を、検索した文字列とする（Visual Studio 2026 の
        /// 検索では、DTE の Find に検索した文字列が入らないため）。タイトルから分からない時は、DTE の Find から読む。
        /// </summary>
        private static void ReadLastFindOptions(NavigationRequest a_request)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                string caption = ActiveWindowCaption();
                string query = QuotedText(caption);
                Log.Write($"検索結果のウィンドウのタイトル：{caption} → 検索語「{query}」");
                if (!string.IsNullOrEmpty(query))
                {
                    a_request.Query = query;
                    return;
                }
                if (Package.GetGlobalService(typeof(EnvDTE.DTE)) is EnvDTE.DTE dte && null != dte.Find)
                {
                    EnvDTE.Find find = dte.Find;
                    a_request.Query = find.FindWhat;
                    a_request.MatchCase = find.MatchCase;
                    a_request.WholeWord = find.MatchWholeWord;
                    a_request.Regex = EnvDTE.vsFindPatternSyntax.vsFindPatternSyntaxRegExpr == find.PatternSyntax;
                }
            }
            catch (Exception ex)
            {
                Log.Write("検索した文字列を読み取れませんでした", ex);
            }
        }

        /// <summary>前面にあるウィンドウ（ダブルクリックした検索結果のウィンドウ）のタイトル。</summary>
        /// <returns>タイトル（分からなければ null）。</returns>
        private static string ActiveWindowCaption()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Package.GetGlobalService(typeof(SVsShellMonitorSelection)) is IVsMonitorSelection selection &&
                ErrorHandler.Succeeded(selection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_WindowFrame, out object frameObject)) &&
                frameObject is IVsWindowFrame frame &&
                ErrorHandler.Succeeded(frame.GetProperty((int)__VSFPROPID.VSFPROPID_Caption, out object caption)))
            {
                return caption as string;
            }
            return null;
        }

        /// <summary>文字列の中の、引用符（"…"・“…”・「…」）で囲まれた部分（最初の開きと最後の閉じの間）。</summary>
        /// <param name="a_text">文字列。</param>
        /// <returns>囲まれた部分（無ければ null）。</returns>
        private static string QuotedText(string a_text)
        {
            if (string.IsNullOrEmpty(a_text))
            {
                return null;
            }
            foreach (string pair in new[] { "\"\"", "“”", "「」" })
            {
                int start = a_text.IndexOf(pair[0]);
                int end = a_text.LastIndexOf(pair[1]);
                if (0 <= start && start < end - 1)
                {
                    return a_text.Substring(start + 1, end - start - 1);
                }
            }
            return null;
        }
    }
}
