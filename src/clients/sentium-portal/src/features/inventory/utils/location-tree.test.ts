import { describe, expect, it } from "vitest";
import { loc } from "../test-fixtures";
import { ancestorsOf, buildTree, descendantsOf, flattenForSelect } from "./location-tree";

const locations = [
  loc("home", "Home"),
  loc("kitchen", "Kitchen", "home", "Home › Kitchen"),
  loc("garage", "garage", "home", "Home › garage"),
  loc("shelf", "Shelf B", "garage", "Home › garage › Shelf B"),
  loc("cabin", "Cabin"),
];

describe("location tree utils", () => {
  it("builds a name-sorted tree from the flat list", () => {
    const tree = buildTree(locations);

    expect(tree.map((n) => n.name)).toEqual(["Cabin", "Home"]);
    expect(tree[1].children.map((n) => n.name)).toEqual(["garage", "Kitchen"]);
    expect(tree[1].children[0].children.map((n) => n.id)).toEqual(["shelf"]);
  });

  it("treats orphans as roots", () => {
    const tree = buildTree([loc("lost", "Lost", "missing-parent")]);

    expect(tree.map((n) => n.id)).toEqual(["lost"]);
  });

  it("finds a location's subtree and ancestors", () => {
    expect([...descendantsOf(locations, "garage")].sort()).toEqual(["garage", "shelf"]);
    expect(ancestorsOf(locations, "shelf")).toEqual(["garage", "home"]);
    expect(ancestorsOf(locations, "home")).toEqual([]);
  });

  it("survives cyclic data", () => {
    const cyclic = [loc("a", "A", "b"), loc("b", "B", "a")];

    expect(ancestorsOf(cyclic, "a")).toEqual(["b", "a"]);
    expect([...descendantsOf(cyclic, "a")].sort()).toEqual(["a", "b"]);
  });

  it("flattens depth-first for selects, skipping excluded subtrees", () => {
    const options = flattenForSelect(locations, descendantsOf(locations, "garage"));

    expect(options.map((o) => o.id)).toEqual(["cabin", "home", "kitchen"]);
    expect(options.find((o) => o.id === "kitchen")?.depth).toBe(1);
  });
});
