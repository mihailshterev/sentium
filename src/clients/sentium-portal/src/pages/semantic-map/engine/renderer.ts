import { Application, Container, Graphics, BlurFilter, Texture, Sprite, Ticker, Text, TextStyle } from "pixi.js";
import type { GraphNode, GraphLink, Particle, TraversalWave, CameraState } from "./types";

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

function generateGlowTexture(app: Application, radius: number): Texture {
  const steps = 8;
  const maxR = radius * 4;
  const size = maxR * 2 + 4;
  const cx = size / 2;
  const g = new Graphics();

  for (let i = steps; i >= 1; i--) {
    const r = (i / steps) * maxR;
    const a = 0.02 + ((steps - i) / steps) * 0.22;
    g.circle(cx, cx, r);
    g.fill({ color: 0xffffff, alpha: a });
  }

  const texture = app.renderer.generateTexture({ target: g, resolution: 1 });
  g.destroy();
  return texture;
}

function generateParticleTexture(app: Application): Texture {
  const g = new Graphics();
  g.circle(8, 8, 6);
  g.fill({ color: 0xffffff, alpha: 0.9 });
  g.circle(8, 8, 3);
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

export class UniverseRenderer {
  app: Application;
  private bgLayer: Container;
  private edgeLayer: Container;
  private glowLayer: Container;
  private nodeLayer: Container;
  private labelLayer: Container;
  private particleLayer: Container;
  private waveLayer: Container;
  private ringLayer: Container;
  private nebulaLayer: Container;
  private gridLayer: Container;
  private world: Container;
  private nodeTextures = new Map<string, Texture>();
  private glowTexture!: Texture;
  private particleTexture!: Texture;
  private starTexture!: Texture;
  private nodeSprites = new Map<string, Sprite>();
  private glowSprites = new Map<string, Sprite>();
  private labelTexts = new Map<string, Text>();
  private edgeGraphics!: Graphics;
  private ringGraphics!: Graphics;
  private particleSprites: Sprite[] = [];
  private particlePool: Sprite[] = [];
  private starSprites: Sprite[] = [];
  private _nodes: GraphNode[] = [];
  private _links: GraphLink[] = [];
  private _particles: Particle[] = [];
  private _waves: TraversalWave[] = [];
  private _camera: CameraState = { x: 0, y: 0, zoom: 1, targetX: 0, targetY: 0, targetZoom: 1 };
  private _tick = 0;
  private _hoveredId: string | null = null;
  private _selectedId: string | null = null;
  private _hasSearchResults = false;
  private _initialized = false;
  private _width = 0;
  private _height = 0;

  onNodeHover: ((node: GraphNode | null) => void) | null = null;
  onNodeClick: ((node: GraphNode | null) => void) | null = null;

  constructor() {
    this.app = new Application();
    this.world = new Container();
    this.bgLayer = new Container();
    this.gridLayer = new Container();
    this.edgeLayer = new Container();
    this.glowLayer = new Container();
    this.nodeLayer = new Container();
    this.labelLayer = new Container();
    this.particleLayer = new Container();
    this.waveLayer = new Container();
    this.ringLayer = new Container();
    this.nebulaLayer = new Container();
  }

  async init(canvas: HTMLCanvasElement, width: number, height: number) {
    this._width = width;
    this._height = height;

    await this.app.init({
      canvas,
      width,
      height,
      antialias: true,
      backgroundColor: 0x03050f,
      resolution: Math.min(window.devicePixelRatio, 2),
      autoDensity: true,
      powerPreference: "high-performance",
    });

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

    this.glowLayer.blendMode = "add";
    this.glowLayer.filters = [new BlurFilter({ strength: 4, quality: 2 })];
    this.glowLayer.alpha = 0.55;

    this.nebulaLayer.filters = [new BlurFilter({ strength: 20, quality: 3 })];
    this.nebulaLayer.alpha = 1;

    this.edgeGraphics = new Graphics();
    this.edgeLayer.addChild(this.edgeGraphics);

    this.ringGraphics = new Graphics();
    this.ringLayer.addChild(this.ringGraphics);

    this.glowTexture = generateGlowTexture(this.app, 10);
    this.particleTexture = generateParticleTexture(this.app);
    this.starTexture = generateStarTexture(this.app);

    this.createStarfield(width, height);
    this.createNebula(width, height);
    this.initGrid(width, height);

    this.app.stage.eventMode = "static";
    this.app.stage.hitArea = this.app.screen;
    this.app.stage.on("pointermove", this.onPointerMove);
    this.app.stage.on("pointerdown", this.onPointerDown);

    this.app.ticker.add(this.update);

    this._initialized = true;
  }

  private createStarfield(width: number, height: number) {
    const layers = [
      { count: 200, alpha: 0.12, scale: 0.25 },
      { count: 100, alpha: 0.22, scale: 0.45 },
      { count: 50, alpha: 0.38, scale: 0.7 },
    ];

    for (const layer of layers) {
      for (let i = 0; i < layer.count; i++) {
        const star = new Sprite(this.starTexture);
        star.anchor.set(0.5);
        star.x = Math.random() * width;
        star.y = Math.random() * height;
        star.alpha = layer.alpha + Math.random() * 0.15;
        star.scale.set(layer.scale + Math.random() * 0.3);
        star.tint = Math.random() > 0.7 ? 0x88ccff : 0xffffff;
        this.bgLayer.addChild(star);
        this.starSprites.push(star);
      }
    }
  }

  private createNebula(width: number, height: number) {
    const blobs = [
      { x: width * 0.5, y: height * 0.5, color: 0x061428, scale: 22, alpha: 0.9 },
      { x: width * 0.5, y: height * 0.5, color: 0x0d2244, scale: 14, alpha: 0.5 },
    ];
    for (const b of blobs) {
      const sprite = new Sprite(this.glowTexture);
      sprite.anchor.set(0.5);
      sprite.x = b.x;
      sprite.y = b.y;
      sprite.tint = b.color;
      sprite.alpha = b.alpha;
      sprite.scale.set(b.scale);
      this.nebulaLayer.addChild(sprite);
    }
  }

  private initGrid(width: number, height: number) {
    const g = new Graphics();
    const extent = 4000;
    const minorSize = 100;
    const majorSize = 500;

    for (let x = -extent; x <= extent; x += minorSize) {
      const isMajor = x % majorSize === 0;
      g.moveTo(x, -extent);
      g.lineTo(x, extent);
      g.stroke({ color: 0x0d1f3c, alpha: isMajor ? 0.5 : 0.18, width: isMajor ? 0.8 : 0.35 });
    }
    for (let y = -extent; y <= extent; y += minorSize) {
      const isMajor = y % majorSize === 0;
      g.moveTo(-extent, y);
      g.lineTo(extent, y);
      g.stroke({ color: 0x0d1f3c, alpha: isMajor ? 0.5 : 0.18, width: isMajor ? 0.8 : 0.35 });
    }

    for (let x = -extent; x <= extent; x += majorSize) {
      for (let y = -extent; y <= extent; y += majorSize) {
        g.circle(x, y, 1.5);
        g.fill({ color: 0x1a3a6c, alpha: 0.7 });
      }
    }

    const zones = [
      { x: width * 0.35, y: height * 0.4, r: 190, color: 0x06b6d4 },
      { x: width * 0.65, y: height * 0.4, r: 175, color: 0xa855f7 },
      { x: width * 0.5, y: height * 0.7, r: 160, color: 0xf59e0b },
    ];
    for (const z of zones) {
      g.circle(z.x, z.y, z.r);
      g.stroke({ color: z.color, alpha: 0.09, width: 1 });
      g.circle(z.x, z.y, z.r * 0.55);
      g.stroke({ color: z.color, alpha: 0.05, width: 0.5 });
      const ch = 10;
      g.moveTo(z.x - ch, z.y);
      g.lineTo(z.x + ch, z.y);
      g.moveTo(z.x, z.y - ch);
      g.lineTo(z.x, z.y + ch);
      g.stroke({ color: z.color, alpha: 0.22, width: 0.7 });
      g.circle(z.x, z.y, 2);
      g.fill({ color: z.color, alpha: 0.35 });
    }

    this.gridLayer.addChild(g);
  }

  setGraph(nodes: GraphNode[], links: GraphLink[]) {
    const isNewGraph = nodes !== this._nodes;
    this._nodes = nodes;
    this._links = links;
    if (isNewGraph) {
      this.rebuildSprites();
    }
  }

  setParticles(particles: Particle[]) {
    this._particles = particles;
  }

  addWave(wave: TraversalWave) {
    this._waves.push(wave);
  }

  setCamera(camera: CameraState) {
    this._camera = camera;
  }

  setHoveredId(id: string | null) {
    this._hoveredId = id;
  }

  setSelectedId(id: string | null) {
    this._selectedId = id;
  }

  setHasSearchResults(v: boolean) {
    this._hasSearchResults = v;
  }

  resize(width: number, height: number) {
    if (!this._initialized) return;
    this._width = width;
    this._height = height;
    this.app.renderer.resize(width, height);
  }

  destroy() {
    this.app.ticker.remove(this.update);
    this.app.stage.off("pointermove", this.onPointerMove);
    this.app.stage.off("pointerdown", this.onPointerDown);
    for (const t of this.nodeTextures.values()) t.destroy(true);
    this.glowTexture?.destroy(true);
    this.particleTexture?.destroy(true);
    this.starTexture?.destroy(true);
    this.app.destroy(false, { children: true });
  }

  private getNodeTexture(radius: number, color: number): Texture {
    const key = `${Math.round(radius)}_${color.toString(16)}`;
    if (!this.nodeTextures.has(key)) {
      this.nodeTextures.set(key, generateNodeTexture(this.app, radius, color));
    }
    return this.nodeTextures.get(key)!;
  }

  private rebuildSprites() {
    for (const s of this.nodeSprites.values()) s.destroy();
    for (const s of this.glowSprites.values()) s.destroy();
    for (const t of this.labelTexts.values()) t.destroy();
    this.nodeSprites.clear();
    this.glowSprites.clear();
    this.labelTexts.clear();
    this.nodeLayer.removeChildren();
    this.glowLayer.removeChildren();
    this.labelLayer.removeChildren();

    for (const node of this._nodes) {
      const glow = new Sprite(this.glowTexture);
      glow.anchor.set(0.5);
      glow.tint = node.color;
      glow.alpha = 0.6;
      glow.scale.set((node.radius * 2.2) / 40);
      this.glowLayer.addChild(glow);
      this.glowSprites.set(node.id, glow);

      const tex = this.getNodeTexture(node.radius, node.color);
      const sprite = new Sprite(tex);
      sprite.anchor.set(0.5);
      this.nodeLayer.addChild(sprite);
      this.nodeSprites.set(node.id, sprite);

      const isHub = node.radius > 8;
      const label = new Text({
        text: node.source.length > 22 ? node.source.slice(0, 22) + "…" : node.source,
        style: new TextStyle({
          fontFamily: "'Geist Mono', monospace",
          fontSize: isHub ? 12 : 10,
          fontWeight: isHub ? "700" : "400",
          fill: node.colorStr,
          align: "left",
          dropShadow: isHub
            ? {
                alpha: 0.8,
                blur: 0,
                color: 0x000000,
                distance: 2,
              }
            : false,
        }),
      });
      label.anchor.set(0, 0.5);
      label.alpha = 0;
      this.labelLayer.addChild(label);
      this.labelTexts.set(node.id, label);
    }
  }

  private update = (_ticker: Ticker) => {
    this._tick++;
    const dt = _ticker.deltaTime;

    const cam = this._camera;
    const lerpSpeed = 0.08;
    cam.x += (cam.targetX - cam.x) * lerpSpeed * dt;
    cam.y += (cam.targetY - cam.y) * lerpSpeed * dt;
    cam.zoom += (cam.targetZoom - cam.zoom) * lerpSpeed * dt;

    this.world.x = this._width / 2 - cam.x * cam.zoom;
    this.world.y = this._height / 2 - cam.y * cam.zoom;
    this.world.scale.set(cam.zoom);

    if (this._tick % 4 === 0) {
      const idx = Math.floor(Math.random() * this.starSprites.length);
      const star = this.starSprites[idx];
      if (star) {
        star.alpha = 0.05 + Math.random() * 0.45;
      }
    }

    const vpLeft = cam.x - this._width / 2 / cam.zoom - 50;
    const vpRight = cam.x + this._width / 2 / cam.zoom + 50;
    const vpTop = cam.y - this._height / 2 / cam.zoom - 50;
    const vpBottom = cam.y + this._height / 2 / cam.zoom + 50;

    this.edgeGraphics.clear();
    for (const link of this._links) {
      const s = link.source as GraphNode;
      const tg = link.target as GraphNode;
      const sx = s.x ?? 0;
      const sy = s.y ?? 0;
      const tx = tg.x ?? 0;
      const ty = tg.y ?? 0;

      if (sx < vpLeft && tx < vpLeft) {
        continue;
      }
      if (sx > vpRight && tx > vpRight) {
        continue;
      }
      if (sy < vpTop && ty < vpTop) {
        continue;
      }
      if (sy > vpBottom && ty > vpBottom) {
        continue;
      }

      const highlighted = (s.highlighted || tg.highlighted) && this._hasSearchResults;
      const pulse = (Math.sin(this._tick * 0.02 + (s.pulse ?? 0)) + 1) / 2;

      const mx = (sx + tx) / 2;
      const my = (sy + ty) / 2;
      const dx = tx - sx;
      const dy = ty - sy;
      const len = Math.sqrt(dx * dx + dy * dy);
      const perpX = (-dy / (len || 1)) * len * 0.05;
      const perpY = (dx / (len || 1)) * len * 0.05;
      const cpx = mx + perpX;
      const cpy = my + perpY;

      const alpha = highlighted ? 0.65 : 0.2 + pulse * 0.08;
      const color = highlighted ? s.color : 0x4a5568;
      const width = highlighted ? 1.5 : 0.7;

      const glowAlpha = highlighted ? 0.2 : 0.07 + pulse * 0.03;
      const glowWidth = highlighted ? 5 : 2.5;
      this.edgeGraphics.moveTo(sx, sy);
      this.edgeGraphics.quadraticCurveTo(cpx, cpy, tx, ty);
      this.edgeGraphics.stroke({ color: highlighted ? s.color : 0x334155, alpha: glowAlpha, width: glowWidth });

      this.edgeGraphics.moveTo(sx, sy);
      this.edgeGraphics.quadraticCurveTo(cpx, cpy, tx, ty);
      this.edgeGraphics.stroke({ color, alpha, width });

      if (highlighted) {
        const flowT = (this._tick * 0.015 + (s.pulse ?? 0)) % 1;
        const t2 = flowT;
        const fx = (1 - t2) * (1 - t2) * sx + 2 * (1 - t2) * t2 * cpx + t2 * t2 * tx;
        const fy = (1 - t2) * (1 - t2) * sy + 2 * (1 - t2) * t2 * cpy + t2 * t2 * ty;
        this.edgeGraphics.circle(fx, fy, 1.5);
        this.edgeGraphics.fill({ color: 0xffffff, alpha: 0.7 });
      }
    }

    for (const node of this._nodes) {
      const nx = node.x ?? 0;
      const ny = node.y ?? 0;
      const isVisible = nx > vpLeft && nx < vpRight && ny > vpTop && ny < vpBottom;

      const sprite = this.nodeSprites.get(node.id);
      const glow = this.glowSprites.get(node.id);
      const label = this.labelTexts.get(node.id);

      if (!sprite || !glow || !label) {
        continue;
      }

      sprite.visible = isVisible;
      glow.visible = isVisible;
      label.visible = isVisible;

      if (!isVisible) {
        continue;
      }

      const phase = (Math.sin(this._tick * 0.025 + node.pulse) + 1) / 2;
      const isHovered = node.id === this._hoveredId;
      const isSelected = node.id === this._selectedId;
      const isHighlighted = node.highlighted ?? false;

      sprite.x = nx;
      sprite.y = ny;
      glow.x = nx;
      glow.y = ny;
      label.x = nx + node.radius + 6;
      label.y = ny;

      const baseScale = 1.0;
      const pulseScale = 0.08 * phase;
      const hoverScale = isHovered ? 0.25 : 0;
      const selectedScale = isSelected ? 0.35 : 0;
      sprite.scale.set(baseScale + pulseScale + hoverScale + selectedScale);

      const glowBase = (node.radius * 2.2) / 40;
      const glowExtra = isSelected ? 0.7 : isHighlighted ? 0.4 : 0.1 * phase;
      glow.scale.set(glowBase + glowExtra + (isHovered ? 0.3 : 0));
      glow.alpha = isSelected ? 1.0 : isHighlighted ? 0.8 : 0.55 + phase * 0.15;

      if (this._hasSearchResults && !isHighlighted && !isSelected && !isHovered) {
        sprite.alpha = 0.2;
        glow.alpha = 0.05;
      } else {
        sprite.alpha = 1;
      }

      const showLabel = cam.zoom > 2.5 || isHovered || isSelected;
      label.alpha = showLabel ? (isSelected ? 1 : isHovered ? 0.95 : 0.55) : 0;

      if (node.queryScore !== undefined && node.queryScore > 0 && isHighlighted) {
        glow.alpha = Math.max(glow.alpha, node.queryScore * 0.9);
      }
    }

    this.ringGraphics.clear();
    for (const node of this._nodes) {
      const nx = node.x ?? 0;
      const ny = node.y ?? 0;
      if (nx < vpLeft || nx > vpRight || ny < vpTop || ny > vpBottom) {
        continue;
      }

      const isHovered = node.id === this._hoveredId;
      const isSelected = node.id === this._selectedId;

      if (isSelected) {
        const bSize = node.radius + 11;
        const bracketLen = bSize * 0.48;
        const corners: Array<{ x: number; y: number; dx: number; dy: number }> = [
          { x: nx - bSize, y: ny - bSize, dx: 1, dy: 1 },
          { x: nx + bSize, y: ny - bSize, dx: -1, dy: 1 },
          { x: nx + bSize, y: ny + bSize, dx: -1, dy: -1 },
          { x: nx - bSize, y: ny + bSize, dx: 1, dy: -1 },
        ];
        for (const c of corners) {
          this.ringGraphics.moveTo(c.x, c.y);
          this.ringGraphics.lineTo(c.x + c.dx * bracketLen, c.y);
          this.ringGraphics.moveTo(c.x, c.y);
          this.ringGraphics.lineTo(c.x, c.y + c.dy * bracketLen);
        }
        this.ringGraphics.stroke({ color: node.color, alpha: 0.9, width: 1.5 });
        const phase = (Math.sin(this._tick * 0.04) + 1) / 2;
        this.ringGraphics.circle(nx, ny, node.radius + 2 + phase * 2);
        this.ringGraphics.stroke({ color: 0xffffff, alpha: 0.45 + phase * 0.2, width: 1 });
      } else if (isHovered) {
        this.ringGraphics.circle(nx, ny, node.radius + 5);
        this.ringGraphics.stroke({ color: node.color, alpha: 0.85, width: 1.5 });
      }
    }

    this.syncParticles();

    this.updateWaves();
  };

  private syncParticles() {
    const alive: Particle[] = [];
    let spriteIdx = 0;

    for (const p of this._particles) {
      p.x += p.vx;
      p.y += p.vy;
      p.vx *= 0.96;
      p.vy *= 0.96;
      p.life -= 0.02;

      if (p.life <= 0) {
        continue;
      }
      alive.push(p);

      let sprite: Sprite;
      if (spriteIdx < this.particleSprites.length) {
        sprite = this.particleSprites[spriteIdx];
      } else if (this.particlePool.length > 0) {
        sprite = this.particlePool.pop()!;
        sprite.visible = true;
        this.particleLayer.addChild(sprite);
        this.particleSprites.push(sprite);
      } else {
        sprite = new Sprite(this.particleTexture);
        sprite.anchor.set(0.5);
        sprite.blendMode = "add";
        this.particleLayer.addChild(sprite);
        this.particleSprites.push(sprite);
      }

      sprite.x = p.x;
      sprite.y = p.y;
      sprite.alpha = p.life * 0.8;
      sprite.scale.set(p.size * p.life * 0.3);
      sprite.tint = p.color;
      spriteIdx++;
    }

    for (let i = spriteIdx; i < this.particleSprites.length; i++) {
      this.particleSprites[i].visible = false;
    }

    this._particles.length = 0;
    this._particles.push(...alive);
  }

  private waveGraphics: Graphics | null = null;

  private updateWaves() {
    if (this._waves.length === 0) {
      if (this.waveGraphics) {
        this.waveGraphics.clear();
      }
      return;
    }

    if (!this.waveGraphics) {
      this.waveGraphics = new Graphics();
      this.waveLayer.addChild(this.waveGraphics);
    }

    this.waveGraphics.clear();
    const alive: TraversalWave[] = [];

    for (const w of this._waves) {
      w.radius += w.speed;
      w.life -= 0.008;

      if (w.life <= 0 || w.radius > w.maxRadius) {
        continue;
      }
      alive.push(w);

      this.waveGraphics.circle(w.originX, w.originY, w.radius);
      this.waveGraphics.stroke({ color: w.color, alpha: w.life * 0.3, width: 2 });
    }

    this._waves = alive;
  }

  private worldFromScreen(sx: number, sy: number): { x: number; y: number } {
    const cam = this._camera;
    const wx = (sx - this._width / 2) / cam.zoom + cam.x;
    const wy = (sy - this._height / 2) / cam.zoom + cam.y;
    return { x: wx, y: wy };
  }

  private hitTest(sx: number, sy: number): GraphNode | null {
    const { x, y } = this.worldFromScreen(sx, sy);
    for (let i = this._nodes.length - 1; i >= 0; i--) {
      const n = this._nodes[i];
      const dx = (n.x ?? 0) - x;
      const dy = (n.y ?? 0) - y;
      if (Math.sqrt(dx * dx + dy * dy) <= n.radius + 8) {
        return n;
      }
    }
    return null;
  }

  private onPointerMove = (e: { global: { x: number; y: number } }) => {
    const node = this.hitTest(e.global.x, e.global.y);
    this._hoveredId = node?.id ?? null;
    this.onNodeHover?.(node);
    if (this.app.canvas) {
      (this.app.canvas as HTMLCanvasElement).style.cursor = node ? "pointer" : "grab";
    }
  };

  private onPointerDown = (e: { global: { x: number; y: number } }) => {
    const node = this.hitTest(e.global.x, e.global.y);
    this.onNodeClick?.(node);
  };
}
