// Text.cs
//
// mdevs の画面の文字（お知らせ・ダイアログ等）の英語対応（日本語・英語の切り替え）。
// 画面側（web\js\i18n.js）と同じく、日本語の文字をそのままキーにして書き（Text.T("表を挿入")）、英語の時はこの辞書の
// 英語を返す。文字の中の {0}・{1}… は、T の 2 つ目以降の引数で置き換える（string.Format と同じ）。
// 言語は、設定の「言語」（MdevsSettings.Language。自動の時は Visual Studio の表示言語）。

using System.Collections.Generic;

namespace mdevs
{
    /// <summary>画面の文字の英語対応。</summary>
    internal static class Text
    {
        /// <summary>英語の辞書（日本語 → 英語）。</summary>
        private static readonly Dictionary<string, string> EN = new Dictionary<string, string>
        {
            ["Markdown の編集画面を表示できませんでした（WebView2 の準備に失敗しました）。\n「出力」ウィンドウの「mdevs」に詳しい内容があります。\n\n{0}"] =
                "Could not show the Markdown editor (failed to prepare WebView2).\nSee \"mdevs\" in the Output window for details.\n\n{0}",
            ["処理中にエラーが発生しました。\n{0}"] = "An error occurred.\n{0}",
            ["このリンクは開けません。\n{0}"] = "This link cannot be opened.\n{0}",
            ["リンク先を開けませんでした。\n{0}\n{1}"] = "Could not open the link.\n{0}\n{1}",
            ["リンク先のファイルが見つかりません。\n{0}"] = "The linked file was not found.\n{0}",
            ["この画像は開けません（元のファイルが見つからないか、埋め込みの画像です）。\n{0}"] =
                "This image cannot be opened (the original file was not found, or it is an embedded image).\n{0}",
            ["画像を開けませんでした。\n{0}\n{1}"] = "Could not open the image.\n{0}\n{1}",
            ["クリップボードの画像を貼り付けられませんでした。\n{0}"] = "Could not paste the image from the clipboard.\n{0}",
            ["保存できませんでした。\n{0}"] = "Could not save the file.\n{0}",
            ["{0} は、ほかのアプリで変更されました。\n保存していない変更を破棄して、読み直しますか？"] =
                "{0} has been changed by another app.\nDiscard the unsaved changes and reload it?",
            ["Markdown ファイル (*.md)\n*.md\n"] = "Markdown files (*.md)\n*.md\n",
            ["キャンセル"] = "Cancel",
            ["表を挿入"] = "Insert Table",
            ["表のサイズを指定してください"] = "Specify the size of the table",
            ["行数（見出し行を含む）"] = "Rows (including the header row)",
            ["列数"] = "Columns",
            ["挿入"] = "Insert",
            ["リンク"] = "Link",
            ["リンク先のURLを入力してください"] = "Enter the URL of the link",
        };

        /// <summary>英語で表示するか（設定の「言語」。MdevsSettings.Language）。</summary>
        public static bool IsEnglish => "en" == MdevsSettings.Language;

        /// <summary>文字を、今の言語にする。</summary>
        /// <param name="a_text">日本語の文字（辞書のキー）。</param>
        /// <param name="a_args">{0}・{1}… に入れる値。</param>
        /// <returns>今の言語の文字。</returns>
        public static string T(string a_text, params object[] a_args)
        {
            string text = IsEnglish && EN.TryGetValue(a_text, out string en) ? en : a_text;
            return 0 < a_args.Length ? string.Format(text, a_args) : text;
        }
    }
}
