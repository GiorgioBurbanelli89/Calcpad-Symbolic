# Calcpad-Symbolic Website

Static site for Calcpad-Symbolic (similar in spirit to
[CalcpadCE-Website](https://github.com/fwolter/CalcpadCE-Website))
but multi-file and integrated with:

- **Editor online** — Blazor WASM (`/editor/`) using the real C# parser
- **FEM WASM** — C++ solvers from `hekatan-struct` compiled with Emscripten

## Layout

```
src/
  index.html              Landing
  features.html           Feature list
  downloads.html          Desktop / CLI / VS Code
  examples.html           Gallery linking rendered .html examples
  docs.html               Language reference
  community.html          GitHub links
  about.html              Project history + credits
  editor.html             Iframe → Blazor WASM editor
  partials/
    head.html             Shared <head> innards
    header.html           Sticky top nav
    footer.html           Dark footer
  css/
    main.css              Theme, typography (OKLCH palette)
    components.css        Header, hero, feature grid, footer
  js/
    include.js            data-include="/partials/…" loader
  icons/favicon.svg
```

Each HTML page stays under ~5 KB; shared chrome lives in `partials/`
and is injected client-side via `fetch()`.

## Local dev

```bash
# Any static server works. With Python:
python -m http.server 4900 --directory src

# Or with npx:
npx serve src -l 4900
```

Then open http://localhost:4900.

For the preview server configured in `.claude/launch.json`, use name
`calcpad-symbolic-website`.

## Docker deploy

```bash
docker build -t calcpad-symbolic-website .
docker compose up -d
# → http://localhost:8080
```

## Integrating the editor

`editor.html` loads `/editor/blazor/` in an iframe. To make that path
serve the Blazor app in production:

```bash
# From repo root
cd calcpad-web-blazor
dotnet publish -c Release -o ../Calcpad-Symbolic/Website/src/editor/blazor
```

## Credits

Inspired by the structure of
[fwolter/CalcpadCE-Website](https://github.com/fwolter/CalcpadCE-Website).
