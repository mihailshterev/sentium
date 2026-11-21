import { readMapColors, type MapColors } from "./map-colors";
import type { GraphNode, GraphLink, Particle, TraversalWave, CameraState } from "./types";

const MIN_ZOOM = 0.15;
const MAX_ZOOM = 6;
const DEFAULT_ZOOM = 0.9;
/** Fraction of the remaining distance the camera covers per 60fps frame. */
const CAMERA_LERP = 0.08;
/** Above this zoom every visible node shows its label, not only the hovered and selected ones. */
const LABEL_ZOOM = 2.5;
const MAX_LABEL_CHARS = 22;
const HUB_RADIUS = 8;
/** Screen-space slack around a node that still counts as pointing at it. */
const HIT_SLACK_PX = 8;

const FRAME_MS = 1000 / 60;
/**
 * The map draws at most 60 frames a second, on a schedule: a browser frame that arrives this early for the next
 * slot still draws. Without the slack, a 60Hz frame arriving a little early would be skipped, halving the rate.
 */
const FRAME_SLACK_MS = FRAME_MS * 0.2;
/** A frame after a long stall (tab switch, debugger) advances the animation at most this many 60fps frames. */
const MAX_FRAME_STEP = 3;
const MAX_PIXEL_RATIO = 2;
/** Sprites are rasterised at this multiple of their world size so they stay sharp when zoomed in. */
const SPRITE_RESOLUTION = 2;

const GLOW_TEXTURE_RADIUS = 64;
/** World units a glow grows per unit of emphasis (selection, search hit, hover). */
const GLOW_EMPHASIS_UNIT = 40;
const GLOW_RADIUS_FACTOR = 3;
/**
 * Glows are soft gradients, so they are drawn into a buffer at a quarter of the canvas resolution and composited in
 * one step; full resolution made thousands of overlapping glows the second-largest cost per frame.
 */
const GLOW_DOWNSCALE = 4;
/** World units around the view still drawn, so edges and glows that cross the view edge are not cut off. */
const CULL_MARGIN = 120;
/** Edges are drawn as straight segments of about this many world units, between these counts per edge. */
const EDGE_SEGMENT_LENGTH = 60;
const MIN_EDGE_SEGMENTS = 2;
const MAX_EDGE_SEGMENTS = 8;

/*
 * Glows and nodes (thousands of draw calls) go into a cached scene layer. It is redrawn when something in it
 * changed, every few frames while the camera moves, and every few frames for the slow ambient pulse; in between,
 * the cached image is composited with the camera change since it was drawn, which is exact for pan and zoom.
 */
/** CSS pixels drawn around the view in the scene layer, so panning between redraws does not expose its edge. */
const SCENE_PADDING_PX = 96;
const AMBIENT_SCENE_FRAMES = 3;
const MOVING_SCENE_FRAMES = 2;
/** Zoom change since the last scene redraw above which the scaled image would look soft. */
const MAX_SCENE_ZOOM_DRIFT = 0.15;
/** The easing camera snaps to its target once this close, so it stops instead of creeping for hundreds of frames. */
const CAMERA_SNAP_DISTANCE = 0.01;
const CAMERA_SNAP_ZOOM = 0.0001;
const NODE_TEXTURE_PAD = 6;
const PARTICLE_TEXTURE_SIZE = 16;
const FLOW_DOT_SIZE = 4;
const STAR_RADIUS = 2;
const STAR_TWINKLE_EVERY_FRAMES = 4;

const LABEL_FONT = "'JetBrains Mono', ui-monospace, monospace";
const WHITE = 0xffffff;
/** Share of stars drawn in the palette's star tint; the rest are white. */
const TINTED_STAR_SHARE = 0.3;
const LABEL_SHADOW_DISTANCE = 2;
const LABEL_SHADOW_ANGLE = Math.PI / 6;

const GRID_EXTENT = 4000;
const GRID_MINOR = 100;
const GRID_MAJOR = 500;

interface EdgeCurve {
  sx: number;
  sy: number;
  cpx: number;
  cpy: number;
  tx: number;
  ty: number;
  phase: number;
  /** Bounding box of the curve (its control polygon), for viewport culling. */
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
}

interface Star {
  /** Position as fractions of the viewport, so the field still covers it after a resize. */
  u: number;
  v: number;
  radius: number;
  alpha: number;
  tinted: boolean;
}

interface GridZone {
  x: number;
  y: number;
  r: number;
  color: number;
}

interface EdgeGroup {
  color: number;
  curves: EdgeCurve[];
}

/** The world-space rectangle on screen, padded so strokes and glows crossing the edge are not cut. */
interface ViewRect {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

const rgba = (color: number, alpha = 1) =>
  `rgba(${(color >> 16) & 255}, ${(color >> 8) & 255}, ${color & 255}, ${alpha})`;

function createCanvas(size: number): { canvas: HTMLCanvasElement; ctx: CanvasRenderingContext2D | null } {
  const canvas = document.createElement("canvas");
  canvas.width = Math.ceil(size * SPRITE_RESOLUTION);
  canvas.height = Math.ceil(size * SPRITE_RESOLUTION);
  const ctx = canvas.getContext("2d");
  ctx?.scale(SPRITE_RESOLUTION, SPRITE_RESOLUTION);
  return { canvas, ctx };
}

function fillCircle(ctx: CanvasRenderingContext2D, x: number, y: number, radius: number, style: string) {
  ctx.fillStyle = style;
  ctx.beginPath();
  ctx.arc(x, y, radius, 0, Math.PI * 2);
  ctx.fill();
}

function renderNodeSprite(radius: number, color: number): HTMLCanvasElement {
  const size = (radius + NODE_TEXTURE_PAD) * 2;
  const c = size / 2;
  const { canvas, ctx } = createCanvas(size);
  if (ctx) {
    fillCircle(ctx, c, c, radius + 5, rgba(color, 0.06));
    fillCircle(ctx, c, c, radius + 3, rgba(color, 0.1));
    fillCircle(ctx, c, c, radius, rgba(color, 1));
    fillCircle(ctx, c - radius * 0.2, c - radius * 0.2, radius * 0.35, rgba(WHITE, 0.5));
  }
  return canvas;
}

/**
 * Soft radial falloff baked once per colour. Glows and the nebula are scaled copies of it, so nothing needs a
 * per-frame blur.
 */
function renderGlowSprite(color: number): HTMLCanvasElement {
  const r = GLOW_TEXTURE_RADIUS;
  const { canvas, ctx } = createCanvas(r * 2);
  if (ctx) {
    const gradient = ctx.createRadialGradient(r, r, 0, r, r, r);
    gradient.addColorStop(0, rgba(color, 0.7));
    gradient.addColorStop(0.3, rgba(color, 0.3));
    gradient.addColorStop(0.65, rgba(color, 0.08));
    gradient.addColorStop(1, rgba(color, 0));
    ctx.fillStyle = gradient;
    ctx.fillRect(0, 0, r * 2, r * 2);
  }
  return canvas;
}

function renderParticleSprite(color: number): HTMLCanvasElement {
  const c = PARTICLE_TEXTURE_SIZE / 2;
  const { canvas, ctx } = createCanvas(PARTICLE_TEXTURE_SIZE);
  if (ctx) {
    fillCircle(ctx, c, c, 6, rgba(color, 0.9));
    fillCircle(ctx, c, c, 3, rgba(color, 0.5));
  }
  return canvas;
}

function drawCentered(ctx: CanvasRenderingContext2D, image: HTMLCanvasElement, x: number, y: number, size: number) {
  ctx.drawImage(image, x - size / 2, y - size / 2, size, size);
}

/** Multiples of `step` from `from` to `to`, inclusive. */
function gridLines(from: number, to: number, step: number): number[] {
  const lines: number[] = [];
  for (let v = Math.ceil(from / step) * step; v <= to; v += step) {
    lines.push(v);
  }
  return lines;
}

function clampZoom(zoom: number): number {
  return Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, zoom));
}

function truncateLabel(text: string): string {
  return text.length > MAX_LABEL_CHARS ? text.slice(0, MAX_LABEL_CHARS) + "…" : text;
}

/**
 * Draws the knowledge universe on a 2D canvas: starfield, grid and nebula, curved edges with flow dots, glowing
 * nodes, selection rings, particles, traversal waves and labels. Pan and zoom ease towards a camera target.
 */
export class UniverseRenderer {
  /** Live particle list; spawn helpers push into it and the renderer ages and removes them. */
  readonly particles: Particle[] = [];

  private canvas: HTMLCanvasElement | null = null;
  private ctx: CanvasRenderingContext2D | null = null;
  private pixelRatio = 1;
  private frameHandle = 0;
  private lastFrameAt = 0;
  private nextFrameAt = 0;

  private nodeSprites = new Map<string, HTMLCanvasElement>();
  private glowSprites = new Map<number, HTMLCanvasElement>();
  private particleSprites = new Map<number, HTMLCanvasElement>();
  private nodesById = new Map<string, GraphNode>();
  private visibleNodes: GraphNode[] = [];
  private stars: Star[] = [];
  private zones: GridZone[] = [];
  private plainEdges: EdgeCurve[] = [];
  private highlightedEdges: EdgeGroup[] = [];
  private flowCurves: EdgeCurve[] = [];
  private view: ViewRect = { left: 0, top: 0, right: 0, bottom: 0 };
  private glowCanvas: HTMLCanvasElement | null = null;
  private glowCtx: CanvasRenderingContext2D | null = null;
  private sceneCanvas: HTMLCanvasElement | null = null;
  private sceneCtx: CanvasRenderingContext2D | null = null;
  /** The camera the scene layer was drawn with; null until it has been drawn once. */
  private sceneCamera: { x: number; y: number; zoom: number } | null = null;
  private sceneDirty = true;
  private framesSinceScene = 0;

  private nodes: GraphNode[] = [];
  private links: GraphLink[] = [];
  private waves: TraversalWave[] = [];
  private camera: CameraState = { x: 0, y: 0, zoom: DEFAULT_ZOOM, targetX: 0, targetY: 0, targetZoom: DEFAULT_ZOOM };

  private isLight = false;
  private colors: MapColors = readMapColors(false);
  private reducedMotion = false;
  /** Animation clock in 60fps-frame units, so motion speed does not depend on the display's refresh rate. */
  private time = 0;
  private frame = 0;
  private hoveredId: string | null = null;
  private selectedId: string | null = null;
  private hasSearchResults = false;
  /** Edge geometry is rebuilt only when the layout or the highlight state changed, never per frame. */
  private edgesDirty = true;
  private initialized = false;
  private width = 0;
  private height = 0;
  /** Size the layout was built for; cluster centres and zone markers stay there when the viewport resizes. */
  private worldWidth = 0;
  private worldHeight = 0;

  /** Creates the canvas inside `host`. Each renderer owns its canvas, so a remount never shares one. */
  async init(host: HTMLElement, width: number, height: number, isLight = false) {
    this.width = width;
    this.height = height;
    this.worldWidth = width;
    this.worldHeight = height;
    this.isLight = isLight;
    this.colors = readMapColors(isLight);
    this.resetCamera(true);

    this.reducedMotion =
      typeof window.matchMedia === "function" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    this.canvas = document.createElement("canvas");
    this.ctx = this.canvas.getContext("2d");
    this.glowCanvas = document.createElement("canvas");
    this.glowCtx = this.glowCanvas.getContext("2d");
    this.sceneCanvas = document.createElement("canvas");
    this.sceneCtx = this.sceneCanvas.getContext("2d");
    this.pixelRatio = Math.min(window.devicePixelRatio || 1, MAX_PIXEL_RATIO);
    this.applyCanvasSize();
    host.appendChild(this.canvas);

    this.createStarfield();
    this.zones = [
      { x: width * 0.35, y: height * 0.4, r: 190, color: 0x06b6d4 },
      { x: width * 0.65, y: height * 0.4, r: 175, color: 0xa855f7 },
      { x: width * 0.5, y: height * 0.7, r: 160, color: 0xf59e0b },
    ];

    this.initialized = true;
    this.rebuildNodeSprites();
    this.frameHandle = requestAnimationFrame(this.tick);
  }

  /** Call when the mode or the palette changed; re-reads the colours from the CSS tokens. */
  setTheme(isLight: boolean) {
    this.isLight = isLight;
    this.colors = readMapColors(isLight);
    this.markLayoutDirty();
  }

  setGraph(nodes: GraphNode[], links: GraphLink[]) {
    const isNewGraph = nodes !== this.nodes;
    this.nodes = nodes;
    this.links = links;
    this.markLayoutDirty();
    if (isNewGraph && this.initialized) {
      this.rebuildNodeSprites();
    }
  }

  /** Call whenever node positions moved (each simulation tick). */
  markLayoutDirty() {
    this.edgesDirty = true;
    this.sceneDirty = true;
  }

  /** Call after node `highlighted` / `queryScore` flags changed. */
  setHasSearchResults(hasResults: boolean) {
    this.hasSearchResults = hasResults;
    this.markLayoutDirty();
  }

  addWave(wave: TraversalWave) {
    if (!this.reducedMotion) {
      this.waves.push(wave);
    }
  }

  setHoveredId(id: string | null) {
    if (id !== this.hoveredId) {
      this.hoveredId = id;
      this.sceneDirty = true;
    }
  }

  setSelectedId(id: string | null) {
    if (id !== this.selectedId) {
      this.selectedId = id;
      this.sceneDirty = true;
    }
  }

  /** Topmost node under a canvas-relative point, or null. */
  nodeAt(screenX: number, screenY: number): GraphNode | null {
    const cam = this.camera;
    const x = (screenX - this.width / 2) / cam.zoom + cam.x;
    const y = (screenY - this.height / 2) / cam.zoom + cam.y;
    const slack = HIT_SLACK_PX / cam.zoom;

    let nearest: GraphNode | null = null;
    let nearestDistance = Infinity;
    for (const node of this.nodes) {
      const dx = (node.x ?? 0) - x;
      const dy = (node.y ?? 0) - y;
      const distance = dx * dx + dy * dy;
      const reach = node.radius + slack;
      if (distance <= reach * reach && distance < nearestDistance) {
        nearest = node;
        nearestDistance = distance;
      }
    }
    return nearest;
  }

  /** Drag-pan by a screen-space delta. Applied immediately so the map stays under the pointer. */
  panBy(dxScreen: number, dyScreen: number) {
    const cam = this.camera;
    cam.targetX -= dxScreen / cam.zoom;
    cam.targetY -= dyScreen / cam.zoom;
    cam.x = cam.targetX;
    cam.y = cam.targetY;
  }

  /** Zooms by a factor, keeping the given canvas-relative point (default: the centre) fixed on screen. */
  zoomBy(factor: number, screenX = this.width / 2, screenY = this.height / 2) {
    const cam = this.camera;
    const nextZoom = clampZoom(cam.targetZoom * factor);
    const offsetX = screenX - this.width / 2;
    const offsetY = screenY - this.height / 2;
    cam.targetX += offsetX / cam.targetZoom - offsetX / nextZoom;
    cam.targetY += offsetY / cam.targetZoom - offsetY / nextZoom;
    cam.targetZoom = nextZoom;
  }

  flyTo(x: number, y: number, zoom?: number) {
    this.camera.targetX = x;
    this.camera.targetY = y;
    if (zoom !== undefined) {
      this.camera.targetZoom = clampZoom(zoom);
    }
  }

  resetCamera(immediate = false) {
    const cam = this.camera;
    cam.targetX = this.worldWidth / 2;
    cam.targetY = this.worldHeight / 2;
    cam.targetZoom = DEFAULT_ZOOM;
    if (immediate) {
      cam.x = cam.targetX;
      cam.y = cam.targetY;
      cam.zoom = cam.targetZoom;
    }
  }

  resize(width: number, height: number) {
    if (!this.initialized || (width === this.width && height === this.height)) {
      return;
    }
    this.width = width;
    this.height = height;
    this.applyCanvasSize();
  }

  destroy() {
    cancelAnimationFrame(this.frameHandle);
    this.initialized = false;
    this.canvas?.remove();
    this.canvas = null;
    this.ctx = null;
    this.glowCanvas = null;
    this.glowCtx = null;
    this.sceneCanvas = null;
    this.sceneCtx = null;
    this.nodeSprites.clear();
    this.glowSprites.clear();
    this.particleSprites.clear();
  }

  private applyCanvasSize() {
    if (!this.canvas) {
      return;
    }
    this.canvas.width = Math.round(this.width * this.pixelRatio);
    this.canvas.height = Math.round(this.height * this.pixelRatio);
    this.canvas.style.width = `${this.width}px`;
    this.canvas.style.height = `${this.height}px`;
    if (this.sceneCanvas) {
      this.sceneCanvas.width = Math.round((this.width + SCENE_PADDING_PX * 2) * this.pixelRatio);
      this.sceneCanvas.height = Math.round((this.height + SCENE_PADDING_PX * 2) * this.pixelRatio);
    }
    if (this.glowCanvas && this.sceneCanvas) {
      this.glowCanvas.width = Math.ceil(this.sceneCanvas.width / GLOW_DOWNSCALE);
      this.glowCanvas.height = Math.ceil(this.sceneCanvas.height / GLOW_DOWNSCALE);
    }
    this.sceneDirty = true;
  }

  private tick = (now: number) => {
    if (!this.initialized) {
      return;
    }
    this.frameHandle = requestAnimationFrame(this.tick);

    if (now < this.nextFrameAt - FRAME_SLACK_MS) {
      return;
    }
    // After a stall the schedule restarts from now instead of drawing a burst of catch-up frames.
    this.nextFrameAt = Math.max(this.nextFrameAt, now - FRAME_MS) + FRAME_MS;

    const elapsed = this.lastFrameAt === 0 ? FRAME_MS : now - this.lastFrameAt;
    this.lastFrameAt = now;
    this.update(Math.min(elapsed / FRAME_MS, MAX_FRAME_STEP));
  };

  private update(dt: number) {
    this.time += dt;
    this.frame++;

    this.updateCamera(dt);
    this.twinkleStar();

    if (this.edgesDirty) {
      this.rebuildEdges();
      this.edgesDirty = false;
    }

    this.ageParticles(dt);
    this.ageWaves(dt);
    this.render();
  }

  private updateCamera(dt: number) {
    const cam = this.camera;
    const k = Math.min(1, CAMERA_LERP * dt);
    cam.x += (cam.targetX - cam.x) * k;
    cam.y += (cam.targetY - cam.y) * k;
    cam.zoom += (cam.targetZoom - cam.zoom) * k;

    const settled =
      Math.abs(cam.targetX - cam.x) < CAMERA_SNAP_DISTANCE &&
      Math.abs(cam.targetY - cam.y) < CAMERA_SNAP_DISTANCE &&
      Math.abs(cam.targetZoom - cam.zoom) < CAMERA_SNAP_ZOOM;
    if (settled) {
      cam.x = cam.targetX;
      cam.y = cam.targetY;
      cam.zoom = cam.targetZoom;
    }
  }

  private twinkleStar() {
    if (this.reducedMotion || this.isLight || this.frame % STAR_TWINKLE_EVERY_FRAMES !== 0) {
      return;
    }
    const star = this.stars[Math.floor(Math.random() * this.stars.length)];
    if (star) {
      star.alpha = 0.05 + Math.random() * 0.45;
    }
  }

  private render() {
    const ctx = this.ctx;
    if (!ctx) {
      return;
    }

    const ratio = this.pixelRatio;

    ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    ctx.globalAlpha = 1;
    ctx.globalCompositeOperation = "source-over";
    ctx.fillStyle = rgba(this.backgroundColor());
    ctx.fillRect(0, 0, this.width, this.height);
    if (!this.isLight) {
      this.drawStars(ctx);
    }

    this.applyWorldTransform(ctx, ratio);
    this.updateView();
    this.drawGrid(ctx);
    if (!this.isLight) {
      this.drawNebula(ctx);
    }
    this.drawWaves(ctx);
    this.drawEdges(ctx);

    if (this.shouldRenderScene()) {
      this.renderScene();
    }
    this.compositeScene(ctx);

    this.applyWorldTransform(ctx, ratio);
    this.updateView();
    this.collectVisibleNodes();
    this.drawFlowDots(ctx);
    this.drawRings(ctx);
    this.drawParticles(ctx);
    this.drawLabels(ctx);
  }

  private shouldRenderScene(): boolean {
    const scene = this.sceneCamera;
    if (this.sceneDirty || !scene) {
      return true;
    }

    this.framesSinceScene++;
    const cam = this.camera;
    if (cam.x !== scene.x || cam.y !== scene.y || cam.zoom !== scene.zoom) {
      const zoomDrift = Math.abs(cam.zoom / scene.zoom - 1);
      const shiftPx = Math.max(Math.abs(cam.x - scene.x), Math.abs(cam.y - scene.y)) * cam.zoom;
      return (
        this.framesSinceScene >= MOVING_SCENE_FRAMES ||
        zoomDrift > MAX_SCENE_ZOOM_DRIFT ||
        shiftPx > SCENE_PADDING_PX / 2
      );
    }
    return !this.reducedMotion && this.framesSinceScene >= AMBIENT_SCENE_FRAMES;
  }

  /**
   * Draws glows and nodes into the scene layer, with the current camera and padding around the view. Edges stay on
   * the visible canvas every frame: stroked into an offscreen canvas while the layout moved, they stalled the frame
   * for tens of milliseconds when the layer was composited.
   */
  private renderScene() {
    const canvas = this.sceneCanvas;
    const scene = this.sceneCtx;
    if (!canvas || !scene) {
      return;
    }

    scene.setTransform(1, 0, 0, 1, 0, 0);
    scene.globalAlpha = 1;
    scene.globalCompositeOperation = "source-over";
    scene.clearRect(0, 0, canvas.width, canvas.height);

    this.updateView(SCENE_PADDING_PX);
    this.collectVisibleNodes();
    this.applyWorldTransform(scene, this.pixelRatio, SCENE_PADDING_PX);
    this.drawGlows(scene);
    this.drawNodes(scene);

    const cam = this.camera;
    this.sceneCamera = { x: cam.x, y: cam.y, zoom: cam.zoom };
    this.sceneDirty = false;
    this.framesSinceScene = 0;
  }

  /** Draws the scene layer moved and scaled by the camera change since it was rendered. */
  private compositeScene(ctx: CanvasRenderingContext2D) {
    const canvas = this.sceneCanvas;
    const scene = this.sceneCamera;
    if (!canvas || !scene) {
      return;
    }

    const cam = this.camera;
    const scale = cam.zoom / scene.zoom;
    const x = (-SCENE_PADDING_PX - this.width / 2) * scale + (scene.x - cam.x) * cam.zoom + this.width / 2;
    const y = (-SCENE_PADDING_PX - this.height / 2) * scale + (scene.y - cam.y) * cam.zoom + this.height / 2;

    ctx.save();
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.globalAlpha = 1;
    ctx.globalCompositeOperation = "source-over";
    ctx.drawImage(canvas, x * this.pixelRatio, y * this.pixelRatio, canvas.width * scale, canvas.height * scale);
    ctx.restore();
  }

  /**
   * Maps world units to canvas pixels. `scale` is the target's device pixels per CSS pixel; `offsetPx` shifts the
   * view right and down, for a target with padding around the visible area.
   */
  private applyWorldTransform(ctx: CanvasRenderingContext2D, scale: number, offsetPx = 0) {
    const cam = this.camera;
    ctx.setTransform(
      scale * cam.zoom,
      0,
      0,
      scale * cam.zoom,
      scale * (this.width / 2 + offsetPx - cam.x * cam.zoom),
      scale * (this.height / 2 + offsetPx - cam.y * cam.zoom),
    );
  }

  private backgroundColor(): number {
    return this.colors.background;
  }

  private glowSprite(color: number): HTMLCanvasElement {
    let sprite = this.glowSprites.get(color);
    if (!sprite) {
      sprite = renderGlowSprite(color);
      this.glowSprites.set(color, sprite);
    }
    return sprite;
  }

  private particleSprite(color: number): HTMLCanvasElement {
    let sprite = this.particleSprites.get(color);
    if (!sprite) {
      sprite = renderParticleSprite(color);
      this.particleSprites.set(color, sprite);
    }
    return sprite;
  }

  private createStarfield() {
    const layers = [
      { count: 200, alpha: 0.12, scale: 0.25 },
      { count: 100, alpha: 0.22, scale: 0.45 },
      { count: 50, alpha: 0.38, scale: 0.7 },
    ];

    for (const layer of layers) {
      for (let i = 0; i < layer.count; i++) {
        this.stars.push({
          u: Math.random(),
          v: Math.random(),
          radius: STAR_RADIUS * (layer.scale + Math.random() * 0.3),
          alpha: layer.alpha + Math.random() * 0.15,
          tinted: Math.random() < TINTED_STAR_SHARE,
        });
      }
    }
  }

  private rebuildNodeSprites() {
    this.nodeSprites.clear();
    this.nodesById.clear();

    const byKey = new Map<string, HTMLCanvasElement>();
    for (const node of this.nodes) {
      this.nodesById.set(node.id, node);
      const radius = Math.round(node.radius);
      const key = `${radius}_${node.color.toString(16)}`;
      let sprite = byKey.get(key);
      if (!sprite) {
        sprite = renderNodeSprite(radius, node.color);
        byKey.set(key, sprite);
      }
      this.nodeSprites.set(node.id, sprite);
    }
  }

  private curveOf(link: GraphLink): EdgeCurve {
    const s = link.source as GraphNode;
    const t = link.target as GraphNode;
    const sx = s.x ?? 0;
    const sy = s.y ?? 0;
    const tx = t.x ?? 0;
    const ty = t.y ?? 0;
    // Control point offset perpendicular to the edge by 5% of its length: a slight, consistent bow.
    const cpx = (sx + tx) / 2 - (ty - sy) * 0.05;
    const cpy = (sy + ty) / 2 + (tx - sx) * 0.05;
    return {
      sx,
      sy,
      cpx,
      cpy,
      tx,
      ty,
      phase: s.pulse ?? 0,
      minX: Math.min(sx, cpx, tx),
      minY: Math.min(sy, cpy, ty),
      maxX: Math.max(sx, cpx, tx),
      maxY: Math.max(sy, cpy, ty),
    };
  }

  /** Sorts the edges into plain ones and highlighted ones per colour; drawn from these lists every frame. */
  private rebuildEdges() {
    this.flowCurves.length = 0;
    const plain: EdgeCurve[] = [];
    const highlighted = new Map<number, EdgeCurve[]>();

    for (const link of this.links) {
      const source = link.source as GraphNode;
      const target = link.target as GraphNode;
      const curve = this.curveOf(link);

      if (this.hasSearchResults && (source.highlighted || target.highlighted)) {
        const group = highlighted.get(source.color) ?? [];
        group.push(curve);
        highlighted.set(source.color, group);
        this.flowCurves.push(curve);
      } else {
        plain.push(curve);
      }
    }

    this.plainEdges = plain;
    this.highlightedEdges = [...highlighted].map(([color, curves]) => ({ color, curves }));
  }

  /** Sets the culling rectangle to the view plus `paddingPx` screen pixels and the cull margin on every side. */
  private updateView(paddingPx = 0) {
    const cam = this.camera;
    const halfW = (this.width / 2 + paddingPx) / cam.zoom + CULL_MARGIN;
    const halfH = (this.height / 2 + paddingPx) / cam.zoom + CULL_MARGIN;
    this.view = { left: cam.x - halfW, top: cam.y - halfH, right: cam.x + halfW, bottom: cam.y + halfH };
  }

  private isCurveVisible(curve: EdgeCurve): boolean {
    const view = this.view;
    return curve.maxX >= view.left && curve.minX <= view.right && curve.maxY >= view.top && curve.minY <= view.bottom;
  }

  /**
   * Strokes each edge as a few straight segments along its curve, each its own one-line path. The browser draws a
   * single line as a cheap quad whatever its length; a curve, or one path holding thousands of them, goes through
   * its general path rasteriser, which cost tens of milliseconds per frame while the layout settled and edges were
   * long.
   */
  private strokeCurves(ctx: CanvasRenderingContext2D, curves: EdgeCurve[]) {
    for (const curve of curves) {
      if (!this.isCurveVisible(curve)) {
        continue;
      }

      const length = Math.hypot(curve.tx - curve.sx, curve.ty - curve.sy);
      const segments = Math.min(MAX_EDGE_SEGMENTS, Math.max(MIN_EDGE_SEGMENTS, Math.ceil(length / EDGE_SEGMENT_LENGTH)));
      let x = curve.sx;
      let y = curve.sy;
      for (let i = 1; i <= segments; i++) {
        const t = i / segments;
        const u = 1 - t;
        const nx = u * u * curve.sx + 2 * u * t * curve.cpx + t * t * curve.tx;
        const ny = u * u * curve.sy + 2 * u * t * curve.cpy + t * t * curve.ty;
        ctx.beginPath();
        ctx.moveTo(x, y);
        ctx.lineTo(nx, ny);
        ctx.stroke();
        x = nx;
        y = ny;
      }
    }
  }

  private collectVisibleNodes() {
    const view = this.view;
    this.visibleNodes.length = 0;
    for (const node of this.nodes) {
      const x = node.x ?? 0;
      const y = node.y ?? 0;
      if (x >= view.left && x <= view.right && y >= view.top && y <= view.bottom) {
        this.visibleNodes.push(node);
      }
    }
  }

  private pulseOf(node: GraphNode): number {
    return this.reducedMotion ? 0.5 : (Math.sin(this.time * 0.025 + node.pulse) + 1) / 2;
  }

  private isDimmed(node: GraphNode): boolean {
    return this.hasSearchResults && !node.highlighted && node.id !== this.selectedId && node.id !== this.hoveredId;
  }

  private drawStars(ctx: CanvasRenderingContext2D) {
    for (const star of this.stars) {
      fillCircle(ctx, star.u * this.width, star.v * this.height, star.radius, rgba(star.tinted ? this.colors.star : WHITE, 0.8 * star.alpha));
    }
  }

  /** Only the grid lines and dots inside the view, each drawn as its own simple shape. */
  private drawGrid(ctx: CanvasRenderingContext2D) {
    const lineColor = this.colors.gridLine;
    const dotColor = this.colors.gridDot;
    const view = this.view;
    const top = Math.max(-GRID_EXTENT, view.top);
    const bottom = Math.min(GRID_EXTENT, view.bottom);
    const left = Math.max(-GRID_EXTENT, view.left);
    const right = Math.min(GRID_EXTENT, view.right);

    for (const major of [false, true]) {
      ctx.lineWidth = major ? 0.8 : 0.35;
      ctx.strokeStyle = rgba(lineColor, major ? 0.5 : 0.18);
      for (const v of gridLines(left, right, GRID_MINOR)) {
        if ((v % GRID_MAJOR === 0) === major) {
          this.strokeLine(ctx, v, top, v, bottom);
        }
      }
      for (const v of gridLines(top, bottom, GRID_MINOR)) {
        if ((v % GRID_MAJOR === 0) === major) {
          this.strokeLine(ctx, left, v, right, v);
        }
      }
    }

    const dotStyle = rgba(dotColor, 0.7);
    for (const x of gridLines(left, right, GRID_MAJOR)) {
      for (const y of gridLines(top, bottom, GRID_MAJOR)) {
        fillCircle(ctx, x, y, 1.5, dotStyle);
      }
    }

    const zoneAlpha = this.isLight ? 0.15 : 0.09;
    const crossAlpha = this.isLight ? 0.4 : 0.22;
    const centerAlpha = this.isLight ? 0.5 : 0.35;
    const crossArm = 10;
    for (const z of this.zones) {
      this.strokeCircle(ctx, z.x, z.y, z.r, rgba(z.color, zoneAlpha), 1);
      this.strokeCircle(ctx, z.x, z.y, z.r * 0.55, rgba(z.color, zoneAlpha * 0.55), 0.5);

      ctx.beginPath();
      ctx.moveTo(z.x - crossArm, z.y);
      ctx.lineTo(z.x + crossArm, z.y);
      ctx.moveTo(z.x, z.y - crossArm);
      ctx.lineTo(z.x, z.y + crossArm);
      ctx.strokeStyle = rgba(z.color, crossAlpha);
      ctx.lineWidth = 0.7;
      ctx.stroke();

      fillCircle(ctx, z.x, z.y, 2, rgba(z.color, centerAlpha));
    }
  }

  private drawNebula(ctx: CanvasRenderingContext2D) {
    const cx = this.worldWidth / 2;
    const cy = this.worldHeight / 2;
    const blobs = [
      { color: this.colors.nebulaOuter, radius: 900, alpha: 0.9 },
      { color: this.colors.nebulaInner, radius: 580, alpha: 0.55 },
    ];
    for (const blob of blobs) {
      ctx.globalAlpha = blob.alpha;
      drawCentered(ctx, this.glowSprite(blob.color), cx, cy, blob.radius * 2);
    }
    ctx.globalAlpha = 1;
  }

  private drawWaves(ctx: CanvasRenderingContext2D) {
    for (const wave of this.waves) {
      this.strokeCircle(ctx, wave.originX, wave.originY, wave.radius, rgba(wave.color, wave.life * 0.3), 2);
    }
  }

  private drawEdges(ctx: CanvasRenderingContext2D) {
    if (this.plainEdges.length > 0) {
      ctx.strokeStyle = rgba(this.colors.edge, this.hasSearchResults ? 0.22 : 0.32);
      ctx.lineWidth = 0.8;
      this.strokeCurves(ctx, this.plainEdges);
    }

    for (const { color, curves } of this.highlightedEdges) {
      for (const style of [
        { alpha: 0.2, width: 5 },
        { alpha: 0.65, width: 1.5 },
      ]) {
        ctx.strokeStyle = rgba(color, style.alpha);
        ctx.lineWidth = style.width;
        this.strokeCurves(ctx, curves);
      }
    }
  }

  /** A bright dot travelling along each highlighted edge. */
  private drawFlowDots(ctx: CanvasRenderingContext2D) {
    if (this.reducedMotion || this.flowCurves.length === 0) {
      return;
    }
    const sprite = this.particleSprite(WHITE);
    ctx.globalAlpha = 0.8;
    for (const c of this.flowCurves) {
      const t = (this.time * 0.015 + c.phase) % 1;
      const u = 1 - t;
      const x = u * u * c.sx + 2 * u * t * c.cpx + t * t * c.tx;
      const y = u * u * c.sy + 2 * u * t * c.cpy + t * t * c.ty;
      drawCentered(ctx, sprite, x, y, FLOW_DOT_SIZE);
    }
    ctx.globalAlpha = 1;
  }

  private drawGlows(ctx: CanvasRenderingContext2D) {
    const glowCanvas = this.glowCanvas;
    const glowCtx = this.glowCtx;
    if (!glowCanvas || !glowCtx || this.visibleNodes.length === 0) {
      return;
    }

    const blend = this.isLight ? "source-over" : "lighter";
    glowCtx.setTransform(1, 0, 0, 1, 0, 0);
    glowCtx.globalAlpha = 1;
    glowCtx.globalCompositeOperation = "source-over";
    glowCtx.clearRect(0, 0, glowCanvas.width, glowCanvas.height);
    this.applyWorldTransform(glowCtx, this.pixelRatio / GLOW_DOWNSCALE, SCENE_PADDING_PX);
    glowCtx.globalCompositeOperation = blend;
    this.drawGlowSprites(glowCtx);

    ctx.save();
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.globalAlpha = this.isLight ? 0.4 : 0.6;
    ctx.globalCompositeOperation = blend;
    ctx.drawImage(glowCanvas, 0, 0, glowCanvas.width * GLOW_DOWNSCALE, glowCanvas.height * GLOW_DOWNSCALE);
    ctx.restore();
  }

  private drawGlowSprites(ctx: CanvasRenderingContext2D) {
    for (const node of this.visibleNodes) {
      const phase = this.pulseOf(node);
      const isHovered = node.id === this.hoveredId;
      const isSelected = node.id === this.selectedId;
      const isHighlighted = node.highlighted ?? false;

      const emphasis = (isSelected ? 0.7 : isHighlighted ? 0.4 : 0.1 * phase) + (isHovered ? 0.3 : 0);
      const glowRadius = node.radius * GLOW_RADIUS_FACTOR + GLOW_EMPHASIS_UNIT * emphasis;

      let alpha: number;
      if (this.isDimmed(node)) {
        alpha = 0.05;
      } else {
        const base = isSelected ? 1 : isHighlighted ? 0.8 : 0.55 + phase * 0.15;
        alpha = isHighlighted && node.queryScore ? Math.max(base, node.queryScore * 0.9) : base;
      }

      ctx.globalAlpha = alpha;
      drawCentered(ctx, this.glowSprite(node.color), node.x ?? 0, node.y ?? 0, glowRadius * 2);
    }
  }

  private drawNodes(ctx: CanvasRenderingContext2D) {
    for (const node of this.visibleNodes) {
      const sprite = this.nodeSprites.get(node.id);
      if (!sprite) {
        continue;
      }
      const phase = this.pulseOf(node);
      const scale =
        1 + 0.08 * phase + (node.id === this.hoveredId ? 0.25 : 0) + (node.id === this.selectedId ? 0.35 : 0);
      const size = (Math.round(node.radius) + NODE_TEXTURE_PAD) * 2 * scale;

      ctx.globalAlpha = this.isDimmed(node) ? 0.2 : 1;
      drawCentered(ctx, sprite, node.x ?? 0, node.y ?? 0, size);
    }
    ctx.globalAlpha = 1;
  }

  private drawRings(ctx: CanvasRenderingContext2D) {
    const selected = this.selectedId ? this.nodesById.get(this.selectedId) : undefined;
    const hovered =
      this.hoveredId && this.hoveredId !== this.selectedId ? this.nodesById.get(this.hoveredId) : undefined;

    if (selected) {
      const nx = selected.x ?? 0;
      const ny = selected.y ?? 0;
      const size = selected.radius + 11;
      const arm = size * 0.48;

      ctx.beginPath();
      for (const [dx, dy] of [
        [1, 1],
        [-1, 1],
        [-1, -1],
        [1, -1],
      ]) {
        const cornerX = nx - dx * size;
        const cornerY = ny - dy * size;
        ctx.moveTo(cornerX + dx * arm, cornerY);
        ctx.lineTo(cornerX, cornerY);
        ctx.lineTo(cornerX, cornerY + dy * arm);
      }
      ctx.strokeStyle = rgba(selected.color, 0.9);
      ctx.lineWidth = 1.5;
      ctx.stroke();

      const phase = this.reducedMotion ? 0.5 : (Math.sin(this.time * 0.04) + 1) / 2;
      const ringColor = this.isLight ? selected.color : WHITE;
      this.strokeCircle(ctx, nx, ny, selected.radius + 2 + phase * 2, rgba(ringColor, 0.45 + phase * 0.2), 1);
    }

    if (hovered) {
      this.strokeCircle(ctx, hovered.x ?? 0, hovered.y ?? 0, hovered.radius + 5, rgba(hovered.color, 0.85), 1.5);
    }
  }

  private drawParticles(ctx: CanvasRenderingContext2D) {
    if (this.particles.length === 0) {
      return;
    }
    ctx.globalCompositeOperation = this.isLight ? "source-over" : "lighter";
    for (const p of this.particles) {
      ctx.globalAlpha = p.life * 0.8;
      drawCentered(ctx, this.particleSprite(p.color), p.x, p.y, PARTICLE_TEXTURE_SIZE * p.size * p.life * 0.3);
    }
    ctx.globalCompositeOperation = "source-over";
    ctx.globalAlpha = 1;
  }

  private drawLabels(ctx: CanvasRenderingContext2D) {
    const labelsEverywhere = this.camera.zoom > LABEL_ZOOM;
    const shadowX = LABEL_SHADOW_DISTANCE * Math.cos(LABEL_SHADOW_ANGLE);
    const shadowY = LABEL_SHADOW_DISTANCE * Math.sin(LABEL_SHADOW_ANGLE);
    ctx.textAlign = "left";
    ctx.textBaseline = "middle";

    for (const node of this.visibleNodes) {
      const isHovered = node.id === this.hoveredId;
      const isSelected = node.id === this.selectedId;
      if (!labelsEverywhere && !isHovered && !isSelected) {
        continue;
      }

      const isHub = node.radius > HUB_RADIUS;
      const text = truncateLabel(node.source);
      const x = (node.x ?? 0) + node.radius + 6;
      const y = node.y ?? 0;
      const alpha = isSelected ? 1 : isHovered ? 0.95 : this.isDimmed(node) ? 0.3 : 0.7;

      ctx.font = `${isHub ? 700 : 400} ${isHub ? 12 : 10}px ${LABEL_FONT}`;
      if (isHub && !this.isLight) {
        ctx.globalAlpha = alpha * 0.8;
        ctx.fillStyle = "#000";
        ctx.fillText(text, x + shadowX, y + shadowY);
      }
      ctx.globalAlpha = alpha;
      ctx.fillStyle = this.isLight ? rgba(this.colors.label) : node.colorStr;
      ctx.fillText(text, x, y);
    }
    ctx.globalAlpha = 1;
  }

  private strokeLine(ctx: CanvasRenderingContext2D, x1: number, y1: number, x2: number, y2: number) {
    ctx.beginPath();
    ctx.moveTo(x1, y1);
    ctx.lineTo(x2, y2);
    ctx.stroke();
  }

  private strokeCircle(ctx: CanvasRenderingContext2D, x: number, y: number, r: number, style: string, width: number) {
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.strokeStyle = style;
    ctx.lineWidth = width;
    ctx.stroke();
  }

  private ageParticles(dt: number) {
    const particles = this.particles;
    if (this.reducedMotion) {
      particles.length = 0;
      return;
    }

    const drag = Math.pow(0.96, dt);
    let alive = 0;
    for (const p of particles) {
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      p.vx *= drag;
      p.vy *= drag;
      p.life -= 0.02 * dt;
      if (p.life > 0) {
        particles[alive++] = p;
      }
    }
    particles.length = alive;
  }

  private ageWaves(dt: number) {
    let alive = 0;
    for (const wave of this.waves) {
      wave.radius += wave.speed * dt;
      wave.life -= 0.008 * dt;
      if (wave.life > 0 && wave.radius <= wave.maxRadius) {
        this.waves[alive++] = wave;
      }
    }
    this.waves.length = alive;
  }
}
