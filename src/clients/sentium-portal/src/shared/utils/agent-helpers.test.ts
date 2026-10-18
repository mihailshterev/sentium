import { describe, it, expect } from "vitest";
import { parseAssignments } from "./agent-helpers";

describe("parseAssignments", () => {
  it("returns null for empty input", () => {
    expect(parseAssignments("")).toBeNull();
  });

  it("returns null when there is no JSON array", () => {
    expect(parseAssignments("no brackets here")).toBeNull();
  });

  it("returns null when brackets are malformed/reversed", () => {
    expect(parseAssignments("] then [")).toBeNull();
  });

  it("returns null for invalid JSON inside the brackets", () => {
    expect(parseAssignments("[ not valid json ]")).toBeNull();
  });

  it("returns null when the parsed JSON is not an array", () => {
    expect(parseAssignments('{"agent":"a","task":"t"}')).toBeNull();
  });

  it("parses a well-formed assignment array", () => {
    const out = parseAssignments('[{"agent":"Analyzer","task":"inspect logs"}]');
    expect(out).toEqual([{ agent: "Analyzer", task: "inspect logs" }]);
  });

  it("extracts the array even when surrounded by prose", () => {
    const out = parseAssignments('Plan: [{"agent":"A","task":"do"}] done.');
    expect(out).toEqual([{ agent: "A", task: "do" }]);
  });

  it("trims whitespace on agent and task", () => {
    const out = parseAssignments('[{"agent":"  A  ","task":"  do  "}]');
    expect(out).toEqual([{ agent: "A", task: "do" }]);
  });

  it("drops entries missing required fields or with blank values", () => {
    const out = parseAssignments(
      '[{"agent":"A","task":"keep"},{"agent":"","task":"x"},{"agent":"B"},{"agent":"   ","task":"   "}]',
    );
    expect(out).toEqual([{ agent: "A", task: "keep" }]);
  });

  it("returns null when every entry is filtered out", () => {
    expect(parseAssignments('[{"agent":"","task":""},{"foo":"bar"}]')).toBeNull();
  });
});
