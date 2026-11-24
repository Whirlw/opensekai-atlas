// Draws the sample covers used by the style guide screenshots and promo recordings: one SVG per song,
// rasterized to ArtSource/SampleCovers/<slug>.png (640x640) by headless Edge. Every cover is geometry
// written here; no third-party picture, logo or album art goes in.
//
//   node ArtSource/Tools/SampleCovers/render_covers.js            all covers
//   node ArtSource/Tools/SampleCovers/render_covers.js neon-tide  only these
//
// The song list that uses them is Assets/Game/Editor/OpenSekai/Screenshots/SampleLibrary.cs.
const fs = require('fs'), os = require('os'), path = require('path'), cp = require('child_process');
const out = path.resolve(__dirname, '../../SampleCovers');
const work = fs.mkdtempSync(path.join(os.tmpdir(), 'sample-covers-'));
let seed = 1;
const R = () => { seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0; return seed / 4294967296; };
const r = (a, b) => a + R() * (b - a);
const grad = (id, a, b, ang = 90) => `<linearGradient id="${id}" gradientTransform="rotate(${ang})"><stop offset="0" stop-color="${a}"/><stop offset="1" stop-color="${b}"/></linearGradient>`;
const rad = (id, a, b) => `<radialGradient id="${id}"><stop offset="0" stop-color="${a}"/><stop offset="1" stop-color="${b}" stop-opacity="0"/></radialGradient>`;
const svg = (defs, body, bg) => `<svg xmlns="http://www.w3.org/2000/svg" width="640" height="640" viewBox="0 0 640 640"><defs>${defs}</defs><rect width="640" height="640" fill="${bg}"/>${body}</svg>`;
const circ = (x, y, q, f, o = 1) => `<circle cx="${x}" cy="${y}" r="${q}" fill="${f}" opacity="${o}"/>`;
const ring = (x, y, q, c, w, o = 1) => `<circle cx="${x}" cy="${y}" r="${q}" fill="none" stroke="${c}" stroke-width="${w}" opacity="${o}"/>`;
const line = (a, b, c, d, s, w, o = 1) => `<line x1="${a}" y1="${b}" x2="${c}" y2="${d}" stroke="${s}" stroke-width="${w}" opacity="${o}" stroke-linecap="round"/>`;
const poly = (p, f, o = 1, e = '') => `<polygon points="${p.map(q => q.join(',')).join(' ')}" fill="${f}" opacity="${o}" ${e}/>`;
const stars = (n, c = '#fff') => { let s = ''; for (let i = 0; i < n; i++) s += circ(r(10, 630).toFixed(1), r(10, 630).toFixed(1), r(.8, 2.6).toFixed(1), c, r(.3, 1).toFixed(2)); return s; };
const wave = (y, a, f, p) => { let d = 'M0 640'; for (let x = 0; x <= 640; x += 8) d += ` L${x} ${(y + Math.sin(x / 640 * Math.PI * 2 * f + p) * a).toFixed(1)}`; return d + ' L640 640 Z'; };
const path_ = (d, f, o = 1) => `<path d="${d}" fill="${f}" opacity="${o}"/>`;
const C = {};

C['aurora-line'] = () => {
  let b = stars(60); const cs = ['#5BE7C4', '#62D2F0', '#7C9BFF', '#A58CFF', '#62D2F0', '#5BE7C4', '#9DF5D6'];
  for (let i = 0; i < 7; i++) { let d = ''; for (let x = 0; x <= 640; x += 16) d += `${x ? ' L' : 'M'}${x} ${(150 + i * 34 + Math.sin(x / 95 + i * .7) * 46).toFixed(1)}`; b += `<path d="${d}" fill="none" stroke="${cs[i]}" stroke-width="${26 - i * 2}" opacity=".42" stroke-linecap="round"/>`; }
  return svg(grad('g', '#07142B', '#0E3350'), b + path_(wave(560, 14, 1.2, 0), '#061019'), 'url(#g)');
};
C['night-signal'] = () => {
  let b = stars(50, '#BFD4FF'); for (let i = 1; i < 9; i++) b += ring(320, 420, i * 44, '#6FA0FF', 5, (1 - i * .1).toFixed(2));
  b += circ(320, 420, 18, '#E8F0FF') + poly([[320, 470], [270, 640], [370, 640]], '#16264F') + line(320, 420, 320, 560, '#E8F0FF', 8);
  return svg(grad('g', '#0A1030', '#1B2C66'), b, 'url(#g)');
};
C['paper-comet'] = () => {
  let b = ''; for (let i = 0; i < 9; i++) b += line(60 + i * 14, 600 - i * 30, 300 + i * 10, 330 - i * 8, '#FFF3E4', 3 + i % 3 * 2, .35);
  b += poly([[540, 90], [230, 300], [330, 340]], '#FFFFFF') + poly([[540, 90], [330, 340], [360, 450]], '#FFD9B8') + poly([[330, 340], [352, 392], [360, 450], [300, 330]], '#F2A97C');
  return svg(grad('g', '#FF9A76', '#E2556C', 60), circ(540, 90, 60, '#fff', .18) + b, 'url(#g)');
};
C['glass-garden'] = () => {
  let b = ''; const c = ['#B9F3C8', '#7FE0B0', '#FFE6A8', '#9BE8E0', '#D7F7B0'];
  for (let i = 0; i < 14; i++) b += `<ellipse cx="320" cy="200" rx="62" ry="150" fill="${c[i % 5]}" opacity=".30" transform="rotate(${i / 14 * 360} 320 330)"/>`;
  return svg(grad('g', '#0F4A3C', '#1E7A5C', 50), b + circ(320, 330, 42, '#FFF6D6', .9) + ring(320, 330, 64, '#fff', 3, .7), 'url(#g)');
};
C['lumen-drive'] = () => {
  let b = circ(320, 300, 130, 'url(#s)'); for (let i = -12; i <= 12; i++) b += line(320 + i * 6, 330, 320 + i * 90, 640, '#F08CFF', 2.5, .7);
  for (let i = 0; i < 9; i++) { const y = 330 + Math.pow(i / 9, 2) * 310; b += line(0, y, 640, y, '#F08CFF', 2.5, .65); }
  return svg(grad('g', '#1A0B3B', '#52156B') + grad('s', '#FFD36E', '#FF5FA2'), b + line(0, 330, 640, 330, '#FFE3FF', 4), 'url(#g)');
};
C['echo-bloom'] = () => {
  let b = ''; const cs = ['#fff', '#FFE1EE', '#FFC3DD', '#FF9FC6', '#F779AE', '#D9578F'];
  for (let k = 6; k > 0; k--) for (let i = 0; i < 8; i++) b += `<ellipse cx="320" cy="${320 - k * 26}" rx="${k * 15}" ry="${k * 30}" fill="${cs[k - 1]}" opacity=".5" transform="rotate(${i * 45 + k * 11} 320 320)"/>`;
  return svg(grad('g', '#5B1B45', '#A63A72', 40), b + circ(320, 320, 24, '#FFF4C9'), 'url(#g)');
};
C['orbit-parade'] = () => {
  let b = stars(70); for (const [x, y] of [[280, 110], [220, 80], [160, 56]]) b += `<ellipse cx="320" cy="320" rx="${x}" ry="${y}" fill="none" stroke="#B7B4FF" stroke-width="4" opacity=".7" transform="rotate(-18 320 320)"/>`;
  b += circ(320, 320, 62, 'url(#p)') + circ(580, 236, 22, '#FFD98A') + circ(120, 352, 16, '#8CF0E0') + circ(210, 300, 12, '#FF9FC6') + circ(470, 330, 10, '#fff');
  return svg(grad('g', '#14103A', '#2E2A7A', 70) + grad('p', '#FFB36B', '#E0567A', 45), b, 'url(#g)');
};
C['silver-static'] = () => {
  let b = ''; const cs = ['#F4F6F8', '#C9D0D8', '#8E99A6', '#5F6B78'];
  for (let i = 0; i < 46; i++) b += `<rect x="${r(-60, 400).toFixed(0)}" y="${r(0, 630).toFixed(0)}" width="${r(60, 420).toFixed(0)}" height="${r(2, 16).toFixed(0)}" fill="${cs[i % 4]}" opacity="${r(.25, .9).toFixed(2)}"/>`;
  return svg(grad('g', '#2A313A', '#6C7682', 80), b + ring(320, 320, 150, '#fff', 10, .85) + ring(320, 320, 176, '#fff', 3, .5), 'url(#g)');
};
C['velvet-prism'] = () => {
  // Faceted gem mosaic: overlapping translucent triangles around the centre.
  let b = ''; const cs = ['#FF6B8B', '#C86BFF', '#7C8BFF', '#FF9AD0', '#9A4DD6', '#FFB2C8'];
  for (let i = 0; i < 12; i++) { const a0 = i * Math.PI / 6, a1 = a0 + Math.PI / 6, q = i % 2 ? 250 : 210; b += poly([[320, 320], [(320 + Math.cos(a0) * q).toFixed(1), (320 + Math.sin(a0) * q).toFixed(1)], [(320 + Math.cos(a1) * (460 - q)).toFixed(1), (320 + Math.sin(a1) * (460 - q)).toFixed(1)]], cs[i % 6], .85); }
  return svg(grad('g', '#2B0B33', '#611A5E', 30), b + poly([[320, 236], [393, 278], [393, 362], [320, 404], [247, 362], [247, 278]], '#FFE3F2', .9), 'url(#g)');
};
C['cobalt-hour'] = () => {
  let b = circ(320, 320, 210, '#0B2C78', .7) + ring(320, 320, 210, '#CFE2FF', 8);
  for (let i = 0; i < 12; i++) { const a = i * Math.PI / 6; b += line(320 + Math.cos(a) * 180, 320 + Math.sin(a) * 180, 320 + Math.cos(a) * 196, 320 + Math.sin(a) * 196, '#CFE2FF', i % 3 ? 4 : 9); }
  return svg(grad('g', '#1C4FD0', '#0A1F5C', 60), b + line(320, 320, 320, 180, '#fff', 12) + line(320, 320, 420, 372, '#8FD0FF', 9) + circ(320, 320, 16, '#fff'), 'url(#g)');
};
C['neon-tide'] = () => svg(grad('g', '#FF6FB5', '#FFC38A'), circ(430, 200, 96, '#FFF1C9') + path_(wave(300, 26, 1.4, 0), '#7A4DFF', .75) + path_(wave(380, 30, 1.7, 1.4), '#3A6BFF', .8) + path_(wave(460, 28, 2, 2.6), '#1FC7E8', .85) + path_(wave(550, 22, 1.6, 4), '#0D2B6B'), 'url(#g)');
C['starlit-atlas'] = () => {
  let b = stars(90, '#F7E9B8'); for (let i = 0; i < 6; i++) b += `<ellipse cx="320" cy="320" rx="${40 + i * 44}" ry="240" fill="none" stroke="#C9B36A" stroke-width="2" opacity=".5"/>`;
  for (let i = -2; i < 3; i++) b += line(80, 320 + i * 80, 560, 320 + i * 80, '#C9B36A', 2, .45);
  const p = [[140, 210], [230, 160], [320, 240], [430, 190], [500, 290], [410, 380]];
  for (let i = 0; i < 5; i++) b += line(p[i][0], p[i][1], p[i + 1][0], p[i + 1][1], '#FFF3C4', 3);
  p.forEach(q => { b += circ(q[0], q[1], 9, '#FFF3C4'); });
  return svg(grad('g', '#0D1B2A', '#23405A', 70), b, 'url(#g)');
};
C['hollow-bell'] = () => {
  let b = ''; for (let i = 5; i > 0; i--) b += ring(320, 360, i * 56, '#FFD98A', 6, (.9 - i * .14).toFixed(2));
  b += path_('M210 420 C220 250 260 170 320 170 C380 170 420 250 430 420 L470 460 L170 460 Z', 'url(#b)') + circ(320, 490, 28, '#FFE6AE') + circ(320, 152, 20, '#FFE6AE');
  return svg(grad('g', '#3A2416', '#7A4A22', 60) + grad('b', '#FFE3A0', '#C9803A', 70), b, 'url(#g)');
};
C['crimson-lattice'] = () => {
  let b = ''; const cs = ['#FF6B6B', '#D83A4E', '#A31E3A', '#FF9A7A'];
  for (let x = -1; x < 8; x++) for (let y = -1; y < 8; y++) { const cx = x * 92 + (y % 2 ? 46 : 0), cy = y * 92; b += poly([[cx, cy - 46], [cx + 46, cy], [cx, cy + 46], [cx - 46, cy]], cs[(x * 3 + y * 5 + 40) % 4], .82); }
  return svg('', b + poly([[320, 190], [450, 320], [320, 450], [190, 320]], '#1C0710', .92) + poly([[320, 236], [404, 320], [320, 404], [236, 320]], '#FFD7C2'), '#4A0E1E');
};
C['ivory-circuit'] = () => {
  let b = ''; for (let i = 0; i < 14; i++) { const x = 40 + i * 42, y = r(60, 560).toFixed(0); b += `<path d="M${x} 0 V${y} l${i % 2 ? 40 : -40} 40 V640" fill="none" stroke="#B59A6A" stroke-width="5" opacity=".75"/>` + circ(x, y, 9, '#8A6A34'); }
  return svg(grad('g', '#FBF5E6', '#E8D9B8', 60), b + '<rect x="220" y="220" width="200" height="200" rx="26" fill="#3B3226"/><rect x="256" y="256" width="128" height="128" rx="14" fill="none" stroke="#FBF5E6" stroke-width="6"/>', 'url(#g)');
};
C['moth-and-lantern'] = () => {
  let b = circ(320, 360, 250, 'url(#l)') + '<rect x="262" y="290" width="116" height="160" rx="30" fill="#FFD27A"/><rect x="250" y="270" width="140" height="26" rx="10" fill="#4A3A1C"/><rect x="250" y="444" width="140" height="26" rx="10" fill="#4A3A1C"/>' + line(320, 90, 320, 270, '#4A3A1C', 8);
  [[150, 210, 1], [470, 180, .8], [500, 420, .6]].forEach(([x, y, s]) => { b += `<g transform="translate(${x} ${y}) scale(${s})"><ellipse cx="-34" cy="-10" rx="40" ry="26" fill="#E9DDC0" transform="rotate(24)"/><ellipse cx="34" cy="-10" rx="40" ry="26" fill="#E9DDC0" transform="rotate(-24)"/><ellipse rx="8" ry="30" fill="#6A5430"/></g>`; });
  return svg(grad('g', '#1D2414', '#39442A', 80) + rad('l', '#FFE7A6', '#FFC24A'), b, 'url(#g)');
};
C['tidal-index'] = () => {
  let b = ''; ['#D6F8EC', '#9CE8D8', '#62CFC2', '#35B0AC', '#1E9097', '#14717F', '#0F5566', '#0B3B4A'].forEach((c, i) => { b += path_(wave(60 + i * 78, 18, 1 + i * .18, i * 1.1), c); });
  return svg('', circ(500, 60, 44, '#FF8C6B') + b, '#F2FFFA');
};
C['quiet-engine'] = () => {
  const gear = (x, y, q, n, c) => { let s = circ(x, y, q, c); for (let i = 0; i < n; i++) s += `<rect x="${x - 14}" y="${y - q - 18}" width="28" height="36" rx="6" fill="${c}" transform="rotate(${i * 360 / n} ${x} ${y})"/>`; return s + circ(x, y, q * .46, '#27323B') + ring(x, y, q * .7, '#27323B', 5, .5); };
  return svg(grad('g', '#3C4A55', '#1B242B', 60), gear(250, 370, 140, 14, '#8FA3B0') + gear(466, 214, 84, 9, '#F2B45C') + gear(120, 150, 56, 7, '#5C707D'), 'url(#g)');
};
C['saffron-relay'] = () => {
  let b = ''; ['#FFF0C2', '#FFD56A', '#FFB02E', '#F07F13', '#B84A0E'].forEach((c, i) => { b += poly([[i * 150 - 240, 640], [i * 150 - 120, 640], [i * 150 + 340, 0], [i * 150 + 220, 0]], c, .9); });
  return svg('', b + circ(320, 320, 76, '#3A1604') + poly([[298, 280], [298, 360], [366, 320]], '#FFF0C2'), '#F59A1B');
};
C['faraway-choir'] = () => svg(grad('g', '#E6D6FF', '#8C7BD8'), circ(320, 230, 110, '#FFF6E2', .9) + path_(wave(370, 40, .9, .4), '#7A69C4') + path_(wave(440, 34, 1.3, 2), '#5A4AA6') + path_(wave(520, 26, 1.7, 3.6), '#3B2F7D') + [90, 180, 300, 420, 540].map((x, i) => line(x, 520 - i % 2 * 20, x, 440 - i % 3 * 18, '#241B57', 7) + circ(x, 432 - i % 3 * 18, 14, '#241B57')).join(''), 'url(#g)');
C['lantern-code'] = () => {
  let b = ''; for (let x = 0; x < 7; x++) for (let y = 0; y < 7; y++) { const v = R(); b += `<rect x="${52 + x * 78}" y="${52 + y * 78}" width="60" height="60" rx="14" fill="${v > .62 ? '#FFE27A' : v > .34 ? '#E8A93A' : '#3D3413'}" opacity="${v > .62 ? 1 : .8}"/>`; }
  return svg(grad('g', '#1E1A08', '#332B0E', 60), b, 'url(#g)');
};
C['northbound'] = () => svg(grad('g', '#BFE3F5', '#F7F1E4'), circ(470, 150, 54, '#FFB98A') + poly([[-40, 640], [200, 220], [440, 640]], '#4F7892') + poly([[200, 220], [140, 325], [190, 300], [220, 335], [262, 330]], '#fff') + poly([[190, 640], [450, 300], [700, 640]], '#2F5670') + poly([[450, 300], [388, 381], [440, 362], [470, 390], [508, 376]], '#fff') + path_(wave(560, 16, 1.6, 1), '#17394F'), 'url(#g)');

const edge = process.env.EDGE_PATH || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const only = process.argv.slice(2);
const unknown = only.filter(k => !C[k]);
if (unknown.length) { console.error('Unknown cover: ' + unknown.join(', ')); process.exit(1); }
fs.mkdirSync(out, { recursive: true });
Object.keys(C).forEach((k, n) => {
  seed = 1000 + n * 77; // each cover draws the same stars and bars on every run
  if (only.length && !only.includes(k)) return;
  const html = path.join(work, k + '.html');
  fs.writeFileSync(html, `<body style="margin:0;background:#000">${C[k]()}</body>`);
  cp.execFileSync(edge, ['--headless=new', '--disable-gpu', '--hide-scrollbars', '--user-data-dir=' + path.join(work, 'profile'),
    '--window-size=640,640', '--screenshot=' + path.join(out, k + '.png'), 'file:///' + html.split(path.sep).join('/')], { stdio: 'ignore' });
  console.log(k);
});
try { fs.rmSync(work, { recursive: true, force: true }); } catch { /* Edge may still hold its profile for a moment */ }
