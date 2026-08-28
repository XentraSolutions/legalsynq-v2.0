import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test-setup.ts'],
    include: ['src/**/*.test.tsx', 'packages/**/*.test.tsx'],
    server: {
      deps: {
        // By default Vitest loads node_modules packages via Node's native
        // resolver, bypassing the react/react-dom aliases below entirely —
        // so @radix-ui/* (used by BaseSelect, Dialog, etc.), its
        // @floating-ui/react-dom dependency (used by Popper-based
        // primitives like Popover), @tanstack/react-query (used by any
        // component under test that fetches via a query hook), and
        // @tanstack/react-table (used by BaseTable) would each resolve
        // their own nested react copy from apps/web's local pnpm store
        // instead of the deduped one, producing a second dispatcher
        // and "Cannot read properties of null" once rendered. Inlining
        // routes them through Vite instead, where the alias/dedupe below
        // actually apply. @react-input/mask and react-day-picker (both
        // used by DatePicker) hit the same issue via their hook calls.
        inline: [
          /@radix-ui\//,
          /@floating-ui\//,
          /@tanstack\/react-query/,
          /@tanstack\/react-table/,
          /@react-input\//,
          /react-day-picker/,
        ],
      },
    },
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
      react: path.resolve(__dirname, './node_modules/react'),
      'react-dom': path.resolve(__dirname, './node_modules/react-dom'),
      'react/jsx-runtime': path.resolve(__dirname, './node_modules/react/jsx-runtime.js'),
      'react/jsx-dev-runtime': path.resolve(__dirname, './node_modules/react/jsx-dev-runtime.js'),
    },
    // apps/web is its own pnpm workspace with its own node_modules, so its
    // react/react-dom live under ./node_modules, not the monorepo root's —
    // aliasing to the root copy (as this used to) leaves apps/web's own
    // copy as a second, un-deduped React instance. A package that nests its
    // own react (e.g. @radix-ui/*, whose dist resolves react via its own
    // node_modules chain rather than the alias above) ends up rendering
    // against that second instance with its own hook dispatcher, so hooks
    // inside it fail with "Cannot read properties of null (reading
    // 'useMemo')" / "Invalid hook call".
    dedupe: ['react', 'react-dom'],
  },
});
