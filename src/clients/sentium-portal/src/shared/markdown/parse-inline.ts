import { normalizeLabel, type Inline, type LinkDefinitions } from "./ast";
import { decodeEntity } from "./entities";

const ESCAPABLE = /[!-/:-@[-`{-~]/;
const PUNCTUATION = /[\p{P}\p{S}]/u;
const WHITESPACE = /\s/;
const URI_AUTOLINK = /^<([a-zA-Z][a-zA-Z0-9+.-]{1,31}:[^\s<>]*)>/;
const EMAIL_AUTOLINK = /^<([a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*)>/;
const ENTITY = /^&(?:#[0-9]{1,7}|#[xX][0-9a-fA-F]{1,6}|[a-zA-Z][a-zA-Z0-9]{1,31});/;
const RULE_OF_THREE = 3;
const SPACE_CODE = 0x20;
const DELETE_CODE = 0x7f;

/** A run of `*` or `_` that may open or close emphasis; `node` is the text node holding its characters. */
interface Delimiter {
  node: { type: "text"; value: string };
  char: "*" | "_";
  count: number;
  originalCount: number;
  canOpen: boolean;
  canClose: boolean;
}

/** An unmatched `[` or `![`. */
interface Bracket {
  node: { type: "text"; value: string };
  image: boolean;
  /** Where the bracketed text starts in the source, for reference labels. */
  textStart: number;
  /** Delimiters at or past this index were opened inside the brackets. */
  delimiterBottom: number;
  /** False once a link has formed after it: links may not contain links. */
  active: boolean;
}

interface LinkTarget {
  href: string;
  title?: string;
  end: number;
}

/** Parses the inline content of one block (a paragraph or heading). */
export function parseInline(source: string, definitions: LinkDefinitions): Inline[] {
  const nodes: Inline[] = [];
  const delimiters: Delimiter[] = [];
  const brackets: Bracket[] = [];
  let text = "";
  let i = 0;

  const flushText = () => {
    if (text) {
      nodes.push({ type: "text", value: text });
      text = "";
    }
  };

  while (i < source.length) {
    const char = source[i];

    if (char === "\\") {
      const next = source[i + 1];
      if (next === "\n") {
        flushText();
        nodes.push({ type: "break" });
        i += 2;
        i = skipLeadingSpaces(source, i);
      } else if (next !== undefined && ESCAPABLE.test(next)) {
        text += next;
        i += 2;
      } else {
        text += char;
        i++;
      }
      continue;
    }

    if (char === "`") {
      const span = codeSpan(source, i);
      if (span) {
        flushText();
        nodes.push({ type: "code", value: span.value });
        i = span.end;
      } else {
        const run = runLength(source, i, "`");
        text += source.slice(i, i + run);
        i += run;
      }
      continue;
    }

    if (char === "\n") {
      const hard = /( {2,})$/.test(text);
      text = text.replace(/ +$/, "");
      flushText();
      nodes.push({ type: hard ? "break" : "softbreak" });
      i = skipLeadingSpaces(source, i + 1);
      continue;
    }

    if (char === "<") {
      const uri = URI_AUTOLINK.exec(source.slice(i));
      const email = uri ? null : EMAIL_AUTOLINK.exec(source.slice(i));
      const match = uri ?? email;
      if (match) {
        flushText();
        const href = uri ? match[1] : `mailto:${match[1]}`;
        nodes.push({ type: "link", href, children: [{ type: "text", value: match[1] }] });
        i += match[0].length;
        continue;
      }
    }

    if (char === "&") {
      const entity = ENTITY.exec(source.slice(i));
      const decoded = entity ? decodeEntity(entity[0]) : null;
      if (entity && decoded !== null) {
        text += decoded;
        i += entity[0].length;
        continue;
      }
    }

    if (char === "*" || char === "_") {
      const run = runLength(source, i, char);
      const before = i > 0 ? source[i - 1] : "\n";
      const after = source[i + run] ?? "\n";
      const { canOpen, canClose } = flanking(char, before, after);
      flushText();
      const node = { type: "text" as const, value: source.slice(i, i + run) };
      nodes.push(node);
      delimiters.push({ node, char, count: run, originalCount: run, canOpen, canClose });
      i += run;
      continue;
    }

    if (char === "[" || (char === "!" && source[i + 1] === "[")) {
      flushText();
      const image = char === "!";
      const node = { type: "text" as const, value: image ? "![" : "[" };
      nodes.push(node);
      i += image ? 2 : 1;
      brackets.push({ node, image, textStart: i, delimiterBottom: delimiters.length, active: true });
      continue;
    }

    if (char === "]") {
      flushText();
      const end = closeBracket(source, i, nodes, delimiters, brackets, definitions);
      if (end === null) {
        nodes.push({ type: "text", value: "]" });
        i++;
      } else {
        i = end;
      }
      continue;
    }

    text += char;
    i++;
  }

  flushText();
  processEmphasis(nodes, delimiters, 0);
  return mergeText(nodes);
}

/** Tries to turn the innermost `[` and this `]` into a link or image; returns the index after it, or null. */
function closeBracket(
  source: string,
  at: number,
  nodes: Inline[],
  delimiters: Delimiter[],
  brackets: Bracket[],
  definitions: LinkDefinitions,
): number | null {
  const opener = brackets.pop();
  if (!opener?.active) {
    return null;
  }

  const openerIndex = nodes.indexOf(opener.node);
  const label = source.slice(opener.textStart, at);
  const target = inlineTarget(source, at + 1) ?? referenceTarget(source, at + 1, label, definitions);
  if (!target) {
    return null;
  }

  processEmphasis(nodes, delimiters, opener.delimiterBottom);
  delimiters.length = Math.min(delimiters.length, opener.delimiterBottom);

  const children = mergeText(nodes.splice(openerIndex + 1));
  nodes.pop();
  if (opener.image) {
    nodes.push({ type: "image", src: target.href, alt: plainText(children), title: target.title });
  } else {
    nodes.push({ type: "link", href: target.href, title: target.title, children });
    for (const bracket of brackets) {
      if (!bracket.image) {
        bracket.active = false;
      }
    }
  }
  return target.end;
}

/** `(destination "title")` right after the closing bracket. */
function inlineTarget(source: string, start: number): LinkTarget | null {
  if (source[start] !== "(") {
    return null;
  }
  let i = skipWhitespace(source, start + 1);

  let href = "";
  if (source[i] === "<") {
    const close = source.indexOf(">", i);
    const candidate = close === -1 ? "" : source.slice(i + 1, close);
    if (close === -1 || /[\n<]/.test(candidate)) {
      return null;
    }
    href = candidate;
    i = close + 1;
  } else {
    let depth = 0;
    const begin = i;
    while (i < source.length && !isSpaceOrControl(source, i)) {
      if (source[i] === "\\" && ESCAPABLE.test(source[i + 1] ?? "")) {
        i += 2;
        continue;
      }
      if (source[i] === "(") {
        depth++;
      } else if (source[i] === ")") {
        if (depth === 0) {
          break;
        }
        depth--;
      }
      i++;
    }
    if (depth !== 0) {
      return null;
    }
    href = source.slice(begin, i);
  }

  const afterHref = i;
  i = skipWhitespace(source, i);
  let title: string | undefined;
  if (i > afterHref && /["'(]/.test(source[i] ?? "")) {
    const close = source[i] === "(" ? ")" : source[i];
    const end = source.indexOf(close, i + 1);
    if (end === -1) {
      return null;
    }
    title = decodeText(source.slice(i + 1, end));
    i = skipWhitespace(source, end + 1);
  }

  if (source[i] !== ")") {
    return null;
  }
  return { href: decodeText(href), title, end: i + 1 };
}

/** `[label]`, `[]` or nothing after the closing bracket, resolved against the document's definitions. */
function referenceTarget(source: string, start: number, text: string, definitions: LinkDefinitions): LinkTarget | null {
  let label = text;
  let end = start;
  if (source[start] === "[") {
    const close = source.indexOf("]", start + 1);
    if (close !== -1) {
      const explicit = source.slice(start + 1, close);
      label = explicit.trim() === "" ? text : explicit;
      end = close + 1;
    }
  }

  const definition = definitions.get(normalizeLabel(label));
  return definition ? { href: definition.href, title: definition.title, end } : null;
}

/** CommonMark's emphasis pass over the delimiters from `bottom` on, nesting the nodes between matched runs. */
function processEmphasis(nodes: Inline[], delimiters: Delimiter[], bottom: number) {
  let closerIndex = bottom;

  while (closerIndex < delimiters.length) {
    const closer = delimiters[closerIndex];
    if (!closer.canClose) {
      closerIndex++;
      continue;
    }

    let openerIndex = closerIndex - 1;
    while (openerIndex >= bottom && !isMatch(delimiters[openerIndex], closer)) {
      openerIndex--;
    }

    if (openerIndex < bottom) {
      if (!closer.canOpen) {
        delimiters.splice(closerIndex, 1);
      } else {
        closerIndex++;
      }
      continue;
    }

    const opener = delimiters[openerIndex];
    const used = opener.count >= 2 && closer.count >= 2 ? 2 : 1;
    opener.count -= used;
    closer.count -= used;
    opener.node.value = opener.node.value.slice(used);
    closer.node.value = closer.node.value.slice(used);

    const start = nodes.indexOf(opener.node);
    const end = nodes.indexOf(closer.node);
    const children = mergeText(nodes.splice(start + 1, end - start - 1));
    nodes.splice(start + 1, 0, used === 2 ? { type: "strong", children } : { type: "emphasis", children });

    delimiters.splice(openerIndex + 1, closerIndex - openerIndex - 1);
    closerIndex = openerIndex + 1;

    if (opener.count === 0) {
      nodes.splice(nodes.indexOf(opener.node), 1);
      delimiters.splice(openerIndex, 1);
      closerIndex--;
    }
    if (closer.count === 0) {
      nodes.splice(nodes.indexOf(closer.node), 1);
      delimiters.splice(closerIndex, 1);
    }
  }
}

function isMatch(opener: Delimiter, closer: Delimiter): boolean {
  if (opener.char !== closer.char || !opener.canOpen || opener.count === 0) {
    return false;
  }
  // A run that can both open and close only pairs with one whose combined length is not a multiple of three.
  const eitherBoth = opener.canClose || closer.canOpen;
  const sum = opener.originalCount + closer.originalCount;
  return !(
    eitherBoth &&
    sum % RULE_OF_THREE === 0 &&
    !(opener.originalCount % RULE_OF_THREE === 0 && closer.originalCount % RULE_OF_THREE === 0)
  );
}

function flanking(char: "*" | "_", before: string, after: string) {
  const afterSpace = WHITESPACE.test(after);
  const beforeSpace = WHITESPACE.test(before);
  const afterPunct = PUNCTUATION.test(after);
  const beforePunct = PUNCTUATION.test(before);

  const left = !afterSpace && (!afterPunct || beforeSpace || beforePunct);
  const right = !beforeSpace && (!beforePunct || afterSpace || afterPunct);

  if (char === "*") {
    return { canOpen: left, canClose: right };
  }
  return { canOpen: left && (!right || beforePunct), canClose: right && (!left || afterPunct) };
}

function codeSpan(source: string, start: number): { value: string; end: number } | null {
  const length = runLength(source, start, "`");
  let i = start + length;
  while (i < source.length) {
    const next = source.indexOf("`", i);
    if (next === -1) {
      return null;
    }
    const run = runLength(source, next, "`");
    if (run === length) {
      let value = source.slice(start + length, next).replace(/\n/g, " ");
      if (value.length > 2 && value.startsWith(" ") && value.endsWith(" ") && value.trim() !== "") {
        value = value.slice(1, -1);
      }
      return { value, end: next + run };
    }
    i = next + run;
  }
  return null;
}

function runLength(source: string, start: number, char: string): number {
  let i = start;
  while (source[i] === char) {
    i++;
  }
  return i - start;
}

function skipLeadingSpaces(source: string, start: number): number {
  let i = start;
  while (source[i] === " ") {
    i++;
  }
  return i;
}

/** A bare link destination ends at whitespace or an ASCII control character. */
function isSpaceOrControl(source: string, index: number): boolean {
  const code = source.charCodeAt(index);
  return code <= SPACE_CODE || code === DELETE_CODE || WHITESPACE.test(source[index]);
}

function skipWhitespace(source: string, start: number): number {
  let i = start;
  while (i < source.length && WHITESPACE.test(source[i])) {
    i++;
  }
  return i;
}

/** Applies backslash escapes and entities, as in link destinations and titles. */
function decodeText(text: string): string {
  return text
    .replace(/\\([!-/:-@[-`{-~])/g, "$1")
    .replace(/&(?:#[0-9]{1,7}|#[xX][0-9a-fA-F]{1,6}|[a-zA-Z][a-zA-Z0-9]{1,31});/g, (entity) => decodeEntity(entity) ?? entity);
}

function mergeText(nodes: Inline[]): Inline[] {
  const merged: Inline[] = [];
  for (const node of nodes) {
    const last = merged[merged.length - 1];
    if (node.type === "text" && last?.type === "text") {
      merged[merged.length - 1] = { type: "text", value: last.value + node.value };
    } else if (node.type !== "text" || node.value !== "") {
      merged.push(node.type === "text" ? { type: "text", value: node.value } : node);
    }
  }
  return merged;
}

export function plainText(nodes: Inline[]): string {
  return nodes
    .map((node) => {
      switch (node.type) {
        case "text":
        case "code":
          return node.value;
        case "image":
          return node.alt;
        case "break":
        case "softbreak":
          return "\n";
        default:
          return plainText(node.children);
      }
    })
    .join("");
}
