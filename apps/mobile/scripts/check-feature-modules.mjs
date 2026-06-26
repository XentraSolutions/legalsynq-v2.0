#!/usr/bin/env node

import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const appRoot = path.resolve(scriptDir, '..');
const featuresRoot = path.join(appRoot, 'src', 'features');
const moduleKinds = [
  { folder: 'screens', label: 'screen' },
  { folder: 'components', label: 'component' },
];

const errors = [];

function listDirectories(folderPath) {
  if (!existsSync(folderPath)) {
    return [];
  }

  return readdirSync(folderPath).filter((entry) => {
    const entryPath = path.join(folderPath, entry);
    return !entry.startsWith('.') && statSync(entryPath).isDirectory();
  });
}

function normalize(filePath) {
  return path.relative(appRoot, filePath);
}

function assertNamedExport(moduleName, indexPath) {
  const contents = readFileSync(indexPath, 'utf8');
  const namedExportPattern = new RegExp(
    `export\\s+(function|const|class)\\s+${moduleName}\\b|export\\s*\\{[^}]*\\b${moduleName}\\b[^}]*\\}`,
    'm',
  );

  if (!namedExportPattern.test(contents)) {
    errors.push(`${normalize(indexPath)} must export named module ${moduleName}.`);
  }
}

if (!existsSync(featuresRoot)) {
  errors.push(`${normalize(featuresRoot)} does not exist.`);
} else {
  for (const featureName of listDirectories(featuresRoot)) {
    const featureRoot = path.join(featuresRoot, featureName);

    for (const { folder, label } of moduleKinds) {
      const collectionRoot = path.join(featureRoot, folder);

      for (const moduleName of listDirectories(collectionRoot)) {
        const moduleRoot = path.join(collectionRoot, moduleName);
        const indexPath = path.join(moduleRoot, 'index.tsx');
        const testPath = path.join(moduleRoot, 'index.test.tsx');
        const sameFolderBarrelPath = path.join(moduleRoot, 'index.ts');
        const legacyImplementationPath = path.join(moduleRoot, `${moduleName}.tsx`);
        const tsxFiles = readdirSync(moduleRoot).filter((entry) => entry.endsWith('.tsx'));
        const extraTsxFiles = tsxFiles.filter((entry) => entry !== 'index.tsx' && entry !== 'index.test.tsx');

        if (!existsSync(indexPath)) {
          errors.push(`${normalize(moduleRoot)} is missing index.tsx for its ${label} implementation.`);
        } else {
          assertNamedExport(moduleName, indexPath);
        }

        if (!existsSync(testPath)) {
          errors.push(`${normalize(moduleRoot)} is missing index.test.tsx.`);
        }

        if (existsSync(sameFolderBarrelPath)) {
          errors.push(`${normalize(sameFolderBarrelPath)} is prohibited; export from index.tsx directly.`);
        }

        if (existsSync(legacyImplementationPath)) {
          errors.push(`${normalize(legacyImplementationPath)} is prohibited; use index.tsx.`);
        }

        for (const fileName of extraTsxFiles) {
          errors.push(`${normalize(path.join(moduleRoot, fileName))} is prohibited; keep the module implementation in index.tsx.`);
        }
      }
    }
  }
}

if (errors.length > 0) {
  console.error('Feature module architecture check failed:');
  for (const error of errors) {
    console.error(`- ${error}`);
  }
  process.exit(1);
}

console.log('Feature module architecture check passed.');
