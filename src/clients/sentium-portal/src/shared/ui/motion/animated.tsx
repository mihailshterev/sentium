import { useContext, useEffectEvent, useLayoutEffect, useRef, type HTMLAttributes, type Ref } from "react";
import { PresenceContext } from "./presence-context";
import { prefersReducedMotion } from "./reduced-motion";
import { DEFAULT_TIMING, type Pose, type Timing } from "./transitions";

interface AnimatedProps extends HTMLAttributes<HTMLElement> {
  as?: "div" | "section" | "span";
  /** Pose the element enters from when it mounts. */
  from?: Pose;
  /** Pose the element leaves towards before a surrounding {@link Presence} unmounts it. */
  exitTo?: Pose;
  enter?: Timing;
  exit?: Timing;
  ref?: Ref<HTMLElement>;
}

/**
 * An element with enter and exit animations, played with the Web Animations API. With reduced motion only opacity
 * animates; where the API is missing (tests) nothing animates and exits complete at once.
 */
export function Animated({
  as: Tag = "div",
  from,
  exitTo,
  enter = DEFAULT_TIMING,
  exit = DEFAULT_TIMING,
  ref: forwardedRef,
  ...rest
}: AnimatedProps) {
  const presence = useContext(PresenceContext);
  const elementRef = useRef<HTMLElement | null>(null);
  const isPresent = presence?.isPresent ?? true;
  const animateEnter = presence?.animateEnter ?? true;

  const setRef = (element: HTMLElement | null) => {
    elementRef.current = element;
    if (typeof forwardedRef === "function") {
      forwardedRef(element);
    } else if (forwardedRef) {
      forwardedRef.current = element;
    }
  };

  const playEnter = useEffectEvent(() => {
    const element = elementRef.current;
    if (!element || !from || !animateEnter || !canAnimate(element)) {
      return undefined;
    }
    const animation = element.animate([{ ...toKeyframe(from), offset: 0 }], toOptions(enter));
    return () => animation.cancel();
  });

  const playExit = useEffectEvent(() => {
    const element = elementRef.current;
    const complete = () => presence?.onExitComplete();
    if (!element || !exitTo || !canAnimate(element)) {
      complete();
      return undefined;
    }

    if (presence?.popLayout) {
      popOutOfLayout(element);
    }
    const animation = element.animate([{ ...toKeyframe(exitTo), offset: 1 }], { ...toOptions(exit), fill: "forwards" });
    animation.finished.then(complete, () => {});
    return () => {
      animation.cancel();
      restoreLayout(element);
    };
  });

  // The enter pose and timing are fixed per element, like CSS keyframes: it plays once, on mount.
  useLayoutEffect(() => playEnter(), []);

  useLayoutEffect(() => (isPresent ? undefined : playExit()), [isPresent]);

  return <Tag ref={setRef} {...rest} />;
}

function canAnimate(element: HTMLElement): boolean {
  return typeof element.animate === "function";
}

function toOptions(timing: Timing): KeyframeAnimationOptions {
  return { duration: timing.duration, easing: timing.easing ?? DEFAULT_TIMING.easing };
}

function toKeyframe(pose: Pose): Keyframe {
  const keyframe: Keyframe = {};
  if (pose.opacity !== undefined) {
    keyframe.opacity = pose.opacity;
  }
  if (prefersReducedMotion()) {
    return keyframe;
  }

  const transforms: string[] = [];
  if (pose.x !== undefined || pose.y !== undefined) {
    transforms.push(`translate(${pose.x ?? 0}px, ${pose.y ?? 0}px)`);
  }
  if (pose.scaleY !== undefined) {
    transforms.push(`scaleY(${pose.scaleY})`);
  }
  if (transforms.length > 0) {
    keyframe.transform = transforms.join(" ");
  }
  return keyframe;
}

const POPPED_PROPERTIES = ["position", "top", "left", "width", "height", "pointer-events"] as const;

function popOutOfLayout(element: HTMLElement) {
  const { offsetTop, offsetLeft, offsetWidth, offsetHeight } = element;
  element.style.position = "absolute";
  element.style.top = `${offsetTop}px`;
  element.style.left = `${offsetLeft}px`;
  element.style.width = `${offsetWidth}px`;
  element.style.height = `${offsetHeight}px`;
  element.style.pointerEvents = "none";
}

function restoreLayout(element: HTMLElement) {
  for (const property of POPPED_PROPERTIES) {
    element.style.removeProperty(property);
  }
}
