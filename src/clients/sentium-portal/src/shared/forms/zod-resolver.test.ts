import type { ResolverOptions } from "react-hook-form";
import { describe, expect, it } from "vitest";
import { z } from "zod";
import { zodResolver } from "./zod-resolver";

type Values = { name: string; steps: { title: string }[] };

const schema = z.object({
  name: z.string().trim().min(1, "Name is required"),
  steps: z.array(z.object({ title: z.string().min(1, "Title is required") })).min(1, "Add a step"),
});

const options = (names?: string[]): ResolverOptions<Values> => ({
  fields: {},
  names: names as ResolverOptions<Values>["names"],
  shouldUseNativeValidation: undefined,
  criteriaMode: "firstError",
});

describe("zodResolver", () => {
  it("returns the parsed output on success", async () => {
    const result = await zodResolver(schema)({ name: "  a ", steps: [{ title: "t" }] }, undefined, options());

    expect(result).toEqual({ values: { name: "a", steps: [{ title: "t" }] }, errors: {} });
  });

  it("nests the first error of each field path", async () => {
    const result = await zodResolver(schema)({ name: "", steps: [{ title: "" }] }, undefined, options());

    expect(result.values).toEqual({});
    expect(result.errors).toMatchObject({
      name: { type: "too_small", message: "Name is required" },
      steps: [{ title: { type: "too_small", message: "Title is required" } }],
    });
  });

  it("puts an error on a field array itself under root", async () => {
    const result = await zodResolver(schema)({ name: "a", steps: [] }, undefined, options(["name", "steps.0.title"]));

    expect(result.errors).toMatchObject({ steps: { root: { message: "Add a step" } } });
  });
});
