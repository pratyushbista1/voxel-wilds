import { chromium } from 'playwright';
import { mkdir } from 'node:fs/promises';
await mkdir('artifacts', { recursive: true });
const browser = await chromium.launch({
  headless: true,
  executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe',
  args: ['--disable-dev-shm-usage'],
  env: { ...process.env, TEMP: process.cwd() + '/.cache', TMP: process.cwd() + '/.cache' },
});
const page = await browser.newPage({
  viewport: { width: 1440, height: 900 },
  deviceScaleFactor: 1,
});
const errors = [];
page.on('pageerror', (e) => {
  errors.push(e.message);
  console.error('PAGE ERROR:', e.message);
});
page.on('console', (m) => {
  if (m.type() === 'error' || m.type() === 'warning') console.log(m.type(), m.text());
});
await page.goto('http://127.0.0.1:4173');
try {
  await page.waitForSelector('#menu:not(.hidden)', { timeout: 90000 });
  await page.waitForFunction(() => window.__wilds?.inspect().chunks > 65, { timeout: 60000 });
  await page.screenshot({ path: 'artifacts/title-screen.png' });
  console.log(
    JSON.stringify({ errors, state: await page.evaluate(() => window.__wilds?.inspect()) }, null, 2)
  );
} catch (e) {
  console.error(e.message);
  await page.screenshot({ path: 'artifacts/initial-error.png' });
  console.log(await page.locator('body').innerText());
  process.exitCode = 1;
}
await browser.close();
