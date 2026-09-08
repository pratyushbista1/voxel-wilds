export const VIDEO_DEFAULTS = {
  maxFps: 60,
  renderScale: 100,
  brightness: 100,
  shadowSize: 2048,
  clouds: true,
  particles: 180,
  viewBobbing: true,
  vignette: true,
  fogDistance: 100,
  entityDistance: 64,
  difficulty: 'normal',
};
export function normalizeSettings(settings) {
  const result = { ...settings };
  for (const [key, min, max, fallback] of [
    ['distance', 3, 12, 5],
    ['fov', 60, 110, 75],
    ['sensitivity', 10, 100, 45],
    ['volume', 0, 100, 40],
    ['renderScale', 25, 150, 100],
    ['brightness', 25, 150, 100],
    ['fogDistance', 60, 140, 100],
    ['entityDistance', 24, 96, 64],
  ])
    result[key] = Math.max(
      min,
      Math.min(max, Number.isFinite(result[key]) ? result[key] : fallback)
    );
  for (const [key, values, fallback] of [
    ['maxFps', [0, 30, 60, 90, 120, 144, 165, 240], 60],
    ['shadowSize', [512, 1024, 2048, 4096], 2048],
    ['particles', [0, 60, 180], 180],
    ['difficulty', ['peaceful', 'easy', 'normal', 'hard'], 'normal'],
  ])
    if (!values.includes(result[key])) result[key] = fallback;
  for (const key of ['clouds', 'viewBobbing', 'vignette', 'shadows', 'showFps'])
    result[key] = typeof result[key] === 'boolean' ? result[key] : key !== 'showFps';
  return result;
}
export function applyVideo(game) {
  const g = game,
    s = (g.settings = normalizeSettings(g.settings));
  const ratio = (Math.min(devicePixelRatio, 1.75) * s.renderScale) / 100;
  if (g.renderer.getPixelRatio() !== ratio) g.renderer.setPixelRatio(ratio);
  g.renderer.toneMappingExposure = (1.12 * s.brightness) / 100;
  if (g.atmosphere) {
    const a = g.atmosphere;
    a.cloudGroup.visible = s.clouds && g.dimension !== 'nether';
    a.flowers.visible = g.dimension !== 'nether';
    if (a.sunLight.shadow.mapSize.x !== s.shadowSize) {
      a.sunLight.shadow.mapSize.set(s.shadowSize, s.shadowSize);
      a.sunLight.shadow.map?.dispose();
      a.sunLight.shadow.map = null;
    }
  }
  if (g.particles) g.particles.maxCount = s.particles;
  document.getElementById('vignette').classList.toggle('hidden', !s.vignette);
}
