export type QueryValue = string | number | boolean | null | undefined;

export type QueryParams = Readonly<Record<string, QueryValue | readonly QueryValue[]>>;

const isPresent = (value: QueryValue): value is string | number | boolean =>
  value !== undefined && value !== null && value !== "";

/**
 * Builds an API path whose interpolated values are each encoded as one path segment, so an id holding `/`, `?` or
 * `#` can never change the route: path`/praxis/agents/${id}/tools`.
 */
export function path(strings: TemplateStringsArray, ...segments: readonly (string | number)[]): string {
  return strings.reduce(
    (result, literal, i) => result + literal + (i < segments.length ? encodeURIComponent(String(segments[i])) : ""),
    "",
  );
}

/**
 * A {@link path} tag that prefixes a fixed, unencoded base: `const url = pathIn("/praxis"); url\`/agents/${id}\``.
 */
export function pathIn(base: string) {
  return (strings: TemplateStringsArray, ...segments: readonly (string | number)[]): string =>
    base + path(strings, ...segments);
}

/**
 * Appends `params` to `base` in key order. `undefined`, `null` and `""` are left out (`false` and `0` are sent), and an
 * array repeats its key. Values are percent-encoded, so a space is `%20`, never `+`.
 */
export function withQuery<T extends { [K in keyof T]: QueryParams[string] }>(base: string, params: T): string {
  const pairs: string[] = [];
  for (const [key, raw] of Object.entries(params) as [string, QueryParams[string]][]) {
    const values: readonly QueryValue[] = Array.isArray(raw) ? raw : [raw as QueryValue];
    for (const value of values) {
      if (isPresent(value)) {
        pairs.push(`${encodeURIComponent(key)}=${encodeURIComponent(String(value))}`);
      }
    }
  }

  if (pairs.length === 0) {
    return base;
  }
  return `${base}${base.includes("?") ? "&" : "?"}${pairs.join("&")}`;
}
