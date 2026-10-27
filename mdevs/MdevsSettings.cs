// MdevsSettings.cs
//
// mdevs（mdew の Visual Studio 版）の一部。
// mdevs の設定（言語・表示スタイル・文字の色・本文の幅・段落中の改行）の読み込みと、変更の通知。

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;

namespace mdevs
{
    /// <summary>
    /// mdevs の設定。Visual Studio 2026 の新しい「オプション」の画面（Unified Settings。registration.json）の値を読む。
    /// 新しい画面を使っていない時（従来の画面に切り替えている時）は、従来の画面のページ（MdevsOptionsPage）の値を読む
    /// （両方の値は、Visual Studio が registration.json の migration で同じにしておく）。
    /// </summary>
    internal static class MdevsSettings
    {
        // registration.json の設定の名前（moniker）
        private const string DOC_STYLE = "mdevs.display.docStyle";
        private const string TEXT_COLOR = "mdevs.display.textColor";
        private const string DOC_WIDTH = "mdevs.display.docWidth";
        private const string SOFT_BREAK = "mdevs.display.softBreak";
        private const string LANGUAGE = "mdevs.display.language";

        /// <summary>画面の言語（"ja"：日本語、"en"：英語）。設定の「言語」が自動の時は、Visual Studio の表示言語（日本語なら
        /// 日本語、それ以外は英語）。設定を読んだ時（CreateMessage・設定が変わった時）に決まる。Text.cs 参照。</summary>
        public static string Language { get; private set; } = ToLanguage(null);

        /// <summary>設定が変わった時（UI スレッドで呼ぶ）。</summary>
        public static event Action Changed;

        /// <summary>パッケージ（従来の画面のページの取得に使う）。</summary>
        private static AsyncPackage s_package;
        /// <summary>UI スレッドの Dispatcher（設定の変更を、UI スレッドで知らせるため）。</summary>
        private static System.Windows.Threading.Dispatcher s_dispatcher;
        /// <summary>新しい画面の設定の変更の通知の登録（Dispose で解除。パッケージが動いている間は持っておく）。</summary>
        private static IDisposable s_subscription;

        /// <summary>Unified Settings のサービス（型が SDK に含まれないため、ここで宣言する）。</summary>
        [Guid("e3684f31-344e-42ea-9047-b620fdc7ac25")]
        private interface SVsUnifiedSettingsManager
        {
        }

        /// <summary>初期化（パッケージの初期化で、UI スレッドで呼ぶ）。新しい画面での設定の変更を受け取れるようにする。</summary>
        /// <param name="a_package">パッケージ。</param>
        public static void Initialize(AsyncPackage a_package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            s_package = a_package;
            s_dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            MdevsOptionsPage.Changed += RaiseChanged; // 従来の画面で「OK」を押した時
            try
            {
                s_subscription = Subscribe();
            }
            catch (Exception ex)
            {
                Log.Write("新しいオプションの画面の設定の変更を受け取れません（従来の画面の設定を使います）", ex);
            }
            // 画面の言語（Language）を、最初に読んでおく（ダイアログ・お知らせの文字に使う）
            try
            {
                CreateMessage();
            }
            catch (Exception ex)
            {
                Log.Write("設定を読めませんでした", ex);
            }
        }

        /// <summary>新しい画面での設定の変更を受け取る登録をする。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDisposable Subscribe()
        {
            ISettingsManager manager = Manager();
            if (null == manager)
            {
                return null;
            }
            // 通知は UI スレッド以外から届くことがあるので、UI スレッドで知らせる
            return manager.GetReader().SubscribeToChanges(a_update => UiThread.Post(s_dispatcher, RaiseChanged), "mdevs.*");
        }

        /// <summary>設定が変わったことを知らせる。</summary>
        private static void RaiseChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log.Write("設定が変わりました。");
            CreateMessage(); // 言語（Language）を読み直す
            Changed?.Invoke();
        }

        /// <summary>Unified Settings のサービス。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ISettingsManager Manager()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return Package.GetGlobalService(typeof(SVsUnifiedSettingsManager)) as ISettingsManager;
        }

        /// <summary>
        /// 画面側に送る設定（settings のメッセージ。web\js\main.js の applySettings）。読めない時は null（画面側は前回の表示のまま）。
        /// </summary>
        public static object CreateMessage()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string docStyle = null;
            string textColor = null;
            string docWidth = null;
            string softBreak = null;
            string language = null;
            try
            {
                ReadUnified(out docStyle, out textColor, out docWidth, out softBreak, out language);
            }
            catch (Exception ex) when (ex is TypeLoadException || ex is MissingMethodException)
            {
                // Unified Settings の無い Visual Studio：従来の画面の値を使う
            }
            if (null == docStyle || null == textColor || null == docWidth || null == softBreak || null == language)
            {
                // 新しい画面を使っていない（従来の画面に切り替えている）等：従来の画面のページの値を使う
                if (!(s_package?.GetDialogPage(typeof(MdevsOptionsPage)) is MdevsOptionsPage page))
                {
                    return null;
                }
                page.LoadSettingsFromStorage();
                docStyle = docStyle ?? page.DocStyle.ToString();
                textColor = textColor ?? page.TextColor.ToString();
                docWidth = docWidth ?? page.DocWidth.ToString();
                softBreak = softBreak ?? page.SoftBreak.ToString();
                language = language ?? page.Language.ToString();
            }
            Language = ToLanguage(language);
            return new
            {
                cmd = "settings",
                docStyle = nameof(DocStyle.VSCode) == docStyle ? "vscode" : "github",
                textColor = nameof(TextColor.Soft) == textColor ? "soft" : "sharp",
                fullWidth = nameof(DocWidth.Full) == docWidth,
                joinSoftBreaks = nameof(SoftBreak.Source) != softBreak,
                // 画面の言語（"ja"・"en"。web\js\i18n.js）
                language = Language,
            };
        }

        /// <summary>設定の「言語」の値から、画面の言語を決める。</summary>
        /// <param name="a_setting">設定の値（UILanguage の値の名前。読めなかった時は null＝自動）。</param>
        /// <returns>"ja"：日本語、"en"：英語。</returns>
        private static string ToLanguage(string a_setting)
        {
            if (nameof(UILanguage.Japanese) == a_setting)
            {
                return "ja";
            }
            if (nameof(UILanguage.English) == a_setting)
            {
                return "en";
            }
            return "ja" == CultureInfo.CurrentUICulture.TwoLetterISOLanguageName ? "ja" : "en";
        }

        /// <summary>新しい画面の設定を読む（読めなかった値は null）。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReadUnified(out string a_docStyle, out string a_textColor, out string a_docWidth, out string a_softBreak,
            out string a_language)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            a_docStyle = a_textColor = a_docWidth = a_softBreak = a_language = null;
            ISettingsManager manager = Manager();
            if (null == manager)
            {
                return;
            }
            ISettingsReader reader = manager.GetReader();
            a_docStyle = ReadString(reader, DOC_STYLE);
            a_textColor = ReadString(reader, TEXT_COLOR);
            a_docWidth = ReadString(reader, DOC_WIDTH);
            a_softBreak = ReadString(reader, SOFT_BREAK);
            a_language = ReadString(reader, LANGUAGE);
        }

        /// <summary>文字列の設定を読む（従来の画面に切り替えている時等、読めなければ null）。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ReadString(ISettingsReader a_reader, string a_moniker)
        {
            SettingRetrieval<string> result = a_reader.GetValue<string>(a_moniker, SettingReadOptions.NoRequirements);
            if (SettingRetrievalOutcome.Success == result.Outcome)
            {
                return result.Value;
            }
            Log.Write($"設定 {a_moniker} を新しいオプションの画面から読めませんでした（{result.Outcome}）。");
            return null;
        }
    }
}
