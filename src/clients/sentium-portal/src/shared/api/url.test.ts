import { describe, expect, it } from "vitest";
import { path, pathIn, withQuery } from "./url";

describe("path", () => {
  it("encodes each interpolated value as a single segment", () => {
    expect(path`/praxis/agents/${"a/b?c#d"}/tools`).toBe("/praxis/agents/a%2Fb%3Fc%23d/tools");
  });

  it("leaves literal parts untouched and stringifies numbers", () => {
    expect(path`/tempus/years/${2026}/months/${11}`).toBe("/tempus/years/2026/months/11");
  });

  it("returns the literal when nothing is interpolated", () => {
    expect(path`/praxis/tools`).toBe("/praxis/tools");
  });
});

describe("pathIn", () => {
  it("prefixes the base unencoded and encodes the segments", () => {
    const url = pathIn("/praxis");
    expect(url`/agents/${"a b"}`).toBe("/praxis/agents/a%20b");
  });
});

describe("withQuery", () => {
  it("appends params in key order with %20 for spaces", () => {
    expect(withQuery("/x", { page: 2, pageSize: 10, search: "trip & tax" })).toBe(
      "/x?page=2&pageSize=10&search=trip%20%26%20tax",
    );
  });

  it("omits undefined, null and empty strings but keeps false and 0", () => {
    expect(withQuery("/x", { a: undefined, b: null, c: "", d: false, e: 0 })).toBe("/x?d=false&e=0");
  });

  it("returns the base unchanged when no param is present", () => {
    expect(withQuery("/x", { a: undefined })).toBe("/x");
  });

  it("repeats the key for array values", () => {
    expect(withQuery("/x", { tag: ["a", "b c"] })).toBe("/x?tag=a&tag=b%20c");
  });

  it("extends a base that already has a query", () => {
    expect(withQuery("/x?scope=All", { today: "2026-11-24" })).toBe("/x?scope=All&today=2026-11-24");
  });
});
