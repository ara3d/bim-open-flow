import { defineConfig } from 'vite';
import { resolve } from 'node:path';
import { toolkitAlias } from '../../toolkit.config';

export default defineConfig({
  resolve: {
    alias: [toolkitAlias, { find: 'gratify', replacement: resolve(__dirname, '../../../../submodules/gratify/src/gratify') }],
    dedupe: ['three'],
  },
  build: {
    outDir: resolve(__dirname, '../../../../artifacts/bim-flow-duckdb/web'),
    rollupOptions: { input: resolve(__dirname, 'duckdb.html') },
  },
  server: {
    host: '127.0.0.1', port: 5308, strictPort: true,
    fs: { allow: [resolve(__dirname, '../../../..')] },
    proxy: { '/api': process.env.BOF_DUCKDB_HOST ?? 'http://127.0.0.1:5218' },
  },
});
