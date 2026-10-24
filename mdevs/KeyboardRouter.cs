// KeyboardRouter.cs
//
// mdevs（mdew の Visual Studio 版）の一部。
// 編集画面（WebView2）でのキー入力を、Visual Studio のショートカットと編集画面とに振り分ける。

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;

namespace mdevs
{
    /// <summary>
    /// キー入力の振り分け。WebView2 は、キー入力を先に Visual Studio に渡す設定にしてある（MarkdownWebHost の
    /// AllowHostInputProcessing）ので、Ctrl+S・F5・Ctrl+Shift+B・Ctrl+Tab・Alt のメニュー等は、Visual Studio の設定どおりに動く。
    /// ただし Visual Studio は、ショートカットに割り当てたキー（Ctrl+B・Ctrl+C・Delete 等）を、編集ペインより先に処理してしまう。
    /// そこで、Visual Studio がキー入力を受け取った直後（WH_GETMESSAGE のフック）に、編集画面で使うキー（IsEditorKey）だけを
    /// 編集画面に直接渡し、Visual Studio には何もしないメッセージ（WM_NULL）として渡す。
    /// Ctrl+F・Ctrl+H は、Visual Studio の「検索と置換」のダイアログを開く（小さな検索の枠は、テキストエディター用で使えないため）。
    /// </summary>
    internal static class KeyboardRouter
    {
        private const int WH_GETMESSAGE = 3;
        private const int PM_REMOVE = 1;
        private const int WM_NULL = 0x0000;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        /// <summary>キー入力の行き先。</summary>
        private enum Target
        {
            /// <summary>Visual Studio（そのまま）。</summary>
            VisualStudio,
            /// <summary>編集画面。</summary>
            Editor,
            /// <summary>Visual Studio の「検索」のダイアログ。</summary>
            Find,
            /// <summary>Visual Studio の「置換」のダイアログ。</summary>
            Replace,
        }

        /// <summary>Windows のメッセージ（MSG）。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public IntPtr hwnd;
            public int message;
            public IntPtr wParam;
            public IntPtr lParam;
            public int time;
            public int ptX;
            public int ptY;
        }

        private delegate IntPtr HookProc(int a_code, IntPtr a_wParam, IntPtr a_lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int a_hookId, HookProc a_proc, IntPtr a_module, int a_threadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr a_hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr a_hook, int a_code, IntPtr a_wParam, IntPtr a_lParam);

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref NativeMessage a_message);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref NativeMessage a_message);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int a_virtualKey);

        /// <summary>フックの処理（ガベージコレクションで消えないよう、フィールドに持っておく）。</summary>
        private static HookProc s_proc;
        /// <summary>フック。</summary>
        private static IntPtr s_hook;
        /// <summary>ログに書いたキー（同じキーは最初の1回だけ書く。動作の確認用）。</summary>
        private static readonly HashSet<string> s_loggedKeys = new HashSet<string>();
        /// <summary>MSG の message の位置。</summary>
        private static readonly int s_messageOffset = Marshal.OffsetOf(typeof(NativeMessage), "message").ToInt32();

        /// <summary>振り分けを始める（Visual Studio の UI スレッドで呼ぶ。始めていれば何もしない）。</summary>
        public static void Install()
        {
            if (IntPtr.Zero != s_hook)
            {
                return;
            }
            s_proc = OnGetMessage;
            s_hook = SetWindowsHookEx(WH_GETMESSAGE, s_proc, IntPtr.Zero, GetCurrentThreadId());
            if (IntPtr.Zero == s_hook)
            {
                Log.Write($"キー入力の振り分けを始められませんでした（エラー {Marshal.GetLastWin32Error()}）。");
            }
            else
            {
                Log.Write("キー入力の振り分けを始めました。");
            }
        }

        /// <summary>振り分けをやめる（編集ペインをすべて閉じた時）。</summary>
        public static void Uninstall()
        {
            if (IntPtr.Zero != s_hook)
            {
                UnhookWindowsHookEx(s_hook);
                s_hook = IntPtr.Zero;
                Log.Write("キー入力の振り分けを終えました。");
            }
        }

        /// <summary>メッセージを取り出した時（Visual Studio が処理する前）に呼ばれる。</summary>
        private static IntPtr OnGetMessage(int a_code, IntPtr a_wParam, IntPtr a_lParam)
        {
            if (0 <= a_code && PM_REMOVE == (a_wParam.ToInt64() & PM_REMOVE))
            {
                try
                {
                    // フックは Visual Studio の UI スレッドに付けてある（Install）ので、UI スレッドで呼ばれる
                    Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                    Route(a_lParam);
                }
                catch (Exception ex)
                {
                    Log.Write("キー入力の振り分けでエラー", ex);
                }
            }
            return CallNextHookEx(s_hook, a_code, a_wParam, a_lParam);
        }

        /// <summary>編集画面へのキー入力（キーを押した時）を振り分ける。</summary>
        /// <param name="a_messagePtr">メッセージ（MSG）の場所。Visual Studio に渡さない時は、WM_NULL に書き換える。</param>
        private static void Route(IntPtr a_messagePtr)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            var message = (NativeMessage)Marshal.PtrToStructure(a_messagePtr, typeof(NativeMessage));
            if (WM_KEYDOWN != message.message && WM_SYSKEYDOWN != message.message)
            {
                return;
            }
            MarkdownEditorPane pane = MarkdownEditorPane.FindByWindow(message.hwnd);
            if (null == pane)
            {
                LogOtherWindow(message.hwnd);
                return;
            }
            int key = (int)(message.wParam.ToInt64() & 0xFFFF);
            bool ctrl = IsKeyDown(0x11);
            bool shift = IsKeyDown(0x10);
            bool alt = IsKeyDown(0x12);
            Target target = GetTarget(key, ctrl, shift, alt);
            LogKey(key, ctrl, shift, alt, target);
            if (Target.VisualStudio == target)
            {
                return;
            }
            // Visual Studio には、何もしないメッセージとして渡す
            Marshal.WriteInt32(a_messagePtr, s_messageOffset, WM_NULL);
            switch (target)
            {
                case Target.Editor:
                    TranslateMessage(ref message); // 文字の入力（WM_CHAR）・IME のため
                    DispatchMessage(ref message);
                    break;
                case Target.Find:
                    pane.PostVsCommand(VSConstants.GUID_VSStandardCommandSet97, (uint)VSConstants.VSStd97CmdID.Find);
                    break;
                case Target.Replace:
                    pane.PostVsCommand(VSConstants.GUID_VSStandardCommandSet97, (uint)VSConstants.VSStd97CmdID.Replace);
                    break;
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr a_window, System.Text.StringBuilder a_name, int a_size);

        /// <summary>編集画面の外へのキー入力をログに書いた回数（動作の確認用。最初の数回だけ書く）。</summary>
        private static int s_otherWindowLogCount;

        /// <summary>編集画面の外のウィンドウへのキー入力を、ログに書く（最初の数回だけ。宛先のウィンドウの種類の確認用）。</summary>
        private static void LogOtherWindow(IntPtr a_window)
        {
            if (3 <= s_otherWindowLogCount)
            {
                return;
            }
            s_otherWindowLogCount++;
            var name = new System.Text.StringBuilder(256);
            GetClassName(a_window, name, name.Capacity);
            Log.Write($"編集画面の外へのキー入力（ウィンドウ {name}）。Visual Studio が処理します。");
        }

        /// <summary>キーが押されているか。</summary>
        private static bool IsKeyDown(int a_virtualKey) => 0 != (GetKeyState(a_virtualKey) & 0x8000);

        /// <summary>
        /// キーの行き先。編集画面で使うキー：
        /// ・Ctrl・Alt なし：ファンクションキー以外のすべて（文字・数字・記号・Enter・Tab・Esc・Delete・BackSpace・矢印・
        /// 　Home・End・PageUp・PageDown・IME 等）。ファンクションキーは F12（開発者ツール）だけ
        /// ・Ctrl（＋Shift）：すべて選択・コピー・切り取り・貼り付け（A・C・X・V・Insert）、元に戻す・やり直し（Z・Y）、太字・斜体・
        /// 　下線（B・I・U）、インラインコード（Ctrl+Shift+`。日本語キーボードは @）、表示倍率（+・;・-・0・テンキー）、
        /// 　単語単位の移動・削除（矢印・Home・End・BackSpace・Delete）、表の行の挿入（Enter）
        /// ・Alt+Shift+5：取り消し線
        /// Ctrl+F・Ctrl+H は「検索と置換」のダイアログ。それ以外は Visual Studio。
        /// </summary>
        private static Target GetTarget(int a_key, bool a_ctrl, bool a_shift, bool a_alt)
        {
            if (a_alt)
            {
                return !a_ctrl && a_shift && 0x35 == a_key ? Target.Editor : Target.VisualStudio; // Alt+Shift+5
            }
            if (a_ctrl)
            {
                switch (a_key)
                {
                    case 0x46: // F
                        return a_shift ? Target.VisualStudio : Target.Find;
                    case 0x48: // H
                        return a_shift ? Target.VisualStudio : Target.Replace;
                    case 0x41: // A
                    case 0x42: // B
                    case 0x43: // C
                    case 0x49: // I
                    case 0x55: // U
                    case 0x56: // V
                    case 0x58: // X
                    case 0x59: // Y
                    case 0x5A: // Z
                    case 0x2D: // Insert
                    case 0x08: // BackSpace
                    case 0x2E: // Delete
                    case 0x0D: // Enter
                    case 0x25: // ←
                    case 0x26: // ↑
                    case 0x27: // →
                    case 0x28: // ↓
                    case 0x24: // Home
                    case 0x23: // End
                    case 0xC0: // `（日本語キーボードは @）
                    case 0xBB: // +・=（日本語キーボードは ;）
                    case 0xBA: // ;（英語キーボード）
                    case 0xBD: // -
                    case 0x30: // 0
                    case 0x6B: // テンキーの +
                    case 0x6D: // テンキーの -
                    case 0x60: // テンキーの 0
                        return Target.Editor;
                    default:
                        return Target.VisualStudio;
                }
            }
            if (0x70 <= a_key && a_key <= 0x87)
            {
                return 0x7B == a_key && !a_shift ? Target.Editor : Target.VisualStudio; // ファンクションキーは F12 だけ
            }
            return Target.Editor;
        }

        /// <summary>キーの行き先をログに書く（Ctrl・Alt 付きのキーとファンクションキーだけ。同じキーは最初の1回だけ）。</summary>
        private static void LogKey(int a_key, bool a_ctrl, bool a_shift, bool a_alt, Target a_target)
        {
            if (!a_ctrl && !a_alt && !(0x70 <= a_key && a_key <= 0x87))
            {
                return;
            }
            if (0x10 <= a_key && a_key <= 0x12)
            {
                return; // Shift・Ctrl・Alt だけ
            }
            string name = (a_ctrl ? "Ctrl+" : "") + (a_shift ? "Shift+" : "") + (a_alt ? "Alt+" : "") +
                System.Windows.Input.KeyInterop.KeyFromVirtualKey(a_key);
            if (s_loggedKeys.Add(name))
            {
                string target = Target.Editor == a_target ? "編集画面" : Target.VisualStudio == a_target ? "Visual Studio"
                    : "Visual Studio の「検索と置換」";
                Log.Write($"キー {name} → {target}");
            }
        }
    }
}
