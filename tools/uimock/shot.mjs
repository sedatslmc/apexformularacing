// Renders the HTML mockups to PNG (1920 x 1080): node tools/uimock/shot.mjs <page.html> [...] -> <page>.png next to them.
import { createRequire } from 'module';
import path from 'path';
const { chromium } = createRequire(path.resolve('tools/carrender/package.json'))('playwright');
const browser = await chromium.launch({ args: ['--allow-file-access-from-files'] });
const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });
for (const f of process.argv.slice(2)) {
  await page.goto('file://' + path.resolve(f));
  await page.waitForTimeout(400);
  const out = process.env.OUT_DIR ? path.join(process.env.OUT_DIR, path.basename(f, '.html') + '.png') : f.replace(/\.html$/, '.png');
  await page.screenshot({ path: out });
  console.log('wrote', out);
}
await browser.close();
