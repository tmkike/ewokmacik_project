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

## Fő útvonalak

- `/home`: nyitóoldal
- `/books`: könyvlista szűréssel és lapozással
- `/books/:id`: könyv adatlapja
- `/add`: új könyv felvétele
- `/contact`: kapcsolat oldal

## Rövid user guide

### Könyvek listája

Nyisd meg a `/books` oldalt. Itt:

- cím, szerző, műfaj és elérhetőség alapján szűrhetsz
- lapozhatsz a találatok között
- egy könyvre kattintva megnyithatod a részletes adatlapot

### Új könyv felvétele

Nyisd meg az `/add` oldalt, töltsd ki az űrlapot, majd mentsd el a könyvet. Sikeres mentés után a rendszer visszairányít a könyvlistára.

### Könyv adatlap

A `/books/:id` oldalon a következő műveletek érhetők el:

- könyvadatok szerkesztése
- elérhetőség módosítása
- kölcsönzés indítása, ha a könyv elérhető
- aktív kölcsönzés adatainak megjelenítése
- kölcsönzés lezárása
- könyv törlése, ha nincs aktív kölcsönzés

## Fontos fájlok

- `src/app/app.routes.ts`: útvonalak
- `src/app/layout`: közös oldalváz
- `src/app/pages`: oldalak
- `src/app/services`: API kommunikáció
- `src/app/shared`: közös helper logika
- `proxy.conf.json`: helyi API proxy beállítás
