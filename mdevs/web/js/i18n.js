// i18n.js
//
// 画面の文字の英語対応（日本語・英語の切り替え）。
// 画面の文字は、日本語の文字をそのままキーにして書き（t('ヘルプを開く')）、英語の時はこの辞書の英語を返す。
// 文字の中の {0}・{1}… は、t の 2 つ目以降の引数で置き換える（t('{0} 件を置換しました', count)）。
// index.html 等に直接書いた文字（ボタンの文字・title・placeholder 等）は、translateDom でまとめて置き換える。
// 言語はホストが決める（設定の「言語」。自動の時は VS Code・Visual Studio の表示言語）。setLanguage 参照。

/** 英語の辞書（日本語 → 英語）。 */
const EN = {
  // ---- 文書・ステータスバー ----
  '無題': 'Untitled',
  '(無題)': '(Untitled)',
  'クリックで開く：{0}': 'Click to open: {0}',
  '● 未保存': '● Unsaved',
  '読み取り専用（ヘルプ）': 'Read-only (Help)',
  'ブラウザ単体（保存・開くは無効）': 'Browser only (saving and opening are disabled)',
  'フォルダを隠す': 'Hide Folder',
  'フォルダを表示': 'Show Folder',
  'アウトラインを隠す': 'Hide Outline',
  'アウトラインを表示': 'Show Outline',
  'Markdownを表示': 'Show Markdown',
  'ソースを表示': 'Show Source',
  '停止 0回': 'Stalls 0',
  '入力遅延 0回': 'Input lag 0',
  '停止 {0}回（最長 {1}ms）': 'Stalls {0} (max {1}ms)',
  '入力遅延 {0}回（最長 {1}ms）': 'Input lag {0} (max {1}ms)',
  '広げる': 'Expand',
  '折りたたむ': 'Collapse',

  // ---- 確認・お知らせ ----
  'リンク先のURLを入力してください': 'Enter the URL of the link',
  'このリンクは開けません。\n{0}': 'This link cannot be opened.\n{0}',
  'リンク先の見出しが見つかりません。\n#{0}': 'The heading of the link was not found.\n#{0}',
  '保存されていない変更があります。破棄して別のファイルを開きますか？': 'There are unsaved changes. Discard them and open another file?',
  '保存されていない変更があります。破棄して別のフォルダを開きますか？': 'There are unsaved changes. Discard them and open another folder?',
  '保存されていない変更があります。破棄してリンク先のファイルを開きますか？': 'There are unsaved changes. Discard them and open the linked file?',
  '編集中のすべてのファイルを保存します。よろしいですか？': 'All files being edited will be saved. Continue?',
  '保存されていない変更を破棄して、ファイルを読み直しますか？': 'Discard the unsaved changes and reload the file?',
  'クリップボードを読み取れませんでした。Ctrl+V で貼り付けてください。': 'Could not read the clipboard. Paste with Ctrl+V.',
  '{0} 個のファイルを保存しました。': 'Saved {0} file(s).',
  '\n\n保存に失敗したファイル:\n{0}': '\n\nFiles that could not be saved:\n{0}',

  // ---- 右クリックのメニュー ----
  '画像を開く': 'Open Image',
  'コピー': 'Copy',
  'Excel用にコピー': 'Copy for Excel',
  'すべて選択': 'Select All',
  'ページトップに戻る': 'Back to Top',
  'ソースモードに切り替え': 'Switch to Source Mode',
  'Markdownモードに切り替え': 'Switch to Markdown Mode',
  '切り取り': 'Cut',
  '貼り付け': 'Paste',
  '書式なしで貼り付け': 'Paste without Formatting',
  'リンクにする…': 'Make Link…',
  '見出し': 'Heading',
  '見出し{0}': 'Heading {0}',
  '{0} 見出し{1}': '{0} Heading {1}',
  '本文（見出しなし）': 'Body Text (No Heading)',
  '表を挿入…': 'Insert Table…',
  '上に行を挿入': 'Insert Row Above',
  '下に行を挿入': 'Insert Row Below',
  '行を削除': 'Delete Row',
  '左に列を挿入': 'Insert Column Left',
  '右に列を挿入': 'Insert Column Right',
  '列を削除': 'Delete Column',
  '表をコピー': 'Copy Table',
  '表を削除': 'Delete Table',
  'ヘルプを開く': 'Open Help',
  '太字': 'Bold',
  '斜体': 'Italic',
  '下線': 'Underline',
  '取り消し線': 'Strikethrough',
  'インラインコード': 'Inline Code',
  'ハイライト': 'Highlight',

  // ---- メニューバーのメニュー ----
  'ファイル(F)': 'File(F)',
  'ファイル (Alt+F)': 'File (Alt+F)',
  '表示(V)': 'View(V)',
  '表示 (Alt+V)': 'View (Alt+V)',
  'ヘルプ(H)': 'Help(H)',
  'ヘルプ (Alt+H)': 'Help (Alt+H)',
  '検索と置換 (Ctrl+F)': 'Find and Replace (Ctrl+F)',
  '新規': 'New',
  '開く': 'Open',
  'フォルダを開く...': 'Open Folder...',
  '最近使ったファイル': 'Recent Files',
  '最近使ったフォルダ': 'Recent Folders',
  '履歴をクリア': 'Clear History',
  '（履歴なし）': '(No History)',
  '保存': 'Save',
  '名前を付けて保存': 'Save As',
  'すべて保存': 'Save All',
  'PDFに書き出し…': 'Export to PDF…',
  'PDFの余白を設定…': 'PDF Margins…',
  '閉じる': 'Close',
  'ステータスバーを表示': 'Show Status Bar',
  'ソースモード': 'Source Mode',
  '表示倍率：{0}%': 'Zoom: {0}%',
  '拡大': 'Zoom In',
  '縮小': 'Zoom Out',
  '100%に戻す': 'Reset to 100%',
  '段落中の改行': 'Line Breaks in Paragraphs',
  'ソースの通りに改行する': 'Break Lines as in the Source',
  '空行が入るまで改行しない': 'Do Not Break Until a Blank Line',
  '本文の幅': 'Text Width',
  '読みやすい幅にする（最大 884px）': 'Readable Width (Max 884px)',
  '編集ペインの幅に合わせる': 'Fit to the Editor Width',
  '文字の色': 'Text Color',
  'くっきりした色': 'Sharp',
  '目にやさしい色': 'Easy on the Eyes',
  '表示スタイル': 'Display Style',
  'GitHubスタイル': 'GitHub Style',
  'VSCodeスタイル': 'VS Code Style',
  'Readmeを開く': 'Open Readme',
  'バージョン情報': 'About',

  // ---- 画面の部品（index.html） ----
  'フォルダ': 'Folder',
  '別のフォルダを開く': 'Open Another Folder',
  'ドラッグで幅を変更（ダブルクリックで元の幅に戻す）': 'Drag to resize (double-click to restore the default width)',
  'アウトライン': 'Outline',
  'Markdown のソース': 'Markdown source',
  'フォルダビューの表示・非表示': 'Show or hide the folder view',
  'アウトラインの表示・非表示': 'Show or hide the outline',
  'ソースモードと Markdownモードの切替': 'Switch between source mode and Markdown mode',
  '画面の処理が200ms以上止まった回数（IMEフリーズ検証用）': 'Number of times the screen stalled for 200ms or more (for IME freeze checks)',
  'キー入力が画面に届くまで200ms以上かかった回数（IMEフリーズ検証用）': 'Number of times a key took 200ms or more to reach the screen (for IME freeze checks)',

  // ---- 検索と置換のバー ----
  '検索と置換': 'Find and Replace',
  '検索': 'Find',
  '置換': 'Replace',
  '置換モードの切り替え': 'Toggle Replace',
  '置換モードを閉じる': 'Close Replace',
  '大文字と小文字を区別する (Alt+C)': 'Match Case (Alt+C)',
  '単語単位で検索する (Alt+W)': 'Match Whole Word (Alt+W)',
  '正規表現を使用する (Alt+R)': 'Use Regular Expression (Alt+R)',
  '前の一致項目 (Shift+Enter)': 'Previous Match (Shift+Enter)',
  '前の一致項目': 'Previous Match',
  '次の一致項目 (Enter)': 'Next Match (Enter)',
  '次の一致項目': 'Next Match',
  '閉じる (Escape)': 'Close (Escape)',
  '置換 (Enter)': 'Replace (Enter)',
  'すべて置換 (Ctrl+Alt+Enter)': 'Replace All (Ctrl+Alt+Enter)',
  'すべて置換': 'Replace All',
  '{0} 件を置換しました': 'Replaced {0} occurrence(s)',
  '正規表現が正しくありません': 'Invalid regular expression',
  '結果なし': 'No results',

  // ---- 検索と置換の処理（find.js） ----
  '正規表現が正しくありません：{0}': 'Invalid regular expression: {0}',
  '見つかりませんでした。': 'Not found.',
  '見つかりました（エディタ内で強調表示しています）。': 'Found (highlighted in the editor).',
  '見つかりました：{0}（{1} / {2}件目）': 'Found: {0} ({1} of {2})',
  '見つかりました：{0}（画面に表示されない部分。リンクの URL 等）': 'Found: {0} (in a part that is not shown on the screen, such as a link URL)',
  'エディタ内': 'the editor',
  'エディタ内・アウトライン': 'the editor and the outline',
  '{0} 件見つかりました（すべて{1}で強調表示しています。ダブルクリックでその箇所へ移動します）。': 'Found {0} match(es) (all highlighted in {1}; double-click to go to a match).',
  '{0}　（{1}件）': '{0}  ({1})',
  '{0} 個のファイルで見つかりました（フォルダビューでも強調表示しています）。ダブルクリックで開きます。': 'Found in {0} file(s) (also highlighted in the folder view). Double-click to open.',
  '{0} 件を置換しました（保存するまでファイルには反映されません）。': 'Replaced {0} occurrence(s) (not written to the file until you save).',
  '{0}　（{1}件置換）': '{0}  ({1} replaced)',
  '{0} 個のファイルで合計 {1} 件を置換しました（保存するまでファイルには書き出されません。メニュー「ファイル」→「すべて保存」で反映してください）。':
    'Replaced {1} occurrence(s) in {0} file(s) (not written to the files until you save; use File > Save All).',
  '現在のファイル': 'Current file',
  'エディタ内で強調表示された箇所をご確認ください。': 'Check the highlighted places in the editor.',
  '{0} 件を置換しました（現在のファイルに直接反映されました。保存するまでファイルには書き出されません）。':
    'Replaced {0} occurrence(s) (applied to the current file; not written to the file until you save).',
  '{0} 件を置換しました（{1} 個のファイル、保存するまでファイルには反映されません）。':
    'Replaced {0} occurrence(s) ({1} file(s); not written to the files until you save).',

  // ---- コピー（clipboard.js） ----
  '[画像]': '[Image]',
  '[画像: {0}]': '[Image: {0}]',
  '・ ': '• ',
};

/** 今の言語（'ja'：日本語、'en'：英語）。 */
let s_lang = 'ja';
/** 言語が変わった時に呼ぶ関数（onLanguageChange）。 */
const s_listeners = [];
/** translateDom で置き換えた文字・属性の、元の日本語（要素 → { text, 属性名 → 値 }）。日本語に戻す時に使う。 */
const s_originals = new WeakMap();
/** translateDom で置き換える属性。 */
const ATTRS = ['title', 'aria-label', 'placeholder'];

/**
 * 画面の文字を、今の言語にする。
 * @param {string} a_text 日本語の文字（辞書のキー）
 * @param {...(string|number)} a_args {0}・{1}… に入れる値
 * @returns {string}
 */
export function t(a_text, ...a_args) {
  const text = 'en' === s_lang && Object.prototype.hasOwnProperty.call(EN, a_text) ? EN[a_text] : a_text;
  return a_args.length ? text.replace(/\{(\d+)\}/g, (m, i) => (i < a_args.length ? String(a_args[i]) : m)) : text;
}

/** @returns {'ja'|'en'} 今の言語 */
export function getLanguage() {
  return s_lang;
}

/**
 * 言語が変わった時に呼ぶ関数を登録する（その時々に作る文字（ステータスバー等）を作り直すため）。
 * @param {() => void} a_listener 呼ぶ関数
 */
export function onLanguageChange(a_listener) {
  s_listeners.push(a_listener);
}

/**
 * 言語を変える。画面に直接書いた文字（index.html 等）を置き換え、登録した関数を呼ぶ。
 * @param {string} a_lang 'en'：英語、それ以外：日本語
 */
export function setLanguage(a_lang) {
  const lang = 'en' === a_lang ? 'en' : 'ja';
  if (lang === s_lang) return;
  s_lang = lang;
  document.documentElement.lang = lang;
  translateDom(document.body);
  for (const listener of s_listeners) listener();
}

/**
 * 要素の中の、直接書いた文字（子の文字のノード）と、title・aria-label・placeholder を今の言語にする。
 * 置き換えるのは、辞書にある文字だけ（文書の中身は辞書に無いので変わらない。文書（#doc・#print-doc）の中は見ない）。
 * @param {Element} a_root 置き換える範囲
 */
export function translateDom(a_root) {
  const walk = (a_el) => {
    if ('doc' === a_el.id || 'print-doc' === a_el.id || 'source' === a_el.id) return;
    let orig = s_originals.get(a_el);
    for (const node of a_el.childNodes) {
      if (3 === node.nodeType) {
        if (!orig) { orig = {}; s_originals.set(a_el, orig); }
        if (!orig.texts) orig.texts = new Map();
        const ja = orig.texts.has(node) ? orig.texts.get(node) : node.nodeValue;
        if (Object.prototype.hasOwnProperty.call(EN, ja.trim())) {
          orig.texts.set(node, ja);
          node.nodeValue = ja.replace(ja.trim(), t(ja.trim()));
        }
      } else if (1 === node.nodeType) {
        walk(node);
      }
    }
    for (const name of ATTRS) {
      if (!a_el.hasAttribute(name)) continue;
      if (!orig) { orig = {}; s_originals.set(a_el, orig); }
      const ja = name in orig ? orig[name] : a_el.getAttribute(name);
      if (Object.prototype.hasOwnProperty.call(EN, ja)) {
        orig[name] = ja;
        a_el.setAttribute(name, t(ja));
      }
    }
  };
  walk(a_root);
}
