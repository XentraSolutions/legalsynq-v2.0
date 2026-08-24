#!/usr/bin/env node

import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const appRoot = path.resolve(scriptDir, '..');
const args = process.argv.slice(2);

function readOption(name) {
  const index = args.indexOf(`--${name}`);
  return index >= 0 ? args[index + 1] : undefined;
}

function fail(message) {
  console.error(message);
  process.exit(1);
}

function ensurePascalCase(value, label) {
  if (!/^[A-Z][A-Za-z0-9]*$/.test(value)) {
    fail(`${label} must be PascalCase. Received: ${value}`);
  }
}

function appendExport(barrelPath, exportLine) {
  const existing = existsSync(barrelPath) ? readFileSync(barrelPath, 'utf8') : '';
  const normalized = existing.endsWith('\n') || existing.length === 0 ? existing : `${existing}\n`;

  if (existing.includes(exportLine)) {
    return;
  }

  writeFileSync(barrelPath, `${normalized}${exportLine}\n`);
}

const feature = readOption('feature');
const kind = readOption('kind');
const name = readOption('name');

if (!feature || !kind || !name) {
  fail('Usage: node scripts/scaffold-feature-module.mjs --feature <feature> --kind <screen|component> --name <PascalCaseName>');
}

if (!/^[a-z][a-z0-9-]*$/.test(feature)) {
  fail(`feature must be kebab-case or lowercase. Received: ${feature}`);
}

if (kind !== 'screen' && kind !== 'component') {
  fail(`kind must be either "screen" or "component". Received: ${kind}`);
}

ensurePascalCase(name, 'name');

const collectionFolder = kind === 'screen' ? 'screens' : 'components';
const moduleRoot = path.join(appRoot, 'src', 'features', feature, collectionFolder, name);

if (existsSync(moduleRoot)) {
  fail(`${path.relative(appRoot, moduleRoot)} already exists. Refusing to overwrite.`);
}

mkdirSync(moduleRoot, { recursive: true });

const componentTemplate = `import { Text, View } from 'react-native';

export type ${name}Props = {
  label?: string;
};

export function ${name}({ label = '${name}' }: ${name}Props) {
  return (
    <View>
      <Text>{label}</Text>
    </View>
  );
}
`;

const screenTemplate = `import { Text, View } from 'react-native';

export function ${name}() {
  return (
    <View>
      <Text>${name}</Text>
    </View>
  );
}
`;

const testTemplate = `import { render, screen } from '@testing-library/react-native';

import { ${name} } from './index';

describe('${name}', () => {
  it('renders the module entrypoint', () => {
    render(<${name} />);

    expect(screen.getByText('${name}')).toBeTruthy();
  });
});
`;

writeFileSync(path.join(moduleRoot, 'index.tsx'), kind === 'screen' ? screenTemplate : componentTemplate);
writeFileSync(path.join(moduleRoot, 'index.test.tsx'), testTemplate);

if (kind === 'component') {
  const barrelPath = path.join(appRoot, 'src', 'features', feature, 'components', 'index.ts');
  appendExport(barrelPath, `export * from './${name}';`);
} else {
  const barrelPath = path.join(appRoot, 'src', 'features', feature, 'index.ts');
  appendExport(barrelPath, `export * from './screens/${name}';`);
}

console.log(`Created ${kind} module at ${path.relative(appRoot, moduleRoot)}.`);
