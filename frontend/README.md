# Frontend

Ez a mappa a könyvtári rendszer Angular alapú felületét tartalmazza.

## Követelmények

- Node.js
- npm

## Fejlesztői indítás

Ha a backend Dockerből fut, a frontend külön is indítható fejlesztői módban:

```bash
npm.cmd start
```

Ez a parancs a `proxy.conf.json` alapján a `/api` hívásokat a `http://localhost:3000` címre továbbítja.

Elérési cím:

```text
http://localhost:4200
```

## Build

```bash
npm.cmd run build
```

A build kimenete a `dist/frontend` mappába kerül.

## Tesztek

```bash
npm.cmd test
```

## Fontos fájlok

- `src/app/app.routes.ts`: útvonalak
- `src/app/layout`: közös oldalváz
- `src/app/pages`: oldalak
- `src/app/services`: API kommunikáció
- `proxy.conf.json`: helyi API proxy beállítás
