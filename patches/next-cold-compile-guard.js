#!/usr/bin/env node
const fs = require('fs');
const path = require('path');

const target = path.join(
  __dirname, '..', 'node_modules', 'next', 'dist', 'server', 'app-render', 'app-render.js'
);

if (!fs.existsSync(target)) {
  console.log('[patch] next app-render.js not found, skipping');
  process.exit(0);
}

let src = fs.readFileSync(target, 'utf8');

const marker = "typeof createMetadataComponents !== 'function'";
if (src.includes(marker)) {
  console.log('[patch] next app-render.js already patched');
  process.exit(0);
}

const needle =
  `const serveStreamingMetadata = !!ctx.renderOpts.serveStreamingMetadata;\n` +
  `    const searchParams = createServerSearchParamsForMetadata(query, workStore);`;

const replacement =
  `if (typeof createMetadataComponents !== 'function') {\n` +
  `        throw Object.defineProperty(new Error('Module not yet initialized (cold-compile race). Refresh to retry.'), "__NEXT_ERROR_CODE", { value: "E000", enumerable: false, configurable: true });\n` +
  `    }\n` +
  `    const serveStreamingMetadata = !!ctx.renderOpts.serveStreamingMetadata;\n` +
  `    const searchParams = createServerSearchParamsForMetadata(query, workStore);`;

if (!src.includes(needle)) {
  console.log('[patch] next app-render.js: needle not found (version mismatch?), skipping');
  process.exit(0);
}

src = src.replace(needle, replacement);
fs.writeFileSync(target, src, 'utf8');
console.log('[patch] next app-render.js patched: cold-compile guard added to getErrorRSCPayload');
