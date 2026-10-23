// MarkdownWebHost.cs
//
// mdevs（mdew の Visual Studio 版）の一部。
// 編集ペインの中身。WebView2 に画面（web フォルダの HTML。mdew の画面を元にしたもの）を表示し、
// ファイルの読み込み・保存、画像の貼り付け、リンクを開く等、画面側（web\js\main.js）とのやり取りを受け持つ。
// mdew の Program.cs（MainWindow）から、Visual Studio の中で必要な部分を移したもの。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace mdevs
{
    /// <summary>
    /// 編集ペインの中身（WebView2 で mdew の画面を表示する）。ファイルの読み込み・保存は、Visual Studio の
    /// 保存の仕組み（MarkdownEditorPane）から呼ばれる。画面側とは WebView2 の postMessage で JSON をやり取りする
    /// （web\js\main.js 冒頭のコメント参照）。
    /// </summary>
    internal sealed class MarkdownWebHost : Grid, IDisposable
    {
        /// <summary>画面（web フォルダ）を読み込む仮想ホスト名（https://app.mdew/index.html。画面側も同じ名前を使う）。</summary>
        private const string APP_HOST = "app.mdew";
        /// <summary>文書内の相対パスの画像を読み込む仮想ホスト名（ServeDocumentResource 参照）。</summary>
        private const string DOC_HOST = "doc.mdew";
        /// <summary>貼り付けた画像を、保存するまでの間置いておく一時フォルダの仮想ホスト名。</summary>
        private const string TMP_HOST = "tmp.mdew";
        /// <summary>貼り付けた画像のファイル名の先頭（clipboard_image_1.png 等。mdew と同じ）。</summary>
        private const string PASTED_IMAGE_BASE_NAME = "clipboard_image";
        /// <summary>保存する内容を画面側から受け取るのを待つ時間（ミリ秒）。</summary>
        private const int SAVE_DATA_TIMEOUT_MS = 5000;

        /// <summary>すべての編集ペインで共有する WebView2 の環境（作業用のデータの場所。最初の編集ペインを開く時に作る）。</summary>
        private static Task<CoreWebView2Environment> s_environment;
        /// <summary>ブラウザのキャッシュを消したか（Visual Studio を起動してから最初の1回だけ消す）。</summary>
        private static bool s_cacheCleared;

        /// <summary>画面を表示する WebView2。</summary>
        private readonly WebView2 m_webView = new WebView2();
        /// <summary>この編集ペイン専用の、貼り付けた画像の一時フォルダ（閉じる時に削除する）。</summary>
        private readonly string m_tempImageDir = Path.Combine(Path.GetTempPath(), "mdevs", Guid.NewGuid().ToString("N"));
        /// <summary>WebView2 の準備を始めたか。</summary>
        private bool m_initStarted;
        /// <summary>画面側の準備ができたか（ready が届いた）。</summary>
        private bool m_ready;
        /// <summary>開いているファイルのパス。</summary>
        private string m_currentPath;
        /// <summary>次に画面側へ load を送る時に、画面側の確認（保存していない変更を破棄するか）を省くか
        /// （Visual Studio 側で確認済みの読み直し。LoadFile の a_force）。</summary>
        private bool m_forceLoad;
        /// <summary>画面側の準備ができたら表示する箇所（NavigateTo。準備前に指定された時だけ。load と一緒に送る）。</summary>
        private object m_pendingNavigate;
        /// <summary>読み込んだファイルの内容（改行は LF）。画面側の準備ができる前に保存する時は、これを書く。</summary>
        private string m_loadedMarkdown = string.Empty;
        /// <summary>保存時の改行を CRLF にするか（読み込んだファイルに合わせる）。</summary>
        private bool m_useCrLf = true;
        /// <summary>保存時に UTF-8 の BOM を付けるか（読み込んだファイルに合わせる）。</summary>
        private bool m_hasBom;
        /// <summary>開いているファイルのフォルダ（相対パスの画像の基準）。</summary>
        private string m_docFolder;
        /// <summary>画像の URL に入れる世代番号（ファイルを開き直すたびに増やす。SetDocumentFolder 参照）。</summary>
        private int m_docGeneration;

        /// <summary>画面側で、保存していない変更の有無が変わった時（引数は変更があるか）。</summary>
        public event Action<bool> DirtyChanged;
        /// <summary>画面側で保存（Ctrl+S。引数は名前を付けて保存か）が求められた時。</summary>
        public event Action<bool> SaveRequested;
        /// <summary>画面側で、相対パスのリンク（別の Markdown ファイル等）が開かれた時（引数はファイルのフルパス）。</summary>
        public event Action<string> OpenFileRequested;
        /// <summary>Visual Studio のコマンドを実行する時（ショートカットキーを Visual Studio に渡す。OnPreviewKeyDown 参照）。</summary>
        public event Action<Guid, uint> CommandRequested;

        /// <summary>中身を作る。WebView2 の準備は、画面に表示された後（Loaded）に行う。</summary>
        public MarkdownWebHost()
        {
            Children.Add(m_webView);
            Loaded += OnLoaded;
            m_webView.PreviewKeyDown += OnPreviewKeyDown;
        }

        /// <summary>開いているファイルのパス。</summary>
        public string CurrentPath => m_currentPath;

        // ===== WebView2 の準備 =====

        /// <summary>画面に表示されたら、WebView2 の準備を始める（1回だけ）。</summary>
        private async void OnLoaded(object a_sender, RoutedEventArgs a_args)
        {
            if (m_initStarted)
            {
                return;
            }
            m_initStarted = true;
            try
            {
                await InitializeWebViewAsync();
            }
            catch (Exception ex)
            {
                Log.Write("WebView2 の準備に失敗しました", ex);
                Children.Clear();
                Children.Add(new TextBlock
                {
                    Text = "Markdown の編集画面を表示できませんでした（WebView2 の準備に失敗しました）。\n" +
                        "「出力」ウィンドウの「mdevs」に詳しい内容があります。\n\n" + ex.Message,
                    Margin = new Thickness(16),
                    TextWrapping = TextWrapping.Wrap,
                });
            }
        }

        /// <summary>すべての編集ペインで共有する WebView2 の環境を作る（作業用のデータは %LOCALAPPDATA%\mdevs\WebView2）。</summary>
        /// <returns>WebView2 の環境。</returns>
        private static Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            if (null == s_environment)
            {
                // WebView2 のネイティブ DLL（WebView2Loader.dll）は、拡張機能のフォルダに同梱したものを使う
                // （Visual Studio が先に読み込んでいる場合は、そちらがそのまま使われる）
                string extensionDir = Path.GetDirectoryName(typeof(MarkdownWebHost).Assembly.Location);
                if (File.Exists(Path.Combine(extensionDir, "WebView2Loader.dll")))
                {
                    try
                    {
                        CoreWebView2Environment.SetLoaderDllFolderPath(extensionDir);
                    }
                    catch (Exception ex)
                    {
                        Log.Write("WebView2Loader.dll の場所を設定できませんでした（Visual Studio が読み込んだものを使います）", ex);
                    }
                }
                string userData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mdevs", "WebView2");
                s_environment = CoreWebView2Environment.CreateAsync(null, userData);
            }
            return s_environment;
        }

        /// <summary>WebView2 を準備して画面（web\index.html）を表示する。仮想ホスト名の設定、画面側とのメッセージ・
        /// 画像の要求・クリップボードの許可の受け付けを行う。</summary>
        /// <returns>準備が終わるまでの非同期処理。</returns>
        private async Task InitializeWebViewAsync()
        {
            CoreWebView2Environment env = await GetEnvironmentAsync();
            await m_webView.EnsureCoreWebView2Async(env);
            CoreWebView2 core = m_webView.CoreWebView2;
            Log.Write($"WebView2 の準備ができました（ランタイム {env.BrowserVersionString}）。");

            // 画面側（web フォルダ）のファイルを更新した時に、古いファイルがブラウザのキャッシュから使われないよう、
            // Visual Studio を起動してから最初の1回だけキャッシュを消す
            if (!s_cacheCleared)
            {
                s_cacheCleared = true;
                await core.Profile.ClearBrowsingDataAsync(
                    CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage);
            }

            // ブラウザ用のショートカット（Ctrl+R の再読み込み等）は無効にする。Ctrl+マウスホイールで画面全体が拡大されない
            // ようにする（文書の表示倍率は画面側で変える）
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;

            string webDir = Path.Combine(Path.GetDirectoryName(typeof(MarkdownWebHost).Assembly.Location), "web");
            core.SetVirtualHostNameToFolderMapping(APP_HOST, webDir, CoreWebView2HostResourceAccessKind.Allow);
            core.AddWebResourceRequestedFilter($"https://{DOC_HOST}/*", CoreWebView2WebResourceContext.All);
            core.AddWebResourceRequestedFilter($"https://{TMP_HOST}/*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += ServeDocumentResource;
            core.WebMessageReceived += OnWebMessage;
            // 右クリックのメニューの「貼り付け」で、画面側からクリップボードを読めるようにする
            core.PermissionRequested += (s, e) =>
            {
                if (CoreWebView2PermissionKind.ClipboardRead == e.PermissionKind)
                {
                    e.State = CoreWebView2PermissionState.Allow;
                }
            };
            core.ProcessFailed += (s, e) =>
            {
                Log.Write($"WebView2 のプロセスが異常終了：種類={e.ProcessFailedKind} 理由={e.Reason} 終了コード={e.ExitCode}");
            };
            core.Navigate($"https://{APP_HOST}/index.html");
        }

        // ===== 画面側とのメッセージ =====

        /// <summary>画面側へメッセージを送る（JSON にして postMessage する）。</summary>
        /// <param name="a_message">メッセージ（cmd と、cmd ごとの値を持つ匿名型）。</param>
        private void Post(object a_message)
        {
            m_webView.CoreWebView2?.PostWebMessageAsString(JsonConvert.SerializeObject(a_message));
        }

        /// <summary>画面側にエラーを表示させる。</summary>
        /// <param name="a_message">表示する内容。</param>
        private void PostError(string a_message)
        {
            Post(new { cmd = "error", message = a_message });
        }

        /// <summary>画面側からのメッセージを受け取る。処理は WebView2 のイベントを抜けてから行う（WebView2 のイベントの中で
        /// ダイアログ等を出すと、WebView2 が再入に対応していないため、反応しなくなることがある）。</summary>
        private void OnWebMessage(object a_sender, CoreWebView2WebMessageReceivedEventArgs a_args)
        {
            string json = a_args.TryGetWebMessageAsString();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    HandleWebMessage(json);
                }
                catch (Exception ex)
                {
                    Log.Write("画面側からのメッセージの処理中にエラー", ex);
                    PostError($"処理中にエラーが発生しました。\n{ex.Message}");
                }
            }));
        }

        /// <summary>画面側からのメッセージを処理する（cmd ごとに振り分ける）。Visual Studio の中では使わないもの
        /// （ファイルを開く・フォルダ・最近使ったファイル等）は無視する。</summary>
        /// <param name="a_json">メッセージの JSON。</param>
        private void HandleWebMessage(string a_json)
        {
            JObject msg = JObject.Parse(a_json);
            string cmd = (string)msg["cmd"];
            switch (cmd)
            {
                case "ready":
                    m_ready = true;
                    Post(new { cmd = "init", debug = false, tempDir = m_tempImageDir });
                    if (null != m_currentPath)
                    {
                        PostLoad();
                    }
                    break;
                case "dirty":
                    DirtyChanged?.Invoke((bool)msg["value"]);
                    break;
                case "save":
                    SaveRequested?.Invoke(true == (bool?)msg["saveAs"]);
                    break;
                case "devTools":
                    m_webView.CoreWebView2?.OpenDevToolsWindow();
                    break;
                case "openUrl":
                    OpenUrl((string)msg["url"]);
                    break;
                case "openRelative":
                    OpenRelativeFile((string)msg["path"]);
                    break;
                case "openImage":
                    OpenImage((string)msg["src"]);
                    break;
                case "pasteImage":
                    SavePastedImage((int)msg["id"], (string)msg["type"], (string)msg["data"]);
                    break;
                case "copyHtml":
                    CopyHtml((string)msg["html"], (string)msg["text"]);
                    break;
                case "importImages":
                    ImportImages((int)msg["id"], msg["srcs"] as JArray);
                    break;
                case "tableSizeDialog":
                    // 右クリックのメニュー「表を挿入…」
                    ShowTableSizeDialog();
                    break;
                case "linkDialog":
                    // 右クリックのメニュー「リンクにする…」
                    ShowLinkDialog();
                    break;
                default:
                    Log.Write($"未対応のメッセージ：{cmd}");
                    break;
            }
        }

        // ===== ショートカットキー =====

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetKeyState(int a_virtualKey);

        /// <summary>修飾キーが押されているか（WebView2 にキーボードの入力先がある時も、正しく分かるよう GetKeyState で調べる）。</summary>
        private static bool IsKeyDown(int a_virtualKey) => 0 != (GetKeyState(a_virtualKey) & 0x8000);

        /// <summary>
        /// WebView2 に入力先がある時の、Ctrl・Alt 付きのキーやファンクションキー（WebView2 が WPF に知らせてくるもの）の処理。
        /// 編集画面で使わない Visual Studio のショートカット（すべて保存・ビルド・デバッグ・ウィンドウの切り替え・検索等）を、
        /// Visual Studio のコマンドとして実行する（よく使うものだけ。Ctrl+S は画面側から save で届く）。
        /// </summary>
        private void OnPreviewKeyDown(object a_sender, KeyEventArgs a_args)
        {
            Key key = Key.System == a_args.Key ? a_args.SystemKey : a_args.Key;
            bool ctrl = IsKeyDown(0x11);
            bool shift = IsKeyDown(0x10);
            bool alt = IsKeyDown(0x12);
            Guid std97 = Microsoft.VisualStudio.VSConstants.GUID_VSStandardCommandSet97;
            uint? command = null;
            if (ctrl && shift && !alt && Key.S == key)
            {
                command = (uint)Microsoft.VisualStudio.VSConstants.VSStd97CmdID.SaveSolution; // すべて保存
            }
            else if (ctrl && shift && !alt && Key.B == key)
            {
                command = (uint)Microsoft.VisualStudio.VSConstants.VSStd97CmdID.BuildSln; // ソリューションのビルド
            }
            else if (ctrl && !shift && !alt && Key.F4 == key)
            {
                command = (uint)Microsoft.VisualStudio.VSConstants.VSStd97CmdID.CloseDocument; // 閉じる
            }
            else if (ctrl && !alt && Key.Tab == key)
            {
                command = (uint)(shift ? Microsoft.VisualStudio.VSConstants.VSStd97CmdID.PrevDocument
                    : Microsoft.VisualStudio.VSConstants.VSStd97CmdID.NextDocument); // 次・前のウィンドウ
            }
            else if (ctrl && !alt && (Key.F == key || Key.H == key))
            {
                // 検索・置換（Ctrl+F・Ctrl+H）、フォルダーを指定して検索・置換（Ctrl+Shift+F・Ctrl+Shift+H）
                command = (uint)(Key.F == key
                    ? (shift ? Microsoft.VisualStudio.VSConstants.VSStd97CmdID.FindInFiles : Microsoft.VisualStudio.VSConstants.VSStd97CmdID.Find)
                    : (shift ? Microsoft.VisualStudio.VSConstants.VSStd97CmdID.ReplaceInFiles : Microsoft.VisualStudio.VSConstants.VSStd97CmdID.Replace));
            }
            else if (!ctrl && !alt && Key.F3 == key)
            {
                // 次を検索・前を検索（F3・Shift+F3）
                command = (uint)(shift ? Microsoft.VisualStudio.VSConstants.VSStd97CmdID.FindPrev
                    : Microsoft.VisualStudio.VSConstants.VSStd97CmdID.FindNext);
            }
            else if (!alt && Key.F5 == key)
            {
                command = (uint)(shift ? Microsoft.VisualStudio.VSConstants.VSStd97CmdID.Stop
                    : ctrl ? Microsoft.VisualStudio.VSConstants.VSStd97CmdID.StartNoDebug
                    : Microsoft.VisualStudio.VSConstants.VSStd97CmdID.Start); // デバッグ
            }
            if (null != command)
            {
                a_args.Handled = true;
                Log.Write($"ショートカット {(ctrl ? "Ctrl+" : "")}{(shift ? "Shift+" : "")}{(alt ? "Alt+" : "")}{key} を Visual Studio に渡します。");
                CommandRequested?.Invoke(std97, command.Value);
            }
        }

        // ===== ダイアログ（右クリックのメニュー「表を挿入…」「リンクにする…」） =====

        /// <summary>「表を挿入…」：行数・列数のダイアログを出し、結果を画面側に tableSize で知らせる（取りやめた時は 0）。</summary>
        private void ShowTableSizeDialog()
        {
            var tableDialog = new dialog.TableSizeDialog();
            bool ok = true == tableDialog.ShowDialog();
            // ダイアログを閉じた後、すぐに表の中へ書けるよう、編集画面にキーボードの入力先を戻す
            m_webView.Focus();
            Post(new { cmd = "tableSize", rows = ok ? tableDialog.Rows : 0, cols = ok ? tableDialog.Columns : 0 });
        }

        /// <summary>「リンクにする…」：URL を入力するダイアログを出し、結果を画面側に linkUrl で知らせる（取りやめた時は空）。</summary>
        private void ShowLinkDialog()
        {
            var linkDialog = new dialog.LinkInputDialog();
            bool ok = true == linkDialog.ShowDialog();
            m_webView.Focus();
            Post(new { cmd = "linkUrl", url = ok ? linkDialog.Url : "" });
        }

        // ===== Visual Studio の検索（MarkdownEditorPane の IVsFindTarget から呼ばれる） =====

        /// <summary>画面側の準備ができているか（検索等を行えるか）。</summary>
        public bool IsReady => m_ready && null != m_webView.CoreWebView2;

        /// <summary>範囲選択している文字列（検索する文字列の初期値。選択していない時・複数行の時は空）。</summary>
        /// <returns>選択している文字列。</returns>
        public string GetSelectedText()
        {
            if (!IsReady)
            {
                return string.Empty;
            }
            return JsonConvert.DeserializeObject<string>(ExecuteScriptAndWait("window.__mdevs.selectedText()", SAVE_DATA_TIMEOUT_MS)) ?? string.Empty;
        }

        /// <summary>次（前）の箇所を探して選ぶ（端まで行ったら反対の端に戻る）。</summary>
        /// <param name="a_query">検索する文字列。</param>
        /// <param name="a_matchCase">大文字・小文字を区別するか。</param>
        /// <param name="a_wholeWord">単語単位で探すか。</param>
        /// <param name="a_regex">正規表現を使うか。</param>
        /// <param name="a_backward">前を検索か。</param>
        /// <returns>"found"・"notfound"・"error"（正規表現の誤り等）。</returns>
        public string FindText(string a_query, bool a_matchCase, bool a_wholeWord, bool a_regex, bool a_backward)
        {
            if (!IsReady)
            {
                return "notfound";
            }
            string args = JsonConvert.SerializeObject(new
            {
                query = a_query, matchCase = a_matchCase, wholeWord = a_wholeWord, regex = a_regex, backward = a_backward,
            });
            return JsonConvert.DeserializeObject<string>(ExecuteScriptAndWait($"window.__mdevs.find({args})", SAVE_DATA_TIMEOUT_MS));
        }

        /// <summary>置換する（a_all が false の時は、直前に見つけた箇所だけ。true の時はすべて）。</summary>
        /// <param name="a_query">検索する文字列。</param>
        /// <param name="a_replacement">置換後の文字列。</param>
        /// <param name="a_matchCase">大文字・小文字を区別するか。</param>
        /// <param name="a_wholeWord">単語単位で探すか。</param>
        /// <param name="a_regex">正規表現を使うか。</param>
        /// <param name="a_all">すべて置換か。</param>
        /// <returns>置換した件数（正規表現の誤り等は -1）。</returns>
        public int ReplaceText(string a_query, string a_replacement, bool a_matchCase, bool a_wholeWord, bool a_regex, bool a_all)
        {
            if (!IsReady)
            {
                return 0;
            }
            string args = JsonConvert.SerializeObject(new
            {
                query = a_query, replacement = a_replacement, matchCase = a_matchCase, wholeWord = a_wholeWord, regex = a_regex, all = a_all,
            });
            return JsonConvert.DeserializeObject<int>(ExecuteScriptAndWait($"window.__mdevs.replace({args})", SAVE_DATA_TIMEOUT_MS));
        }

        /// <summary>ファイルの行・列の範囲を表示して選ぶ（行・列は 0 から数える）。画面側の準備がまだの時は、準備ができて
        /// ファイルを表示した後に行う。</summary>
        public void NavigateTo(int a_startLine, int a_startCol, int a_endLine, int a_endCol)
        {
            NavigateTo(new { startLine = a_startLine, startCol = a_startCol, endLine = a_endLine, endCol = a_endCol });
        }

        /// <summary>「検索結果」でダブルクリックした箇所を表示する。検索した文字列が分かる時は、見つかった箇所すべてに色を付け、
        /// ダブルクリックした箇所を選ぶ（F3 で次の箇所へ進める）。</summary>
        /// <param name="a_request">表示する箇所と検索の条件。</param>
        public void NavigateTo(NavigationRequest a_request)
        {
            NavigateTo(new
            {
                startLine = a_request.Line, startCol = a_request.Column, endLine = a_request.Line, endCol = a_request.Column,
                query = a_request.Query, matchCase = a_request.MatchCase, wholeWord = a_request.WholeWord, regex = a_request.Regex,
            });
        }

        /// <summary>画面側に、箇所の表示（window.__mdevs.navigateTo）を頼む。</summary>
        /// <param name="a_span">箇所（navigateTo に渡すもの）。</param>
        private void NavigateTo(object a_span)
        {
            if (!IsReady)
            {
                m_pendingNavigate = a_span;
                return;
            }
            m_webView.Focus();
#pragma warning disable VSTHRD110
            m_webView.CoreWebView2.ExecuteScriptAsync($"window.__mdevs.navigateTo({JsonConvert.SerializeObject(a_span)})");
#pragma warning restore VSTHRD110
        }

        // ===== ファイルの読み込み・保存 =====

        /// <summary>ファイルを読み込んで画面側に表示させる。BOM の有無・改行の種類は保存時のために覚えておく。</summary>
        /// <param name="a_path">ファイルのパス。</param>
        /// <param name="a_force">保存していない変更があっても、画面側で確認せずに読み直すか（Visual Studio 側で確認済みの時）。</param>
        public void LoadFile(string a_path, bool a_force = false)
        {
            byte[] bytes = File.ReadAllBytes(a_path);
            // UTF-8 の BOM の有無を覚えておき、保存時も同じにする（BOM は取り除いてから画面側へ渡す）
            m_hasBom = bytes.Length >= 3 && 0xEF == bytes[0] && 0xBB == bytes[1] && 0xBF == bytes[2];
            string text = new UTF8Encoding(false).GetString(bytes, m_hasBom ? 3 : 0, bytes.Length - (m_hasBom ? 3 : 0));
            m_useCrLf = text.Contains("\r\n");
            m_currentPath = a_path;
            m_loadedMarkdown = text.Replace("\r\n", "\n");
            m_forceLoad = a_force;
            if (m_ready)
            {
                PostLoad();
            }
        }

        /// <summary>読み込んだファイルを画面側に表示させる（load を送る）。</summary>
        private void PostLoad()
        {
            string docBase = SetDocumentFolder(m_currentPath);
            Post(new
            {
                cmd = "load", path = m_currentPath, name = Path.GetFileName(m_currentPath), docBase, markdown = m_loadedMarkdown,
                force = m_forceLoad, navigate = m_pendingNavigate,
            });
            m_forceLoad = false;
            m_pendingNavigate = null;
        }

        /// <summary>
        /// ファイルに保存する。保存する内容は画面側から受け取る（画面側の準備ができる前は、読み込んだ内容のまま）。
        /// 貼り付けた画像は保存先の「&lt;ファイル名&gt;.images」へ移し、画像の参照先を書き換える。
        /// </summary>
        /// <param name="a_path">保存先のパス。</param>
        /// <param name="a_remember">保存先を、このファイルのパスとして覚えるか（「コピーを保存」の時は false）。</param>
        public void SaveFile(string a_path, bool a_remember)
        {
            string markdown = m_loadedMarkdown;
            var images = new List<string>();
            if (m_ready && null != m_webView.CoreWebView2)
            {
                string json = ExecuteScriptAndWait("JSON.stringify(window.__mdevs.saveData())", SAVE_DATA_TIMEOUT_MS);
                // ExecuteScriptAsync の結果は JSON（ここでは JSON の文字列を JSON にしたもの）
                JObject data = JObject.Parse(JsonConvert.DeserializeObject<string>(json));
                markdown = (string)data["markdown"];
                if (data["images"] is JArray array)
                {
                    foreach (JToken item in array)
                    {
                        images.Add((string)item);
                    }
                }
            }

            var replacements = new Dictionary<string, string>();
            // 保存先のフォルダやファイル名が変わる場合は、相対パスの画像も新しい保存先へコピーする
            string result = RelocateRelativeImages(markdown, m_currentPath, a_path, images, replacements);
            // 貼り付けた画像（一時フォルダ）を保存先の「<ファイル名>.images」へ移し、URL を書き換える
            result = RelocateTempImages(result, a_path, replacements);
            string text = m_useCrLf ? result.Replace("\r\n", "\n").Replace("\n", "\r\n") : result;
            File.WriteAllText(a_path, text, new UTF8Encoding(m_hasBom));
            Log.Write($"保存しました：{a_path}");
            if (!a_remember)
            {
                return;
            }
            m_currentPath = a_path;
            m_loadedMarkdown = result;
            if (m_ready)
            {
                string docBase = SetDocumentFolder(a_path);
                Post(new { cmd = "saved", path = a_path, name = Path.GetFileName(a_path), docBase, markdown = result, replacements });
            }
        }

        /// <summary>
        /// 画面側でスクリプトを実行し、結果が返るまで待つ（Visual Studio の保存は、その場で終える必要があるため）。
        /// 待つ間は、入れ子のメッセージループで WebView2 からの結果を受け取る。
        /// </summary>
        /// <param name="a_script">実行するスクリプト。</param>
        /// <param name="a_timeoutMs">待つ時間の上限（ミリ秒）。</param>
        /// <returns>スクリプトの結果（JSON）。</returns>
        private string ExecuteScriptAndWait(string a_script, int a_timeoutMs)
        {
#pragma warning disable VSTHRD002, VSTHRD110
            Task<string> task = m_webView.CoreWebView2.ExecuteScriptAsync(a_script);
            if (!task.IsCompleted)
            {
                var frame = new DispatcherFrame();
                var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(a_timeoutMs), DispatcherPriority.Send,
                    (s, e) => frame.Continue = false, Dispatcher);
                task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
                timer.Start();
                Dispatcher.PushFrame(frame);
                timer.Stop();
            }
            if (!task.IsCompleted)
            {
                throw new TimeoutException("編集画面から保存する内容を受け取れませんでした（時間切れ）。");
            }
            return task.Result;
#pragma warning restore VSTHRD002, VSTHRD110
        }

        /// <summary>開いているファイルのフォルダを、画像の読み込み元として設定する。戻り値は画面側で &lt;base href&gt; に
        /// 使う URL。ファイルを開き直すたびに世代番号を変えて、別フォルダの同名画像がキャッシュから表示されないようにする。</summary>
        /// <param name="a_path">開いた・保存したファイルのパス。</param>
        /// <returns>https://doc.mdew/&lt;世代&gt;/ の形の URL。</returns>
        private string SetDocumentFolder(string a_path)
        {
            m_docFolder = Path.GetDirectoryName(Path.GetFullPath(a_path));
            m_docGeneration++;
            return $"https://{DOC_HOST}/{m_docGeneration}/";
        }

        // ===== リンク・画像を開く =====

        /// <summary>リンク先の URL を、既定のブラウザ・メールソフトで開く（http・https・mailto だけ）。</summary>
        /// <param name="a_url">リンク先の URL。</param>
        private void OpenUrl(string a_url)
        {
            if (!Uri.TryCreate(a_url, UriKind.Absolute, out Uri uri) ||
                ("http" != uri.Scheme && "https" != uri.Scheme && "mailto" != uri.Scheme))
            {
                PostError($"このリンクは開けません。\n{a_url}");
                return;
            }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                PostError($"リンク先を開けませんでした。\n{a_url}\n{ex.Message}");
            }
        }

        /// <summary>相対パスで書かれたファイルへのリンクを、開いているファイルのフォルダ基準で解決して、Visual Studio で開く。</summary>
        /// <param name="a_relativePath">リンクに書かれた相対パス（# より後ろは除いたもの）。</param>
        private void OpenRelativeFile(string a_relativePath)
        {
            string full = null;
            try
            {
                full = Path.GetFullPath(Path.Combine(m_docFolder ?? string.Empty, a_relativePath.Replace('/', Path.DirectorySeparatorChar)));
            }
            catch (Exception)
            {
                // 下で「見つかりません」にする
            }
            if (null == full || !File.Exists(full))
            {
                PostError($"リンク先のファイルが見つかりません。\n{a_relativePath}");
                return;
            }
            OpenFileRequested?.Invoke(full);
        }

        /// <summary>画像を既定のアプリで開く（画像のダブルクリック・右クリックの「画像を開く」）。</summary>
        /// <param name="a_src">画像の参照先。</param>
        private void OpenImage(string a_src)
        {
            string src = (a_src ?? string.Empty).Trim();
            if (Uri.TryCreate(src, UriKind.Absolute, out Uri uri) && ("http" == uri.Scheme || "https" == uri.Scheme) &&
                !string.Equals(uri.Host, TMP_HOST, StringComparison.OrdinalIgnoreCase))
            {
                OpenUrl(src);
                return;
            }
            string path = ResolveImagePath(src);
            if (null == path || !File.Exists(path))
            {
                PostError($"この画像は開けません（元のファイルが見つからないか、埋め込みの画像です）。\n{src}");
                return;
            }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                PostError($"画像を開けませんでした。\n{path}\n{ex.Message}");
            }
        }

        /// <summary>画像の参照先を、ファイルのパスにする（ファイルではない参照先・解決できない場合は null）。</summary>
        /// <param name="a_src">画像の参照先（文書に書かれたまま）。</param>
        /// <returns>ファイルのフルパス（存在するかは確かめない）。</returns>
        private string ResolveImagePath(string a_src)
        {
            try
            {
                if (a_src.StartsWith($"https://{TMP_HOST}/", StringComparison.OrdinalIgnoreCase))
                {
                    string name = Path.GetFileName(Uri.UnescapeDataString(new Uri(a_src).AbsolutePath));
                    return 0 < name.Length ? Path.Combine(m_tempImageDir, name) : null;
                }
                if (a_src.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                {
                    return new Uri(a_src).LocalPath;
                }
                if (a_src.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || a_src.StartsWith("//") ||
                    (Uri.TryCreate(a_src, UriKind.Absolute, out Uri uri) && !uri.IsFile))
                {
                    return null;
                }
                string pathPart = a_src.Split('?', '#')[0].Replace('/', Path.DirectorySeparatorChar);
                if (Path.IsPathRooted(pathPart) && pathPart.Length >= 2 && (':' == pathPart[1] || pathPart.StartsWith(@"\\")))
                {
                    return Path.GetFullPath(pathPart);
                }
                if (string.IsNullOrEmpty(m_docFolder))
                {
                    return null;
                }
                string full = Path.GetFullPath(Path.Combine(m_docFolder, pathPart));
                return File.Exists(full) ? full : Path.GetFullPath(Path.Combine(m_docFolder, Uri.UnescapeDataString(pathPart)));
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ===== 画像の貼り付け・一時フォルダ =====

        /// <summary>貼り付けられた画像を一時フォルダに「clipboard_image_連番.拡張子」で保存し、表示用の仮の URL を
        /// 画面側へ返す（imagePasted を送る）。</summary>
        /// <param name="a_id">画面側が付けた画像の番号（そのまま返す）。</param>
        /// <param name="a_mimeType">画像の種類（image/png 等）。</param>
        /// <param name="a_base64">画像のデータ（Base64）。</param>
        private void SavePastedImage(int a_id, string a_mimeType, string a_base64)
        {
            try
            {
                string ext;
                switch (a_mimeType)
                {
                    case "image/jpeg": ext = ".jpg"; break;
                    case "image/gif": ext = ".gif"; break;
                    case "image/bmp": ext = ".bmp"; break;
                    case "image/webp": ext = ".webp"; break;
                    default: ext = ".png"; break;
                }
                Directory.CreateDirectory(m_tempImageDir);
                string fileName = GenerateSequentialFileName(m_tempImageDir, PASTED_IMAGE_BASE_NAME, ext);
                File.WriteAllBytes(Path.Combine(m_tempImageDir, fileName), Convert.FromBase64String(a_base64));
                Post(new
                {
                    cmd = "imagePasted",
                    id = a_id,
                    url = $"https://{TMP_HOST}/{Uri.EscapeDataString(fileName)}",
                    alt = Path.GetFileNameWithoutExtension(fileName),
                });
            }
            catch (Exception ex)
            {
                PostError($"クリップボードの画像を貼り付けられませんでした。\n{ex.Message}");
            }
        }

        /// <summary>他のアプリが置いた画像のファイル（Excel の一時ファイル等。file:///…）を、貼り付けた画像の一時フォルダへ
        /// コピーする（保存する時に「&lt;ファイル名&gt;.images」へ移る）。結果は imagesImported で返す。</summary>
        /// <param name="a_id">画面側が付けた要求の番号（そのまま返す）。</param>
        /// <param name="a_srcs">画像の参照先の配列。</param>
        private void ImportImages(int a_id, JArray a_srcs)
        {
            var urls = new List<string>();
            foreach (JToken item in a_srcs ?? new JArray())
            {
                string url = null;
                try
                {
                    string src = (string)item;
                    if (Uri.TryCreate(src, UriKind.Absolute, out Uri uri) && uri.IsFile && File.Exists(uri.LocalPath) &&
                        s_imageTypes.ContainsKey(Path.GetExtension(uri.LocalPath)))
                    {
                        Directory.CreateDirectory(m_tempImageDir);
                        string ext = Path.GetExtension(uri.LocalPath).ToLowerInvariant();
                        string fileName = GenerateSequentialFileName(m_tempImageDir, PASTED_IMAGE_BASE_NAME, ext);
                        File.Copy(uri.LocalPath, Path.Combine(m_tempImageDir, fileName));
                        url = $"https://{TMP_HOST}/{Uri.EscapeDataString(fileName)}";
                    }
                }
                catch (Exception ex)
                {
                    Log.Write("貼り付けた画像をコピーできませんでした", ex);
                }
                urls.Add(url);
            }
            Post(new { cmd = "imagesImported", id = a_id, urls });
        }

        /// <summary>保存する Markdown の中の、貼り付けた画像の仮の URL（https://tmp.mdew/...）について、画像を保存先の隣の
        /// 「&lt;ファイル名&gt;.images」フォルダへコピーし、URL をそのフォルダへの相対パスに書き換える（mdew と同じ）。</summary>
        /// <param name="a_markdown">保存する内容。</param>
        /// <param name="a_savePath">保存先のファイルのパス。</param>
        /// <param name="a_replacements">書き換えた URL の対応（仮の URL → 相対パス）を受け取る。</param>
        /// <returns>URL を書き換えた内容。</returns>
        private string RelocateTempImages(string a_markdown, string a_savePath, Dictionary<string, string> a_replacements)
        {
            string prefix = $"https://{TMP_HOST}/";
            if (!a_markdown.Contains(prefix))
            {
                return a_markdown;
            }
            string folderName = Path.GetFileNameWithoutExtension(a_savePath) + ".images";
            string destDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(a_savePath)), folderName);
            MatchCollection urls = Regex.Matches(a_markdown, @"https://tmp\.mdew/[^\s)>""']+");
            string result = a_markdown;
            foreach (Match m in urls)
            {
                string url = m.Value;
                if (a_replacements.ContainsKey(url))
                {
                    continue;
                }
                string src = Path.Combine(m_tempImageDir, Path.GetFileName(Uri.UnescapeDataString(url.Substring(prefix.Length))));
                if (!File.Exists(src))
                {
                    continue;
                }
                Directory.CreateDirectory(destDir);
                string ext = Path.GetExtension(src);
                string fileName = Path.GetFileName(src);
                if (File.Exists(Path.Combine(destDir, fileName)))
                {
                    fileName = GenerateSequentialFileName(destDir, Path.GetFileNameWithoutExtension(src), ext);
                }
                File.Copy(src, Path.Combine(destDir, fileName));
                string rel = folderName + "/" + fileName;
                a_replacements[url] = rel;
                // Markdown の画像記法では、空白や括弧を含むパスは <...> で囲む必要がある
                string dest = rel.IndexOfAny(new[] { ' ', '(', ')' }) >= 0 ? $"<{rel}>" : rel;
                result = result.Replace($"]({url})", $"]({dest})").Replace(url, rel);
            }
            return result;
        }

        /// <summary>
        /// 「名前を付けて保存」で保存先のフォルダやファイル名が変わる場合に、文書内の相対パスの画像を新しい保存先から見て
        /// 同じ相対パスの位置へコピーする（mdew と同じ。変更前のファイル専用の画像フォルダ「&lt;ファイル名&gt;.images」は、
        /// 新しいファイル名のフォルダに変え、文中のパスも書き換える）。
        /// </summary>
        /// <param name="a_markdown">保存する内容。</param>
        /// <param name="a_oldPath">変更前のファイルのパス。</param>
        /// <param name="a_newPath">保存先のファイルのパス。</param>
        /// <param name="a_images">文書内の画像の参照先。</param>
        /// <param name="a_replacements">書き換えたパスの対応（変更前 → 変更後）を受け取る。</param>
        /// <returns>パスを書き換えた内容。</returns>
        private static string RelocateRelativeImages(string a_markdown, string a_oldPath, string a_newPath,
            IReadOnlyList<string> a_images, Dictionary<string, string> a_replacements)
        {
            if (string.IsNullOrEmpty(a_oldPath) || null == a_images || 0 == a_images.Count)
            {
                return a_markdown;
            }
            string oldDir = Path.GetDirectoryName(Path.GetFullPath(a_oldPath));
            string newDir = Path.GetDirectoryName(Path.GetFullPath(a_newPath));
            bool sameFolder = string.Equals(oldDir, newDir, StringComparison.OrdinalIgnoreCase);
            string oldOwnedFolder = Path.GetFileNameWithoutExtension(a_oldPath) + ".images";
            string newOwnedFolder = Path.GetFileNameWithoutExtension(a_newPath) + ".images";
            bool renameOwnedFolder = !string.Equals(oldOwnedFolder, newOwnedFolder, StringComparison.OrdinalIgnoreCase);
            if (sameFolder && !renameOwnedFolder)
            {
                return a_markdown;
            }

            string result = a_markdown;
            foreach (string src in a_images)
            {
                if (string.IsNullOrEmpty(src) || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                    src.StartsWith("//") || Path.IsPathRooted(src) || Uri.TryCreate(src, UriKind.Absolute, out _))
                {
                    continue;
                }
                string pathPart = src.Split('?', '#')[0];
                string relSrc = pathPart.Replace('/', Path.DirectorySeparatorChar);
                if (!File.Exists(Path.Combine(oldDir, relSrc)))
                {
                    relSrc = Uri.UnescapeDataString(relSrc);
                }

                string destRelSrc = relSrc;
                string firstSegment = relSrc.Split(Path.DirectorySeparatorChar)[0];
                if (renameOwnedFolder && string.Equals(firstSegment, oldOwnedFolder, StringComparison.OrdinalIgnoreCase))
                {
                    destRelSrc = newOwnedFolder + relSrc.Substring(firstSegment.Length);
                }
                bool relSrcChanged = !string.Equals(relSrc, destRelSrc, StringComparison.OrdinalIgnoreCase);
                if (sameFolder && !relSrcChanged)
                {
                    continue;
                }

                string fullSrc;
                string fullDest;
                try
                {
                    fullSrc = Path.GetFullPath(Path.Combine(oldDir, relSrc));
                    fullDest = Path.GetFullPath(Path.Combine(newDir, destRelSrc));
                }
                catch (Exception)
                {
                    continue;
                }
                if (string.Equals(fullSrc, fullDest, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!File.Exists(fullDest))
                {
                    if (!File.Exists(fullSrc))
                    {
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(fullDest));
                    File.Copy(fullSrc, fullDest, false);
                }

                if (relSrcChanged)
                {
                    string newSrc = destRelSrc.Replace(Path.DirectorySeparatorChar, '/');
                    a_replacements[src] = newSrc;
                    result = ReplaceImageReference(result, src, newSrc);
                }
            }
            return result;
        }

        /// <summary>Markdown の中の画像の参照先を書き換える（画像記法・HTML の src・リンク定義の箇所だけ。mdew と同じ）。</summary>
        /// <param name="a_markdown">書き換える Markdown。</param>
        /// <param name="a_oldSrc">変更前の参照先。</param>
        /// <param name="a_newSrc">変更後の参照先。</param>
        /// <returns>書き換えた Markdown。</returns>
        private static string ReplaceImageReference(string a_markdown, string a_oldSrc, string a_newSrc)
        {
            bool needsAngle = a_newSrc.IndexOfAny(new[] { ' ', '(', ')' }) >= 0;
            string dest = needsAngle ? $"<{a_newSrc}>" : a_newSrc;
            string result = a_markdown
                .Replace($"](<{a_oldSrc}>", $"]({dest}")
                .Replace($"]({a_oldSrc})", $"]({dest})")
                .Replace($"]({a_oldSrc} ", $"]({dest} ")
                .Replace($"src=\"{a_oldSrc}\"", $"src=\"{a_newSrc}\"")
                .Replace($"src='{a_oldSrc}'", $"src='{a_newSrc}'");
            string encodedOld = a_oldSrc.Replace(" ", "%20");
            if (!string.Equals(encodedOld, a_oldSrc, StringComparison.Ordinal))
            {
                result = result.Replace($"]({encodedOld})", $"]({a_newSrc.Replace(" ", "%20")})")
                    .Replace($"]({encodedOld} ", $"]({a_newSrc.Replace(" ", "%20")} ");
            }
            return Regex.Replace(result, @"^(\s*\[[^\]]+\]:\s*)" + Regex.Escape(a_oldSrc) + @"(?=\s|$)",
                m => m.Groups[1].Value + dest, RegexOptions.Multiline);
        }

        /// <summary>フォルダ内で既存ファイルと重ならない「ベース名_連番.拡張子」を作る（連番は 1 から）。</summary>
        private static string GenerateSequentialFileName(string a_dir, string a_baseName, string a_ext)
        {
            for (int i = 1; ; i++)
            {
                string name = $"{a_baseName}_{i}{a_ext}";
                if (!File.Exists(Path.Combine(a_dir, name)))
                {
                    return name;
                }
            }
        }

        /// <summary>画像として返すファイルの拡張子と、その Content-Type。</summary>
        private static readonly Dictionary<string, string> s_imageTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".gif"] = "image/gif",
                [".bmp"] = "image/bmp", [".webp"] = "image/webp", [".svg"] = "image/svg+xml", [".ico"] = "image/x-icon",
            };

        /// <summary>https://doc.mdew/&lt;世代&gt;/&lt;相対パス&gt; の要求に、開いているファイルのフォルダ基準で画像ファイルを返す。
        /// https://tmp.mdew/&lt;ファイル名&gt; の要求には、貼り付けた画像の一時フォルダの画像を返す（mdew と同じ）。</summary>
        private void ServeDocumentResource(object a_sender, CoreWebView2WebResourceRequestedEventArgs a_args)
        {
            CoreWebView2Environment env = m_webView.CoreWebView2.Environment;
            try
            {
                var uri = new Uri(a_args.Request.Uri);
                string rel = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
                string full = null;
                if (string.Equals(uri.Host, TMP_HOST, StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = Path.GetFileName(rel);
                    full = 0 < fileName.Length ? Path.Combine(m_tempImageDir, fileName) : null;
                }
                else if (!string.Equals(uri.Host, DOC_HOST, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                int slash = rel.IndexOf('/');
                rel = slash >= 0 ? rel.Substring(slash + 1) : string.Empty; // 先頭の世代番号を除く
                if (null == full && !string.IsNullOrEmpty(m_docFolder) && rel.Length > 0)
                {
                    full = Path.GetFullPath(Path.Combine(m_docFolder, rel.Replace('/', Path.DirectorySeparatorChar)));
                }
                if (null == full || !File.Exists(full) || !s_imageTypes.TryGetValue(Path.GetExtension(full), out string mime))
                {
                    a_args.Response = env.CreateWebResourceResponse(null, 404, "Not Found", "Cache-Control: no-store");
                    return;
                }
                var stream = new MemoryStream(File.ReadAllBytes(full));
                a_args.Response = env.CreateWebResourceResponse(stream, 200, "OK",
                    $"Content-Type: {mime}\r\nCache-Control: no-cache\r\nAccess-Control-Allow-Origin: *");
            }
            catch (Exception)
            {
                a_args.Response = env.CreateWebResourceResponse(null, 500, "Error", "Cache-Control: no-store");
            }
        }

        // ===== Excel 等とのコピー（web\js\clipboard.js の「Excel用にコピー」「表をコピー」） =====

        /// <summary>Excel 等へ貼り付ける画像を置く一時フォルダ（コピーのたびに古いものを消す）。</summary>
        private static readonly string s_clipboardTempDir = Path.Combine(Path.GetTempPath(), "mdevs", "clipboard_images");

        /// <summary>HTML と文字をクリップボードに書き込む（画像は一時フォルダへコピーしてその場所にする。mdew と同じ）。</summary>
        /// <param name="a_html">HTML の断片。</param>
        /// <param name="a_text">文字。</param>
        private void CopyHtml(string a_html, string a_text)
        {
            string html = Regex.Replace(a_html, "(<img\\b[^>]*?\\bsrc=\")([^\"]*)(\")",
                m => m.Groups[1].Value + System.Net.WebUtility.HtmlEncode(CopyImageForClipboard(System.Net.WebUtility.HtmlDecode(m.Groups[2].Value))) + m.Groups[3].Value,
                RegexOptions.IgnoreCase);
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, (a_text ?? string.Empty).Replace("\r\n", "\n").Replace("\n", "\r\n"));
            data.SetData(DataFormats.Html, BuildHtmlClipboardFragment(html));
            for (int i = 0; ; i++)
            {
                try
                {
                    Clipboard.SetDataObject(data, true);
                    return;
                }
                catch (Exception ex) when (i < 5)
                {
                    Log.Write("クリップボードに書き込めませんでした（再試行）", ex);
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        /// <summary>Excel 等に貼り付ける画像を一時フォルダへコピーし、その file:/// の URL を返す（mdew と同じ）。</summary>
        /// <param name="a_url">画像の参照先。</param>
        /// <returns>一時フォルダの画像の URL（コピーできない場合はそのまま）。</returns>
        private static string CopyImageForClipboard(string a_url)
        {
            try
            {
                if (!Uri.TryCreate(a_url, UriKind.Absolute, out Uri uri) || !uri.IsFile || !File.Exists(uri.LocalPath))
                {
                    return a_url;
                }
                Directory.CreateDirectory(s_clipboardTempDir);
                DateTime cutoff = DateTime.Now.AddMinutes(-10);
                foreach (string old in Directory.GetFiles(s_clipboardTempDir))
                {
                    try
                    {
                        if (File.GetLastWriteTime(old) < cutoff) File.Delete(old);
                    }
                    catch (Exception)
                    {
                        // 使用中等で消せないファイルは、次の機会に消す
                    }
                }
                string ext = Path.GetExtension(uri.LocalPath).ToLowerInvariant();
                string dest = Path.Combine(s_clipboardTempDir, Guid.NewGuid().ToString("N") + ext);
                File.Copy(uri.LocalPath, dest, true);
                if (".bmp" != ext)
                {
                    using (var stream = new FileStream(dest, FileMode.Append, FileAccess.Write))
                    {
                        byte[] tail = Guid.NewGuid().ToByteArray();
                        stream.Write(tail, 0, tail.Length);
                    }
                }
                return new Uri(dest).AbsoluteUri;
            }
            catch (Exception)
            {
                return a_url;
            }
        }

        /// <summary>HTML の断片を、クリップボードの CF_HTML の形にする（mdew と同じ）。</summary>
        /// <param name="a_fragment">HTML の断片。</param>
        /// <returns>CF_HTML の文字列。</returns>
        private static string BuildHtmlClipboardFragment(string a_fragment)
        {
            const string HTML_PREFIX = "<html><body><!--StartFragment-->";
            const string HTML_SUFFIX = "<!--EndFragment--></body></html>";
            const string HEADER_TEMPLATE = "Version:0.9\r\nStartHTML:{0:0000000000}\r\nEndHTML:{1:0000000000}\r\n" +
                "StartFragment:{2:0000000000}\r\nEndFragment:{3:0000000000}\r\n";
            int startHtml = Encoding.UTF8.GetByteCount(string.Format(HEADER_TEMPLATE, 0, 0, 0, 0));
            int startFragment = startHtml + Encoding.UTF8.GetByteCount(HTML_PREFIX);
            int endFragment = startFragment + Encoding.UTF8.GetByteCount(a_fragment);
            int endHtml = endFragment + Encoding.UTF8.GetByteCount(HTML_SUFFIX);
            return string.Format(HEADER_TEMPLATE, startHtml, endHtml, startFragment, endFragment) + HTML_PREFIX + a_fragment + HTML_SUFFIX;
        }

        // ===== 後始末 =====

        /// <summary>WebView2 を閉じ、貼り付けた画像の一時フォルダを削除する。</summary>
        public void Dispose()
        {
            try
            {
                m_webView.Dispose();
            }
            catch (Exception ex)
            {
                Log.Write("WebView2 を閉じる時にエラー", ex);
            }
            try
            {
                if (Directory.Exists(m_tempImageDir))
                {
                    Directory.Delete(m_tempImageDir, true);
                }
            }
            catch (Exception)
            {
                // 削除できなくても致命的ではない
            }
        }
    }
}
