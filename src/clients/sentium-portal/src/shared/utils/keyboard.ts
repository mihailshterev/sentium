/** keyCode browsers report for a keydown that an IME consumes (VK_PROCESSKEY). */
const IME_PROCESS_KEY_CODE = 229;

/**
 * True while an IME (Japanese, Chinese, Korean…) is composing text. The Enter that confirms a
 * conversion candidate must not submit. Safari delivers `compositionend` before that keydown,
 * leaving `isComposing` false, so the deprecated keyCode 229 check is kept as the fallback.
 */
export const isImeComposing = (event: KeyboardEvent | { nativeEvent: KeyboardEvent }): boolean => {
  const native = "nativeEvent" in event ? event.nativeEvent : event;
  return native.isComposing || native.keyCode === IME_PROCESS_KEY_CODE;
};
