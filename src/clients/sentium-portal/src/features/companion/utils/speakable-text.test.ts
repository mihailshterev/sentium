import { describe, expect, it } from "vitest";
import { CODE_OMITTED, createSpeechChunker, toSpeakable } from "./speakable-text";

describe("toSpeakable", () => {
  it("drops markdown marks and keeps the words", () => {
    expect(toSpeakable("## The **drill** is in the _garage_")).toBe("The drill is in the garage");
    expect(toSpeakable("- first item")).toBe("first item");
    expect(toSpeakable("3. third step")).toBe("third step");
    expect(toSpeakable("> quoted `inline` text")).toBe("quoted inline text");
  });

  it("keeps link and image text but never reads a URL", () => {
    expect(toSpeakable("See [the manual](https://example.com/a?b=1) first")).toBe("See the manual first");
    expect(toSpeakable("![shelf photo](/img/1.png)")).toBe("shelf photo");
    expect(toSpeakable("Docs at https://example.com/very/long/path today")).toBe("Docs at link today");
  });

  it("flattens tables and removes HTML tags", () => {
    expect(toSpeakable("| Item | Place |")).toBe("Item Place");
    expect(toSpeakable("a <br/> b")).toBe("a b");
  });

  it("returns nothing when no words are left", () => {
    expect(toSpeakable("---")).toBe("");
    expect(toSpeakable("**  **")).toBe("");
    expect(toSpeakable("|---|---|")).toBe("");
  });
});

describe("createSpeechChunker", () => {
  const speak = (...deltas: string[]) => {
    const chunker = createSpeechChunker();
    return [...deltas.flatMap((delta) => chunker.push(delta)), ...chunker.flush()];
  };

  it("emits a sentence as soon as it is complete, while the answer is still streaming", () => {
    const chunker = createSpeechChunker();

    expect(chunker.push("The drill is in the gar")).toEqual([]);
    expect(chunker.push("age on the second shelf. It is ")).toEqual(["The drill is in the garage on the second shelf."]);
    expect(chunker.push("next to the saw")).toEqual([]);
    expect(chunker.flush()).toEqual(["It is next to the saw"]);
  });

  it("does not split inside a number", () => {
    expect(speak("The shelf is 3.14 metres from the door, roughly. ")).toEqual([
      "The shelf is 3.14 metres from the door, roughly.",
    ]);
  });

  it("merges short fragments into the next sentence instead of speaking them alone", () => {
    expect(speak("Yes. The drill is in the garage on the second shelf. ")).toEqual([
      "Yes. The drill is in the garage on the second shelf.",
    ]);
  });

  it("still speaks a short answer when it is all there is", () => {
    expect(speak("Yes.")).toEqual(["Yes."]);
  });

  it("says code omitted once instead of reading a fenced block", () => {
    const pieces = speak(
      "Run this command to list the files:\n",
      "```bash\nls -la /srv",
      "\nrm -rf nothing\n``",
      "`\nThen check the output carefully. ",
    );

    expect(pieces).toEqual([
      "Run this command to list the files:",
      CODE_OMITTED,
      "Then check the output carefully.",
    ]);
  });

  it("drops an unterminated code block at the end", () => {
    expect(speak("Here is the script you asked for:\n```js\nconst a = 1;")).toEqual([
      "Here is the script you asked for:",
      CODE_OMITTED,
    ]);
  });

  it("treats list items as separate pieces", () => {
    expect(speak("- The drill is on the second shelf\n- The saw hangs on the back wall\n")).toEqual([
      "The drill is on the second shelf",
      "The saw hangs on the back wall",
    ]);
  });

  it("splits a very long sentence so no piece exceeds the synthesis limit", () => {
    const long = `${"word ".repeat(200).trim()}.`;
    const pieces = speak(long);

    expect(pieces.length).toBeGreaterThan(1);
    expect(pieces.every((piece) => piece.length <= 400)).toBe(true);
    expect(pieces.join(" ")).toBe(long);
  });

  it("emits nothing for an answer with no words", () => {
    expect(speak("---\n", "***\n")).toEqual([]);
  });
});
