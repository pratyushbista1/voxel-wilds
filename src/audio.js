export class Sound {
  constructor() {
    this.context = null;
    this.volume = 0.4;
    this.nextStep = 0;
    this.nextBird = 0;
  }
  async start() {
    if (!this.context) {
      this.context = new AudioContext();
      this.master = this.context.createGain();
      this.master.gain.value = this.volume * 0.22;
      this.master.connect(this.context.destination);
    }
    if (this.context.state === 'suspended') await this.context.resume();
  }
  setVolume(value) {
    this.volume = value;
    if (this.master) this.master.gain.setTargetAtTime(value * 0.22, this.context.currentTime, 0.15);
  }
  tone(freq, duration = 0.12, type = 'sine', gain = 0.3, slide = 0) {
    if (!this.context || this.context.state !== 'running') return;
    const t = this.context.currentTime,
      o = this.context.createOscillator(),
      g = this.context.createGain();
    o.type = type;
    o.frequency.setValueAtTime(freq, t);
    if (slide) o.frequency.exponentialRampToValueAtTime(Math.max(20, freq + slide), t + duration);
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(gain, t + 0.006);
    g.gain.exponentialRampToValueAtTime(0.001, t + duration);
    o.connect(g);
    g.connect(this.master);
    o.start(t);
    o.stop(t + duration + 0.02);
  }
  noise(duration = 0.1, gain = 0.5, lowpass = 1800) {
    if (!this.context || this.context.state !== 'running') return;
    const ctx = this.context,
      buffer = ctx.createBuffer(1, Math.ceil(ctx.sampleRate * duration), ctx.sampleRate),
      data = buffer.getChannelData(0);
    for (let i = 0; i < data.length; i++) data[i] = (Math.random() * 2 - 1) * (1 - i / data.length);
    const src = ctx.createBufferSource();
    src.buffer = buffer;
    const filter = ctx.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.value = lowpass;
    const vol = ctx.createGain();
    vol.gain.value = gain;
    src.connect(filter);
    filter.connect(vol);
    vol.connect(this.master);
    src.start();
  }
  play(name) {
    switch (name) {
      case 'mine':
        this.noise(0.09, 0.48, 2200);
        this.tone(150, 0.055, 'triangle', 0.5, -60);
        break;
      case 'place':
        this.tone(140, 0.085, 'triangle', 0.9, -55);
        this.noise(0.05, 0.25, 800);
        break;
      case 'click':
        this.tone(520, 0.055, 'sine', 0.4, 120);
        break;
      case 'craft':
        this.tone(520, 0.15, 'sine', 0.4, 180);
        setTimeout(() => this.tone(880, 0.25, 'sine', 0.3), 90);
        break;
      case 'hurt':
        this.tone(180, 0.2, 'sawtooth', 0.3, -100);
        this.noise(0.13, 0.25, 900);
        break;
      case 'eat':
        this.noise(0.1, 0.4, 2200);
        setTimeout(() => this.noise(0.08, 0.3, 1800), 130);
        break;
      case 'step':
        this.noise(0.065, 0.24, 750);
        this.tone(85, 0.045, 'sine', 0.35, -20);
        break;
      case 'water':
        this.noise(0.16, 0.15, 2800);
        break;
    }
  }
  ambient(time, day) {
    if (!this.context || time < this.nextBird || !day) return;
    this.nextBird = time + 6 + Math.random() * 10;
    this.tone(2100, 0.1, 'sine', 0.07, 800);
    setTimeout(() => this.tone(2500, 0.12, 'sine', 0.05, 600), 140);
  }
}
