# Library System

Ez a mappa a könyvtári rendszer jelenlegi, mikroszervizes változatát tartalmazza.

## Felépítés

- `book-service`: a könyvek listázása, létrehozása, módosítása és törlése
- `loan-service`: a kölcsönzések kezelése
- `api-gateway`: egységes belépési pont a frontend számára
- `book-mongodb`: a könyvek adatbázisa
- `loan-mongodb`: a kölcsönzések adatbázisa
- `database/book-seed`: a kezdeti könyvlista seed adatai
- `k8s`: Kubernetes manifestek

## Követelmények

Helyi futtatáshoz ezek szükségesek:

- Docker Desktop
- Docker Compose
- .NET 8 SDK

## Architektúra

```text
frontend -> api-gateway -> book-service -> book-mongodb
frontend -> api-gateway -> loan-service -> loan-mongodb

book-service <-> loan-service
```

Fontos, hogy a két szolgáltatás már nem olvas közvetlenül egymás adatbázisából. Az állapotok egyeztetése belső HTTP-hívásokkal történik.

## Indítás Dockerrel

Nyiss terminált a `library-system` mappában, majd futtasd:

```bash
docker compose up -d --build --force-recreate
```

Hasznos címek:

```text
http://localhost:4200
http://localhost:3000/health
http://localhost:3001/health
http://localhost:3002/health
```

Portkiosztás:

- `4200`: frontend
- `3000`: API gateway
- `3001`: book-service
- `3002`: loan-service
- `27018`: book-mongodb
- `27019`: loan-mongodb

Teljesen tiszta újraindításhoz:

```bash
docker compose down -v --remove-orphans
docker compose up -d --build --force-recreate
```

## Helyi fejlesztői futtatás

Ha a backend szolgáltatásokat Docker nélkül szeretnéd indítani, futtasd őket külön:

```bash
cd book-service
dotnet restore
dotnet run
```

```bash
cd loan-service
dotnet restore
dotnet run
```

Alapértelmezett környezeti változók a `book-service` számára:

```text
PORT=3001
MONGODB_URI=mongodb://localhost:27018
MONGODB_DB=books
LOAN_SERVICE_URL=http://localhost:3002
```

Alapértelmezett környezeti változók a `loan-service` számára:

```text
PORT=3002
MONGODB_URI=mongodb://localhost:27019
MONGODB_DB=loans
BOOK_SERVICE_URL=http://localhost:3001
```

## REST API

A publikus végpontok az API gateway mögött érhetők el:

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

## Integrációs tesztek

A `tests/LibrarySystem.StackTests` projekt a futó gatewayn keresztül ellenőrzi a fő üzleti folyamatokat.

Példa futtatás:

```bash
dotnet test tests/LibrarySystem.StackTests/LibrarySystem.StackTests.csproj
```

## Seed adatok

A könyvek kezdeti adatai a `database/book-seed` mappából töltődnek be a `book-mongodb` konténer indulásakor.

A `loan-service` külön, üres adatbázissal indul. A kölcsönzési adatok kizárólag a `loan-mongodb` adatbázisban tárolódnak.

## Kubernetes

A `k8s` mappa a teljes jelenlegi architektúrához tartalmaz manifesteket.

Telepítés:

```bash
kubectl apply -k k8s
```

Az image-ek, amelyekre a manifestek mutatnak:

```text
ghcr.io/tmkike/ewokmacik_project-frontend:latest
ghcr.io/tmkike/ewokmacik_project-book-service:latest
ghcr.io/tmkike/ewokmacik_project-loan-service:latest
```
