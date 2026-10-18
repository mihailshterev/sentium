import type { GraphNode, Particle, TraversalWave } from "./types";

export function spawnClickParticles(node: GraphNode, particles: Particle[]) {
  const count = 28;
  for (let i = 0; i < count; i++) {
    const angle = (i / count) * Math.PI * 2;
    const speed = 1.5 + Math.random() * 3;
    particles.push({
      x: node.x ?? 0,
      y: node.y ?? 0,
      vx: Math.cos(angle) * speed,
      vy: Math.sin(angle) * speed,
      life: 1,
      maxLife: 1,
      color: node.color,
      size: 2 + Math.random() * 3,
    });
  }
}

export function spawnSearchParticles(node: GraphNode, particles: Particle[]) {
  const count = 50;
  for (let i = 0; i < count; i++) {
    const angle = (i / count) * Math.PI * 2;
    const speed = 2.5 + Math.random() * 4;
    particles.push({
      x: node.x ?? 0,
      y: node.y ?? 0,
      vx: Math.cos(angle) * speed,
      vy: Math.sin(angle) * speed,
      life: 1,
      maxLife: 1,
      color: 0xffffff,
      size: 2.5 + Math.random() * 2.5,
    });
  }
}

export function spawnTrailParticles(x: number, y: number, color: number, particles: Particle[], count = 5) {
  for (let i = 0; i < count; i++) {
    particles.push({
      x: x + (Math.random() - 0.5) * 6,
      y: y + (Math.random() - 0.5) * 6,
      vx: (Math.random() - 0.5) * 0.8,
      vy: (Math.random() - 0.5) * 0.8,
      life: 0.6 + Math.random() * 0.4,
      maxLife: 1,
      color,
      size: 1 + Math.random() * 1.5,
    });
  }
}

export function spawnTraversalWave(node: GraphNode, maxRadius = 400): TraversalWave {
  return {
    originX: node.x ?? 0,
    originY: node.y ?? 0,
    radius: 0,
    maxRadius,
    speed: 3,
    life: 1,
    color: node.color,
  };
}

export function spawnSearchWaves(node: GraphNode): TraversalWave[] {
  return [
    spawnTraversalWave(node, 300),
    { ...spawnTraversalWave(node, 500), speed: 2, color: 0xffffff },
    { ...spawnTraversalWave(node, 700), speed: 1.5, color: 0x06b6d4 },
  ];
}
