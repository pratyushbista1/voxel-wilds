import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { hash, SEA } from './world.js';
import { B, isSolid } from './blocks.js';

export class Assets {
  constructor() {
    this.models = new Map();
    this.loaded = [];
    const torch = new THREE.Group();
    const stick = new THREE.Mesh(
      new THREE.BoxGeometry(0.12, 0.58, 0.12),
      new THREE.MeshLambertMaterial({ color: 0x9d7444 })
    );
    stick.position.y = 0.3;
    const flame = new THREE.Mesh(
      new THREE.BoxGeometry(0.16, 0.19, 0.16),
      new THREE.MeshBasicMaterial({ color: 0xffd372 })
    );
    flame.position.y = 0.63;
    flame.name = 'flame';
    torch.add(stick, flame);
    this.models.set('torch', torch);
    this.portalMaterial = new THREE.ShaderMaterial({
      uniforms: { time: { value: 0 } },
      transparent: true,
      side: THREE.DoubleSide,
      depthWrite: false,
      vertexShader:
        'varying vec2 uvPos; void main(){uvPos=uv;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.0);}',
      fragmentShader:
        'uniform float time; varying vec2 uvPos; void main(){vec2 p=uvPos*12.0;float wave=sin(p.x+sin(p.y+time)*2.0+time)+cos(p.y*1.4-time);vec3 c=mix(vec3(.19,.03,.34),vec3(.78,.25,.98),.5+.25*wave);gl_FragColor=vec4(c,.78);}',
    });
    for (const [name, rotation] of [
      ['portal_x', 0],
      ['portal_z', Math.PI / 2],
    ]) {
      const plane = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), this.portalMaterial);
      plane.position.y = 0.5;
      plane.rotation.y = rotation;
      const root = new THREE.Group();
      root.add(plane);
      this.models.set(name, root);
    }
    const wart = new THREE.Group();
    for (const [x, z] of [
      [-0.2, -0.2],
      [0.2, 0],
      [0, 0.2],
    ]) {
      const cap = new THREE.Mesh(
        new THREE.BoxGeometry(0.3, 0.25, 0.3),
        new THREE.MeshLambertMaterial({ color: '#aa263b' })
      );
      cap.position.set(x, 0.17, z);
      wart.add(cap);
    }
    this.models.set('wart', wart);
    const spawner = new THREE.Group(),
      cageMaterial = new THREE.MeshLambertMaterial({ color: '#252633' });
    for (let axis = 0; axis < 3; axis++)
      for (const a of [-0.45, 0, 0.45])
        for (const b of [-0.45, 0.45]) {
          const size = [0.04, 0.04, 0.04],
            pos = [0, 0, 0];
          size[axis] = 0.94;
          pos[(axis + 1) % 3] = a;
          pos[(axis + 2) % 3] = b;
          const bar = new THREE.Mesh(new THREE.BoxGeometry(...size), cageMaterial);
          bar.position.set(pos[0], pos[1] + 0.5, pos[2]);
          spawner.add(bar);
        }
    const core = new THREE.Mesh(
      new THREE.BoxGeometry(0.25, 0.25, 0.25),
      new THREE.MeshBasicMaterial({ color: '#ffbe43' })
    );
    core.position.y = 0.5;
    spawner.add(core);
    this.models.set('spawner', spawner);
  }
  async load(onProgress = () => {}) {
    const loader = new GLTFLoader(),
      names = [
        'sheep',
        'fox',
        'sentinel',
        'campfire',
        'lantern',
        'pickaxe',
        'sword',
        'zombie',
        'skeleton',
        'piglin',
        'cow',
        'pig',
        'chicken',
        'creeper',
        'spider',
        'blaze',
        'bed_head',
        'bed_foot',
        'chest',
      ];
    await Promise.all(
      names.map(async (name) => {
        const gltf = await loader.loadAsync(`/assets/models/${name}.glb`);
        gltf.scene.traverse((o) => {
          if (o.isMesh) {
            o.castShadow = true;
            o.receiveShadow = true;
          }
        });
        this.models.set(name, gltf.scene);
        this.loaded.push(name);
        onProgress(this.loaded.length / names.length, name);
      })
    );
  }
  make(name) {
    return this.models.get(name)?.clone(true) || null;
  }
}

export class Atmosphere {
  constructor(scene, world) {
    this.scene = scene;
    this.world = world;
    this.day = 1;
    this.lightLevel = 1;
    scene.background = new THREE.Color(0xa6c9cf);
    scene.fog = new THREE.Fog(0xa6c9cf, 65, 120);
    this.hemi = new THREE.HemisphereLight(0xe4f3ea, 0x65724a, 2.3);
    scene.add(this.hemi);
    this.sunLight = new THREE.DirectionalLight(0xffe2a7, 2.5);
    this.sunLight.castShadow = true;
    this.sunLight.shadow.mapSize.set(2048, 2048);
    Object.assign(this.sunLight.shadow.camera, {
      left: -38,
      right: 38,
      top: 38,
      bottom: -38,
      near: 1,
      far: 150,
    });
    this.sunLight.shadow.bias = -0.00035;
    this.sunLight.shadow.normalBias = 0.035;
    this.sunLight.shadow.radius = 2;
    scene.add(this.sunLight, this.sunLight.target);
    this.sun = new THREE.Mesh(
      new THREE.BoxGeometry(6, 6, 0.25),
      new THREE.MeshBasicMaterial({ color: 0xffedb1, fog: false })
    );
    scene.add(this.sun);
    this.moon = new THREE.Mesh(
      new THREE.BoxGeometry(4, 4, 0.2),
      new THREE.MeshBasicMaterial({ color: 0xc6e6e4, fog: false })
    );
    scene.add(this.moon);
    this.cloudGroup = new THREE.Group();
    scene.add(this.cloudGroup);
    this.cloudMaterial = new THREE.MeshLambertMaterial({
      color: 0xf0f0d9,
      transparent: true,
      opacity: 0.85,
      depthWrite: false,
    });
    const cloudGeo = new THREE.BoxGeometry(1, 1, 1);
    const clouds = new THREE.InstancedMesh(cloudGeo, this.cloudMaterial, 75);
    const dummy = new THREE.Object3D();
    for (let i = 0; i < 75; i++) {
      const cluster = Math.floor(i / 3),
        part = i % 3;
      dummy.position.set(
        (hash(cluster, 1, 33) - 0.5) * 270 + part * 7,
        61 + hash(cluster, 3, 34) * 12,
        (hash(cluster, 2, 32) - 0.5) * 270
      );
      dummy.scale.set(9 + hash(i, 6, 64) * 10, 2 + hash(i, 3, 64) * 2, 6 + hash(i, 5, 44) * 8);
      dummy.updateMatrix();
      clouds.setMatrixAt(i, dummy.matrix);
    }
    this.cloudGroup.add(clouds);
    const starsGeo = new THREE.BufferGeometry(),
      positions = [];
    for (let i = 0; i < 250; i++) {
      const a = hash(i, 0, 511) * Math.PI * 2,
        e = 0.15 + hash(i, 2, 519) * 1.3;
      positions.push(
        Math.cos(a) * Math.cos(e) * 130,
        Math.sin(e) * 130,
        Math.sin(a) * Math.cos(e) * 130
      );
    }
    starsGeo.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    this.stars = new THREE.Points(
      starsGeo,
      new THREE.PointsMaterial({
        color: 0xd3e8e4,
        size: 0.4,
        fog: false,
        transparent: true,
        opacity: 0,
      })
    );
    scene.add(this.stars);
    this.nightColor = new THREE.Color(0x132b42);
    this.dayColor = new THREE.Color(0xabcbd0);
    this.duskColor = new THREE.Color(0xc9ae93);
    this.green = new THREE.Color(0x63764b);
    this.darkGround = new THREE.Color(0x202f35);
    this.flowers = new THREE.Group();
    scene.add(this.flowers);
    if (world.dimension !== 'nether') this.populateFlowers();
  }
  populateFlowers() {
    const geometry = new THREE.PlaneGeometry(0.55, 0.55);
    geometry.translate(0, 0.275, 0);
    const grassCanvas = document.createElement('canvas');
    grassCanvas.width = 16;
    grassCanvas.height = 24;
    const brush = grassCanvas.getContext('2d');
    for (const [points, color] of [
      [
        [
          [7, 24],
          [6, 15],
          [3, 12],
          [2, 6],
          [5, 9],
          [8, 15],
          [9, 24],
        ],
        '#7c9f4c',
      ],
      [
        [
          [8, 24],
          [8, 9],
          [10, 2],
          [11, 0],
          [10, 12],
          [11, 24],
        ],
        '#94b65c',
      ],
      [
        [
          [10, 24],
          [11, 16],
          [13, 11],
          [15, 9],
          [14, 15],
          [12, 24],
        ],
        '#698942',
      ],
      [
        [
          [5, 24],
          [3, 19],
          [1, 18],
          [0, 14],
          [4, 16],
          [7, 24],
        ],
        '#88a64d',
      ],
    ]) {
      brush.fillStyle = color;
      brush.beginPath();
      points.forEach(([x, y], i) => (i ? brush.lineTo(x, y) : brush.moveTo(x, y)));
      brush.closePath();
      brush.fill();
    }
    this.grassTexture = new THREE.CanvasTexture(grassCanvas);
    this.grassTexture.colorSpace = THREE.SRGBColorSpace;
    this.grassTexture.magFilter = THREE.NearestFilter;
    this.grassTexture.minFilter = THREE.NearestFilter;
    const mat = new THREE.MeshLambertMaterial({
      map: this.grassTexture,
      alphaTest: 0.5,
      side: THREE.DoubleSide,
    });
    const petals = [0xf1df92, 0xe7d5c2, 0xca91a3].map(
      (c) => new THREE.MeshLambertMaterial({ color: c, side: THREE.DoubleSide })
    );
    const dummy = new THREE.Object3D(),
      grassData = [],
      flowerData = [[], [], []];
    for (let i = 0; i < 850; i++) {
      const x = Math.floor((hash(i, 22, 73) - 0.5) * 122),
        z = Math.floor((hash(i, 23, 31) - 0.5) * 122),
        y = this.world.surface(x, z);
      if (this.world.get(x, y - 1, z) !== B.GRASS || this.world.get(x, y, z) !== 0) continue;
      grassData.push({ x: x + 0.5, y, z: z + 0.5, a: hash(i, 21, 4) * Math.PI });
      if (i % 5 === 0) flowerData[i % 3].push({ x: x + 0.5, y: y + 0.43, z: z + 0.5 });
    }
    const grass = new THREE.InstancedMesh(geometry, mat, grassData.length * 2);
    let n = 0;
    for (const p of grassData)
      for (let side = 0; side < 2; side++) {
        dummy.position.set(p.x, p.y, p.z);
        dummy.rotation.set(0, p.a + (side * Math.PI) / 2, 0);
        dummy.scale.set(1, 1, 1);
        dummy.updateMatrix();
        grass.setMatrixAt(n++, dummy.matrix);
      }
    grass.receiveShadow = true;
    this.flowers.add(grass);
    for (let color = 0; color < 3; color++) {
      const geo = new THREE.BoxGeometry(0.22, 0.11, 0.22),
        mesh = new THREE.InstancedMesh(geo, petals[color], flowerData[color].length);
      flowerData[color].forEach((p, i) => {
        dummy.position.set(p.x, p.y, p.z);
        dummy.rotation.set(0, 0, 0);
        dummy.updateMatrix();
        mesh.setMatrixAt(i, dummy.matrix);
      });
      this.flowers.add(mesh);
    }
    this.flowerPositions = grassData;
    this.petalPositions = flowerData;
  }
  update(time, player, dt, radius = 5, menu = false) {
    if (this.world.dimension === 'nether') {
      this.lightLevel = 0.25;
      this.scene.background.set('#351718');
      this.scene.fog.color.set('#4a2020');
      this.scene.fog.near = radius * 5;
      this.scene.fog.far = radius * 16;
      this.hemi.color.set('#ef9b72');
      this.hemi.groundColor.set('#733229');
      this.hemi.intensity = 1.65;
      this.sunLight.intensity = 0.45;
      this.sunLight.color.set('#ffa475');
      this.sunLight.position.set(player.x + 20, player.y + 20, player.z - 20);
      this.sunLight.target.position.set(player.x, player.y, player.z);
      this.sun.visible =
        this.moon.visible =
        this.cloudGroup.visible =
        this.stars.visible =
        this.flowers.visible =
          false;
      return;
    }
    const fraction = (time % 1200) / 1200,
      angle = fraction * Math.PI * 2 - Math.PI / 2;
    const altitude = Math.sin(angle),
      daylight = THREE.MathUtils.smoothstep(altitude, -0.22, 0.25);
    this.lightLevel = daylight;
    const color = this.nightColor.clone().lerp(this.dayColor, daylight);
    const dusk = Math.max(0, 1 - Math.abs(altitude) / 0.26) * 0.4;
    color.lerp(this.duskColor, dusk);
    this.scene.background.copy(color);
    this.scene.fog.color.copy(color);
    this.scene.fog.near = radius * 16 * 0.64;
    this.scene.fog.far = radius * 16 * 1.15;
    this.hemi.intensity = 0.48 + daylight * 1.55;
    this.hemi.color.set(0xd7e9e6);
    this.hemi.groundColor.copy(this.darkGround).lerp(this.green, daylight);
    const lightAngle = altitude > 0 ? angle : angle + Math.PI;
    this.sunLight.position.set(
      player.x + Math.cos(lightAngle) * 55,
      player.y + Math.abs(Math.sin(lightAngle)) * 65 + 12,
      player.z - 38
    );
    this.sunLight.target.position.set(player.x, player.y - 5, player.z);
    this.sunLight.target.updateMatrixWorld();
    this.sunLight.intensity = 0.2 + daylight * 2.0;
    this.sunLight.color.set(daylight > 0.4 ? 0xffe7b6 : 0x9dcad9);
    this.sun.position.set(
      player.x + Math.cos(angle) * 105,
      Math.sin(angle) * 105 + player.y,
      player.z - 55
    );
    this.sun.lookAt(player.x, player.y, player.z);
    this.sun.visible = altitude > -0.08;
    this.moon.position.set(
      player.x - Math.cos(angle) * 105,
      -Math.sin(angle) * 105 + player.y,
      player.z + 55
    );
    this.moon.lookAt(player.x, player.y, player.z);
    this.moon.visible = altitude < 0.08;
    this.cloudGroup.position.set(
      Math.floor(player.x / 180) * 180 + Math.sin(time / 250) * 13,
      0,
      Math.floor(player.z / 180) * 180 + ((time * 0.006) % 30)
    );
    this.cloudMaterial.opacity = 0.54 + daylight * 0.3;
    this.stars.position.set(player.x, player.y, player.z);
    this.stars.material.opacity = (1 - daylight) * 0.9;
  }
  dispose() {
    for (const o of [
      this.hemi,
      this.sunLight,
      this.sunLight.target,
      this.sun,
      this.moon,
      this.cloudGroup,
      this.stars,
      this.flowers,
    ]) {
      this.scene.remove(o);
      o.traverse((child) => {
        if (child.geometry) child.geometry.dispose();
        if (child.material) {
          for (const m of Array.isArray(child.material) ? child.material : [child.material])
            m.dispose();
        }
      });
    }
    this.grassTexture?.dispose();
    this.sunLight.shadow.dispose();
  }
}

export class Creatures {
  constructor(scene, world, assets) {
    this.scene = scene;
    this.world = world;
    this.assets = assets;
    this.list = [];
    this.clock = 0;
    this.nightSpawned = false;
    this.spawnTimer = 0;
  }
  spawn(kind, x, z, y = this.world.surface(Math.floor(x), Math.floor(z))) {
    if (y < 1 || y > 69) return;
    const mesh = this.assets.make(kind);
    if (!mesh) return;
    const legs = [],
      arms = [];
    mesh.traverse((o) => {
      if (!o.isMesh && o.name.startsWith('leg_')) legs.push(o);
      if (!o.isMesh && o.name.startsWith('arm_')) arms.push(o);
    });
    mesh.position.set(x, y, z);
    mesh.rotation.y = hash(Math.floor(x), Math.floor(z), 137) * Math.PI * 2;
    this.scene.add(mesh);
    const head = mesh.getObjectByName('head');
    let headRig = null;
    if (head) {
      headRig = new THREE.Group();
      headRig.position.copy(head.position);
      head.parent.add(headRig);
      const parts = [];
      const names =
        /^(head|fringe|muzzle|ear|eye|glint|brow|nose|snout|jaw|mouth|tusk|horn|comb)([_.]|$)/;
      mesh.traverse((o) => {
        if (o.isMesh && names.test(o.name)) parts.push(o);
      });
      mesh.updateMatrixWorld(true);
      for (const part of parts) headRig.attach(part);
    }
    const creature = {
      mesh,
      kind,
      legs,
      arms,
      headRig,
      x,
      z,
      y,
      yaw: mesh.rotation.y,
      timer: 2,
      walking: true,
      walkBlend: 0,
      walkPhase: 0,
      health: kind === 'sentinel' ? 20 : 8,
      cooldown: 0,
      phase: hash(Math.floor(x), 3, 2) * 20,
      home: { x, z },
    };
    this.list.push(creature);
    return creature;
  }
  populate() {
    for (const [kind, x, z] of [
      ['sheep', -7, 0],
      ['sheep', -11, -4],
      ['fox', 1, 8],
      ['sheep', -6, -13],
      ['fox', -20, 7],
      ['sheep', -22, -14],
      ['sheep', -8, 19],
      ['fox', -23, 25],
    ])
      this.spawn(kind, x, z);
  }
  update(dt, player, night, survival, onAttack) {
    this.clock += dt;
    this.spawnTimer += dt;
    if (
      survival &&
      night &&
      this.spawnTimer > 12 &&
      this.list.filter((m) => m.kind === 'sentinel').length < 4
    ) {
      this.spawnTimer = 0;
      const a = Math.random() * Math.PI * 2;
      this.spawn('sentinel', player.x + Math.sin(a) * 19, player.z + Math.cos(a) * 19);
    }
    if (this.spawnTimer > 35 && !night) {
      this.spawnTimer = 0;
      if (this.list.filter((m) => m.kind !== 'sentinel').length < 10) {
        const a = Math.random() * Math.PI * 2;
        this.spawn(
          Math.random() > 0.4 ? 'sheep' : 'fox',
          player.x + Math.sin(a) * 24,
          player.z + Math.cos(a) * 24
        );
      }
    }
    for (const m of [...this.list]) {
      const distance = Math.hypot(m.x - player.x, m.z - player.z);
      if (distance > 100 || (m.kind === 'sentinel' && !night)) {
        this.remove(m);
        continue;
      }
      if (distance > 58) continue;
      m.timer -= dt;
      m.cooldown -= dt;
      if (m.kind === 'sentinel' && survival && distance < 30) {
        m.yaw = Math.atan2(player.x - m.x, player.z - m.z);
        m.walking = distance > 1.4;
        if (distance < 1.9 && Math.abs(player.y - m.y) < 2.2 && m.cooldown <= 0) {
          m.cooldown = 1.6;
          onAttack(3, 'A stone sentinel caught you.');
        }
      } else if (m.timer <= 0) {
        m.timer = 2 + Math.random() * 4;
        m.walking = Math.random() > 0.28;
        m.yaw += (Math.random() - 0.5) * 1.8;
        if (Math.hypot(m.x - m.home.x, m.z - m.home.z) > 12)
          m.yaw = Math.atan2(m.home.x - m.x, m.home.z - m.z);
      }
      const speed = m.kind === 'sentinel' ? 1.75 : m.kind === 'fox' ? 1.15 : 0.65;
      if (m.walking) {
        const nx = m.x + Math.sin(m.yaw) * speed * dt,
          nz = m.z + Math.cos(m.yaw) * speed * dt;
        const sy = this.world.surface(Math.floor(nx), Math.floor(nz));
        if (
          Math.abs(sy - m.y) <= 1.2 &&
          sy > SEA &&
          this.world.get(nx, sy, nz) !== B.WATER &&
          !isSolid(this.world.get(nx, sy, nz)) &&
          !isSolid(this.world.get(nx, sy + 1, nz))
        ) {
          m.x = nx;
          m.z = nz;
          m.y = sy;
        } else {
          m.yaw += 1.4;
          m.timer = 0.6;
        }
      }
      const dy = m.y - m.mesh.position.y;
      m.mesh.position.set(m.x, m.mesh.position.y + dy * Math.min(1, dt * 12), m.z);
      const angle = Math.atan2(
        Math.sin(m.yaw - m.mesh.rotation.y),
        Math.cos(m.yaw - m.mesh.rotation.y)
      );
      m.mesh.rotation.y += angle * (1 - Math.exp(-dt * 8));
      m.walkBlend = THREE.MathUtils.damp(m.walkBlend, m.walking ? 1 : 0, 7, dt);
      m.walkPhase += dt * speed * 8 * m.walkBlend;
      if (m.headRig) {
        const look =
          distance < 7
            ? Math.atan2(player.x - m.x, player.z - m.z) - m.mesh.rotation.y
            : Math.sin(this.clock * 0.7 + m.phase) * 0.2;
        m.headRig.rotation.y = THREE.MathUtils.damp(
          m.headRig.rotation.y,
          THREE.MathUtils.clamp(Math.atan2(Math.sin(look), Math.cos(look)), -0.45, 0.45),
          5,
          dt
        );
        m.headRig.rotation.x = Math.sin(this.clock * 1.3 + m.phase) * 0.025;
      }
      m.legs.forEach((leg, i) => {
        const phase = m.legs.length === 4 ? [0, 1, 1, 0][i] : i % 2;
        leg.rotation.x = Math.sin(m.walkPhase + m.phase + phase * Math.PI) * 0.45 * m.walkBlend;
      });
      m.arms.forEach((arm, i) => {
        arm.rotation.x = Math.sin(m.walkPhase + m.phase + (i % 2) * Math.PI) * 0.32 * m.walkBlend;
      });
      if (m.kind === 'fox') {
        const tail = m.mesh.getObjectByName('tail');
        if (tail) tail.rotation.y = Math.sin(this.clock * 3 + m.phase) * 0.13;
      }
    }
  }
  hit(origin, direction, reach = 4) {
    let nearest = null,
      best = reach;
    for (const m of this.list) {
      const v = new THREE.Vector3(
          m.x - origin.x,
          m.mesh.position.y + (m.kind === 'sentinel' ? 1.1 : 0.65) - origin.y,
          m.z - origin.z
        ),
        t = v.dot(direction);
      if (t < 0 || t > best) continue;
      const radius = m.kind === 'sentinel' ? 0.85 : 0.72;
      if (v.lengthSq() - t * t < radius * radius) {
        nearest = m;
        best = t;
      }
    }
    return nearest;
  }
  remove(m) {
    this.scene.remove(m.mesh);
    this.list.splice(this.list.indexOf(m), 1);
  }
  dispose() {
    for (const m of [...this.list]) this.remove(m);
  }
}

export class Particles {
  constructor(scene) {
    this.scene = scene;
    this.list = [];
    this.geo = new THREE.BoxGeometry(0.1, 0.1, 0.1);
    this.materials = new Map();
  }
  burst(x, y, z, color, count = 12) {
    count = Math.min(count, this.maxCount ?? 180);
    if (!this.materials.has(color))
      this.materials.set(color, new THREE.MeshLambertMaterial({ color }));
    for (let n = 0; n < count; n++) {
      const mesh = new THREE.Mesh(this.geo, this.materials.get(color));
      mesh.position.set(
        x + Math.random() * 0.7 - 0.35,
        y + Math.random() * 0.7 - 0.35,
        z + Math.random() * 0.7 - 0.35
      );
      mesh.scale.setScalar(0.6 + Math.random() * 0.7);
      this.scene.add(mesh);
      this.list.push({
        mesh,
        v: new THREE.Vector3(
          (Math.random() - 0.5) * 3,
          1.5 + Math.random() * 3,
          (Math.random() - 0.5) * 3
        ),
        life: 0.4 + Math.random() * 0.5,
      });
    }
    while (this.list.length > (this.maxCount ?? 180)) this.remove(this.list[0]);
  }
  update(dt) {
    for (const p of [...this.list]) {
      p.life -= dt;
      p.v.y -= 10 * dt;
      p.mesh.position.addScaledVector(p.v, dt);
      p.mesh.rotation.x += dt * 3;
      p.mesh.rotation.z += dt * 2;
      if (p.life < 0.2) p.mesh.scale.multiplyScalar(0.85);
      if (p.life <= 0) this.remove(p);
    }
  }
  remove(p) {
    this.scene.remove(p.mesh);
    this.list.splice(this.list.indexOf(p), 1);
  }
  dispose() {
    for (const p of [...this.list]) this.remove(p);
    this.geo.dispose();
    for (const mat of this.materials.values()) mat.dispose();
  }
}
