// Downloads the site's photography from media-manifest.json into wwwroot/img.
//
// Why this exists: wwwroot/img is gitignored, so a fresh clone has no images and every cover falls
// back to the crest placeholder. This script is what makes that recoverable — the manifest is the
// committed source of truth, the binaries are not.
//
//   npm run fetch:media            fetch anything missing
//   npm run fetch:media -- --force re-fetch everything
//
// Resizing is done by the Unsplash CDN (the w= parameter), so there is no sharp/ImageSharp
// dependency here and nothing to keep up to date. That is the whole reason two hand-picked widths
// beat a local image pipeline at this stage.
//
// Node 18+ for global fetch. No npm dependencies on purpose — this must work immediately after a
// clone, before anyone has run `npm install`.

import { mkdir, writeFile, access, readFile } from 'node:fs/promises';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const manifestPath = resolve(here, '..', 'media-manifest.json');
const force = process.argv.includes('--force');

const exists = (p) => access(p).then(() => true, () => false);

/** Unsplash serves a resized JPEG straight from the CDN; fit=crop keeps the aspect predictable. */
function sourceUrl(asset, width, quality) {
  if (asset.source !== 'unsplash') {
    throw new Error(`Unsupported source "${asset.source}" for ${asset.target}. Add a builder for it here.`);
  }
  return `https://images.unsplash.com/${asset.id}?w=${width}&q=${quality}&fm=jpg&fit=crop&crop=entropy`;
}

async function fetchOne(url, destination) {
  const response = await fetch(url, { redirect: 'follow' });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);

  const type = response.headers.get('content-type') ?? '';
  // A CDN that has lost the asset can still answer 200 with an HTML error page. Writing that to a
  // .jpg would produce a file that exists, passes an exists() check forever, and renders as a
  // broken image — the worst possible failure mode for a cache-on-disk script.
  if (!type.startsWith('image/')) throw new Error(`expected an image, got ${type || 'no content-type'}`);

  const bytes = Buffer.from(await response.arrayBuffer());
  if (bytes.length < 1024) throw new Error(`suspiciously small (${bytes.length} B)`);

  await mkdir(dirname(destination), { recursive: true });
  await writeFile(destination, bytes);
  return bytes.length;
}

const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
const outputRoot = resolve(here, '..', manifest.outputRoot);
const { widths, quality, assets } = manifest;

console.log(`Fetching ${assets.length} assets × ${widths.length} widths → ${outputRoot}`);

let fetched = 0;
let skipped = 0;
const failures = [];

for (const asset of assets) {
  for (const width of widths) {
    const destination = join(outputRoot, `${asset.target}-${width}.jpg`);

    if (!force && (await exists(destination))) {
      skipped += 1;
      continue;
    }

    try {
      const size = await fetchOne(sourceUrl(asset, width, quality), destination);
      fetched += 1;
      console.log(`  ok    ${asset.target}-${width}.jpg  ${(size / 1024).toFixed(0)} KB`);
    } catch (error) {
      failures.push(`${asset.target}-${width}.jpg — ${error.message}`);
      console.log(`  FAIL  ${asset.target}-${width}.jpg  ${error.message}`);
    }
  }
}

console.log(`\n${fetched} fetched, ${skipped} already present, ${failures.length} failed.`);

if (failures.length > 0) {
  console.error('\nFailures:');
  for (const failure of failures) console.error(`  ${failure}`);
  // Non-zero so this can gate a deployment step rather than silently shipping a site with holes in it.
  process.exit(1);
}
