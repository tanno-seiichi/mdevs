# mdevs Help

mdevs is a Visual Studio extension that lets you edit Markdown files directly, in a view that looks just like the
preview. There is no separate editing pane and preview pane, so you can keep writing while you see exactly how the
formatting looks.

You can open this help at any time by right-clicking in the editor and choosing "Open Help".
It opens read-only.

## Contents

- [Opening files](#opening-files)
- [The screen](#the-screen)
- [Basic operations](#basic-operations)
- [Formatting](#formatting)
- [Tables](#tables)
- [Excel and other apps](#excel-and-other-apps)
- [Images](#images)
- [Links](#links)
- [Outline](#outline)
- [Find and replace](#find-and-replace)
- [Source mode](#source-mode)
- [Settings](#settings)
- [Keyboard shortcuts](#keyboard-shortcuts)
- [Troubleshooting](#troubleshooting)

## Opening files

When you open a `.md` or `.markdown` file from Solution Explorer or elsewhere, it opens in the mdevs editor.

To edit the file as plain text, right-click the file in Solution Explorer → "Open With...", and choose
"Source Code (Text) Editor" (click "Set as Default" in the same dialog to change the default way of opening
Markdown files).

## The screen

- **Outline** (left): the list of headings in the current file. Click one to move to that heading.
- **Editing area** (most of the right side): where you write. What you see here is exactly what is saved.
- **Status bar** (bottom): the "Hide Outline" ("Show Outline") button at the left
  shows or hides the outline with one click. The "Show Source" button next to it
  switches between source mode and the normal view (in source mode, it reads "Show Markdown").
  When there are unsaved changes, "● Unsaved" is shown; when you hover over a link, its target is shown.

Drag the border between the outline and the editing area
to change the width of the outline (double-click the border to restore the default width).

You can change the zoom of the document with **Ctrl++**, **Ctrl+-**, **Ctrl+0** (back to 100%), or **Ctrl+mouse wheel**
(over the editing area). Text and images are zoomed together. The outline and other parts keep their size.

The light or dark appearance follows the Visual Studio color theme
(Tools > Theme) automatically.

## Basic operations

| Operation | How |
|---|---|
| Save | `Ctrl+S` (Visual Studio File > Save) |
| Save as | Visual Studio File > Save As |
| Undo / redo | `Ctrl+Z` / `Ctrl+Y` |
| Close | Close the Visual Studio tab |

When there are unsaved changes, a "*" appears in the tab name. Confirmation when closing,
and reloading when another app changes the file, work just as they do for text files in Visual Studio.

When you use Save As to save into another folder, images with a relative path in the document are also copied
to the same relative position from the new location.

### Parts you did not edit are saved as they were

mdevs rewrites only the blocks (headings, paragraphs, lists, tables, code blocks, and so on) that you actually edited.
Everything you did not touch keeps the way it was written when the file was loaded (spacing, line break positions, etc.).
So opening and saving a file does not fill your diff with unrelated changes.

## Formatting

Formatting is shown as it will look, right in the editing area.

| What you want | How to type it |
|---|---|
| Heading | `#` followed by a space at the start of a line (`#` to `######` for headings 1 to 6) |
| Quote | `>` followed by a space at the start of a line |
| Bold | `**text to make bold**`, or select text and press `Ctrl+B` |
| Italic | `*text to make italic*`, or select text and press `Ctrl+I` |
| Strikethrough | `~~text to strike through~~`, or select text and press `Alt+Shift+5` |
| Underline | Select text and press `Ctrl+U` (saved as `<u>…</u>`) |
| Highlight | `==text to highlight==` |
| Inline code | `` `text to make code` ``, or select text and press `` Ctrl+Shift+` `` (on a Japanese keyboard, the key at the position of `Ctrl+Shift+@`) |
| Bulleted list | `-` (or `*`, `+`) followed by a space at the start of a line |
| Numbered list | A number and a period, such as `1.`, followed by a space at the start of a line |
| Check box | `[ ]` (unchecked) or `[x]` (checked) followed by a space, at the start of a list item or at the start of a table cell (except in the header row; in a table cell, the space can be omitted) |
| Horizontal rule | Type `---` and press Enter |
| Code block | Type `` ``` `` at the start of a line (optionally followed by a language name: `` ```csharp ``) and press Enter |
| Table | Type something like `|Header 1|Header 2|` and press Enter |
| Link | `[text to show](URL)`, or `<https://...>` |
| Email link | `<name@example.com>` |

These marks are turned into formatting as soon as you type the closing mark. When the file is saved,
they are written back as the original Markdown syntax.

Select text and right-click to turn bold, italic, underline, strikethrough, inline code, and highlight on or off
from the menu (formats that are already applied have a ✓).

Select text and right-click → "Make Link…" to enter a URL and turn the selected text into a link.

Right-click → "Back to Top" shows the start of the document (and moves the cursor there).
It also works in source mode.

On a paragraph or heading, right-click → "Heading" to change the heading level (1 to 6)
or turn it back into body text (the current level has a ✓).

### Lists

- **Enter** in the middle of an item: adds the next item
- **Shift+Enter** in the middle of an item: inserts a line break inside the item (no new item)
- **Tab**: indents the item (makes it a nested list)
- **Shift+Tab**: outdents the item
- **Enter** on an empty item: leaves the list (in a nested item, moves it up one level)
  - If you move up one level in the middle of a list, the following items become nested under that item
    (this is how Markdown works). Type text in the item you moved up, and the following items line up below it.
    If you want to leave it empty, press **BackSpace** on that empty line to restore the previous shape
- **BackSpace** at the start of an item that has nested items: joins the item's text to the line above
  and moves the nested items under the previous item (as in Word)

Click a check box to check or uncheck it.
Press BackSpace at the start of a check box item to remove the check box and make it a normal item.

You can also put a check box at the start of a table cell (except in the header row).
Type `[ ]` or `[x]` at the start of a cell (the space after it is optional),
and it becomes a check box (it is saved as `[ ]` or `[x]` at the start of the cell; other apps such as GitHub
show it as the text `[ ]`). Press BackSpace at the start of the cell to remove the check box.

### Headings and quotes

Press BackSpace at the start of a heading to turn it back into a normal paragraph. Press BackSpace at the start of a
quote to remove the quote. Press Enter on the last, empty line of a quote to leave the quote.

### Code blocks

Inside a code block, Enter inserts a line break. **Tab** inserts a tab character. To leave a code block,
click the line above or below it.

### Pasting

Text copied from other apps is read as Markdown and pasted as headings, lists, tables, bold, and so on.
Formatted content copied from chat apps such as Claude, from web pages, or from Word
is converted to Markdown when pasted (bulleted and numbered lists (including nested ones),
headings, bold, italic, strikethrough, links, inline code, code blocks, quotes, tables, and images.
Decorations such as text color and size are dropped). To paste without formatting,
use **Ctrl+Shift+V** (or right-click → "Paste without Formatting"). Copied images
are pasted as images (see [Images](#images)).

### Line breaks within a paragraph

How a single line break in the source (one without a blank line) is shown can be chosen with the
"Line Breaks in Paragraphs" setting (see [Settings](#settings)). The default is "Do not break until a blank line":
a line break without a blank line is treated as a space, and paragraphs wrap at the width of the screen
(the same as the VS Code Markdown preview). If you switch to "Break lines as in the source",
the line breaks in the source are shown as they are. Either way, the saved content does not change.

Press **Shift+Enter** in the middle of a paragraph to break the line there (it is saved as a line break in the source).

## Tables

Type something like `|Header 1|Header 2|` and press Enter to make a table (a header row and one empty row are created).
You can also right-click → "Insert Table…" and specify the number of rows (including the header row)
and columns to create an empty table (it is created after the paragraph you right-clicked; on an empty paragraph,
that paragraph becomes the table).

- **Tab** / **Shift+Tab**: move to the next / previous cell. Pressing Tab in the last cell adds a row.
- **Ctrl+Enter**: inserts a row below.
- Right-click on a table cell to insert or delete rows and columns, copy the table, or delete the table.

## Excel and other apps

- **Pasting from Excel**: copy a range in Excel and paste it to make a table (the first row becomes the header row).
  Images in the range are pasted too. Tab-separated text is also pasted as a table.
- **Pasting into Excel**: right-click → "Copy for Excel" copies the selected headings, paragraphs,
  list items, and so on in a form that Excel pastes one line per cell, keeping bold and other formatting. In a table,
  the selected range of cells is copied. Right-click → "Copy Table" on a table cell copies the whole
  table with borders. Images can be pasted too (they are written out as temporary files, so paste soon after copying).
  In Excel, images may overlap the text in the rows below.
- **Normal copy** (`Ctrl+C`): pastes into other apps such as Word with headings, tables, and other formatting.
  Between mdevs editors, content pastes with its headings, lists, tables, and other formatting intact.

## Images

Besides the `![description](images/a.png)` form, images written as an HTML tag such as `<img src="images/a.png" width="200">`
are also shown. Images with a relative path are shown relative to the folder of the open file.
Images with an absolute path such as `C:\...` are also shown.

You can paste an image from the clipboard (such as a screenshot) with `Ctrl+V`. A pasted image is
**kept in a temporary folder until you save**; when you save, it is moved
into a `<file name>.images` folder next to the file you are editing (if a file with the same name exists,
a number is added so that it does not overwrite it).

Double-click an image, or right-click → "Open Image", to open it in the default Windows app for images
(such as Photos).

## Links

Links are shown in blue with an underline and open with a **click** (no need to hold Ctrl). To edit the text of a link,
move the cursor with the arrow keys. When you hover over a link, its target is shown in the status bar.

- `http://`, `https://`, and `mailto:` links: open in the default browser or mail app.
- An email address written as plain text (`name@example.com`) is also shown as a mailto link (as on GitHub;
  it is saved as written, without `< >`). When you type one, it becomes a link when you type a space or punctuation
  after it, or press Enter.
- `#heading` links: move to that heading in the document.
- Links to a Markdown file with a relative path (such as `other.md`): open that file in another Visual Studio tab.

## Outline

The outline lists the headings of the current file with their hierarchy. Click a heading to move to that position in
the editing area. This is handy for quickly finding your way around a long document.

Headings that have lower-level headings show a "▾"; click it to collapse the headings below (click the "▸"
to expand them again). The collapsed state is kept while you edit and when you switch to source mode.

## Find and replace

Find and replace uses the Visual Studio search.

- **Ctrl+F** / **Ctrl+H**: open the Visual Studio "Find and Replace" window. If you select text before opening it,
  that text is entered as the search text. Use "Find Next" to select the matches one by one, and "Replace" or
  "Replace All" to replace them. Match case, whole word, and regular expressions are also available. The current match
  is shown in orange, and the other matches in yellow.
- **F3** / **Shift+F3**: move to the next / previous match.
- **Replace All**: works on the Markdown source (so `**`, link URLs, and so on are also replaced).
  The result is shown on the screen immediately and becomes an unsaved change.
- **Ctrl+Shift+F** (Find in Files) and **Ctrl+Shift+H** (Replace in Files) also work as usual.
  Double-click a line in the "Find Results" window to open the file and show the match.

## Source mode

The "Show Source" button in the status bar (or right-click → "Switch to
Source Mode") switches to a mode where you edit the Markdown source as it is. Use it when you want to edit the text in
bulk without worrying about formatting. The "Show Markdown" button in the status bar (or right-click →
"Switch to Markdown Mode") brings back the normal view.

When you switch, the paragraph you were looking at stays at the top of the screen. Saving and find and replace
also work in source mode.

## Settings

In Visual Studio, open Tools > Options > mdevs > Display to change the following settings.
Changes are applied immediately to all open editors.

| Setting | Description |
|---|---|
| Language | "Auto" (default; follows the Visual Studio display language: Japanese if it is Japanese, otherwise English), "Japanese", or "English". Changes the language of the editor, its messages and dialogs, and this help |
| Display Style | "GitHub style" (default), or "VS Code style", which resembles the Visual Studio Code Markdown preview (14px Segoe UI, a thin line under headings, links without underlines, code in red, tables without vertical lines, etc.) |
| Text Color | "Sharp" (default; almost black), or "Easy on the eyes", a slightly lighter gray that looks thinner (with a dark theme, a light color and a slightly darker color, respectively) |
| Text Width | "Readable width, max 884px" (default; the text is centered with a maximum width of 884px), or "Fit to the editor width" (for example, to show tables with many columns wider) |
| Line Breaks in Paragraphs | "Do not break until a blank line" (default) or "Break lines as in the source" (see [Line breaks within a paragraph](#line-breaks-within-a-paragraph)) |

## Keyboard shortcuts

| Key | Action |
|---|---|
| `Ctrl+S` | Save |
| `Ctrl+F` / `Ctrl+H` | Find and replace (the Visual Studio "Find and Replace" window) |
| `F3` / `Shift+F3` | Find next / find previous |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo |
| `Ctrl+B` / `Ctrl+I` / `Ctrl+U` | Bold / italic / underline |
| `Alt+Shift+5` | Strikethrough |
| `` Ctrl+Shift+` `` | Inline code |
| `Ctrl+Shift+V` | Paste without formatting |
| `Ctrl+Enter` (in a table) | Insert a row below |
| `Tab` / `Shift+Tab` | Indent / outdent (lists), next / previous cell (tables) |
| `Shift+Enter` | Line break inside a paragraph or list item |
| `Ctrl++` / `Ctrl+-` / `Ctrl+0` | Zoom in / zoom out / reset zoom to 100% (Ctrl+mouse wheel also zooms) |

Keys not listed above (`Ctrl+Tab`, `F5`, `Ctrl+Shift+B`, etc.) work as Visual Studio shortcuts.

## Troubleshooting

- If something does not work as expected, save the file, then close the tab and open the file again.
- The log is shown in Visual Studio under View > Output by choosing "mdevs" in "Show output from".
- Press **F12** in the editor to open the developer tools of the editor (for investigating the editor itself).
