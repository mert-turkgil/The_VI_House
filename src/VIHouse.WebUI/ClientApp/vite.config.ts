import { defineConfig } from 'vite';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

// package.json has "type": "module", so this file runs as ESM — __dirname isn't available,
// hence deriving it from import.meta.url instead.
const __dirname = dirname(fileURLToPath(import.meta.url));

// Builds ClientApp/src -> ../wwwroot/dist/main.{css,js} with fixed (non-hashed) filenames,
// referenced directly from _Layout.cshtml with asp-append-version for cache-busting.
// Triggered automatically by `dotnet build`/`dotnet run` via the NpmInstall/ViteBuild MSBuild
// targets in VIHouse.WebUI.csproj.
export default defineConfig({
  build: {
    outDir: resolve(__dirname, '../wwwroot/dist'),
    emptyOutDir: true,
    rollupOptions: {
      input: resolve(__dirname, 'src/js/main.ts'),
      output: {
        entryFileNames: 'main.js',
        assetFileNames: 'main.[ext]',
      },
    },
  },
});
