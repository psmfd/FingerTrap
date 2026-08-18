import { defineConfig } from 'vitest/config';

// First UI test suite (FT-2 slice 2): narrowly scoped to the layout tree's
// pure functions and PaneRegistry's kind-dispatch against a fake
// PaneContent — the trap class typechecking can't catch (a correctly-typed
// close() that still called the wrong wire method). xterm rendering, WebGL,
// and pixel geometry stay operator smoke per the repo's stated posture
// (ADR-0024 consequences).
export default defineConfig({
  // The registry transitively imports vscode-jsonrpc/browser, whose export
  // map only resolves under the browser condition — match the WebView's
  // resolution rather than Node's.
  resolve: {
    conditions: ['browser', 'module', 'import', 'default'],
  },
  test: {
    environment: 'jsdom',
    include: ['test/**/*.test.ts'],
  },
});
