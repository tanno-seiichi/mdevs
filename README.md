# mdevs

mdevs is a Visual Studio extension that lets you edit Markdown files directly, in a view that looks just like the
preview (WYSIWYG). There is no separate editing pane and preview pane, so you can keep writing while you see exactly
how the formatting looks.

The editor is available in English and Japanese (see the Language setting below).

日本語の説明は [README.ja.md](https://github.com/tanno-seiichi/mdevs/blob/master/README.ja.md) を参照してください。

## Installation

In Visual Studio, open Extensions > Manage Extensions, search for "mdevs", and install it.

To install from a `.vsix` file:

1. Close all instances of Visual Studio.
2. Double-click the `mdevs.vsix` file. The VSIX Installer opens; follow the instructions on the screen.

Requires Visual Studio 2026 (version 18.0 or later, 64-bit). The editor uses the Microsoft Edge WebView2 Runtime
(included with Windows 11).

To uninstall, choose mdevs in Visual Studio under Extensions > Manage Extensions.

## Usage

- `.md` and `.markdown` files opened from Solution Explorer or elsewhere open in the mdevs editor.
- For how to write formatting, work with tables, and so on, see the help (read-only), which opens from the editor's
  right-click menu → Open Help. The help is shown in the language of the editor (see the Language setting below).
- To edit a file as plain text, right-click the file in Solution Explorer → "Open With...", and choose
  "Source Code (Text) Editor" (click "Set as Default" in the same dialog to change the default way of opening).
- Save (Ctrl+S), Save As, the unsaved-changes marker, confirmation when closing, and reloading when another app
  changes the file are all handled by Visual Studio, just as for normal text files.
- Find in Files (Ctrl+Shift+F) and Replace in Files (Ctrl+Shift+H) also work as usual. Replacements are shown
  immediately in any open mdevs editor. Double-click a line in "Find Results" to open the file and show the match.

### Settings

In Visual Studio, open Tools > Options > mdevs > Display to change the following settings. Changes are applied
immediately to all open editors.

| Setting | Values |
|---|---|
| Language | Auto (default; follows the Visual Studio display language: Japanese if it is Japanese, otherwise English) / Japanese / English |
| Display Style | GitHub style (default) / VS Code style |
| Text Color | Sharp (default) / Easy on the eyes |
| Text Width | Readable width (max 884px, default) / Fit to the editor width |
| Line Breaks in Paragraphs | Do not break until a blank line (default) / Break lines as in the source |

The Language setting applies to the editor screen, messages, dialogs, and the help. The options page itself is shown
by Visual Studio, so its labels are written in both English and Japanese.

The light or dark appearance follows the Visual Studio color theme automatically.

### Find and replace

Find and replace uses the Visual Studio search.

| Operation | Keys |
|---|---|
| Open the Find and Replace window | Ctrl+F / Ctrl+H |
| Find next / find previous | F3 / Shift+F3 |

"Replace All" works on the Markdown source (so `**`, link URLs, and so on are also replaced).

### Keyboard

Keys used for editing (typing, Ctrl+B bold, Ctrl+I italic, Ctrl+U underline, undo / redo, copy / paste, zoom with
Ctrl++ / Ctrl+- / Ctrl+0, Alt+Shift+5 strikethrough, etc.) are handled by the editor.
Other keys (Ctrl+S, Ctrl+Tab, F5, Ctrl+Shift+B, etc.) work as Visual Studio shortcuts.

### Pasting images

When you paste an image from the clipboard, it is kept in a temporary folder until you save. When you save, it is moved
into a "`<file name>.images`" folder next to the file you are editing, with a name such as `clipboard_image_1.png`, and
a relative path to that folder is written into the document.

### Notes

- Use Visual Studio to open files and work with folders. Find and replace uses the Visual Studio search.

## Development

### Folder structure

| Path | Contents |
|---|---|
| `mdevs.slnx` | Solution |
| `mdevs/mdevsPackage.cs` | Entry point of the extension (registers the editor and the options page) |
| `mdevs/MarkdownEditorFactory.cs` | Editor factory that creates the editor pane when a `.md` or `.markdown` file is opened |
| `mdevs/MarkdownEditorPane.cs` | Editor pane (loading and saving as a Visual Studio document, find, detecting changes by other apps, etc.) |
| `mdevs/MarkdownWebHost.cs` | Contents of the editor pane (shows the screen with WebView2; reads and writes files, pastes images, opens links, etc.) |
| `mdevs/KeyboardRouter.cs` | Routes key input between Visual Studio shortcuts and the editor screen |
| `mdevs/NavigationCapture.cs` | Shows the match when a file is opened from "Find Results" |
| `mdevs/MdevsOptionsPage.cs`, `MdevsSettings.cs`, `registration.json` | Settings under Tools > Options |
| `mdevs/UiThread.cs`, `Log.cs` | Work on the UI thread, and the log (the "mdevs" pane of the Output window) |
| `mdevs/dialog/` | The "Insert Table…" and "Make Link…" dialogs |
| `mdevs/help/HELP.en.md`, `HELP.md` | Help in English / Japanese (opened read-only from the right-click menu → Open Help. The one for the language of the editor is opened) |
| `mdevs/web/` | Editor screen (HTML, JavaScript, CSS). `js/i18n.js` holds the English text of the screen (the Japanese text is the key) |
| `mdevs/Text.cs` | English text of the messages and dialogs shown by the extension (the Japanese text is the key) |
| `mdevs/images/mde.png` | Icon of the extension |
| `mdevs/LICENSE.txt` | License included in the VSIX (the mdevs MIT License and the WebView2 license) |
| `mdevs/source.extension.vsixmanifest` | Information about the extension (name, version, publisher, supported Visual Studio, etc.) |
| `README.md`, `README.ja.md` | This file (English) / Japanese version |

### Building and trying it out

1. Install the "Visual Studio extension development" workload in Visual Studio 2026.
2. Open `mdevs.slnx` and build.
3. Press F5 to start Visual Studio with mdevs loaded (the experimental instance), and open a `.md` file.
4. The log is shown under View > Output by choosing "mdevs" in "Show output from". For the editor screen itself, press
   F12 in the editor to open the developer tools.

### Building the VSIX

Build with the "Release" configuration to create `mdevs\bin\Release\net472\mdevs.vsix`. This is the file to distribute.
Change the version with `Version` in `source.extension.vsixmanifest` (the VSIX version) and `Version` in
`mdevs.csproj` (the DLL version).

## License

MIT License (LICENSE.txt). The license of WebView2, which mdevs uses, is in `mdevs\LICENSE.txt`.
