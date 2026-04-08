# LibraryAdmin Backend

ASP.NET Core backend projekt egy könyvtári adminisztrációs rendszerhez, külön fókuszban a könyvkölcsönzés kezeléssel.

Ez a backend az `ewokmacik_project-main` projekthez lett igazítva, ahol:

- a frontend külön `frontend` mappában van
- a MongoDB infrastruktúra a `library-system` mappában van
- a Mongo adatbázis neve `library`
- a seedelt könyvek a `books` collectionbe kerülnek

## Funkciók

- könyvek listázása, lekérése, létrehozása, módosítása, törlése
- kölcsönzés indítása
- visszahozás rögzítése
- aktív kölcsönzések listázása
- üzleti szabály: egy könyv egyszerre csak egy aktív kölcsönzésben szerepelhet

## Projektstruktúra

- `source/Domain`: domain modellek
- `source/DataAccess`: MongoDB kapcsolat
- `source/WebApi`: minimal API és üzleti logika
- `source/WebApiTests`: teszt projekt helye

## Futtatás

1. Indítsd el az `ewokmacik_project-main/library-system` alatti MongoDB-t:

```powershell
docker compose up -d
```

2. Restore és build:

```powershell
dotnet restore .\LibraryAdmin.sln --configfile .\NuGet.Config
dotnet build .\LibraryAdmin.sln --no-restore
```

3. Indítás:

```powershell
dotnet run --project .\source\WebApi\WebApi.csproj
```

4. HTTP minták:

`source/WebApi/WebApi.http`

## Beemelés az ewokmacik projektbe

Ajánlott repo szerkezet:

```text
ewokmacik_project-main/
  frontend/
  library-system/
  backend/
```

Ebben a mappában lévő teljes backend tartalom mehet a repo `backend` mappájába.

## Mongo kompatibilitás

- adatbázis: `library`
- könyvek collection: `books`
- kölcsönzések collection: `loans`

## Fontos megjegyzés

A projekt .NET 9-re készült, mert ezen a gépen ez érhető el. A szerkezet kompatibilis marad egy későbbi .NET 10 frissítéssel.
