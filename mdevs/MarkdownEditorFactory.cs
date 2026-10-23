// MarkdownEditorFactory.cs
//
// mdevs（mdew の Visual Studio 版）の一部。
// Visual Studio で .md・.markdown のファイルを開く時に、編集ペイン（MarkdownEditorPane）を作るエディターファクトリー。

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace mdevs
{
    /// <summary>
    /// .md・.markdown のファイルを、mdew と同じ見た目で直接編集できる編集ペイン（MarkdownEditorPane）で開く。
    /// 編集ペインは、ファイルの内容（ドキュメントデータ）と表示（ドキュメントビュー）を1つのオブジェクトで兼ねる。
    /// </summary>
    [Guid(GuidString)]
    public sealed class MarkdownEditorFactory : IVsEditorFactory
    {
        /// <summary>このエディターファクトリーの GUID（Visual Studio への登録・「ファイルを開くアプリケーションの選択」で使う）。</summary>
        public const string GuidString = "6eba55bc-130e-4dbb-92a4-3e0107f75213";

        /// <summary>このエディターを登録したパッケージ。</summary>
        private readonly MdevsPackage m_package;

        /// <param name="a_package">このエディターを登録するパッケージ。</param>
        public MarkdownEditorFactory(MdevsPackage a_package)
        {
            m_package = a_package;
        }

        /// <summary>Visual Studio のサービスを受け取る（このエディターでは使わない）。</summary>
        public int SetSite(IOleServiceProvider a_serviceProvider)
        {
            return VSConstants.S_OK;
        }

        /// <summary>表示の種類（ビュー）を、このエディターの表示に対応付ける。通常の表示（Primary）のほか、テキスト・コードの
        /// 表示（Visual Studio の「検索結果」等から開く時に求められる）も、この編集ペインで表示する（どれも同じ1つの表示）。</summary>
        public int MapLogicalView(ref Guid a_logicalView, out string a_physicalView)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            a_physicalView = null;
            if (VSConstants.LOGVIEWID_Primary == a_logicalView)
            {
                return VSConstants.S_OK;
            }
            if (VSConstants.LOGVIEWID_TextView == a_logicalView || VSConstants.LOGVIEWID_Code == a_logicalView ||
                VSConstants.LOGVIEWID_Debugging == a_logicalView)
            {
                // 「検索結果」等から開く時：ダブルクリックした箇所を読み取っておき、開いた後に表示する（NavigationCapture）
                Log.Write($"テキストの表示として開きます（{a_logicalView}）。");
                NavigationCapture.CaptureFromFindResults();
                return VSConstants.S_OK;
            }
            Log.Write($"対応していない表示の種類：{a_logicalView}");
            return VSConstants.E_NOTIMPL;
        }

        /// <summary>後始末（何もしない）。</summary>
        public int Close()
        {
            return VSConstants.S_OK;
        }

        /// <summary>ファイルを開く時に、編集ペインを作る。同じファイルを別のエディター（テキストエディター等）で
        /// 開いている時は、VS_E_INCOMPATIBLEDOCDATA を返す（Visual Studio が、そちらを閉じるかを確認する）。</summary>
        public int CreateEditorInstance(uint a_createFlags, string a_documentPath, string a_physicalView, IVsHierarchy a_hierarchy,
            uint a_itemId, IntPtr a_existingDocData, out IntPtr a_docView, out IntPtr a_docData, out string a_editorCaption,
            out Guid a_commandUiGuid, out int a_createWindowFlags)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            a_docView = IntPtr.Zero;
            a_docData = IntPtr.Zero;
            a_editorCaption = null;
            a_commandUiGuid = Guid.Empty;
            a_createWindowFlags = 0;

            if (0 == (a_createFlags & (VSConstants.CEF_OPENFILE | VSConstants.CEF_SILENT)))
            {
                return VSConstants.E_INVALIDARG;
            }
            if (IntPtr.Zero != a_existingDocData)
            {
                return VSConstants.VS_E_INCOMPATIBLEDOCDATA;
            }

            var pane = new MarkdownEditorPane(m_package);
            a_docView = Marshal.GetIUnknownForObject(pane);
            a_docData = Marshal.GetIUnknownForObject(pane);
            a_editorCaption = string.Empty;
            a_commandUiGuid = typeof(MarkdownEditorFactory).GUID;
            return VSConstants.S_OK;
        }
    }
}
