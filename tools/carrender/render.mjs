// Renders inspection views of an exported car OBJ with three.js in headless Chromium.
// Usage: node render.mjs <car.obj> <out_prefix> [view1,view2,...]
import { createRequire } from 'module';
const { chromium } = createRequire(import.meta.url)('playwright');
import http from 'http';
import fs from 'fs';
import path from 'path';

const [objPath, outPrefix, only] = process.argv.slice(2);
const root = path.dirname(new URL(import.meta.url).pathname);
const server = http.createServer((req, res) => {
  const url = decodeURIComponent(req.url.split('?')[0]);
  const file = url === '/car.obj' ? objPath : path.join(root, url);
  fs.readFile(file, (err, data) => {
    if (err) { res.writeHead(404); res.end(); return; }
    const type = file.endsWith('.js') ? 'text/javascript' : file.endsWith('.html') ? 'text/html' : 'text/plain';
    res.writeHead(200, { 'Content-Type': type }); res.end(data);
  });
}).listen(0);
const port = server.address().port;
const views = {
  side: { eye: '9.5,0.55,0.15', target: '0,0.15,0.15', fov: 32 },
  front: { eye: '0,0.6,11', target: '0,0.2,0', fov: 22 },
  rear: { eye: '0,0.8,-11', target: '0,0.25,0', fov: 22 },
  top: { eye: '0.001,12,0.15', target: '0,0,0.15', fov: 28 },
  front34: { eye: '5.2,1.6,6.2', target: '0,0.1,0.3', fov: 30 },
  rear34: { eye: '-5.4,1.4,-5.6', target: '0,0.15,-0.3', fov: 30 },
  frontwing: { eye: '2.2,0.7,4.6', target: '0,-0.05,2.6', fov: 34 },
  cockpit: { eye: '0,0.47,0.02', target: '0,0.33,6', fov: 70 },
};
const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 1600, height: 900 } });
page.on('console', (m) => { if (m.type() === 'error') console.error('page:', m.text()); });
for (const [name, v] of Object.entries(views)) {
  if (only && !only.split(',').includes(name)) continue;
  await page.goto(`http://localhost:${port}/render.html?obj=/car.obj&eye=${v.eye}&target=${v.target}&fov=${v.fov}`);
  await page.waitForFunction('window.renderDone === true', null, { timeout: 120000 });
  await page.locator('canvas').screenshot({ path: `${outPrefix}_${name}.png` });
  console.log('rendered', name);
}
await browser.close();
server.close();
