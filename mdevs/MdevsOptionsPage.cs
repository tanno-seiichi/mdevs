// MdevsOptionsPage.cs
//
// mdevs（mdew の Visual Studio 版）の一部。
// Visual Studio の「ツール」→「オプション」の mdevs の設定（言語・表示スタイル・文字の色・本文の幅・段落中の改行）。
// オプションの画面は Visual Studio が表示するため、mdevs の「言語」の設定では切り替わらない。名前・説明は英語と日本語を並べて書く。
// mdew のメニュー「表示」の同じ名前の項目に当たる。

using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace mdevs
{
    /// <summary>画面の言語（自動：Visual Studio の表示言語に合わせる）。</summary>
    [TypeConverter(typeof(DisplayNameEnumConverter))]
    public enum UILanguage
    {
        [Description("Auto (自動)")]
        Auto,
        [Description("日本語 (Japanese)")]
        Japanese,
        [Description("English (英語)")]
        English,
    }

    /// <summary>表示スタイル（mdew のメニュー「表示」→「表示スタイル」）。</summary>
    [TypeConverter(typeof(DisplayNameEnumConverter))]
    public enum DocStyle
    {
        [Description("GitHub style (GitHubスタイル)")]
        GitHub,
        [Description("VS Code style (VSCodeスタイル)")]
        VSCode,
    }

    /// <summary>文字の色（mdew のメニュー「表示」→「文字の色」）。</summary>
    [TypeConverter(typeof(DisplayNameEnumConverter))]
    public enum TextColor
    {
        [Description("Sharp (くっきりした色)")]
        Sharp,
        [Description("Easy on the eyes (目にやさしい色)")]
        Soft,
    }

    /// <summary>本文の幅（mdew のメニュー「表示」→「本文の幅」）。</summary>
    [TypeConverter(typeof(DisplayNameEnumConverter))]
    public enum DocWidth
    {
        [Description("Readable width, max 884px (読みやすい幅にする（最大 884px）)")]
        Limited,
        [Description("Fit to the editor width (編集ペインの幅に合わせる)")]
        Full,
    }

    /// <summary>段落中の改行（mdew のメニュー「表示」→「段落中の改行」）。</summary>
    [TypeConverter(typeof(DisplayNameEnumConverter))]
    public enum SoftBreak
    {
        [Description("Do not break until a blank line (空行が入るまで改行しない)")]
        Join,
        [Description("Break lines as in the source (ソースの通りに改行する)")]
        Source,
    }

    /// <summary>
    /// 従来の「オプション」の画面（Visual Studio 2026 で、新しい画面を使わない設定にしている時）の、「mdevs」→「表示」のページ。
    /// 新しい画面では、registration.json の設定が代わりに表示される（このページは表示されない。値は Visual Studio が
    /// registration.json の migration で、このページの値と同じにしておく）。値の読み込みは MdevsSettings。
    /// 「OK」で変えた設定は、開いているすべての編集ペインにすぐに反映する（Changed）。
    /// </summary>
    [Guid(GuidString)]
    public sealed class MdevsOptionsPage : DialogPage
    {
        /// <summary>このページの GUID（registration.json の legacyOptionPageId と同じ）。</summary>
        public const string GuidString = "0edecf49-2fc1-4fbd-b44d-494d53d7fd05";

        /// <summary>設定が変わった時（「OK」を押した時）。</summary>
        public static event Action Changed;

        [Category("Display (表示)")]
        [DisplayName("Language (言語)")]
        [Description("Language of the editor screen, messages, dialogs, and help. Auto follows the Visual Studio display language (Japanese if it is Japanese, otherwise English). / 編集画面・お知らせ・ダイアログ・ヘルプの言語。自動の時は Visual Studio の表示言語に合わせる（日本語の時は日本語、それ以外は英語）。")]
        public UILanguage Language { get; set; } = UILanguage.Auto;

        [Category("Display (表示)")]
        [DisplayName("Display Style (表示スタイル)")]
        [Description("The look of documents. GitHub style looks like Markdown on GitHub; VS Code style looks like the Markdown preview of Visual Studio Code. / 文書の見た目。GitHubスタイルは GitHub の Markdown に近い見た目、VSCodeスタイルは Visual Studio Code の Markdown のプレビューに近い見た目。")]
        public DocStyle DocStyle { get; set; } = DocStyle.GitHub;

        [Category("Display (表示)")]
        [DisplayName("Text Color (文字の色)")]
        [Description("Sharp is almost black; Easy on the eyes is a slightly lighter gray (with a dark theme, a light color and a slightly darker color, respectively). / くっきりした色はほぼ黒、目にやさしい色は少し薄い灰色（暗いテーマの時は、それぞれ明るい色・少し暗い色）。")]
        public TextColor TextColor { get; set; } = TextColor.Sharp;

        [Category("Display (表示)")]
        [DisplayName("Text Width (本文の幅)")]
        [Description("A readable width (centered, max 884px), or the full width of the editor. / 読みやすい幅（最大 884px で中央に置く）か、編集ペインの幅いっぱいに広げるか。")]
        public DocWidth DocWidth { get; set; } = DocWidth.Limited;

        [Category("Display (表示)")]
        [DisplayName("Line Breaks in Paragraphs (段落中の改行)")]
        [Description("How line breaks within a paragraph (line breaks without a blank line) are shown. The saved content is the same either way. / 段落の中の改行（空行を挟まない改行）の表示。保存する内容は、どちらでも変わらない。")]
        public SoftBreak SoftBreak { get; set; } = SoftBreak.Join;

        /// <summary>「OK」を押した時：保存して、開いている編集ペインに反映する。</summary>
        protected override void OnApply(PageApplyEventArgs a_args)
        {
            base.OnApply(a_args);
            if (ApplyKind.Apply == a_args.ApplyBehavior)
            {
                Log.Write($"設定を変えました（言語：{Language}、表示スタイル：{DocStyle}、文字の色：{TextColor}、本文の幅：{DocWidth}、段落中の改行：{SoftBreak}）。");
                Changed?.Invoke();
            }
        }
    }

    /// <summary>列挙型の値を、Description の名前（英語と日本語）で表示する（オプションのページの選択肢）。保存する値は、列挙型の
    /// 値の名前（GitHub 等。registration.json の enum と同じ）のまま。</summary>
    public sealed class DisplayNameEnumConverter : EnumConverter
    {
        /// <param name="a_type">列挙型。</param>
        public DisplayNameEnumConverter(Type a_type) : base(a_type)
        {
        }

        /// <summary>値の表示名。</summary>
        private string NameOf(object a_value)
        {
            FieldInfo field = EnumType.GetField(Enum.GetName(EnumType, a_value) ?? string.Empty);
            var description = field?.GetCustomAttribute<DescriptionAttribute>();
            return description?.Description ?? a_value.ToString();
        }

        public override object ConvertTo(ITypeDescriptorContext a_context, CultureInfo a_culture, object a_value, Type a_destinationType)
        {
            // 保存（ConvertToInvariantString）は値の名前のまま
            if (typeof(string) == a_destinationType && null != a_value && EnumType.IsInstanceOfType(a_value) &&
                !CultureInfo.InvariantCulture.Equals(a_culture))
            {
                return NameOf(a_value);
            }
            return base.ConvertTo(a_context, a_culture, a_value, a_destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext a_context, CultureInfo a_culture, object a_value)
        {
            if (a_value is string text)
            {
                foreach (object value in Enum.GetValues(EnumType))
                {
                    if (NameOf(value) == text || Enum.GetName(EnumType, value) == text)
                    {
                        return value;
                    }
                }
                // 知らない値（古い保存内容等）は既定（最初の値）
                return Enum.GetValues(EnumType).Cast<object>().First();
            }
            return base.ConvertFrom(a_context, a_culture, a_value);
        }
    }
}
