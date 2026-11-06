/**
 * Minimal WebGL1 runner for a single full-canvas fragment shader: compiles, renders at a reduced
 * resolution, throttles to a target fps, and pauses while hidden, off-screen, reduced-motion or on request.
 */

const VERTEX_SHADER = `
attribute vec2 aPosition;
void main() {
  gl_Position = vec4(aPosition, 0.0, 1.0);
}
`;

type UniformLookup = (name: string) => WebGLUniformLocation | null;

export interface ShaderRunnerOptions {
  fragment: string;
  /** Rendered pixels per CSS pixel. */
  scale: number;
  fps: number;
  /** Transparent canvas (premultiplied alpha), e.g. for a masked orb. */
  alpha?: boolean;
  /** Shader time at start; also the static frame shown under reduced motion. */
  startTime?: number;
  /** Stop rendering while the canvas is scrolled out of view. */
  pauseOffscreen?: boolean;
  /** Sets static uniforms. Runs after linking and again whenever the theme changes. */
  setup: (gl: WebGLRenderingContext, uniform: UniformLookup) => void;
}

export interface ShaderRunner {
  setPaused: (paused: boolean) => void;
  dispose: () => void;
}

function compile(gl: WebGLRenderingContext, type: number, source: string): WebGLShader | null {
  const shader = gl.createShader(type);
  if (!shader) {
    return null;
  }
  gl.shaderSource(shader, source);
  gl.compileShader(shader);
  if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
    gl.deleteShader(shader);
    return null;
  }
  return shader;
}

/** Returns null when WebGL is unavailable (jsdom, blocked GPU); callers keep a CSS fallback visible. */
export function startShader(canvas: HTMLCanvasElement, options: ShaderRunnerOptions): ShaderRunner | null {
  if (typeof WebGLRenderingContext === "undefined") {
    return null;
  }
  const gl = canvas.getContext("webgl", {
    alpha: options.alpha ?? false,
    premultipliedAlpha: true,
    antialias: false,
    depth: false,
    stencil: false,
    powerPreference: "low-power",
  });
  if (!gl) {
    return null;
  }
  const vertex = compile(gl, gl.VERTEX_SHADER, VERTEX_SHADER);
  const fragment = compile(gl, gl.FRAGMENT_SHADER, options.fragment);
  const program = gl.createProgram();
  if (!vertex || !fragment || !program) {
    return null;
  }
  gl.attachShader(program, vertex);
  gl.attachShader(program, fragment);
  gl.linkProgram(program);
  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
    return null;
  }

  gl.useProgram(program);
  const buffer = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
  // One oversized triangle covers the canvas.
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);
  const position = gl.getAttribLocation(program, "aPosition");
  gl.enableVertexAttribArray(position);
  gl.vertexAttribPointer(position, 2, gl.FLOAT, false, 0, 0);

  const uniform: UniformLookup = (name) => gl.getUniformLocation(program, name);
  const uResolution = uniform("uResolution");
  const uTime = uniform("uTime");
  const uScale = uniform("uScale");
  const frameInterval = 1000 / options.fps;

  let time = options.startTime ?? 0;
  let raf = 0;
  let last = 0;
  let lost = false;
  let paused = false;
  let visible = true;

  const draw = () => {
    if (lost) {
      return;
    }
    gl.uniform1f(uTime, time);
    gl.drawArrays(gl.TRIANGLES, 0, 3);
    canvas.dataset.ready = "true";
  };

  const resize = () => {
    const width = Math.max(1, Math.round(canvas.clientWidth * options.scale));
    const height = Math.max(1, Math.round(canvas.clientHeight * options.scale));
    if (canvas.width !== width || canvas.height !== height) {
      canvas.width = width;
      canvas.height = height;
      gl.viewport(0, 0, width, height);
    }
    gl.uniform2f(uResolution, width, height);
    gl.uniform1f(uScale, options.scale);
  };

  const frame = (now: number) => {
    raf = requestAnimationFrame(frame);
    const elapsed = now - last;
    if (elapsed < frameInterval) {
      return;
    }
    last = now;
    time += Math.min(elapsed, 100) / 1000;
    draw();
  };

  const reducedMotion =
    typeof window.matchMedia === "function" ? window.matchMedia("(prefers-reduced-motion: reduce)") : null;

  const sync = () => {
    const run = !paused && visible && !document.hidden && !reducedMotion?.matches && !lost;
    if (run && !raf) {
      last = performance.now();
      raf = requestAnimationFrame(frame);
    } else if (!run && raf) {
      cancelAnimationFrame(raf);
      raf = 0;
    }
    if (!run) {
      draw();
    }
  };

  options.setup(gl, uniform);
  resize();
  draw();
  sync();

  const resizeObserver = new ResizeObserver(() => {
    resize();
    if (!raf) {
      draw();
    }
  });
  resizeObserver.observe(canvas);

  const themeObserver = new MutationObserver(() => {
    options.setup(gl, uniform);
    if (!raf) {
      draw();
    }
  });
  themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });

  const intersection =
    options.pauseOffscreen && typeof IntersectionObserver !== "undefined"
      ? new IntersectionObserver(([entry]) => {
          visible = entry.isIntersecting;
          sync();
        })
      : null;
  intersection?.observe(canvas);

  const onContextLost = (e: Event) => {
    e.preventDefault();
    lost = true;
    canvas.dataset.ready = "false";
    sync();
  };

  document.addEventListener("visibilitychange", sync);
  reducedMotion?.addEventListener("change", sync);
  canvas.addEventListener("webglcontextlost", onContextLost);

  return {
    setPaused: (value) => {
      paused = value;
      sync();
    },
    dispose: () => {
      cancelAnimationFrame(raf);
      resizeObserver.disconnect();
      themeObserver.disconnect();
      intersection?.disconnect();
      document.removeEventListener("visibilitychange", sync);
      reducedMotion?.removeEventListener("change", sync);
      canvas.removeEventListener("webglcontextlost", onContextLost);
      // Free GPU resources but keep the context: a remount (e.g. StrictMode) gets the same context back from
      // this canvas, so losing it here would leave it blank. It is released together with the canvas.
      gl.deleteBuffer(buffer);
      gl.deleteProgram(program);
      canvas.dataset.ready = "false";
    },
  };
}
