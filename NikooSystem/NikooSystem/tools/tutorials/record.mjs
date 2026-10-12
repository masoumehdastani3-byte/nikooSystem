// Records Content/tutorials/videos/{Page}.webm for every tutorial whose page changed since its last recording.
// Usage: node record.mjs --base http://localhost:8095 --company FASHION --user admin [--only Products,Settings] [--force]
// The admin password is read from the TUTORIAL_PASSWORD environment variable.
// Tours and recorded fingerprints live in the database; the site lists them at /ShopAdmin/{company}/TutorialIndex.
// Each finished video is printed as "RECORDED<tab>page<tab>file<tab>fingerprint<tab>time" for update-tutorials.ps1,
// which stores it in the TutorialVideos table.
import { chromium } from 'playwright-core';
import { mkdirSync, renameSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const site = resolve(here, '../../NikooSystem');
const videosDir = join(site, 'Content', 'tutorials', 'videos');
const workDir = join(here, '.recordings');
const viewport = { width: 1280, height: 720 };

function parseArgs(argv) {
  const args = { base: 'http://localhost:8095', company: 'FASHION', user: 'admin', only: null, force: false, channel: 'msedge' };
  for (let i = 0; i < argv.length; i++) {
    const key = argv[i].replace(/^--/, '');
    if (key === 'force') args.force = true;
    else if (key in args) args[key] = argv[++i];
    else throw new Error(`Unknown option: ${argv[i]}`);
  }
  if (typeof args.only === 'string') args.only = args.only.split(',').map(s => s.trim()).filter(Boolean);
  args.base = args.base.replace(/\/+$/, '');
  args.password = process.env.TUTORIAL_PASSWORD;
  if (!args.password) throw new Error('Set the TUTORIAL_PASSWORD environment variable.');
  return args;
}

function adminUrl(args, path) {
  return `${args.base}/ShopAdmin/${encodeURIComponent(args.company)}${path ? '/' + path : ''}`;
}

async function login(browser, args) {
  const context = await browser.newContext({ viewport, locale: 'fa-IR' });
  const page = await context.newPage();
  await page.goto(adminUrl(args, 'Login'));
  await page.fill('#Username', args.user);
  await page.fill('#Password', args.password);
  await page.click('button[type=submit]');
  try {
    await page.waitForURL(url => !/\/Login/i.test(url.pathname), { timeout: 15000 });
  } catch {
    throw new Error('Login failed; check --company, --user and TUTORIAL_PASSWORD.');
  }
  const state = await context.storageState();
  await context.close();
  return state;
}

async function loadIndex(browser, storageState, args) {
  const context = await browser.newContext({ storageState });
  try {
    const response = await context.request.get(adminUrl(args, 'TutorialIndex'), { maxRedirects: 0 });
    if (!response.ok()) throw new Error(`TutorialIndex returned HTTP ${response.status()}`);
    return await response.json();
  } finally {
    await context.close();
  }
}

async function record(browser, storageState, args, item) {
  const context = await browser.newContext({
    viewport,
    storageState,
    locale: 'fa-IR',
    recordVideo: { dir: workDir, size: viewport }
  });
  const page = await context.newPage();
  const video = page.video();
  try {
    await page.goto(adminUrl(args, item.Path), { waitUntil: 'load', timeout: 60000 });
    if (/\/Login/i.test(new URL(page.url()).pathname)) throw new Error('redirected to login');
    await page.waitForFunction(() => window.NikooTutorial, null, { timeout: 15000 });
    await page.waitForTimeout(800);
    const started = await page.evaluate(() => window.NikooTutorial.start({ autoplay: true }));
    if (!started) throw new Error('tutorial has no steps on this page');
    await page.waitForFunction(() => window.__tutorialDone === true, null, { timeout: 10 * 60 * 1000, polling: 500 });
    await page.waitForTimeout(600);
  } finally {
    await context.close();
  }
  const name = `${item.Page}.webm`;
  rmSync(join(videosDir, name), { force: true });
  renameSync(await video.path(), join(videosDir, name));
  return name;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  mkdirSync(videosDir, { recursive: true });
  mkdirSync(workDir, { recursive: true });
  const browser = await chromium.launch({ channel: args.channel, headless: true });
  let failed = 0;
  try {
    const state = await login(browser, args);
    const tours = (await loadIndex(browser, state, args)).filter(t => !args.only || args.only.includes(t.Page));
    const pending = tours.filter(t => args.force || t.VideoFingerprint !== t.Fingerprint);
    if (!pending.length) {
      console.log(`All ${tours.length} tutorial videos are up to date.`);
      return;
    }
    console.log(`Recording ${pending.length} of ${tours.length} tutorials: ${pending.map(t => t.Page).join(', ')}`);

    for (const item of pending) {
      const startedAt = Date.now();
      try {
        const video = await record(browser, state, args, item);
        console.log(['RECORDED', item.Page, video, item.Fingerprint, new Date().toISOString()].join('\t'));
        console.log(`  ✓ ${item.Page} (${Math.round((Date.now() - startedAt) / 1000)}s)`);
      } catch (err) {
        failed++;
        console.error(`  ✗ ${item.Page}: ${err.message}`);
      }
    }
  } finally {
    await browser.close();
    rmSync(workDir, { recursive: true, force: true });
  }
  if (failed) {
    console.error(`${failed} tutorial(s) failed; their previous videos stay hidden until re-recorded.`);
    process.exitCode = 1;
  }
}

main().catch(err => {
  console.error(err.message);
  process.exitCode = 1;
});
