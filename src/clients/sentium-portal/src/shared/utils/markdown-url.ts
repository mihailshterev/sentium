import { defaultUrlTransform } from "react-markdown";

const ALLOWED_APP_SCHEMES = ["obsidian:"];

/**
 * react-markdown drops every protocol it does not know, which would break the `obsidian://open` links assistants use
 * to cite vault notes. Those are let through; everything else gets the default sanitising.
 */
export function markdownUrlTransform(url: string): string {
  const lower = url.trim().toLowerCase();
  return ALLOWED_APP_SCHEMES.some((scheme) => lower.startsWith(scheme)) ? url : defaultUrlTransform(url);
}
