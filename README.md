# mdevs

mdevs は、Markdown ファイルをプレビューと同じ見た目のまま直接編集できる、Visual Studio の拡張機能です。
Windows デスクトップ用の Markdown エディタ **mdew** の画面を、Visual Studio の中で使えるようにしたものです（VS Code 版は **mdevc**）。

## インストール

1. Visual Studio をすべて閉じます。
2. 配布された `mdevs.vsix` をダブルクリックすると、VSIX インストーラーが開くので、画面の指示に従ってインストールします。

対応する Visual Studio は Visual Studio 2026（バージョン 18.0 以降、64 ビット）です。編集画面の表示に Microsoft Edge WebView2 ランタイムを使います（Windows 11 には最初から入っています）。

アンインストールは、Visual Studio の「拡張機能」→「拡張機能の管理」で mdevs を選んで行います。

## 使い方

- `.md`・`.markdown` のファイルをソリューションエクスプローラー等から開くと、mdevs の編集画面で開きます。
- 書式の書き方・表の操作などの使い方は、編集画面の右クリック→「ヘルプを開く」で表示されるヘルプ（読み取り専用）を見てください。
- テキストとして編集したい時は、ソリューションエクスプローラーでファイルを右クリック→「ファイルを開くアプリケーションの選択...」で「ソース コード (テキスト) エディター」を選んで開きます（同じ画面の「既定値として設定」で、既定の開き方も変えられます）。
- 保存（Ctrl+S）、名前を付けて保存、未保存の印、閉じる時の確認、ほかのアプリでファイルが変更された時の読み直しは、通常のテキストファイルと同じく Visual Studio が行います。
- 「フォルダーを指定して検索」（Ctrl+Shift+F）・「フォルダーを指定して置換」（Ctrl+Shift+H）も、そのまま使えます。置換した結果は、開いている mdevs の画面にもすぐに反映されます。「検索結果」の行をダブルクリックすると、ファイルを開いてその箇所を表示します。

### 設定

Visual Studio の「ツール」→「オプション」→「mdevs」→「表示」で、次の設定を変えられます。変えた設定は、開いているすべての画面にすぐに反映されます。

| 設定 | 内容 |
|---|---|
| 表示スタイル | GitHubスタイル（既定）／VSCodeスタイル |
| 文字の色 | くっきりした色（既定）／目にやさしい色 |
| 本文の幅 | 読みやすい幅にする（最大 884px。既定）／編集ペインの幅に合わせる |
| 段落中の改行 | 空行が入るまで改行しない（既定）／ソースの通りに改行する |

配色（明るい・暗い）は、Visual Studio の配色に合わせて自動で切り替わります。

### 検索と置換

検索と置換には、Visual Studio の検索を使います。

| 操作 | キー |
|---|---|
| 検索と置換のウィンドウを開く | Ctrl+F・Ctrl+H |
| 次を検索・前を検索 | F3・Shift+F3 |

「すべて置換」は、Markdown のソースに対して置き換えます（「**」やリンクの URL なども対象になります）。

### キー操作

編集画面で使うキー（文字の入力、太字の Ctrl+B・斜体の Ctrl+I・下線の Ctrl+U、元に戻す・やり直し、コピー・貼り付け、表示倍率の Ctrl++・Ctrl+-・Ctrl+0、取り消し線の Alt+Shift+5 等）は、編集画面が受け取ります。
それ以外のキー（Ctrl+S・Ctrl+Tab・F5・Ctrl+Shift+B 等）は、Visual Studio のショートカットとして動きます。

### 画像の貼り付け

クリップボードの画像を貼り付けると、保存するまでの間は一時フォルダに置き、保存した時に、編集しているファイルの隣の「`<ファイル名>.images`」フォルダへ `clipboard_image_1.png` のような名前で移して、そのフォルダへの相対パスを文書に書きます（mdew と同じです）。

### mdew・mdevc との違い

- メニューバー・フォルダビュー・PDF への書き出しはありません（ファイルを開く・フォルダの操作は Visual Studio のものを使います）。
- 検索と置換は、mdew の検索と置換のウィンドウ・mdevc の検索と置換のバーの代わりに、Visual Studio の検索を使います。

## 開発

### フォルダの構成

| 場所 | 内容 |
|---|---|
| `mdevs.slnx` | ソリューション |
| `mdevs/mdevsPackage.cs` | 拡張機能の入り口（エディターの登録・オプションのページの登録） |
| `mdevs/MarkdownEditorFactory.cs` | `.md`・`.markdown` を開く時に、編集ペインを作るエディターファクトリー |
| `mdevs/MarkdownEditorPane.cs` | 編集ペイン（Visual Studio の文書としての読み込み・保存、検索、ほかのアプリでの変更の検知等） |
| `mdevs/MarkdownWebHost.cs` | 編集ペインの中身（WebView2 で画面を表示し、ファイルの読み書き・画像の貼り付け・リンクを開く等を受け持つ） |
| `mdevs/KeyboardRouter.cs` | キー入力を、Visual Studio のショートカットと編集画面とに振り分ける |
| `mdevs/NavigationCapture.cs` | 「検索結果」から開いた時に、その箇所を表示する |
| `mdevs/MdevsOptionsPage.cs`・`MdevsSettings.cs`・`registration.json` | 「ツール」→「オプション」の設定 |
| `mdevs/UiThread.cs`・`Log.cs` | UI スレッドでの処理、動作の記録（「出力」ウィンドウの「mdevs」） |
| `mdevs/dialog/` | 「表を挿入…」「リンクにする…」のダイアログ |
| `mdevs/help/HELP.md` | ヘルプ（右クリックの「ヘルプを開く」で、読み取り専用で開く。mdevc の help\HELP.md と内容をそろえる） |
| `mdevs/web/` | 画面（mdew の画面を元にしたもの） |
| `mdevs/images/mde.png` | 拡張機能のアイコン |
| `mdevs/LICENSE.txt` | VSIX に入れるライセンス（mdevs の MIT License と、WebView2 のライセンス） |
| `mdevs/source.extension.vsixmanifest` | 拡張機能の情報（名前・バージョン・発行者・対応する Visual Studio 等） |

### ビルド・動作の確認

1. Visual Studio 2026 に「Visual Studio 拡張機能の開発」のワークロードを入れておきます。
2. `mdevs.slnx` を開いてビルドします。
3. F5 を押すと、mdevs を読み込んだ Visual Studio（実験用インスタンス）が起動するので、`.md` ファイルを開いて確かめます。
4. 動作の記録は「表示」→「出力」の「出力元」で「mdevs」を選ぶと表示されます。画面側は、編集画面で F12 を押すと開発者ツールが開きます。

### 配布用の VSIX の作り方

構成を「Release」にしてビルドすると、`mdevs\bin\Release\net472\mdevs.vsix` ができます。これが配布するファイルです。
バージョンは `source.extension.vsixmanifest` の `Version`（VSIX のバージョン）と、`mdevs.csproj` の `Version`（DLL のバージョン）で変えます。

## ライセンス

MIT License（LICENSE.txt）。mdevs が使用している WebView2 のライセンスは `mdevs\LICENSE.txt` にあります。
