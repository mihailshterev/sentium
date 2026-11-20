import { normalizeLabel, type Block, type LinkDefinitions } from "./ast";

const TAB_WIDTH = 4;
const CODE_INDENT = 4;

const FENCE_OPEN = /^( {0,3})(`{3,}|~{3,})[ \t]*(.*)$/;
const ATX_HEADING = /^ {0,3}(#{1,6})(?:[ \t]+(.*?))?(?:[ \t]+#+)?[ \t]*$/;
const THEMATIC_BREAK = /^ {0,3}(?:(?:\*[ \t]*){3,}|(?:-[ \t]*){3,}|(?:_[ \t]*){3,})$/;
const BLOCKQUOTE = /^ {0,3}> ?/;
const LIST_ITEM = /^( {0,3})([-+*]|\d{1,9}[.)])(?= |$)( *)/;
const SETEXT_UNDERLINE = /^ {0,3}(=+|-+)[ \t]*$/;
const LINK_DEFINITION =
  /^ {0,3}\[((?:[^\]\\]|\\.){1,999})\]:[ \t]*<?([^\s>]+)>?(?:[ \t]+(?:"([^"]*)"|'([^']*)'|\(([^)]*)\)))?[ \t]*$/;

interface ListMarker {
  ordered: boolean;
  /** The bullet character, or the ordered delimiter (`.` or `)`); a different one starts a new list. */
  symbol: string;
  start: number;
  /** Columns from the line start to the item's content. */
  contentIndent: number;
}

interface ParsedLines {
  blocks: Block[];
  /** A blank line separated two of the blocks: inside a list item, that makes the list loose. */
  hasGap: boolean;
}

/** Splits markdown into blocks, collecting link reference definitions into `definitions` as it goes. */
export function parseBlocks(markdown: string, definitions: LinkDefinitions): Block[] {
  const lines = markdown.replace(/\r\n?/g, "\n").split("\n").map(expandTabs);
  return parseLines(lines, definitions).blocks;
}

function parseLines(lines: string[], definitions: LinkDefinitions): ParsedLines {
  const blocks: Block[] = [];
  let hasGap = false;
  let blankSinceLastBlock = false;
  let i = 0;

  while (i < lines.length) {
    if (isBlank(lines[i])) {
      blankSinceLastBlock = true;
      i++;
      continue;
    }

    const before = blocks.length;
    i = parseBlock(lines, i, blocks, definitions);
    if (blocks.length > before) {
      hasGap ||= blankSinceLastBlock && before > 0;
      blankSinceLastBlock = false;
    }
  }

  return { blocks, hasGap };
}

/** Parses the block starting at `start` into `blocks` and returns the index of the first line after it. */
function parseBlock(lines: string[], start: number, blocks: Block[], definitions: LinkDefinitions): number {
  const line = lines[start];

  const fence = openingFence(line);
  if (fence) {
    return parseFencedCode(lines, start, fence, blocks);
  }

  const heading = ATX_HEADING.exec(line);
  if (heading) {
    const level = heading[1].length as 1 | 2 | 3 | 4 | 5 | 6;
    blocks.push({ type: "heading", level, content: (heading[2] ?? "").trim() });
    return start + 1;
  }

  if (THEMATIC_BREAK.test(line)) {
    blocks.push({ type: "thematicBreak" });
    return start + 1;
  }

  if (BLOCKQUOTE.test(line)) {
    return parseBlockquote(lines, start, blocks, definitions);
  }

  if (listMarker(line)) {
    return parseList(lines, start, blocks, definitions);
  }

  if (indentOf(line) >= CODE_INDENT) {
    return parseIndentedCode(lines, start, blocks);
  }

  return parseParagraph(lines, start, blocks, definitions);
}

function parseFencedCode(lines: string[], start: number, fence: RegExpExecArray, blocks: Block[]): number {
  const [, indent, marker, info] = fence;
  const closing = new RegExp(`^ {0,3}${marker[0] === "`" ? "`" : "~"}{${marker.length},}[ \\t]*$`);
  const content: string[] = [];

  let i = start + 1;
  while (i < lines.length && !closing.test(lines[i])) {
    content.push(stripIndent(lines[i], indent.length));
    i++;
  }

  const lang = info.trim().split(/\s+/)[0];
  blocks.push({
    type: "code",
    lang: lang ? unescapeBackslashes(lang) : undefined,
    value: content.length > 0 ? content.join("\n") + "\n" : "",
  });
  return i + 1;
}

function parseIndentedCode(lines: string[], start: number, blocks: Block[]): number {
  const content: string[] = [];
  let i = start;
  while (i < lines.length && (isBlank(lines[i]) || indentOf(lines[i]) >= CODE_INDENT)) {
    content.push(stripIndent(lines[i], CODE_INDENT));
    i++;
  }

  // Trailing blank lines are not part of the code; leave them for the caller to see.
  while (isBlank(content[content.length - 1])) {
    content.pop();
    i--;
  }

  blocks.push({ type: "code", value: content.join("\n") + "\n" });
  return i;
}

function parseBlockquote(lines: string[], start: number, blocks: Block[], definitions: LinkDefinitions): number {
  const content: string[] = [];
  let i = start;

  while (i < lines.length) {
    const line = lines[i];
    if (BLOCKQUOTE.test(line)) {
      content.push(line.replace(BLOCKQUOTE, ""));
    } else if (isLazyContinuation(line, content)) {
      content.push(line);
    } else {
      break;
    }
    i++;
  }

  blocks.push({ type: "blockquote", children: parseLines(content, definitions).blocks });
  return i;
}

function parseList(lines: string[], start: number, blocks: Block[], definitions: LinkDefinitions): number {
  const first = listMarker(lines[start])!;
  const items: Block[][] = [];
  let loose = false;
  let i = start;

  while (i < lines.length) {
    const marker = listMarker(lines[i]);
    if (!marker || !sameList(marker, first)) {
      break;
    }

    const itemLines = [lines[i].slice(marker.contentIndent)];
    i++;
    while (i < lines.length) {
      const line = lines[i];
      const previousBlank = isBlank(itemLines[itemLines.length - 1]);
      if (isBlank(line) || indentOf(line) >= marker.contentIndent) {
        itemLines.push(line.slice(marker.contentIndent));
      } else if (isSibling(line, first)) {
        break;
      } else if (!previousBlank && isLazyContinuation(line, itemLines)) {
        itemLines.push(line);
      } else {
        break;
      }
      i++;
    }

    let trailingBlanks = 0;
    while (itemLines.length > 1 && isBlank(itemLines[itemLines.length - 1])) {
      itemLines.pop();
      trailingBlanks++;
    }

    const parsed = parseLines(itemLines, definitions);
    items.push(parsed.blocks);
    loose ||= parsed.hasGap;

    const next = i < lines.length ? listMarker(lines[i]) : null;
    if (next && sameList(next, first)) {
      // A blank line between two items makes the list loose.
      loose ||= trailingBlanks > 0;
      continue;
    }
    i -= trailingBlanks;
    break;
  }

  blocks.push({ type: "list", ordered: first.ordered, start: first.start, tight: !loose, items });
  return i;
}

function parseParagraph(lines: string[], start: number, blocks: Block[], definitions: LinkDefinitions): number {
  const content: string[] = [];
  let i = start;

  while (i < lines.length) {
    const line = lines[i];
    if (isBlank(line)) {
      break;
    }
    if (content.length > 0) {
      const setext = SETEXT_UNDERLINE.exec(line);
      if (setext) {
        const text = takeDefinitions(content, definitions);
        if (text.length > 0) {
          blocks.push({ type: "heading", level: setext[1][0] === "=" ? 1 : 2, content: joinParagraph(text) });
          return i + 1;
        }
      }
      if (interruptsParagraph(line)) {
        break;
      }
    }
    content.push(line);
    i++;
  }

  const text = takeDefinitions(content, definitions);
  if (text.length > 0) {
    blocks.push({ type: "paragraph", content: joinParagraph(text) });
  }
  return i;
}

function joinParagraph(lines: string[]): string {
  return lines
    .map((line) => line.trimStart())
    .join("\n")
    .trimEnd();
}

/** Removes link reference definitions from the start of a paragraph, recording them. */
function takeDefinitions(lines: string[], definitions: LinkDefinitions): string[] {
  let i = 0;
  while (i < lines.length) {
    const match = LINK_DEFINITION.exec(lines[i]);
    if (!match) {
      break;
    }
    const label = normalizeLabel(match[1]);
    if (label && !definitions.has(label)) {
      definitions.set(label, { href: unescapeBackslashes(match[2]), title: match[3] ?? match[4] ?? match[5] });
    }
    i++;
  }
  return lines.slice(i);
}

function interruptsParagraph(line: string): boolean {
  if (ATX_HEADING.test(line) || THEMATIC_BREAK.test(line) || BLOCKQUOTE.test(line) || openingFence(line)) {
    return true;
  }
  // An empty item, or an ordered list not starting at 1, cannot interrupt a paragraph.
  const marker = listMarker(line);
  return marker !== null && !isBlank(line.slice(marker.contentIndent)) && (!marker.ordered || marker.start === 1);
}

/** A line that continues the previous paragraph even without its container's marker or indent. */
function isLazyContinuation(line: string, previous: string[]): boolean {
  const last = previous[previous.length - 1];
  if (last === undefined || isBlank(last) || interruptsParagraph(line)) {
    return false;
  }
  return !openingFence(last) && !ATX_HEADING.test(last) && !THEMATIC_BREAK.test(last) && indentOf(last) < CODE_INDENT;
}

function openingFence(line: string): RegExpExecArray | null {
  const fence = FENCE_OPEN.exec(line);
  // A backtick fence's info string may not contain backticks (that is inline code instead).
  return fence && !(fence[2][0] === "`" && fence[3].includes("`")) ? fence : null;
}

function listMarker(line: string): ListMarker | null {
  if (THEMATIC_BREAK.test(line)) {
    return null;
  }
  const match = LIST_ITEM.exec(line);
  if (!match) {
    return null;
  }

  const [whole, indent, marker, spaces] = match;
  const ordered = /\d/.test(marker[0]);
  const isEmpty = isBlank(line.slice(whole.length));
  // Content five or more spaces past the marker is indented code inside the item, which starts one space in.
  const gap = isEmpty || spaces.length > CODE_INDENT ? 1 : spaces.length;
  return {
    ordered,
    symbol: ordered ? marker[marker.length - 1] : marker,
    start: ordered ? parseInt(marker, 10) : 1,
    contentIndent: indent.length + marker.length + gap,
  };
}

function sameList(a: ListMarker, b: ListMarker): boolean {
  return a.ordered === b.ordered && a.symbol === b.symbol;
}

/** The next item of the same list always starts a new item, even where it could not interrupt a paragraph. */
function isSibling(line: string, first: ListMarker): boolean {
  const marker = listMarker(line);
  return marker !== null && sameList(marker, first);
}

function expandTabs(line: string): string {
  if (!line.includes("\t")) {
    return line;
  }
  let column = 0;
  let out = "";
  for (const char of line) {
    if (char === "\t") {
      const spaces = TAB_WIDTH - (column % TAB_WIDTH);
      out += " ".repeat(spaces);
      column += spaces;
    } else {
      out += char;
      column++;
    }
  }
  return out;
}

function isBlank(line: string | undefined): boolean {
  return line !== undefined && line.trim() === "";
}

function indentOf(line: string): number {
  return line.length - line.trimStart().length;
}

function stripIndent(line: string, count: number): string {
  let i = 0;
  while (i < count && line[i] === " ") {
    i++;
  }
  return line.slice(i);
}

export function unescapeBackslashes(text: string): string {
  return text.replace(/\\([!-/:-@[-`{-~])/g, "$1");
}
