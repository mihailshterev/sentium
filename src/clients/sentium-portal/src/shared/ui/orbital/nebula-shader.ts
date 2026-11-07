/**
 * Procedural nebula inside a circle: a tilted galactic band of teal light with gold dust along its core,
 * violet clouds around it and a sprinkle of stars. Output is premultiplied alpha, masked to the disc.
 */
export const NEBULA_SHADER = `
precision mediump float;

uniform vec2 uResolution;
uniform float uTime;
uniform vec3 uBase;
uniform vec3 uTeal;
uniform vec3 uViolet;
uniform vec3 uGold;

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
  for (int i = 0; i < 6; i++) {
    v += a * noise(p);
    p = p * 2.02 + vec2(3.1, 1.7);
    a *= 0.5;
  }
  return v;
}

void main() {
  float side = min(uResolution.x, uResolution.y);
  vec2 uv = (gl_FragCoord.xy - 0.5 * uResolution) / (0.5 * side);
  float r = length(uv);
  float t = uTime;

  // Slow rotation of the whole field.
  float a = t * 0.02;
  vec2 p = mat2(cos(a), -sin(a), sin(a), cos(a)) * uv;

  // Domain-warped clouds with a gentle swirl around the centre.
  float ang = atan(p.y, p.x) + r * 1.8;
  vec2 q = vec2(cos(ang), sin(ang)) * r;
  vec2 w = vec2(fbm(q * 2.2 + t * 0.02), fbm(q * 2.2 - t * 0.017 + 5.2));
  float n = fbm(q * 2.6 + w * 1.6);
  float n2 = fbm(p * 4.2 + w * 2.0 - 3.0);

  // Tilted galactic band.
  float bandDist = (p.y - p.x * 0.62) * 2.3;
  float band = exp(-bandDist * bandDist);
  float lane = exp(-pow((p.y - p.x * 0.62 + 0.06) * 9.0, 2.0)); // dark dust lane

  vec3 color = uBase;
  color = mix(color, uViolet * 0.75, smoothstep(0.35, 0.95, n) * (1.0 - band * 0.6) * 0.85);
  color = mix(color, uTeal, smoothstep(0.25, 0.9, n2) * band * 0.9);
  color += uTeal * band * 0.18;
  color += uGold * pow(band, 4.0) * smoothstep(0.3, 0.95, n) * 0.85;
  color *= 1.0 - lane * 0.55 * smoothstep(0.2, 0.8, n2);

  // Bright core.
  color += vec3(1.0, 0.93, 0.78) * exp(-r * r * 22.0) * 0.55;

  // Stars.
  vec2 cell = floor(gl_FragCoord.xy / 3.0);
  float h = hash(cell);
  float star = step(0.992, h) * (0.5 + 0.5 * sin(t * (1.0 + h * 3.0) + h * 40.0));
  color += vec3(0.85, 0.92, 1.0) * star * (0.4 + band * 0.6);

  // Disc mask, darker towards the rim.
  float mask = smoothstep(1.0, 0.985, r);
  color *= mix(0.45, 1.0, smoothstep(1.0, 0.35, r));
  gl_FragColor = vec4(color * mask, mask);
}
`;
