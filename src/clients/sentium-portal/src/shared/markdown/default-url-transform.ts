const SAFE_PROTOCOL = /^(https?|ircs?|mailto|xmpp)$/i;

/**
 * Keeps relative URLs and the http(s), irc(s), mailto and xmpp protocols; any other protocol (`javascript:`,
 * `data:`, `vbscript:` and the like) becomes an empty string. A colon only counts as a protocol separator when it
 * comes before any `/`, `?` or `#`.
 */
export function defaultUrlTransform(url: string): string {
  const colon = url.indexOf(":");
  const questionMark = url.indexOf("?");
  const numberSign = url.indexOf("#");
  const slash = url.indexOf("/");

  if (
    colon === -1 ||
    (slash !== -1 && colon > slash) ||
    (questionMark !== -1 && colon > questionMark) ||
    (numberSign !== -1 && colon > numberSign) ||
    SAFE_PROTOCOL.test(url.slice(0, colon))
  ) {
    return url;
  }

  return "";
}
