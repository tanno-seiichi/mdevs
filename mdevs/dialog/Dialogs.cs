// Dialogs.cs
//
// mdevs（mdew の Visual Studio 版）の一部。
// 右クリックのメニュー「表を挿入…」「リンクにする…」のダイアログ（mdew の dialog\TableSizeDialog・LinkInputDialog と
// 同じ見た目・項目）。mdevs では XAML をビルドしないため、画面はコードで組み立て、部品の見た目（DialogStyles.xaml）は
// DLL に埋め込んだものを実行時に読み込む。

using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace mdevs.dialog
{
    /// <summary>
    /// ダイアログの部品（ボタン・入力欄）の見た目（dialog\DialogStyles.xaml。Windows 11 風。mdew と同じ）。
    /// </summary>
    internal static class DialogStyles
    {
        /// <summary>読み込んだ見た目（最初に使う時に読み込む）。</summary>
        private static ResourceDictionary s_styles;

        /// <summary>見た目を読み込む（DLL に埋め込んだ DialogStyles.xaml）。</summary>
        /// <returns>見た目のリソース。</returns>
        public static ResourceDictionary Load()
        {
            if (null == s_styles)
            {
                using (var stream = typeof(DialogStyles).Assembly.GetManifestResourceStream("mdevs.dialog.DialogStyles.xaml"))
                {
                    s_styles = (ResourceDictionary)XamlReader.Load(stream);
                }
            }
            return s_styles;
        }

        /// <summary>ダイアログのウィンドウの共通の設定（大きさは中身に合わせる・Visual Studio の中央に出す・
        /// 文字は Yu Gothic UI の 12px・白い背景・部品の見た目）。</summary>
        /// <param name="a_window">ダイアログのウィンドウ。</param>
        /// <param name="a_title">タイトル。</param>
        /// <param name="a_width">幅。</param>
        public static void Apply(Window a_window, string a_title, double a_width)
        {
            a_window.Title = a_title;
            a_window.Width = a_width;
            a_window.SizeToContent = SizeToContent.Height;
            a_window.ResizeMode = ResizeMode.NoResize;
            a_window.ShowInTaskbar = false;
            a_window.FontFamily = new FontFamily("Yu Gothic UI, Meiryo UI, Segoe UI");
            a_window.FontSize = 12;
            a_window.Background = Brushes.White;
            a_window.Resources.MergedDictionaries.Add(Load());
            Window owner = Application.Current?.MainWindow;
            if (null != owner && owner.IsVisible)
            {
                a_window.Owner = owner;
                a_window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                a_window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        /// <summary>見出し（太字）の文字。</summary>
        public static TextBlock Heading(string a_text) => new TextBlock
        {
            Text = a_text,
            Margin = new Thickness(0, 0, 0, 16),
            Foreground = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1C)),
            FontWeight = FontWeights.Bold,
        };

        /// <summary>入力欄の上の説明の文字。</summary>
        public static TextBlock Caption(string a_text) => new TextBlock
        {
            Text = a_text,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
        };

        /// <summary>右下に並べる「キャンセル」と、決定のボタン。</summary>
        /// <param name="a_okText">決定のボタンの文字。</param>
        /// <param name="a_onOk">決定のボタンを押した時の処理。</param>
        /// <returns>ボタンを並べたもの。</returns>
        public static StackPanel Buttons(string a_okText, RoutedEventHandler a_onOk)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            panel.Children.Add(new Button { Content = Text.T("キャンセル"), MinWidth = 84, Margin = new Thickness(0, 0, 8, 0), IsCancel = true });
            var ok = new Button { Content = a_okText, MinWidth = 84, IsDefault = true };
            ok.Click += a_onOk;
            panel.Children.Add(ok);
            return panel;
        }
    }

    /// <summary>
    /// 挿入する表の行数（見出しの行を含む）と列数を指定するダイアログ（mdew の TableSizeDialog と同じ）。
    /// 行数は 1〜50、列数は 1〜20 の範囲にし、数字でない場合は 1 にする。
    /// </summary>
    internal sealed class TableSizeDialog : Window
    {
        /// <summary>行数の最大値。</summary>
        private const int MAX_ROWS = 50;
        /// <summary>列数の最大値。</summary>
        private const int MAX_COLUMNS = 20;

        /// <summary>行数の入力欄。</summary>
        private readonly TextBox m_rowsBox = new TextBox { Text = "3", Margin = new Thickness(0, 0, 0, 14), Padding = new Thickness(6, 4, 6, 4) };
        /// <summary>列数の入力欄。</summary>
        private readonly TextBox m_colsBox = new TextBox { Text = "3", Margin = new Thickness(0, 0, 0, 22), Padding = new Thickness(6, 4, 6, 4) };

        /// <summary>「挿入」を押した時の行数（見出しの行を含む）。</summary>
        public int Rows { get; private set; } = 3;
        /// <summary>「挿入」を押した時の列数。</summary>
        public int Columns { get; private set; } = 3;

        /// <summary>ダイアログを作る（行数・列数とも 3 を表示し、行数の欄を選択した状態にする）。</summary>
        public TableSizeDialog()
        {
            DialogStyles.Apply(this, Text.T("表を挿入"), 300);
            var panel = new StackPanel { Margin = new Thickness(22) };
            panel.Children.Add(DialogStyles.Heading(Text.T("表のサイズを指定してください")));
            panel.Children.Add(DialogStyles.Caption(Text.T("行数（見出し行を含む）")));
            panel.Children.Add(m_rowsBox);
            panel.Children.Add(DialogStyles.Caption(Text.T("列数")));
            panel.Children.Add(m_colsBox);
            panel.Children.Add(DialogStyles.Buttons(Text.T("挿入"), OkClick));
            Content = panel;
            Loaded += (s, e) =>
            {
                m_rowsBox.Focus();
                m_rowsBox.SelectAll();
            };
        }

        /// <summary>「挿入」：入力された値を範囲に収めて、ダイアログを閉じる。</summary>
        private void OkClick(object a_sender, RoutedEventArgs a_args)
        {
            Rows = Clamp(m_rowsBox.Text, MAX_ROWS);
            Columns = Clamp(m_colsBox.Text, MAX_COLUMNS);
            DialogResult = true;
        }

        /// <summary>入力された文字を、1〜a_max の整数にする（数字でなければ 1）。</summary>
        private static int Clamp(string a_text, int a_max)
        {
            if (!int.TryParse(a_text, out int value) || value < 1)
            {
                return 1;
            }
            return value > a_max ? a_max : value;
        }
    }

    /// <summary>
    /// リンク先の URL を入力するダイアログ（mdew の LinkInputDialog と同じ）。前後の空白は取り除く。
    /// </summary>
    internal sealed class LinkInputDialog : Window
    {
        /// <summary>URL の入力欄。</summary>
        private readonly TextBox m_urlBox = new TextBox { Margin = new Thickness(0, 0, 0, 14), Padding = new Thickness(6, 4, 6, 4) };

        /// <summary>OK を押した時の URL（前後の空白を取り除いたもの）。</summary>
        public string Url { get; private set; } = "";

        /// <summary>ダイアログを作る（URL の欄を選択した状態にする）。</summary>
        public LinkInputDialog()
        {
            DialogStyles.Apply(this, Text.T("リンク"), 380);
            var panel = new StackPanel { Margin = new Thickness(22) };
            panel.Children.Add(DialogStyles.Heading(Text.T("リンク先のURLを入力してください")));
            panel.Children.Add(DialogStyles.Caption("URL"));
            panel.Children.Add(m_urlBox);
            panel.Children.Add(DialogStyles.Buttons("OK", OkClick));
            Content = panel;
            Loaded += (s, e) =>
            {
                m_urlBox.Focus();
                m_urlBox.SelectAll();
            };
        }

        /// <summary>OK：入力された URL を受け取って、ダイアログを閉じる。</summary>
        private void OkClick(object a_sender, RoutedEventArgs a_args)
        {
            Url = m_urlBox.Text.Trim();
            DialogResult = true;
        }
    }
}
