// MarkdownEditorPane.cs
//
// mdevs（mdew の Visual Studio 版）の一部。
// Visual Studio の編集ペイン（.md を開いたタブ）。中身（MarkdownWebHost）を表示し、ファイルの読み込み・保存・
// 未保存の印（*）を、Visual Studio の文書の仕組み（Running Document Table）に合わせて行う。
// Visual Studio の検索（IVsFindTarget）・ほかのアプリでのファイルの変更の検知（IVsFileChangeEvents）も受け持つ。

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace mdevs
{
    /// <summary>
    /// .md を開いた編集ペイン。ファイルの内容（ドキュメントデータ。IVsPersistDocData・IPersistFileFormat）と、
    /// 表示（ドキュメントビュー。WindowPane）を兼ねる。保存（Ctrl+S・すべて保存・閉じる時の確認等）は Visual Studio が
    /// SaveDocData を呼び、内容は MarkdownWebHost が画面側から受け取って書き込む。
    /// Visual Studio の検索（Ctrl+F の「検索と置換」）は IVsFindTarget で、検索・置換の処理は画面側（web\js\find.js）で行う。
    /// ほかのアプリでファイルが変更されたら、読み直す（保存していない変更がある時は確認する）。
    /// </summary>
    public sealed class MarkdownEditorPane : WindowPane, IVsPersistDocData, IPersistFileFormat, IVsFindTarget,
        IVsFileChangeEvents, IVsDocDataFileChangeControl, IVsTextBufferProvider
    {
        /// <summary>「名前を付けて保存」のダイアログに出すファイルの種類（説明と拡張子を改行で区切る）。</summary>
        private const string FILE_FORMAT_LIST = "Markdown ファイル (*.md)\n*.md\n";

        /// <summary>このペインを開いたパッケージ（Visual Studio のサービスの取得に使う）。</summary>
        private readonly MdevsPackage m_package;
        /// <summary>中身（WebView2 で編集画面を表示する）。</summary>
        private readonly MarkdownWebHost m_host;
        /// <summary>開いているファイルのパス。</summary>
        private string m_fileName;
        /// <summary>保存していない変更があるか（画面側から dirty で知らされる）。</summary>
        private bool m_isDirty;
        /// <summary>Visual Studio の文書の一覧（Running Document Table）での、この文書の番号（未保存の印の更新に使う）。</summary>
        private uint m_docCookie;
        /// <summary>Visual Studio の検索が覚えておく状態（SetFindState・GetFindState）。</summary>
        private object m_findState;
        /// <summary>ファイルの変更の通知を受け取る登録の番号（受け取っていない時は 0。StartWatchingFile）。</summary>
        private uint m_fileChangeCookie;
        /// <summary>ファイルの変更の通知を無視する回数（Visual Studio が IgnoreFileChanges で指示する。0 なら無視しない）。</summary>
        private int m_ignoreFileChanges;
        /// <summary>ファイルの変更の通知を受けて、読み直しを予約済みか（続けて届いた通知をまとめる）。</summary>
        private bool m_reloadPending;

        /// <param name="a_package">このペインを開いたパッケージ。</param>
        /// <summary>開いている編集ペイン（「検索結果」から開いた箇所を表示する時に、ファイルから探す。TryNavigate）。</summary>
        private static readonly System.Collections.Generic.List<MarkdownEditorPane> s_openPanes =
            new System.Collections.Generic.List<MarkdownEditorPane>();

        /// <summary>そのファイルを開いている編集ペインで、箇所を表示する。</summary>
        /// <param name="a_request">表示する箇所。</param>
        /// <returns>そのファイルを開いている編集ペインがあれば true。</returns>
        internal static bool TryNavigate(NavigationRequest a_request)
        {
            foreach (MarkdownEditorPane pane in s_openPanes)
            {
                if (NavigationCapture.IsSamePath(pane.m_fileName, a_request.Path))
                {
                    pane.NavigateTo(a_request);
                    return true;
                }
            }
            return false;
        }

        /// <summary>箇所を表示する（「検索結果」でダブルクリックした箇所。NavigationCapture）。</summary>
        /// <param name="a_request">表示する箇所。</param>
        internal void NavigateTo(NavigationRequest a_request)
        {
            Log.Write($"箇所を表示します：{m_fileName} {a_request.Line + 1}行 {a_request.Column + 1}文字目");
            m_host.NavigateTo(a_request);
        }

        public MarkdownEditorPane(MdevsPackage a_package) : base(a_package)
        {
            s_openPanes.Add(this);
            m_package = a_package;
            m_host = new MarkdownWebHost();
            m_host.DirtyChanged += SetDirty;
            m_host.SaveRequested += SaveFromEditor;
            m_host.OpenFileRequested += OpenFileInVisualStudio;
            m_host.CommandRequested += PostCommand;
            m_host.HelpRequested += OpenHelp;
            Content = m_host;
            // 編集画面で使うキーを、Visual Studio のショートカットより先に編集画面に渡す
            KeyboardRouter.Install();
        }

        /// <summary>閉じる時の後始末（WebView2 を閉じ、一時フォルダを消す）。</summary>
        protected override void Dispose(bool a_disposing)
        {
            if (a_disposing)
            {
                DisposeManaged();
            }
            base.Dispose(a_disposing);
        }

        /// <summary>閉じる時の後始末の本体（UI スレッドで、Dispose から呼ぶ）。</summary>
        private void DisposeManaged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            s_openPanes.Remove(this);
            if (0 == s_openPanes.Count)
            {
                KeyboardRouter.Uninstall();
            }
            StopWatchingFile();
            if (null != m_textBuffer)
            {
                m_textBuffer.Changed -= OnTextBufferChanged;
            }
            m_host.Dispose();
        }

        // ===== キー入力の振り分け（KeyboardRouter） =====

        /// <summary>指定のウィンドウ（キー入力の宛先）が、開いている編集ペインのどれかの編集画面の中なら、その編集ペインを返す。</summary>
        /// <param name="a_window">ウィンドウのハンドル。</param>
        /// <returns>編集ペイン。無ければ null。</returns>
        internal static MarkdownEditorPane FindByWindow(IntPtr a_window)
        {
            foreach (MarkdownEditorPane pane in s_openPanes)
            {
                if (pane.m_host.ContainsWindow(a_window))
                {
                    return pane;
                }
            }
            return null;
        }

        /// <summary>Visual Studio のコマンドを、キー入力の処理が終わった後に実行する（KeyboardRouter から。Ctrl+F・Ctrl+H 等）。</summary>
        /// <param name="a_group">コマンドのグループ。</param>
        /// <param name="a_id">コマンドの番号。</param>
        internal void PostVsCommand(Guid a_group, uint a_id)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            PostCommand(a_group, a_id);
        }

        // ===== 未保存の印・保存の要求 =====

        /// <summary>保存していない変更の有無を変え、タブの未保存の印（*）を更新する。</summary>
        /// <param name="a_dirty">変更があるか。</param>
        private void SetDirty(bool a_dirty)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (m_isDirty == a_dirty)
            {
                return;
            }
            m_isDirty = a_dirty;
            if (0 != m_docCookie && GetService(typeof(SVsUIShell)) is IVsUIShell uiShell)
            {
                uiShell.UpdateDocDataIsDirtyFeedback(m_docCookie, a_dirty ? 1 : 0);
            }
        }

        /// <summary>画面側で保存（Ctrl+S・Ctrl+Shift+S）が求められた時に、Visual Studio の保存の仕組みで保存する
        /// （Visual Studio から SaveDocData が呼ばれる）。</summary>
        /// <param name="a_saveAs">名前を付けて保存か。</param>
        private void SaveFromEditor(bool a_saveAs)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (a_saveAs)
            {
                PostCommand(VSConstants.GUID_VSStandardCommandSet97, (uint)VSConstants.VSStd97CmdID.SaveProjectItemAs);
                return;
            }
            if (0 != m_docCookie && GetService(typeof(SVsRunningDocumentTable)) is IVsRunningDocumentTable rdt)
            {
                int hr = rdt.SaveDocuments((uint)__VSRDTSAVEOPTIONS.RDTSAVEOPT_ForceSave, null, VSConstants.VSITEMID_NIL, m_docCookie);
                if (ErrorHandler.Failed(hr))
                {
                    Log.Write($"保存できませんでした（0x{hr:X8}）。");
                }
            }
        }

        /// <summary>Visual Studio のコマンドを実行する（実行は、今の処理を抜けてから）。</summary>
        /// <param name="a_group">コマンドのグループ。</param>
        /// <param name="a_id">コマンドの番号。</param>
        private void PostCommand(Guid a_group, uint a_id)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (GetService(typeof(SVsUIShell)) is IVsUIShell uiShell)
            {
                object arg = null;
                uiShell.PostExecCommand(ref a_group, a_id, 0, ref arg);
            }
        }

        /// <summary>リンク先のファイルを Visual Studio で開く（.md なら、このエディターで開く）。</summary>
        /// <param name="a_path">ファイルのフルパス。</param>
        private void OpenFileInVisualStudio(string a_path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                VsShellUtilities.OpenDocument(m_package, a_path);
            }
            catch (Exception ex)
            {
                Log.Write($"ファイルを開けませんでした：{a_path}", ex);
            }
        }

        /// <summary>ヘルプ（拡張機能の help\HELP.md）を、このエディターで開く（右クリックの「ヘルプを開く」。読み取り専用で
        /// 表示する。MarkdownWebHost.HelpPath）。.md の既定の開き方をテキストエディターに変えていても、このエディターで開く。</summary>
        private void OpenHelp()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string path = MarkdownWebHost.HelpPath;
            try
            {
                if (!File.Exists(path))
                {
                    Log.Write($"ヘルプが見つかりません：{path}");
                    return;
                }
                IVsWindowFrame frame = VsShellUtilities.OpenDocumentWithSpecificEditor(m_package, path,
                    typeof(MarkdownEditorFactory).GUID, VSConstants.LOGVIEWID_Primary);
                frame?.Show();
            }
            catch (Exception ex)
            {
                Log.Write($"ヘルプを開けませんでした：{path}", ex);
            }
        }

        // ===== IVsPersistDocData（Visual Studio の文書としての読み込み・保存） =====

        int IVsPersistDocData.GetGuidEditorType(out Guid a_classId)
        {
            a_classId = typeof(MarkdownEditorFactory).GUID;
            return VSConstants.S_OK;
        }

        int IVsPersistDocData.IsDocDataDirty(out int a_dirty)
        {
            a_dirty = m_isDirty ? 1 : 0;
            return VSConstants.S_OK;
        }

        int IVsPersistDocData.SetUntitledDocPath(string a_docDataPath)
        {
            return VSConstants.S_OK;
        }

        int IVsPersistDocData.LoadDocData(string a_documentPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return ((IPersistFileFormat)this).Load(a_documentPath, 0, 0);
        }

        /// <summary>保存する（Visual Studio が、保存・名前を付けて保存・コピーを保存の時に呼ぶ）。保存先を選ぶダイアログ等は、
        /// Visual Studio の SaveDocDataToFile に任せる（書き込みは IPersistFileFormat.Save）。</summary>
        int IVsPersistDocData.SaveDocData(VSSAVEFLAGS a_saveFlags, out string a_newDocumentPath, out int a_saveCanceled)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            a_newDocumentPath = null;
            a_saveCanceled = 0;
            switch (a_saveFlags)
            {
                case VSSAVEFLAGS.VSSAVE_Save:
                case VSSAVEFLAGS.VSSAVE_SilentSave:
                case VSSAVEFLAGS.VSSAVE_SaveAs:
                case VSSAVEFLAGS.VSSAVE_SaveCopyAs:
                    var uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
                    return uiShell.SaveDocDataToFile(a_saveFlags, this, m_fileName, out a_newDocumentPath, out a_saveCanceled);
                default:
                    return VSConstants.E_INVALIDARG;
            }
        }

        int IVsPersistDocData.Close()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            StopWatchingFile();
            return VSConstants.S_OK;
        }

        int IVsPersistDocData.OnRegisterDocData(uint a_docCookie, IVsHierarchy a_hierarchy, uint a_itemId)
        {
            m_docCookie = a_docCookie;
            return VSConstants.S_OK;
        }

        int IVsPersistDocData.RenameDocData(uint a_attributes, IVsHierarchy a_newHierarchy, uint a_newItemId, string a_newDocumentPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            m_fileName = a_newDocumentPath;
            StartWatchingFile();
            return VSConstants.S_OK;
        }

        int IVsPersistDocData.IsDocDataReloadable(out int a_reloadable)
        {
            a_reloadable = 1;
            return VSConstants.S_OK;
        }

        int IVsPersistDocData.ReloadDocData(uint a_flags)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                m_host.LoadFile(m_fileName, true);
                SetDirty(false);
                StartWatchingFile();
                return VSConstants.S_OK;
            }
            catch (Exception ex)
            {
                Log.Write($"読み直せませんでした：{m_fileName}", ex);
                return Marshal.GetHRForException(ex);
            }
        }

        // ===== IPersistFileFormat（ファイルの読み書き） =====

        int IPersistFileFormat.GetClassID(out Guid a_classId)
        {
            a_classId = typeof(MarkdownEditorFactory).GUID;
            return VSConstants.S_OK;
        }

        int IPersist.GetClassID(out Guid a_classId)
        {
            a_classId = typeof(MarkdownEditorFactory).GUID;
            return VSConstants.S_OK;
        }

        int IPersistFileFormat.IsDirty(out int a_dirty)
        {
            a_dirty = m_isDirty ? 1 : 0;
            return VSConstants.S_OK;
        }

        int IPersistFileFormat.InitNew(uint a_formatIndex)
        {
            return VSConstants.S_OK;
        }

        /// <summary>ファイルを読み込んで、編集画面に表示する。</summary>
        int IPersistFileFormat.Load(string a_fileName, uint a_mode, int a_readOnly)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                m_host.LoadFile(a_fileName);
                m_fileName = a_fileName;
                SetDirty(false);
                StartWatchingFile();
                Log.Write($"開きました：{a_fileName}");
                // 「検索結果」から開いた時は、ダブルクリックした箇所を表示する（NavigationCapture）
                NavigationRequest request = NavigationCapture.Take(a_fileName);
                if (null != request)
                {
                    NavigateTo(request);
                }
                return VSConstants.S_OK;
            }
            catch (Exception ex)
            {
                Log.Write($"開けませんでした：{a_fileName}", ex);
                return Marshal.GetHRForException(ex);
            }
        }

        /// <summary>ファイルに書き込む（a_remember が false の時は「コピーを保存」。開いているファイルは変えない）。</summary>
        int IPersistFileFormat.Save(string a_fileName, int a_remember, uint a_formatIndex)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string path = string.IsNullOrEmpty(a_fileName) ? m_fileName : a_fileName;
            // 自分で書き込んだことを、ほかのアプリでの変更と取り違えないよう、書き込む間は変更の通知を受け取らない
            // （書き込んだ後に受け取り直すと、書き込んだ後の状態が基準になる）
            StopWatchingFile();
            try
            {
                m_host.SaveFile(path, 0 != a_remember);
                if (0 != a_remember)
                {
                    m_fileName = path;
                    SetDirty(false);
                }
                return VSConstants.S_OK;
            }
            catch (Exception ex)
            {
                Log.Write($"保存できませんでした：{path}", ex);
                VsShellUtilities.ShowMessageBox(m_package, $"保存できませんでした。\n{ex.Message}", "mdevs",
                    OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return Marshal.GetHRForException(ex);
            }
            finally
            {
                StartWatchingFile();
            }
        }

        int IPersistFileFormat.SaveCompleted(string a_fileName)
        {
            return VSConstants.S_OK;
        }

        int IPersistFileFormat.GetCurFile(out string a_fileName, out uint a_formatIndex)
        {
            a_fileName = m_fileName;
            a_formatIndex = 0;
            return VSConstants.S_OK;
        }

        int IPersistFileFormat.GetFormatList(out string a_formatList)
        {
            a_formatList = FILE_FORMAT_LIST;
            return VSConstants.S_OK;
        }
        // ===== IVsFindTarget（Visual Studio の検索。Ctrl+F の「検索と置換」） =====

        /// <summary>検索の条件に、指定した項目が含まれるか。</summary>
        private static bool HasOption(uint a_options, __VSFINDOPTIONS a_option) => 0 != (a_options & (uint)a_option);

        /// <summary>対応している検索の機能。文字の画像（テキストバッファー）は持たず、検索・置換は Find・Replace で行う。</summary>
        int IVsFindTarget.GetCapabilities(bool[] a_image, uint[] a_options)
        {
            if (null != a_image && 0 < a_image.Length)
            {
                a_image[0] = false;
            }
            if (null != a_options && 0 < a_options.Length)
            {
                a_options[0] = (uint)(__VSFINDOPTIONS.FR_MatchCase | __VSFINDOPTIONS.FR_WholeWord | __VSFINDOPTIONS.FR_Backwards |
                    __VSFINDOPTIONS.FR_RegExpr | __VSFINDOPTIONS.FR_Find | __VSFINDOPTIONS.FR_Replace | __VSFINDOPTIONS.FR_ReplaceAll);
            }
            return VSConstants.S_OK;
        }

        /// <summary>検索で使う情報（文書の名前・検索する文字列の初期値・ウィンドウ等）。</summary>
        int IVsFindTarget.GetProperty(uint a_propId, out object a_value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            a_value = null;
            switch ((__VSFTPROPID)a_propId)
            {
                case __VSFTPROPID.VSFTPROPID_DocName:
                    a_value = m_fileName;
                    return VSConstants.S_OK;
                case __VSFTPROPID.VSFTPROPID_InitialPattern:
                case __VSFTPROPID.VSFTPROPID_InitialPatternAggressive:
                    // 範囲選択している文字列を、検索する文字列にする（選択していなければ、Visual Studio の前回の文字列のまま）
                    try
                    {
                        string text = m_host.GetSelectedText();
                        if (string.IsNullOrEmpty(text))
                        {
                            return VSConstants.E_NOTIMPL;
                        }
                        a_value = text;
                        return VSConstants.S_OK;
                    }
                    catch (Exception ex)
                    {
                        Log.Write("選択している文字列を受け取れませんでした", ex);
                        return VSConstants.E_NOTIMPL;
                    }
                case __VSFTPROPID.VSFTPROPID_WindowFrame:
                    a_value = GetService(typeof(SVsWindowFrame));
                    return VSConstants.S_OK;
                case __VSFTPROPID.VSFTPROPID_IsDiskFile:
                    a_value = true;
                    return VSConstants.S_OK;
                default:
                    return VSConstants.E_NOTIMPL;
            }
        }

        /// <summary>次（前）を検索する（端まで行ったら反対の端に戻る）。</summary>
        int IVsFindTarget.Find(string a_search, uint a_options, int a_resetStartPoint, IVsFindHelper a_helper, out uint a_result)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            a_result = (uint)__VSFINDRESULT.VSFR_NotFound;
            try
            {
                string result = m_host.FindText(a_search, HasOption(a_options, __VSFINDOPTIONS.FR_MatchCase),
                    HasOption(a_options, __VSFINDOPTIONS.FR_WholeWord), HasOption(a_options, __VSFINDOPTIONS.FR_RegExpr),
                    HasOption(a_options, __VSFINDOPTIONS.FR_Backwards));
                Log.Write($"検索：「{a_search}」 条件=0x{a_options:X8} 最初から={a_resetStartPoint} → {result}");
                a_result = (uint)("found" == result ? __VSFINDRESULT.VSFR_Found
                    : "error" == result ? __VSFINDRESULT.VSFR_Error : __VSFINDRESULT.VSFR_NotFound);
            }
            catch (Exception ex)
            {
                Log.Write("検索できませんでした", ex);
                a_result = (uint)__VSFINDRESULT.VSFR_Error;
            }
            return VSConstants.S_OK;
        }

        /// <summary>置換する（すべて置換の時は、すべての箇所。それ以外は、直前に見つけた箇所だけ）。</summary>
        int IVsFindTarget.Replace(string a_search, string a_replace, uint a_options, int a_resetStartPoint, IVsFindHelper a_helper,
            out int a_replaced)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            a_replaced = 0;
            try
            {
                bool all = HasOption(a_options, __VSFINDOPTIONS.FR_ReplaceAll);
                int count = m_host.ReplaceText(a_search, a_replace ?? string.Empty, HasOption(a_options, __VSFINDOPTIONS.FR_MatchCase),
                    HasOption(a_options, __VSFINDOPTIONS.FR_WholeWord), HasOption(a_options, __VSFINDOPTIONS.FR_RegExpr), all);
                Log.Write($"置換：「{a_search}」→「{a_replace}」 条件=0x{a_options:X8} すべて={all} → {count} 件");
                a_replaced = 0 < count ? 1 : 0;
            }
            catch (Exception ex)
            {
                Log.Write("置換できませんでした", ex);
            }
            return VSConstants.S_OK;
        }

        int IVsFindTarget.GetSearchImage(uint a_options, IVsTextSpanSet[] a_spans, out IVsTextImage a_textImage)
        {
            a_textImage = null;
            return VSConstants.E_NOTIMPL;
        }

        int IVsFindTarget.GetMatchRect(RECT[] a_rect)
        {
            return VSConstants.E_NOTIMPL;
        }

        /// <summary>ファイルの行・列の範囲を表示して選ぶ（「検索結果」をダブルクリックした時等）。</summary>
        int IVsFindTarget.NavigateTo(TextSpan[] a_span)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (null == a_span || 0 == a_span.Length)
            {
                return VSConstants.E_INVALIDARG;
            }
            TextSpan span = a_span[0];
            Log.Write($"移動：{span.iStartLine + 1}行 {span.iStartIndex + 1}文字目 ～ {span.iEndLine + 1}行 {span.iEndIndex + 1}文字目");
            m_host.NavigateTo(span.iStartLine, span.iStartIndex, span.iEndLine, span.iEndIndex);
            return VSConstants.S_OK;
        }

        int IVsFindTarget.GetCurrentSpan(TextSpan[] a_span)
        {
            return VSConstants.E_NOTIMPL;
        }

        int IVsFindTarget.SetFindState(object a_state)
        {
            m_findState = a_state;
            return VSConstants.S_OK;
        }

        int IVsFindTarget.GetFindState(out object a_state)
        {
            a_state = m_findState;
            return VSConstants.S_OK;
        }

        int IVsFindTarget.NotifyFindTarget(uint a_notification)
        {
            return VSConstants.S_OK;
        }

        int IVsFindTarget.MarkSpan(TextSpan[] a_span)
        {
            return VSConstants.E_NOTIMPL;
        }

        // ===== テキストとしての内容（IVsTextBufferProvider。Visual Studio の「すべて置換」・「すべて検索」） =====
        //
        // Visual Studio の「検索と置換」の「すべて置換」（検索場所：現在のドキュメント等）は、開いている文書の内容を
        // テキストバッファーとして直接書き換える。編集画面はテキストバッファーを持たないので、求められた時に、編集画面の
        // 今の内容（Markdown）を入れたテキストバッファーを渡す。Visual Studio がそれを書き換えたら、その内容を編集画面に
        // 読み込む（保存していない変更として。元に戻す（Ctrl+Z）では戻せない）。

        /// <summary>Visual Studio に渡すテキストバッファー（初めて求められた時に作る）。</summary>
        private IVsTextLines m_textLines;
        /// <summary>m_textLines の中身（変更の通知を受け取る）。</summary>
        private ITextBuffer m_textBuffer;
        /// <summary>編集画面の内容をテキストバッファーに入れている最中か（その変更は編集画面に戻さない）。</summary>
        private bool m_syncingTextBuffer;
        /// <summary>テキストバッファーの変更を、編集画面に読み込む予定があるか（まとめて1回にする）。</summary>
        private bool m_textBufferApplyPending;

        /// <summary>テキストバッファーを渡す。中身は、編集画面の今の内容にしてから渡す。</summary>
        int IVsTextBufferProvider.GetTextBuffer(out IVsTextLines a_textBuffer)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            a_textBuffer = null;
            if (m_syncingTextBuffer)
            {
                // 編集画面の内容を受け取るのを待っている間に、また求められた：今のテキストバッファーをそのまま渡す
                a_textBuffer = m_textLines;
                return null == a_textBuffer ? VSConstants.E_FAIL : VSConstants.S_OK;
            }
            try
            {
                m_syncingTextBuffer = true;
                string text = m_host.GetCurrentMarkdown();
                if (null == m_textLines)
                {
                    var componentModel = (IComponentModel)GetService(typeof(SComponentModel));
                    IVsEditorAdaptersFactoryService adapters = componentModel.GetService<IVsEditorAdaptersFactoryService>();
                    var oleServiceProvider = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)ServiceProvider.GlobalProvider
                        .GetService(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider));
                    IVsTextBuffer buffer = adapters.CreateVsTextBufferAdapter(oleServiceProvider);
                    ErrorHandler.ThrowOnFailure(buffer.InitializeContent(text, text.Length));
                    m_textLines = (IVsTextLines)buffer;
                    m_textBuffer = adapters.GetDocumentBuffer(buffer);
                    m_textBuffer.Changed += OnTextBufferChanged;
                    Log.Write("Visual Studio にテキストとしての内容を渡しました。");
                }
                else if (m_textBuffer.CurrentSnapshot.GetText() != text)
                {
                    m_textBuffer.Replace(new Span(0, m_textBuffer.CurrentSnapshot.Length), text);
                }
                a_textBuffer = m_textLines;
                return VSConstants.S_OK;
            }
            catch (Exception ex)
            {
                Log.Write("Visual Studio にテキストとしての内容を渡せませんでした", ex);
                return VSConstants.E_FAIL;
            }
            finally
            {
                m_syncingTextBuffer = false;
            }
        }

        int IVsTextBufferProvider.SetTextBuffer(IVsTextLines a_textBuffer)
        {
            return VSConstants.E_NOTIMPL;
        }

        int IVsTextBufferProvider.LockTextBuffer(int a_lock)
        {
            return VSConstants.S_OK;
        }

        /// <summary>Visual Studio がテキストバッファーを書き換えた時：書き換え終わってから（まとめて1回）編集画面に読み込む。</summary>
        private void OnTextBufferChanged(object a_sender, TextContentChangedEventArgs a_args)
        {
            if (m_syncingTextBuffer || m_textBufferApplyPending)
            {
                return;
            }
            m_textBufferApplyPending = true;
            UiThread.Post(m_host.Dispatcher, () =>
            {
                m_textBufferApplyPending = false;
                string text = m_textBuffer.CurrentSnapshot.GetText();
                Log.Write("Visual Studio が内容を書き換えました（すべて置換等）。編集画面に読み込みます。");
                m_host.SetMarkdown(text);
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        // ===== ほかのアプリでのファイルの変更（IVsFileChangeEvents・IVsDocDataFileChangeControl） =====

        /// <summary>開いているファイルの変更（更新日時・大きさ）の通知を受け取り始める（受け取っていれば、受け取り直す）。</summary>
        private void StartWatchingFile()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            StopWatchingFile();
            if (string.IsNullOrEmpty(m_fileName) || !(GetService(typeof(SVsFileChangeEx)) is IVsFileChangeEx service))
            {
                return;
            }
            int hr = service.AdviseFileChange(m_fileName,
                (uint)(_VSFILECHANGEFLAGS.VSFILECHG_Time | _VSFILECHANGEFLAGS.VSFILECHG_Size), this, out m_fileChangeCookie);
            if (ErrorHandler.Failed(hr))
            {
                m_fileChangeCookie = 0;
                Log.Write($"ファイルの変更の通知を受け取れません（0x{hr:X8}）：{m_fileName}");
            }
        }

        /// <summary>ファイルの変更の通知を受け取るのをやめる。</summary>
        private void StopWatchingFile()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (0 != m_fileChangeCookie && GetService(typeof(SVsFileChangeEx)) is IVsFileChangeEx service)
            {
                service.UnadviseFileChange(m_fileChangeCookie);
            }
            m_fileChangeCookie = 0;
        }

        /// <summary>ファイルが変更された（ほかのアプリで書き換えられた）。読み直しは、今の処理を抜けてから行う。</summary>
        int IVsFileChangeEvents.FilesChanged(uint a_count, string[] a_files, uint[] a_changes)
        {
            if (0 < m_ignoreFileChanges || m_reloadPending)
            {
                return VSConstants.S_OK;
            }
            m_reloadPending = true;
            UiThread.Post(m_host.Dispatcher, ReloadChangedFile, System.Windows.Threading.DispatcherPriority.Background);
            return VSConstants.S_OK;
        }

        int IVsFileChangeEvents.DirectoryChanged(string a_directory)
        {
            return VSConstants.S_OK;
        }

        /// <summary>ほかのアプリで変更されたファイルを読み直す。保存していない変更がある時は、読み直すかを確認する。
        /// ファイルが削除された時は何もしない（保存すると、また作られる）。</summary>
        private void ReloadChangedFile()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            m_reloadPending = false;
            if (0 < m_ignoreFileChanges || string.IsNullOrEmpty(m_fileName) || !File.Exists(m_fileName))
            {
                return;
            }
            if (m_isDirty)
            {
                int answer = VsShellUtilities.ShowMessageBox(m_package,
                    $"{Path.GetFileName(m_fileName)} は、ほかのアプリで変更されました。\n保存していない変更を破棄して、読み直しますか？",
                    "mdevs", OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);
                if ((int)VSConstants.MessageBoxResult.IDYES != answer)
                {
                    return;
                }
            }
            try
            {
                m_host.LoadFile(m_fileName, true);
                SetDirty(false);
                Log.Write($"ほかのアプリで変更されたため、読み直しました：{m_fileName}");
            }
            catch (Exception ex)
            {
                Log.Write($"読み直せませんでした：{m_fileName}", ex);
            }
        }

        /// <summary>Visual Studio が、ファイルの変更の通知を一時的に無視するよう指示する（ソース管理の操作等）。
        /// 無視し終えた時は、その時点のファイルの状態を基準にして受け取り直す。</summary>
        int IVsDocDataFileChangeControl.IgnoreFileChanges(int a_ignore)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (0 != a_ignore)
            {
                m_ignoreFileChanges++;
            }
            else if (0 < m_ignoreFileChanges && 0 == --m_ignoreFileChanges)
            {
                StartWatchingFile();
            }
            return VSConstants.S_OK;
        }
    }
}
