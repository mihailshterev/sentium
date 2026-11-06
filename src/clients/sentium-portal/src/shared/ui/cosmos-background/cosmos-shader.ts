/**
 * Deep-space field: very faint nebula haze (teal / violet / gold dust) plus a twinkling starfield.
 * Rendered at reduced resolution; crisp geometry (grid, coordinate rings) is drawn in the DOM above it.
 */
export const FRAGMENT_SHADER = `
precision mediump float;

uniform vec2 uResolution;
uniform float uTime;
uniform float uScale;
uniform vec3 uBase;
uniform vec3 uHazeA;
uniform vec3 uHazeB;
uniform vec3 uHazeC;
uniform vec3 uStar;
uniform float uStars;

float hash(vec2 p) {
  p = fract(p * vec2(123.34, 456.21));
  p += dot(p, p + 45.32);
  return fract(p.x * p.y);
}

float noise(vec2 p) {
  vec2 i = floor(p);
  vec2 f = fract(p);
  vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p) {
  float v = 0.0;
  float a = 0.5;
  for (int i = 0; i < 5; i++) {
    v += a * noise(p);
    p = p * 2.03 + vec2(1.7, 9.2);
    a *= 0.5;
  }
  return v;
}

/* One star layer: at most one star per cell, size in CSS pixels. */
float starLayer(vec2 px, float cell, float density, float size, float t) {
  vec2 id = floor(px / cell);
  vec2 local = fract(px / cell);
  float h = hash(id);
  if (h > density) {
    return 0.0;
  }
  vec2 pos = vec2(hash(id + 13.1), hash(id + 7.7)) * 0.8 + 0.1;
  float d = length((local - pos) * cell);
  float twinkle = 0.55 + 0.45 * sin(t * (0.6 + 1.6 * hash(id + 5.1)) + h * 60.0);
  return smoothstep(size, 0.0, d) * twinkle * (0.5 + 0.5 * hash(id + 2.9));
}

void main() {
  vec2 uv = gl_FragCoord.xy / uResolution;
  vec2 px = gl_FragCoord.xy / uScale;
  float aspect = uResolution.x / uResolution.y;
  vec2 p = vec2(uv.x * aspect, uv.y);
  float t = uTime;

  vec3 color = uBase;

  // Haze: three soft regions, slowly drifting.
  float n = fbm(p * 1.6 + vec2(t * 0.012, -t * 0.008));
  float n2 = fbm(p * 2.4 - vec2(t * 0.01, t * 0.006) + 4.0);
  float teal = smoothstep(0.95, 0.0, length((uv - vec2(0.78, 0.68)) * vec2(1.1, 1.5))) * n;
  float violet = smoothstep(0.9, 0.0, length((uv - vec2(0.18, 0.22)) * vec2(1.2, 1.4))) * n2;
  float dust = smoothstep(0.55, 0.0, length((uv - vec2(0.62, 0.48)) * vec2(1.6, 2.2))) * n * n2;
  color = mix(color, uHazeA, clamp(teal * 0.85, 0.0, 1.0));
  color = mix(color, uHazeB, clamp(violet * 0.8, 0.0, 1.0));
  color = mix(color, uHazeC, clamp(dust * 0.9, 0.0, 1.0));

  // Stars: two layers, denser where the haze is.
  float s = starLayer(px, 22.0, 0.14, 1.1, t) + starLayer(px + 300.0, 61.0, 0.35, 1.7, t * 0.8);
  s *= 0.55 + 0.9 * (teal + violet);
  color += uStar * s * uStars;

  gl_FragColor = vec4(color, 1.0);
}
`;
