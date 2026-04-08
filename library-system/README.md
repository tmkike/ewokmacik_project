# Library System

Ez a mappa most mar teljesebben leválasztott mikroszervizes felallasban tartalmazza a konyvtari rendszert:

- `book-service`: a konyvek CRUD es listazo API-ja
- `loan-service`: a kolcsonzesek kezelese
- `api-gateway`: a frontend egyetlen belepesi pontja
- `book-mongodb`: a konyvek sajat adatbazis-szolgaltatasa
- `loan-mongodb`: a kolcsonzesek sajat adatbazis-szolgaltatasa

## Kovetelmenyek

Helyi futtatashoz ezek kellenek:

- Docker Desktop
- Docker Compose
- .NET 8 SDK

## Architektura

A frontend tovabbra is a `http://localhost:3000/api/...` vegpontokat hivja, de a hatter mar kulon adat-es szolgaltatas-hatarokkal fut:

```text
frontend -> api-gateway -> book-service -> book-mongodb
frontend -> api-gateway -> loan-service -> loan-mongodb

book-service <-> loan-service   # HTTP service-to-service kommunikacio
loan-service <-> book-service   # book reserve / release folyamatokhoz
```

A fo valtozas, hogy:

- a `book-service` mar nem olvas a kolcsonzes adatbazisabol
- a `loan-service` mar nem olvas a konyvek adatbazisabol
- a ket service egymas allapotat belso HTTP hivassal kerdezi le vagy modositja
- mindket service sajat MongoDB szolgaltatast kapott

## Inditas Dockerrel

Nyiss terminalt a `library-system` mappaban, majd futtasd:

```bash
docker compose up -d --build --remove-orphans
```

Ezzel elindul:

- a frontend Angular alkalmazas a `4200` porton
- az API gateway a `3000` porton
- a `book-service` a `3001` porton
- a `loan-service` a `3002` porton
- a `book-mongodb` a `27018` porton
- a `loan-mongodb` a `27019` porton

Hasznos cimek:

```text
http://localhost:4200
http://localhost:3000/health
http://localhost:3001/health
http://localhost:3002/health
```

## Helyi fejlesztoi futtatas

Ha nem Dockerbol szeretned inditani a service-eket, kulon-kulon futtasd oket:

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

Alapertelmezett kornyezeti valtozok:

```text
PORT=3001
MONGODB_URI=mongodb://localhost:27018
MONGODB_DB=books
LOAN_SERVICE_URL=http://localhost:3002
```

```text
PORT=3002
MONGODB_URI=mongodb://localhost:27019
MONGODB_DB=loans
BOOK_SERVICE_URL=http://localhost:3001
```

## REST API

A gateway mogott ugyanazok a publikus vegpontok maradtak:

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

A belso service-to-service vegpontok nem a gatewayen mennek at, hanem a Docker halozaton keresztul futnak.

## Seed adatok

A konyvek kezdoadatai a `database/book-seed` mappabol toltodnek be a `book-mongodb` szolgaltatasba.

A `loan-service` kulon ures adatbazissal indul, es a kolcsonzeseket mar csak a sajat `loan-mongodb` adatbazisaban tarolja.

## Kubernetes

A `k8s` mappa most mar a teljes jelenlegi architekturahoz tartalmaz manifesteket:

- `frontend`
- `api-gateway`
- `book-service`
- `loan-service`
- `book-mongodb`
- `loan-mongodb`

A javasolt telepitesi mod:

```bash
kubectl apply -k library-system/k8s
```

Vagy ha mar a `library-system` mappaban vagy:

```bash
kubectl apply -k k8s
```

A `frontend` Kubernetesben is ugyanazon az originen keresztul hivja az API-t, a `/api` utvonalakat az Nginx a clusteren beluli `api-gateway` service fele proxyzza.

Az image-ek, amikre a manifestek mutatnak:

```text
ghcr.io/tmkike/ewokmacik_project-frontend:latest
ghcr.io/tmkike/ewokmacik_project-book-service:latest
ghcr.io/tmkike/ewokmacik_project-loan-service:latest
```

Ha a GHCR csomagok privatkent vannak beallitva, szukseged lesz egy `imagePullSecret`-re is a clusterben. Az ArgoCD alkalmazas manifest mar a teljes `k8s` konyvtarra mutat, es `CreateNamespace=true` sync optiont is kapott.
