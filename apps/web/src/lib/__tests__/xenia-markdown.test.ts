import assert from 'node:assert/strict';
import { test } from 'node:test';
import { parseMarkdownBlocks, parseMarkdownInlines } from '../xenia/markdown';

test('parseMarkdownBlocks recognizes headings, nested lists, and tables', () => {
  const blocks = parseMarkdownBlocks(`
# Referral Summary

1. Review the referral
2. Check details:
   - Status
   - Provider

| Field | Value |
| --- | --- |
| Status | New |
| Provider | Jane Smith |
`);

  assert.equal(blocks[0]?.type, 'heading');
  assert.equal(blocks[1]?.type, 'list');
  assert.equal(blocks[2]?.type, 'table');

  const listBlock = blocks[1];
  assert.ok(listBlock && listBlock.type === 'list');
  assert.equal(listBlock.items.length, 2);

  const secondItem = listBlock.items[1];
  assert.equal(secondItem[0]?.type, 'paragraph');
  assert.equal(secondItem[1]?.type, 'list');

  const tableBlock = blocks[2];
  assert.ok(tableBlock && tableBlock.type === 'table');
  assert.deepEqual(tableBlock.headers, ['Field', 'Value']);
  assert.deepEqual(tableBlock.rows[0], ['Status', 'New']);
});

test('parseMarkdownInlines recognizes emphasis, code, and links', () => {
  const tokens = parseMarkdownInlines('**Bold** with `code` and [link](/careconnect/referrals/1).');

  assert.equal(tokens[0]?.type, 'strong');
  assert.equal(tokens[1]?.type, 'text');
  assert.equal(tokens[2]?.type, 'code');
  assert.equal(tokens[3]?.type, 'text');
  assert.equal(tokens[4]?.type, 'link');
});
