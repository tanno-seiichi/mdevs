// mdevsPackage.cs
//
// mdevs（mdew の Visual Studio 版）の一部。
// Visual Studio の拡張機能の入口（パッケージ）。.md・.markdown を開くエディター（MarkdownEditorFactory）を登録する。

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace mdevs
{
    /// <summary>
    /// mdevs のパッケージ。.md・.markdown のファイルを開く時に、Visual Studio がこのパッケージを読み込み、
    /// 登録したエディターファクトリー（MarkdownEditorFactory）で編集ペインを作る。
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    // エディターの名前（110）は VSPackage.resx。「ファイルを開くアプリケーションの選択」に表示される
    [ProvideEditorFactory(typeof(MarkdownEditorFactory), 110, TrustLevel = __VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted)]
    [ProvideEditorExtension(typeof(MarkdownEditorFactory), ".md", 100, NameResourceID = 110)]
    [ProvideEditorExtension(typeof(MarkdownEditorFactory), ".markdown", 100, NameResourceID = 110)]
    // 「検索結果」等から、テキスト・コードの表示として開く時も、このエディターで開く（MarkdownEditorFactory.MapLogicalView）
    [ProvideEditorLogicalView(typeof(MarkdownEditorFactory), VSConstants.LOGVIEWID.TextView_string)]
    [ProvideEditorLogicalView(typeof(MarkdownEditorFactory), VSConstants.LOGVIEWID.Code_string)]
    [ProvideEditorLogicalView(typeof(MarkdownEditorFactory), VSConstants.LOGVIEWID.Debugging_string)]
    // 拡張機能のフォルダにある DLL（WebView2 等）を読み込めるようにする
    [ProvideBindingPath]
    // 「ツール」→「オプション」→「mdevs」→「表示」（表示スタイル・文字の色・本文の幅・段落中の改行）。Visual Studio 2026 の
    // 新しいオプションの画面には registration.json の設定を表示する（ProvideSettingsManifest）。従来の画面には MdevsOptionsPage
    // を表示する（IsInUnifiedSettings：新しい画面では表示しない）
    [ProvideSettingsManifest]
    [ProvideOptionPage(typeof(MdevsOptionsPage), "mdevs", "表示", 0, 0, true, IsInUnifiedSettings = true)]
    public sealed class MdevsPackage : AsyncPackage
    {
        /// <summary>パッケージの GUID。</summary>
        public const string PackageGuidString = "f373b597-1bb7-4d3d-96b9-5dc268c49708";

        /// <summary>パッケージの初期化。エディターファクトリーを登録する。</summary>
        /// <param name="a_cancellationToken">中断の通知。</param>
        /// <param name="a_progress">進み具合の通知先。</param>
        /// <returns>初期化の非同期処理。</returns>
        protected override async Task InitializeAsync(CancellationToken a_cancellationToken, IProgress<ServiceProgressData> a_progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(a_cancellationToken);
            MdevsSettings.Initialize(this);
            RegisterEditorFactory(new MarkdownEditorFactory(this));
            Log.Write("パッケージを初期化しました。");
        }
    }
}
