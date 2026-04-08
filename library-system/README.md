# Library System

Ez a mappa a könyvtári rendszer MongoDB adatbázisát és az ahhoz kapcsolódó ASP.NET 10 / C# REST API-t tartalmazza.

## Követelmények

Helyi futtatáshoz ezek kellenek:

- Docker Desktop
- Docker Compose
- .NET 10 SDK

## Indítás Dockerrel

Nyiss terminált a `library-system` mappában, majd futtasd:

```bash
docker compose up --build
```

Ezzel elindul:

- a MongoDB adatbázis a `27017` porton
- az ASP.NET API a `3000` porton

Az API címe:

```text
http://localhost:3000
```

## Helyi fejlesztői futtatás

Ha nem Dockerből szeretnéd indítani az API-t:

```bash
cd backend
dotnet restore
dotnet run
```

Alapértelmezett környezeti változók:

```text
MONGODB_URI=mongodb://localhost:27017
MONGODB_DB=library
PORT=3000
```

## Adatbázis

Az adatbázis neve:

```text
library
```

A seed adatok automatikusan betöltik a példa könyveket a MongoDB indulásakor.

## REST API

Elérhető végpontok:

```text
GET    /api/books
GET    /api/books/:id
POST   /api/books
PUT    /api/books/:id
PATCH  /api/books/:id/availability
DELETE /api/books/:id

GET    /api/loans
GET    /api/loans/active
POST   /api/loans
PUT    /api/loans/:id
PUT    /api/loans/:id/return
```

Keresés és szűrés:

```text
GET /api/books?search=tolkien
GET /api/books?title=dune
GET /api/books?author=orwell
GET /api/books?genre=Fantasy
GET /api/books?available=true
```

## Minta payloadok

Könyv létrehozása vagy módosítása:

```json
{
  "title": "Dune",
  "author": "Frank Herbert",
  "year": 1965,
  "genre": "Science Fiction",
  "available": true
}
```

Kölcsönzés indítása:

```json
{
  "bookId": "PUT_BOOK_ID_HERE",
  "borrowerName": "Teszt Elek",
  "borrowerEmail": "teszt.elek@example.com",
  "dueAt": "2026-04-22",
  "notes": "Első kölcsönzés"
}
```

Kölcsönzés visszahozása:

```json
{
  "returnedAt": "2026-04-15T14:30:00.000Z"
}
```

## Konténeres háttér

Az API a hivatalos Microsoft .NET 10 SDK és ASP.NET 10 futtatókörnyezet képeire épül, a MongoDB driver pedig a hivatalos `MongoDB.Driver` NuGet csomagot használja.

## Kubernetes

A MongoDB-hez tartozó meglévő Kubernetes manifestek a `k8s` mappában találhatók. Az API-hoz szükséges manifestek következő lépésként erre a C# backend képre építhetők rá.
