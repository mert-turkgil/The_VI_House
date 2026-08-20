import { defineConfig } from 'vite';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

// package.json has "type": "module", so this file runs as ESM — __dirname isn't available,
// hence deriving it from import.meta.url instead.
const __dirname = dirname(fileURLToPath(import.meta.url));

// Two entry points, not one: the public site loads main.{css,js}, the admin panel loads
// admin.{css,js}. They're split because the admin bundle carries CKEditor and Chart.js, which
// together dwarf everything on the public site and are useless to a visitor — a single shared
// bundle would push all of that onto every homepage load. Filenames stay fixed (non-hashed) and are
// referenced directly from _Layout.cshtml / _AdminLayout.cshtml with asp-append-version for
// cache-busting. Triggered automatically by `dotnet build`/`dotnet run` via the NpmInstall/ViteBuild
// MSBuild targets in VIHouse.WebUI.csproj.
export default defineConfig({
  build: {
    outDir: resolve(__dirname, '../wwwroot/dist'),
    emptyOutDir: true,
    rollupOptions: {
      input: {
        main: resolve(__dirname, 'src/js/main.ts'),
        admin: resolve(__dirname, 'src/js/admin.ts'),
      },
      output: {
        entryFileNames: '[name].js',
        chunkFileNames: 'chunks/[name]-[hash].js',
        // Stylesheets keep their bare entry name so the Razor layouts can hard-code the path;
        // everything else (CKEditor's fonts/icons, etc.) is content-hashed into assets/ where
        // nothing links to it by name and collisions between entries can't happen.
        assetFileNames: (info) =>
          info.names?.some((n) => n.endsWith('.css'))
            ? '[name].[ext]'
            : 'assets/[name]-[hash][extname]',
      },
    },
  },
});
