import * as THREE from 'three';
import { ITEMS, BLOCKS, tileFor } from './blocks.js';

export const TILE = 16,
  PAD = 2,
  CELL = 20,
  COLS = 8,
  ROWS = 4;
const palettes = [
  '#81a84b',
  '#906444',
  '#956b49',
  '#909b94',
  '#dece99',
  '#805b38',
  '#b98d50',
  '#527f3c',
  '#c39a61',
  '#86928a',
  '#b5d8d7',
  '#a96c55',
  '#589cac',
  '#68756e',
  '#8c9890',
  '#758f8a',
  '#e6ece4',
  '#b1854d',
  '#ae814f',
  '#ffcf78',
  '#465453',
  '#b39d87',
  '#eee4cc',
  '#3e484b',
  '#87918d',
];
export function createAtlas() {
  const canvas = document.createElement('canvas');
  canvas.width = COLS * CELL;
  canvas.height = ROWS * CELL;
  const ctx = canvas.getContext('2d');
  let rng = 81743;
  const random = () => {
    rng ^= rng << 13;
    rng ^= rng >>> 17;
    rng ^= rng << 5;
    return (rng >>> 0) / 4294967296;
  };
  for (let t = 0; t < palettes.length; t++) {
    const tx = (t % COLS) * CELL + PAD,
      ty = Math.floor(t / COLS) * CELL + PAD;
    ctx.fillStyle = palettes[t];
    ctx.fillRect(tx, ty, 16, 16);
    for (let y = 0; y < 16; y++)
      for (let x = 0; x < 16; x++) {
        const shade = Math.floor((random() - 0.5) * 32);
        ctx.fillStyle =
          shade > 0 ? `rgba(255,255,240,${shade / 140})` : `rgba(24,34,25,${-shade / 80})`;
        ctx.fillRect(tx + x, ty + y, 1, 1);
      }
    const rect = (x, y, w, h, color) => {
      ctx.fillStyle = color;
      ctx.fillRect(tx + x, ty + y, w, h);
    };
    if (t === 0) {
      for (let n = 0; n < 15; n++)
        rect(Math.floor(random() * 15), Math.floor(random() * 15), 1, 2, '#90b557');
    }
    if (t === 1) {
      rect(0, 0, 16, 3, '#81a84b');
      for (let x = 0; x < 16; x++) rect(x, 3, 1, Math.floor(random() * 4), '#729743');
    }
    if (t === 5) {
      for (let x = 1; x < 16; x += 4) {
        rect(x, 0, 1, 16, '#5d462e');
        rect(x + 1, Math.floor(random() * 8), 1, 7, '#a07845');
      }
    }
    if (t === 6) {
      for (let n = 2; n < 8; n += 2) {
        ctx.strokeStyle = '#856338';
        ctx.lineWidth = 1;
        ctx.strokeRect(tx + n + 0.5, ty + n + 0.5, 15 - n * 2, 15 - n * 2);
      }
    }
    if (t === 7) {
      for (let n = 0; n < 26; n++)
        rect(
          Math.floor(random() * 14),
          Math.floor(random() * 14),
          2,
          2,
          random() > 0.5 ? '#679749' : '#3e6d36'
        );
    }
    if (t === 8 || t === 17 || t === 18) {
      for (let y = 3; y < 16; y += 4) rect(0, y, 16, 1, '#997240');
      rect(6, 0, 1, 3, '#a77d45');
      rect(12, 4, 1, 3, '#a77d45');
      rect(4, 8, 1, 3, '#a77d45');
    }
    if (t === 9 || t === 11 || t === 20) {
      for (let y = 3; y < 16; y += 4) {
        rect(0, y, 16, 1, t === 11 ? '#d2b49a' : '#5e6e65');
        const off = y % 8 === 3 ? 0 : 4;
        for (let x = off; x < 16; x += 8) rect(x, y - 3, 1, 3, t === 11 ? '#d2b49a' : '#5e6e65');
      }
    }
    if (t === 10) {
      ctx.clearRect(tx, ty, 16, 16);
      rect(0, 0, 16, 16, 'rgba(175,221,224,.18)');
      rect(0, 0, 16, 1, '#cfeded');
      rect(0, 0, 1, 16, '#cfeded');
      rect(15, 0, 1, 16, '#6da3a4');
      rect(0, 15, 16, 1, '#6da3a4');
      for (let x = 3; x < 10; x++) rect(x, 13 - x, 2, 1, '#dfefec');
    }
    if (t === 12) {
      for (let n = 0; n < 10; n++)
        rect(Math.floor(random() * 13), Math.floor(random() * 16), 3, 1, '#82bec7');
    }
    if (t >= 13 && t <= 15) {
      for (let n = 0; n < 7; n++) {
        const x = 1 + Math.floor(random() * 12),
          y = 1 + Math.floor(random() * 12);
        rect(x, y, 3, 2, ['#303c38', '#cbaf91', '#79d8d8'][t - 13]);
        rect(x, y, 1, 1, ['#53615a', '#e0c9ad', '#c0f6e5'][t - 13]);
      }
    }
    if (t === 17) {
      rect(2, 2, 12, 12, '#8c623b');
      for (let n = 3; n < 14; n += 4) {
        rect(n, 2, 1, 12, '#bd9359');
        rect(2, n, 12, 1, '#bd9359');
      }
    }
    if (t === 18) {
      rect(2, 4, 4, 7, '#614831');
      rect(9, 4, 4, 7, '#614831');
      rect(3, 5, 1, 4, '#d1ad70');
    }
    if (t === 19) {
      rect(0, 0, 16, 2, '#685746');
      rect(0, 14, 16, 2, '#685746');
      rect(0, 0, 2, 16, '#685746');
      rect(14, 0, 2, 16, '#685746');
      rect(5, 4, 6, 8, '#fff0b8');
    }
    if (t === 22) {
      for (let y = 0; y < 16; y += 3)
        for (let x = y % 2; x < 16; x += 3) rect(x, y, 1, 1, '#d7cbb2');
    }
    if (t === 24) {
      rect(3, 3, 10, 4, '#252c2a');
      rect(2, 10, 12, 4, '#262c29');
      rect(4, 11, 8, 2, '#101714');
      rect(3, 8, 10, 1, '#b1bbb5');
    }
    for (let p = 1; p <= PAD; p++) {
      ctx.drawImage(canvas, tx, ty, 16, 1, tx, ty - p, 16, 1);
      ctx.drawImage(canvas, tx, ty + 15, 16, 1, tx, ty + 15 + p, 16, 1);
      ctx.drawImage(canvas, tx, ty - PAD, 1, CELL, tx - p, ty - PAD, 1, CELL);
      ctx.drawImage(canvas, tx + 15, ty - PAD, 1, CELL, tx + 15 + p, ty - PAD, 1, CELL);
    }
  }
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.magFilter = THREE.NearestFilter;
  texture.minFilter = THREE.NearestFilter;
  texture.generateMipmaps = false;
  return { canvas, texture };
}
export function uvFor(tile) {
  const x = (tile % COLS) * CELL + PAD,
    y = Math.floor(tile / COLS) * CELL + PAD;
  return [
    x / (COLS * CELL),
    1 - (y + TILE) / (ROWS * CELL),
    (x + TILE) / (COLS * CELL),
    1 - y / (ROWS * CELL),
  ];
}

export function makeIcons(atlas) {
  const icons = {};
  for (const [key, item] of Object.entries(ITEMS)) {
    if (key === '0') continue;
    const canvas = document.createElement('canvas');
    canvas.width = 64;
    canvas.height = 64;
    const c = canvas.getContext('2d');
    c.imageSmoothingEnabled = false;
    const drawTile = (t, transform, shade = 0) => {
      c.save();
      c.setTransform(...transform);
      c.drawImage(
        atlas,
        (t % COLS) * CELL + PAD,
        Math.floor(t / COLS) * CELL + PAD,
        16,
        16,
        0,
        0,
        16,
        16
      );
      if (shade) {
        c.fillStyle = `rgba(18,27,27,${shade})`;
        c.fillRect(0, 0, 16, 16);
      }
      c.restore();
    };
    if (BLOCKS[key] && !BLOCKS[key].model) {
      drawTile(tileFor(key, 2), [1.5, 0.75, -1.5, 0.75, 32, 5]);
      drawTile(tileFor(key, 5), [1.5, 0.75, 0, 1.65, 8, 17], 0.16);
      drawTile(tileFor(key, 0), [1.5, -0.75, 0, 1.65, 32, 29], 0.3);
    } else {
      const square = (x, y, w, h, color) => {
        c.fillStyle = color;
        c.fillRect(x, y, w, h);
      };
      c.save();
      if (item.tool) {
        c.translate(32, 32);
        c.rotate(0.62);
        square(-3, -18, 6, 43, '#583f2b');
        square(-2, -18, 4, 42, '#b38850');
        if (Number(key) === 103) {
          square(-4, -30, 8, 31, '#d7e5e1');
          square(-1, -30, 3, 31, '#ffffff');
          square(-11, 1, 22, 5, '#816c42');
        } else {
          square(-19, -20, 38, 8, item.color);
          square(-23, -16, 7, 12, item.color);
          square(16, -16, 7, 10, item.color);
          square(-17, -21, 33, 3, '#e6e0bd');
        }
      } else if (item.armor !== undefined) {
        const color = item.color;
        if (item.armor === 0) {
          square(13, 13, 38, 32, color);
          square(21, 29, 22, 18, '#66766f');
          square(13, 13, 38, 6, '#f0f3ec');
        } else if (item.armor === 1) {
          square(11, 12, 42, 15, color);
          square(20, 20, 24, 32, color);
          square(25, 11, 14, 9, '#62726b');
          square(23, 27, 5, 22, '#f0f3ec');
        } else if (item.armor === 2) {
          square(16, 12, 32, 15, color);
          square(16, 24, 12, 29, color);
          square(36, 24, 12, 29, color);
          square(16, 12, 32, 4, '#f0f3ec');
        } else {
          square(13, 22, 13, 27, color);
          square(9, 43, 17, 10, color);
          square(38, 22, 13, 27, color);
          square(34, 43, 17, 10, color);
        }
      } else if (item.shield) {
        square(13, 10, 38, 38, '#78827b');
        square(19, 47, 26, 6, '#78827b');
        square(18, 15, 28, 30, '#b99a68');
        square(23, 45, 16, 5, '#b99a68');
        square(29, 15, 4, 31, '#8c683f');
      } else if (Number(key) === 24) {
        square(28, 22, 9, 33, '#986f42');
        square(25, 12, 15, 15, '#eaa03d');
        square(29, 8, 7, 16, '#ffe7a1');
      } else if (Number(key) === 90) {
        c.translate(32, 32);
        c.rotate(0.6);
        square(-3, -22, 7, 44, '#92683d');
        square(-3, -22, 2, 44, '#c6a16c');
      } else if (Number(key) === 93) {
        square(27, 7, 5, 15, '#769747');
        square(32, 8, 12, 5, '#769747');
        for (const [x, y] of [
          [15, 23],
          [30, 19],
          [37, 32],
          [20, 37],
        ]) {
          square(x, y, 13, 13, '#9f435c');
          square(x + 2, y + 2, 6, 4, '#df8793');
        }
      } else if (Number(key) === 21) {
        square(12, 41, 40, 8, '#86603b');
        square(19, 48, 31, 5, '#60452e');
        square(21, 24, 22, 18, '#ea913c');
        square(26, 12, 12, 26, '#ffcb66');
        square(29, 26, 8, 17, '#fff0b1');
      } else if (Number(key) === 17) {
        square(25, 8, 15, 7, '#745c43');
        square(16, 18, 33, 34, '#654e38');
        square(21, 24, 23, 21, '#ffca67');
        square(28, 27, 10, 15, '#fff0b4');
        square(14, 16, 37, 6, '#9e8460');
        square(14, 50, 37, 6, '#9e8460');
      } else if (Number(key) === 92) {
        square(10, 30, 43, 17, '#91a39b');
        square(16, 22, 32, 15, '#d7e0d7');
        square(16, 22, 32, 5, '#f0f1db');
      } else {
        c.translate(32, 32);
        c.rotate(Math.PI / 4);
        square(-14, -14, 28, 28, item.color);
        square(-12, -12, 10, 24, Number(key) === 94 ? '#c0f5eb' : '#62716b');
      }
      c.restore();
    }
    icons[key] = canvas.toDataURL();
  }
  return icons;
}
