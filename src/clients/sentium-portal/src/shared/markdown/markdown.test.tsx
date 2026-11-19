import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Markdown } from "./markdown";

const html = (markdown: string, props: Partial<Parameters<typeof Markdown>[0]> = {}) =>
  render(<Markdown {...props}>{markdown}</Markdown>).container.innerHTML;

describe("Markdown blocks", () => {
  it("renders ATX and setext headings", () => {
    expect(html("# One\n## Two ##\n\nThree\n=====\n\nFour\n----")).toBe(
      "<h1>One</h1><h2>Two</h2><h1>Three</h1><h2>Four</h2>",
    );
  });

  it("joins paragraph lines with soft breaks and splits paragraphs on blank lines", () => {
    expect(html("first line\nsecond line\n\nnext")).toBe("<p>first line\nsecond line</p><p>next</p>");
  });

  it("renders hard line breaks from trailing spaces and backslashes", () => {
    expect(html("a  \nb\\\nc")).toBe("<p>a<br>b<br>c</p>");
  });

  it("renders fenced code with its language and keeps the content verbatim", () => {
    expect(html("```ts\nconst a = <b>*x*</b>;\n\n```")).toBe(
      '<pre><code class="language-ts">const a = &lt;b&gt;*x*&lt;/b&gt;;\n\n</code></pre>',
    );
  });

  it("renders indented code", () => {
    expect(html("    code\n      more")).toBe("<pre><code>code\n  more\n</code></pre>");
  });

  it("renders a tight bullet list without paragraphs", () => {
    expect(html("- one\n- two\n* other")).toBe("<ul><li>one</li><li>two</li></ul><ul><li>other</li></ul>");
  });

  it("renders a loose list with paragraphs", () => {
    expect(html("1. one\n\n2. two")).toBe("<ol><li><p>one</p></li><li><p>two</p></li></ol>");
  });

  it("keeps an ordered list's start number", () => {
    expect(html("3) three\n4) four")).toBe('<ol start="3"><li>three</li><li>four</li></ol>');
  });

  it("nests lists by indentation without loosening the outer list", () => {
    expect(html("- a\n  - b\n\n  - c\n- d")).toBe(
      "<ul><li>a<ul><li><p>b</p></li><li><p>c</p></li></ul></li><li>d</li></ul>",
    );
  });

  it("continues a list item lazily and with indented paragraphs", () => {
    expect(html("- item\ncontinued\n\n  second")).toBe("<ul><li><p>item\ncontinued</p><p>second</p></li></ul>");
  });

  it("renders blockquotes, including lazy continuation lines", () => {
    expect(html("> quoted\nlazy\n> > nested")).toBe(
      "<blockquote><p>quoted\nlazy</p><blockquote><p>nested</p></blockquote></blockquote>",
    );
  });

  it("tells thematic breaks from list items", () => {
    expect(html("a\n\n---\n\n* * *\n- - -")).toBe("<p>a</p><hr><hr><hr>");
  });

  it("shows raw HTML as text instead of rendering it", () => {
    expect(html('<script>alert(1)</script>\n\n<img src=x onerror="alert(1)">')).toBe(
      "<p>&lt;script&gt;alert(1)&lt;/script&gt;</p><p>&lt;img src=x onerror=\"alert(1)\"&gt;</p>",
    );
  });
});

describe("Markdown inline", () => {
  it("renders emphasis, strong and their nesting", () => {
    expect(html("*a* **b** ***c*** _d_ __e__ **f *g* h**")).toBe(
      "<p><em>a</em> <strong>b</strong> <em><strong>c</strong></em> <em>d</em> <strong>e</strong> <strong>f <em>g</em> h</strong></p>",
    );
  });

  it("does not treat intraword underscores or lone stars as emphasis", () => {
    expect(html("snake_case_name and 2 * 3 * 4")).toBe("<p>snake_case_name and 2 * 3 * 4</p>");
  });

  it("renders code spans literally", () => {
    expect(html("use `a *b* <c>` and `` x ` y ``")).toBe("<p>use <code>a *b* &lt;c&gt;</code> and <code>x ` y</code></p>");
  });

  it("applies backslash escapes and entities", () => {
    expect(html("\\*not em\\* &amp; &copy; &#65; &#x42; &bogus;")).toBe("<p>*not em* &amp; © A B &amp;bogus;</p>");
  });

  it("renders inline links with titles and emphasis inside", () => {
    expect(html('[**docs**](https://example.com/a_(b) "Title")')).toBe(
      '<p><a href="https://example.com/a_(b)" title="Title"><strong>docs</strong></a></p>',
    );
  });

  it("resolves reference links, including collapsed and shortcut forms", () => {
    expect(html("[full][Ref] [ref][] [ref]\n\n[ref]: https://example.com 'T'")).toBe(
      '<p><a href="https://example.com" title="T">full</a> <a href="https://example.com" title="T">ref</a> <a href="https://example.com" title="T">ref</a></p>',
    );
  });

  it("renders autolinks and leaves unmatched brackets as text", () => {
    expect(html("<https://example.com> <me@example.com> [not a link] a]b")).toBe(
      '<p><a href="https://example.com">https://example.com</a> <a href="mailto:me@example.com">me@example.com</a> [not a link] a]b</p>',
    );
  });

  it("renders images with their alt text", () => {
    const { container } = render(<Markdown>{"![a *b*](/img.png)"}</Markdown>);
    const image = container.querySelector("img")!;

    expect(image.getAttribute("src")).toBe("/img.png");
    expect(image.getAttribute("alt")).toBe("a b");
    expect(image.className).not.toBe("");
  });

  it("does not nest links", () => {
    expect(html("[a [b](/b) c](/a)")).toBe('<p>[a <a href="/b">b</a> c](/a)</p>');
  });
});

describe("Markdown URLs", () => {
  it("blanks unsafe protocols by default", () => {
    expect(html("[x](javascript:alert(1)) [y](data:text/html,hi) [z](/relative?a=b:c)")).toBe(
      '<p><a href="">x</a> <a href="">y</a> <a href="/relative?a=b:c">z</a></p>',
    );
  });

  it("uses a custom URL transform and link renderer", () => {
    const markup = html("[note](wiki:Some%20Note)", {
      urlTransform: (url) => url.toUpperCase(),
      components: { a: ({ href, children }) => <button data-href={href}>{children}</button> },
    });

    expect(markup).toBe('<p><button data-href="WIKI:SOME%20NOTE">note</button></p>');
  });
});
