import { Fragment, useDeferredValue, useMemo, type ReactNode } from "react";
import type { Block, Inline, LinkDefinitions } from "./ast";
import { defaultUrlTransform } from "./default-url-transform";
import { parseBlocks } from "./parse-blocks";
import { parseInline } from "./parse-inline";
import styles from "./markdown.module.css";

export interface MarkdownLinkProps {
  href: string;
  title?: string;
  children: ReactNode;
}

interface MarkdownProps {
  children: string;
  /** Rewrites (or blanks) every link and image URL; defaults to {@link defaultUrlTransform}. */
  urlTransform?: (url: string) => string;
  components?: {
    a?: (props: MarkdownLinkProps) => ReactNode;
  };
}

interface RenderContext {
  definitions: LinkDefinitions;
  urlTransform: (url: string) => string;
  renderLink: (props: MarkdownLinkProps) => ReactNode;
}

const defaultLink = ({ href, title, children }: MarkdownLinkProps) => (
  <a href={href} title={title}>
    {children}
  </a>
);

/**
 * Renders CommonMark as React elements. Raw HTML is shown as text and nothing is injected as markup, so untrusted
 * content (model output, notes) cannot add elements or scripts; URLs go through `urlTransform`.
 */
export function Markdown({ children, urlTransform = defaultUrlTransform, components }: MarkdownProps) {
  // A streamed answer changes many times a second and is re-parsed whole each time; deferring lets React skip
  // intermediate versions while it is busy instead of parsing and rendering every one.
  const source = useDeferredValue(children);
  const document = useMemo(() => {
    const definitions: LinkDefinitions = new Map();
    const blocks = parseBlocks(source, definitions);
    return { blocks, definitions };
  }, [source]);

  const context: RenderContext = {
    definitions: document.definitions,
    urlTransform,
    renderLink: components?.a ?? defaultLink,
  };

  return <>{renderBlocks(document.blocks, context, false)}</>;
}

function renderBlocks(blocks: Block[], context: RenderContext, tight: boolean): ReactNode[] {
  return blocks.map((block, index) => renderBlock(block, index, context, tight));
}

function renderBlock(block: Block, key: number, context: RenderContext, tight: boolean): ReactNode {
  switch (block.type) {
    case "heading": {
      const Tag = `h${block.level}` as const;
      return <Tag key={key}>{renderInline(block.content, context)}</Tag>;
    }
    case "paragraph":
      // Paragraphs in a tight list item render their text directly, without a <p>.
      return tight ? (
        <Fragment key={key}>{renderInline(block.content, context)}</Fragment>
      ) : (
        <p key={key}>{renderInline(block.content, context)}</p>
      );
    case "code":
      return (
        <pre key={key}>
          <code className={block.lang ? `language-${block.lang}` : undefined}>{block.value}</code>
        </pre>
      );
    case "blockquote":
      return <blockquote key={key}>{renderBlocks(block.children, context, false)}</blockquote>;
    case "list": {
      const items = block.items.map((item, index) => <li key={index}>{renderBlocks(item, context, block.tight)}</li>);
      return block.ordered ? (
        <ol key={key} start={block.start === 1 ? undefined : block.start}>
          {items}
        </ol>
      ) : (
        <ul key={key}>{items}</ul>
      );
    }
    case "thematicBreak":
      return <hr key={key} />;
  }
}

function renderInline(source: string, context: RenderContext): ReactNode[] {
  return renderNodes(parseInline(source, context.definitions), context);
}

function renderNodes(nodes: Inline[], context: RenderContext): ReactNode[] {
  return nodes.map((node, key) => {
    switch (node.type) {
      case "text":
        return node.value;
      case "code":
        return <code key={key}>{node.value}</code>;
      case "emphasis":
        return <em key={key}>{renderNodes(node.children, context)}</em>;
      case "strong":
        return <strong key={key}>{renderNodes(node.children, context)}</strong>;
      case "break":
        return <br key={key} />;
      case "softbreak":
        return "\n";
      case "image":
        return (
          <img
            key={key}
            className={styles.image}
            src={context.urlTransform(node.src)}
            alt={node.alt}
            title={node.title}
            loading="lazy"
          />
        );
      case "link":
        return (
          <LinkSlot key={key}>
            {context.renderLink({
              href: context.urlTransform(node.href),
              title: node.title,
              children: renderNodes(node.children, context),
            })}
          </LinkSlot>
        );
    }
  });
}

/** Gives a custom link renderer's output a stable key without asking the renderer to accept one. */
function LinkSlot({ children }: { children: ReactNode }) {
  return children;
}
