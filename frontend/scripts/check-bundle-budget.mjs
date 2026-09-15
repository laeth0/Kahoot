import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { gzipSync } from 'node:zlib';

const assetsDirectory = new URL('../dist/assets/', import.meta.url);
const javascriptFiles = readdirSync(assetsDirectory).filter((fileName) => fileName.endsWith('.js'));

if (javascriptFiles.length === 0) {
  throw new Error('Bundle budget check found no JavaScript assets.');
}

const assets = javascriptFiles.map((fileName) => {
  const contents = readFileSync(join(assetsDirectory.pathname, fileName));
  return {
    fileName,
    rawBytes: contents.byteLength,
    gzipBytes: gzipSync(contents).byteLength,
  };
});

const totals = assets.reduce(
  (result, asset) => ({
    rawBytes: result.rawBytes + asset.rawBytes,
    gzipBytes: result.gzipBytes + asset.gzipBytes,
  }),
  { rawBytes: 0, gzipBytes: 0 },
);
const largestAsset = assets.reduce((largest, asset) =>
  asset.rawBytes > largest.rawBytes ? asset : largest,
);

const budgets = {
  totalGzipBytes: 370_000,
  largestGzipBytes: 130_000,
};

const violations = [
  totals.gzipBytes > budgets.totalGzipBytes &&
    `total gzip JavaScript ${totals.gzipBytes} exceeds ${budgets.totalGzipBytes}`,
  largestAsset.gzipBytes > budgets.largestGzipBytes &&
    `${largestAsset.fileName} gzip size ${largestAsset.gzipBytes} exceeds ${budgets.largestGzipBytes}`,
].filter(Boolean);

if (violations.length > 0) {
  throw new Error(`Bundle budget exceeded:\n${violations.join('\n')}`);
}

console.log(
  `Bundle budget passed: ${totals.rawBytes} raw bytes, ${totals.gzipBytes} gzip bytes; largest ${largestAsset.fileName} at ${largestAsset.rawBytes} raw bytes and ${largestAsset.gzipBytes} gzip bytes.`,
);
