import { Application, Container, Graphics, Texture, Sprite, Ticker, Text, TextStyle } from "pixi.js";
// The Content-Security-Policy forbids eval; this swaps Pixi's generated shader/uniform code for eval-free versions.
import "pixi.js/unsafe-eval";
import type { GraphNode, GraphLink, Particle, TraversalWave, CameraState } from "./types";

const MIN_ZOOM = 0.15;
const MAX_ZOOM = 6;
const DEFAULT_ZOOM = 0.9;
/** Fraction of the remaining distance the camera covers per 60fps frame. */
const CAMERA_LERP = 0.08;
/** Above this zoom every visible node shows its label, not only the hovered and selected ones. */
const LABEL_ZOOM = 2.5;
/** Text textures are rasterised on demand; spreading them over frames keeps zooming in smooth. */
const MAX_LABELS_CREATED_PER_FRAME = 12;
const MAX_LABEL_CHARS = 22;
const HUB_RADIUS = 8;
const VIEWPORT_MARGIN = 50;
/** Screen-space slack around a node that still counts as pointing at it. */
const HIT_SLACK_PX = 8;

const GLOW_TEXTURE_RADIUS = 64;
/** World units a glow grows per unit of emphasis (selection, search hit, hover). */
const GLOW_EMPHASIS_UNIT = 40;
const GLOW_RADIUS_FACTOR = 3;
const PARTICLE_TEXTURE_RADIUS = 8;
const FLOW_DOT_RADIUS = 1.5;

const LABEL_FONT = "'JetBrains Mono', ui-monospace, monospace";
const LIGHT_LABEL_COLOR = 0x1b2233;

interface EdgeCurve {
  sx: number;
  sy: number;
  cpx: number;
  cpy: number;
  tx: number;
  ty: number;
  phase: number;
}

function generateNodeTexture(app: Application, radius: number, color: number): Texture {
  const pad = 6;
  const size = (radius + pad) * 2;
  const cx = size / 2;
  const g = new Graphics();

  g.circle(cx, cx, radius + 5);
  g.fill({ color, alpha: 0.06 });
  g.circle(cx, cx, radius + 3);
  g.fill({ color, alpha: 0.1 });

  g.circle(cx, cx, radius);
  g.fill({ color, alpha: 1.0 });

  g.circle(cx - radius * 0.2, cx - radius * 0.2, radius * 0.35);
  g.fill({ color: 0xffffff, alpha: 0.5 });

  const texture = app.renderer.generateTexture({ target: g, resolution: 2 });
  g.destroy();
  return texture;
}

/**
 * Soft radial falloff baked into a texture once. Glows and the nebula are tinted sprites of it, so nothing
 * needs a per-frame blur filter (each filter is an extra full-size render pass on every frame).
 */
function generateGlowTexture(): Texture {
  const r = GLOW_TEXTURE_RADIUS;
  const canvas = document.createElement("canvas");
  canvas.width = r * 2;
  canvas.height = r * 2;
  const ctx = canvas.getContext("2d");
  if (ctx) {
    const gradient = ctx.createRadialGradient(r, r, 0, r, r, r);
    gradient.addColorStop(0, "rgba(255, 255, 255, 0.7)");
    gradient.addColorStop(0.3, "rgba(255, 255, 255, 0.3)");
    gradient.addColorStop(0.65, "rgba(255, 255, 255, 0.08)");
    gradient.addColorStop(1, "rgba(255, 255, 255, 0)");
    ctx.fillStyle = gradient;
    ctx.fillRect(0, 0, r * 2, r * 2);
  }
  return Texture.from(canvas);
}

function generateParticleTexture(app: Application): Texture {
  const c = PARTICLE_TEXTURE_RADIUS;
  const g = new Graphics();
  g.circle(c, c, 6);
  g.fill({ color: 0xffffff, alpha: 0.9 });
  g.circle(c, c, 3);
  g.fill({ color: 0xffffff, alpha: 0.5 });
  const texture = app.renderer.generateTexture({ target: g, resolution: 2 });
  g.destroy();
  return texture;
}

function generateStarTexture(app: Application): Texture {
  const g = new Graphics();
  g.circle(4, 4, 2);
  g.fill({ color: 0xffffff, alpha: 0.8 });
  const texture = app.renderer.generateTexture({ target: g, resolution: 1 });
  g.destroy();
  return texture;
}

function clampZoom(zoom: number): number {
  return Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, zoom));
}

function truncateLabel(text: string): string {
  return text.length > MAX_LABEL_CHARS ? text.slice(0, MAX_LABEL_CHARS) + "…" : text;
}

export class UniverseRenderer {
  app = new Application();
  /** Live particle list; spawn helpers push into it and the renderer ages and removes them. */
  readonly particles: Particle[] = [];

  private bgLayer = new Container();
  private world = new Container();
  private gridLayer = new Container();
  private nebulaLayer = new Container();
  private waveLayer = new Container();
  private edgeLayer = new Container();
  private glowLayer = new Container();
  private nodeLayer = new Container();
  private ringLayer = new Container();
  private particleLayer = new Container();
  private labelLayer = new Container();

  private nodeTextures = new Map<string, Texture>();
  private glowTexture!: Texture;
  private particleTexture!: Texture;
  private starTexture!: Texture;

  private nodeSprites = new Map<string, Sprite>();
  private glowSprites = new Map<string, Sprite>();
  private labelTexts = new Map<string, Text>();
  private labelStyles = new Map<string, TextStyle>();
  private nodesById = new Map<string, GraphNode>();
  private edgeGraphics!: Graphics;
  private ringGraphics!: Graphics;
  private waveGraphics!: Graphics;
  private particleSprites: Sprite[] = [];
  private flowSprites: Sprite[] = [];
  private flowCurves: EdgeCurve[] = [];
  private starSprites: Sprite[] = [];
  /** Star positions as fractions of the viewport, so the field still covers it after a resize. */
  private starAnchors: Array<{ u: number; v: number }> = [];

  private nodes: GraphNode[] = [];
  private links: GraphLink[] = [];
  private waves: TraversalWave[] = [];
  private camera: CameraState = { x: 0, y: 0, zoom: DEFAULT_ZOOM, targetX: 0, targetY: 0, targetZoom: DEFAULT_ZOOM };

  private isLight = false;
  private reducedMotion = false;
  /** Animation clock in 60fps-frame units, so motion speed does not depend on the display's refresh rate. */
  private time = 0;
  private frame = 0;
  private hoveredId: string | null = null;
  private selectedId: string | null = null;
  private hasSearchResults = false;
  /** Edge geometry is rebuilt only when the layout or the highlight state changed, never per frame. */
  private edgesDirty = true;
  private ringsDrawn = false;
  private wavesDrawn = false;
  private initialized = false;
  private destroyed = false;
  private width = 0;
  private height = 0;
  /** Size the layout was built for; cluster centres and zone markers stay there when the viewport resizes. */
  private worldWidth = 0;
  private worldHeight = 0;

  /**
   * Creates the canvas inside `host`. Each renderer owns its canvas, and with it its WebGL context, so a
   * remount (React StrictMode, navigation) never shares a context with a renderer that is being torn down.
   */
  async init(host: HTMLElement, width: number, height: number, isLight = false) {
    this.width = width;
    this.height = height;
    this.worldWidth = width;
    this.worldHeight = height;
    this.isLight = isLight;
    this.resetCamera(true);

    const pixelRatio = Math.min(window.devicePixelRatio || 1, 2);

    await this.app.init({
      width,
      height,
      // At 2x the edges are already sharp; multisampling there only multiplies the fill cost.
      antialias: pixelRatio < 2,
      backgroundColor: this.backgroundColor(),
      resolution: pixelRatio,
      autoDensity: true,
      powerPreference: "high-performance",
    });

    // Destroyed while init was pending (e.g. React StrictMode's mount/unmount/mount): tear down now.
    if (this.destroyed) {
      this.app.destroy(true, { children: true });
      return;
    }

    host.appendChild(this.app.canvas);

    this.reducedMotion =
      typeof window.matchMedia === "function" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    // Picking is done in world space by nodeAt(); Pixi's own event system would walk every sprite per move.
    this.app.stage.eventMode = "none";
    this.app.stage.interactiveChildren = false;

    this.app.stage.addChild(this.bgLayer);
    this.world.addChild(this.gridLayer);
    this.world.addChild(this.nebulaLayer);
    this.world.addChild(this.waveLayer);
    this.world.addChild(this.edgeLayer);
    this.world.addChild(this.glowLayer);
    this.world.addChild(this.nodeLayer);
    this.world.addChild(this.ringLayer);
    this.world.addChild(this.particleLayer);
    this.world.addChild(this.labelLayer);
    this.app.stage.addChild(this.world);

    this.applyGlowTheme();

    this.edgeGraphics = new Graphics();
    this.edgeLayer.addChild(this.edgeGraphics);

    this.ringGraphics = new Graphics();
    this.ringLayer.addChild(this.ringGraphics);

    this.waveGraphics = new Graphics();
    this.waveLayer.addChild(this.waveGraphics);

    this.glowTexture = generateGlowTexture();
    this.particleTexture = generateParticleTexture(this.app);
    this.starTexture = generateStarTexture(this.app);

    this.createStarfield();
    this.createNebula();
    this.initGrid();

    this.app.ticker.maxFPS = 60;
    this.app.ticker.add(this.update);

    this.initialized = true;
    if (this.nodes.length > 0) {
      this.rebuildSprites();
    }
  }

  setTheme(isLight: boolean) {
    this.isLight = isLight;
    if (!this.initialized) {
      return;
    }

    this.app.renderer.background.color = this.backgroundColor();

    for (const star of this.starSprites) {
      star.visible = !isLight;
    }
    this.nebulaLayer.visible = !isLight;

    for (const child of this.gridLayer.removeChildren()) {
      child.destroy();
    }
    this.initGrid();
    this.applyGlowTheme();

    // Label colours are baked into their textures; they are recreated on demand in the new theme.
    this.destroyLabels();
    this.edgesDirty = true;
  }

  setGraph(nodes: GraphNode[], links: GraphLink[]) {
    const isNewGraph = nodes !== this.nodes;
    this.nodes = nodes;
    this.links = links;
    this.edgesDirty = true;
    // Before init resolves there is no renderer to build textures with; init() builds them instead.
    if (isNewGraph && this.initialized) {
      this.rebuildSprites();
    }
  }

  /** Call whenever node positions moved (each simulation tick). */
  markLayoutDirty() {
    this.edgesDirty = true;
  }

  /** Call after node `highlighted` / `queryScore` flags changed. */
  setHasSearchResults(hasResults: boolean) {
    this.hasSearchResults = hasResults;
    this.edgesDirty = true;
  }

  addWave(wave: TraversalWave) {
    if (!this.reducedMotion) {
      this.waves.push(wave);
    }
  }

  setHoveredId(id: string | null) {
    this.hoveredId = id;
  }

  setSelectedId(id: string | null) {
    this.selectedId = id;
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
    this.app.renderer.resize(width, height);
    this.starSprites.forEach((star, i) => {
      star.x = this.starAnchors[i].u * width;
      star.y = this.starAnchors[i].v * height;
    });
  }

  destroy() {
    this.destroyed = true;
    if (!this.initialized) {
      // init() finishes the teardown once the async setup resolves.
      return;
    }
    this.app.ticker.remove(this.update);
    for (const t of this.nodeTextures.values()) t.destroy(true);
    this.glowTexture?.destroy(true);
    this.particleTexture?.destroy(true);
    this.starTexture?.destroy(true);
    this.app.destroy(true, { children: true });
  }

  private backgroundColor(): number {
    return this.isLight ? 0xedf1f8 : 0x060a14;
  }

  private applyGlowTheme() {
    this.glowLayer.blendMode = this.isLight ? "normal" : "add";
    this.glowLayer.alpha = this.isLight ? 0.4 : 0.6;
  }

  private createStarfield() {
    const layers = [
      { count: 200, alpha: 0.12, scale: 0.25 },
      { count: 100, alpha: 0.22, scale: 0.45 },
      { count: 50, alpha: 0.38, scale: 0.7 },
    ];

    for (const layer of layers) {
      for (let i = 0; i < layer.count; i++) {
        const anchor = { u: Math.random(), v: Math.random() };
        const star = new Sprite(this.starTexture);
        star.anchor.set(0.5);
        star.x = anchor.u * this.width;
        star.y = anchor.v * this.height;
        star.alpha = layer.alpha + Math.random() * 0.15;
        star.scale.set(layer.scale + Math.random() * 0.3);
        star.tint = Math.random() > 0.7 ? 0x88ccff : 0xffffff;
        star.visible = !this.isLight;
        this.bgLayer.addChild(star);
        this.starSprites.push(star);
        this.starAnchors.push(anchor);
      }
    }
  }

  private createNebula() {
    const cx = this.worldWidth / 2;
    const cy = this.worldHeight / 2;
    const blobs = [
      { color: 0x061428, radius: 900, alpha: 0.9 },
      { color: 0x0d2244, radius: 580, alpha: 0.55 },
    ];
    for (const blob of blobs) {
      const sprite = new Sprite(this.glowTexture);
      sprite.anchor.set(0.5);
      sprite.position.set(cx, cy);
      sprite.tint = blob.color;
      sprite.alpha = blob.alpha;
      sprite.scale.set(blob.radius / GLOW_TEXTURE_RADIUS);
      this.nebulaLayer.addChild(sprite);
    }
    this.nebulaLayer.visible = !this.isLight;
  }

  private initGrid() {
    const width = this.worldWidth;
    const height = this.worldHeight;
    const g = new Graphics();
    const extent = 4000;
    const minorSize = 100;
    const majorSize = 500;

    const lineColor = this.isLight ? 0xc8ccd8 : 0x0d1f3c;
    const dotColor = this.isLight ? 0xa8aec0 : 0x1a3a6c;

    // One stroke per line weight: two draw instructions instead of one per grid line.
    for (const major of [false, true]) {
      for (let v = -extent; v <= extent; v += minorSize) {
        if ((v % majorSize === 0) !== major) {
          continue;
        }
        g.moveTo(v, -extent);
        g.lineTo(v, extent);
        g.moveTo(-extent, v);
        g.lineTo(extent, v);
      }
      g.stroke({ color: lineColor, alpha: major ? 0.5 : 0.18, width: major ? 0.8 : 0.35 });
    }

    for (let x = -extent; x <= extent; x += majorSize) {
      for (let y = -extent; y <= extent; y += majorSize) {
        g.circle(x, y, 1.5);
      }
    }
    g.fill({ color: dotColor, alpha: 0.7 });

    const zones = [
      { x: width * 0.35, y: height * 0.4, r: 190, color: 0x06b6d4 },
      { x: width * 0.65, y: height * 0.4, r: 175, color: 0xa855f7 },
      { x: width * 0.5, y: height * 0.7, r: 160, color: 0xf59e0b },
    ];
    for (const z of zones) {
      const zoneAlpha = this.isLight ? 0.15 : 0.09;
      const crossAlpha = this.isLight ? 0.4 : 0.22;
      const centerAlpha = this.isLight ? 0.5 : 0.35;
      g.circle(z.x, z.y, z.r);
      g.stroke({ color: z.color, alpha: zoneAlpha, width: 1 });
      g.circle(z.x, z.y, z.r * 0.55);
      g.stroke({ color: z.color, alpha: zoneAlpha * 0.55, width: 0.5 });
      const ch = 10;
      g.moveTo(z.x - ch, z.y);
      g.lineTo(z.x + ch, z.y);
      g.moveTo(z.x, z.y - ch);
      g.lineTo(z.x, z.y + ch);
      g.stroke({ color: z.color, alpha: crossAlpha, width: 0.7 });
      g.circle(z.x, z.y, 2);
      g.fill({ color: z.color, alpha: centerAlpha });
    }

    this.gridLayer.addChild(g);
  }

  private getNodeTexture(radius: number, color: number): Texture {
    const key = `${Math.round(radius)}_${color.toString(16)}`;
    let texture = this.nodeTextures.get(key);
    if (!texture) {
      texture = generateNodeTexture(this.app, Math.round(radius), color);
      this.nodeTextures.set(key, texture);
    }
    return texture;
  }

  private destroyLabels() {
    for (const label of this.labelTexts.values()) {
      label.destroy();
    }
    this.labelTexts.clear();
    this.labelStyles.clear();
  }

  private rebuildSprites() {
    for (const s of this.nodeSprites.values()) s.destroy();
    for (const s of this.glowSprites.values()) s.destroy();
    this.destroyLabels();
    this.nodeSprites.clear();
    this.glowSprites.clear();
    this.nodesById.clear();

    for (const node of this.nodes) {
      this.nodesById.set(node.id, node);

      const glow = new Sprite(this.glowTexture);
      glow.anchor.set(0.5);
      glow.tint = node.color;
      this.glowLayer.addChild(glow);
      this.glowSprites.set(node.id, glow);

      const sprite = new Sprite(this.getNodeTexture(node.radius, node.color));
      sprite.anchor.set(0.5);
      this.nodeLayer.addChild(sprite);
      this.nodeSprites.set(node.id, sprite);
    }
  }

  private labelStyleFor(node: GraphNode, isHub: boolean): TextStyle {
    const key = `${node.colorStr}_${isHub}`;
    let style = this.labelStyles.get(key);
    if (!style) {
      style = new TextStyle({
        fontFamily: LABEL_FONT,
        fontSize: isHub ? 12 : 10,
        fontWeight: isHub ? "700" : "400",
        fill: this.isLight ? LIGHT_LABEL_COLOR : node.colorStr,
        dropShadow: isHub && !this.isLight ? { alpha: 0.8, blur: 0, color: 0x000000, distance: 2 } : false,
      });
      this.labelStyles.set(key, style);
    }
    return style;
  }

  private createLabel(node: GraphNode): Text {
    const label = new Text({
      text: truncateLabel(node.source),
      style: this.labelStyleFor(node, node.radius > HUB_RADIUS),
      // Labels live in world space and are read zoomed in.
      resolution: 3,
    });
    label.anchor.set(0, 0.5);
    this.labelLayer.addChild(label);
    this.labelTexts.set(node.id, label);
    return label;
  }

  private update = (ticker: Ticker) => {
    const dt = Math.min(ticker.deltaTime, 3);
    this.time += dt;
    this.frame++;

    this.updateCamera(dt);

    if (!this.reducedMotion && this.frame % 4 === 0 && !this.isLight) {
      const star = this.starSprites[Math.floor(Math.random() * this.starSprites.length)];
      if (star) {
        star.alpha = 0.05 + Math.random() * 0.45;
      }
    }

    if (this.edgesDirty) {
      this.rebuildEdges();
      this.edgesDirty = false;
    }

    this.updateNodes();
    this.updateFlowDots();
    this.updateRings();
    this.updateParticles(dt);
    this.updateWaves(dt);
  };

  private updateCamera(dt: number) {
    const cam = this.camera;
    const k = Math.min(1, CAMERA_LERP * dt);
    cam.x += (cam.targetX - cam.x) * k;
    cam.y += (cam.targetY - cam.y) * k;
    cam.zoom += (cam.targetZoom - cam.zoom) * k;

    this.world.x = this.width / 2 - cam.x * cam.zoom;
    this.world.y = this.height / 2 - cam.y * cam.zoom;
    this.world.scale.set(cam.zoom);
  }

  private curveOf(link: GraphLink): EdgeCurve {
    const s = link.source as GraphNode;
    const t = link.target as GraphNode;
    const sx = s.x ?? 0;
    const sy = s.y ?? 0;
    const tx = t.x ?? 0;
    const ty = t.y ?? 0;
    // Control point offset perpendicular to the edge by 5% of its length: a slight, consistent bow.
    return {
      sx,
      sy,
      cpx: (sx + tx) / 2 - (ty - sy) * 0.05,
      cpy: (sy + ty) / 2 + (tx - sx) * 0.05,
      tx,
      ty,
      phase: s.pulse ?? 0,
    };
  }

  /**
   * Edges live in world space, so panning and zooming never touch them. All plain edges share one stroke and
   * highlighted ones share two per colour, which keeps the geometry to a handful of batches.
   */
  private rebuildEdges() {
    const g = this.edgeGraphics;
    g.clear();
    this.flowCurves.length = 0;

    const highlightedByColor = new Map<number, EdgeCurve[]>();
    let hasPlainEdges = false;

    for (const link of this.links) {
      const source = link.source as GraphNode;
      const target = link.target as GraphNode;
      const curve = this.curveOf(link);

      if (this.hasSearchResults && (source.highlighted || target.highlighted)) {
        const group = highlightedByColor.get(source.color) ?? [];
        group.push(curve);
        highlightedByColor.set(source.color, group);
        this.flowCurves.push(curve);
        continue;
      }

      g.moveTo(curve.sx, curve.sy);
      g.quadraticCurveTo(curve.cpx, curve.cpy, curve.tx, curve.ty);
      hasPlainEdges = true;
    }

    if (hasPlainEdges) {
      g.stroke({
        color: this.isLight ? 0x9aa4b8 : 0x4a5568,
        alpha: this.hasSearchResults ? 0.22 : 0.32,
        width: 0.8,
      });
    }

    for (const [color, curves] of highlightedByColor) {
      for (const style of [
        { alpha: 0.2, width: 5 },
        { alpha: 0.65, width: 1.5 },
      ]) {
        for (const curve of curves) {
          g.moveTo(curve.sx, curve.sy);
          g.quadraticCurveTo(curve.cpx, curve.cpy, curve.tx, curve.ty);
        }
        g.stroke({ color, ...style });
      }
    }
  }

  private updateNodes() {
    const cam = this.camera;
    const halfW = this.width / 2 / cam.zoom + VIEWPORT_MARGIN;
    const halfH = this.height / 2 / cam.zoom + VIEWPORT_MARGIN;
    const labelsEverywhere = cam.zoom > LABEL_ZOOM;
    let labelBudget = MAX_LABELS_CREATED_PER_FRAME;

    for (const node of this.nodes) {
      const sprite = this.nodeSprites.get(node.id);
      const glow = this.glowSprites.get(node.id);
      if (!sprite || !glow) {
        continue;
      }

      const nx = node.x ?? 0;
      const ny = node.y ?? 0;
      const isVisible = Math.abs(nx - cam.x) < halfW && Math.abs(ny - cam.y) < halfH;
      let label = this.labelTexts.get(node.id);

      sprite.visible = isVisible;
      glow.visible = isVisible;
      if (!isVisible) {
        if (label) {
          label.visible = false;
        }
        continue;
      }

      const phase = this.reducedMotion ? 0.5 : (Math.sin(this.time * 0.025 + node.pulse) + 1) / 2;
      const isHovered = node.id === this.hoveredId;
      const isSelected = node.id === this.selectedId;
      const isHighlighted = node.highlighted ?? false;
      const isDimmed = this.hasSearchResults && !isHighlighted && !isSelected && !isHovered;

      sprite.position.set(nx, ny);
      sprite.scale.set(1 + 0.08 * phase + (isHovered ? 0.25 : 0) + (isSelected ? 0.35 : 0));
      sprite.alpha = isDimmed ? 0.2 : 1;

      const emphasis = (isSelected ? 0.7 : isHighlighted ? 0.4 : 0.1 * phase) + (isHovered ? 0.3 : 0);
      const glowRadius = node.radius * GLOW_RADIUS_FACTOR + GLOW_EMPHASIS_UNIT * emphasis;
      glow.position.set(nx, ny);
      glow.scale.set(glowRadius / GLOW_TEXTURE_RADIUS);
      if (isDimmed) {
        glow.alpha = 0.05;
      } else {
        const base = isSelected ? 1 : isHighlighted ? 0.8 : 0.55 + phase * 0.15;
        glow.alpha = isHighlighted && node.queryScore ? Math.max(base, node.queryScore * 0.9) : base;
      }

      const showLabel = labelsEverywhere || isHovered || isSelected;
      if (showLabel && !label && (labelBudget > 0 || isHovered || isSelected)) {
        label = this.createLabel(node);
        labelBudget--;
      }
      if (label) {
        label.visible = showLabel;
        if (showLabel) {
          label.position.set(nx + node.radius + 6, ny);
          label.alpha = isSelected ? 1 : isHovered ? 0.95 : isDimmed ? 0.3 : 0.7;
        }
      }
    }
  }

  /** A bright dot travelling along each highlighted edge. */
  private updateFlowDots() {
    const count = this.reducedMotion ? 0 : this.flowCurves.length;

    for (let i = this.flowSprites.length; i < count; i++) {
      const dot = new Sprite(this.particleTexture);
      dot.anchor.set(0.5);
      dot.alpha = 0.8;
      dot.scale.set(FLOW_DOT_RADIUS / 6);
      this.edgeLayer.addChild(dot);
      this.flowSprites.push(dot);
    }

    for (let i = 0; i < this.flowSprites.length; i++) {
      const dot = this.flowSprites[i];
      dot.visible = i < count;
      if (i >= count) {
        continue;
      }
      const c = this.flowCurves[i];
      const t = (this.time * 0.015 + c.phase) % 1;
      const u = 1 - t;
      dot.x = u * u * c.sx + 2 * u * t * c.cpx + t * t * c.tx;
      dot.y = u * u * c.sy + 2 * u * t * c.cpy + t * t * c.ty;
    }
  }

  private updateRings() {
    const selected = this.selectedId ? this.nodesById.get(this.selectedId) : undefined;
    const hovered =
      this.hoveredId && this.hoveredId !== this.selectedId ? this.nodesById.get(this.hoveredId) : undefined;

    if (!selected && !hovered) {
      if (this.ringsDrawn) {
        this.ringGraphics.clear();
        this.ringsDrawn = false;
      }
      return;
    }

    const g = this.ringGraphics;
    g.clear();
    this.ringsDrawn = true;

    if (selected) {
      const nx = selected.x ?? 0;
      const ny = selected.y ?? 0;
      const size = selected.radius + 11;
      const arm = size * 0.48;
      for (const [dx, dy] of [
        [1, 1],
        [-1, 1],
        [-1, -1],
        [1, -1],
      ]) {
        const cornerX = nx - dx * size;
        const cornerY = ny - dy * size;
        g.moveTo(cornerX + dx * arm, cornerY);
        g.lineTo(cornerX, cornerY);
        g.lineTo(cornerX, cornerY + dy * arm);
      }
      g.stroke({ color: selected.color, alpha: 0.9, width: 1.5 });

      const phase = this.reducedMotion ? 0.5 : (Math.sin(this.time * 0.04) + 1) / 2;
      g.circle(nx, ny, selected.radius + 2 + phase * 2);
      g.stroke({ color: this.isLight ? selected.color : 0xffffff, alpha: 0.45 + phase * 0.2, width: 1 });
    }

    if (hovered) {
      g.circle(hovered.x ?? 0, hovered.y ?? 0, hovered.radius + 5);
      g.stroke({ color: hovered.color, alpha: 0.85, width: 1.5 });
    }
  }

  private updateParticles(dt: number) {
    const particles = this.particles;
    if (this.reducedMotion) {
      particles.length = 0;
    }
    if (particles.length === 0 && this.particleSprites.every((s) => !s.visible)) {
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
      if (p.life <= 0) {
        continue;
      }

      let sprite = this.particleSprites[alive];
      if (!sprite) {
        sprite = new Sprite(this.particleTexture);
        sprite.anchor.set(0.5);
        sprite.blendMode = this.isLight ? "normal" : "add";
        this.particleLayer.addChild(sprite);
        this.particleSprites.push(sprite);
      }
      sprite.visible = true;
      sprite.position.set(p.x, p.y);
      sprite.alpha = p.life * 0.8;
      sprite.scale.set(p.size * p.life * 0.3);
      sprite.tint = p.color;

      particles[alive++] = p;
    }
    particles.length = alive;

    for (let i = alive; i < this.particleSprites.length; i++) {
      this.particleSprites[i].visible = false;
    }
  }

  private updateWaves(dt: number) {
    if (this.waves.length === 0) {
      if (this.wavesDrawn) {
        this.waveGraphics.clear();
        this.wavesDrawn = false;
      }
      return;
    }

    const g = this.waveGraphics;
    g.clear();
    this.wavesDrawn = true;

    let alive = 0;
    for (const wave of this.waves) {
      wave.radius += wave.speed * dt;
      wave.life -= 0.008 * dt;
      if (wave.life <= 0 || wave.radius > wave.maxRadius) {
        continue;
      }
      g.circle(wave.originX, wave.originY, wave.radius);
      g.stroke({ color: wave.color, alpha: wave.life * 0.3, width: 2 });
      this.waves[alive++] = wave;
    }
    this.waves.length = alive;
  }
}
