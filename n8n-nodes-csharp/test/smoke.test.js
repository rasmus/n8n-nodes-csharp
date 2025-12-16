const { test } = require('node:test');
const assert = require('node:assert/strict');

test('exports CSharpCode node', () => {
  // Ensure the built output is importable and exports the node.
  // This is a lightweight sanity check that catches broken TS builds/exports.
  // eslint-disable-next-line @typescript-eslint/no-var-requires
  const mod = require('../dist/nodes/CSharpCode/CSharpCode.node.js');
  assert.equal(typeof mod.CSharpCode, 'function');
});
