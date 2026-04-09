# Felhasználói kézikönyv

Ez a rövid útmutató bemutatja, hogyan használd a könyvtári rendszert és annak fő funkcióit.

## 1. Belépés a rendszerbe

Miután a rendszer fut, nyisd meg a frontend alkalmazást a böngésződben:

```text
http://localhost:4200
```

A frontend az API gatewayen keresztül kommunikál a backend szolgáltatásokkal.

## 2. Könyvek kezelése

A rendszer egyik fő része a könyvek kezelése. A könyvoldalon a következő műveletek érhetők el:

- könyvek listázása
- új könyv hozzáadása
- meglévő könyv részleteinek megtekintése
- könyv módosítása
- könyv elérhetőségének (availability) frissítése
- könyv törlése

### 2.1. Könyvlista

A főoldalon vagy a `Books` menüpont alatt a rendszer megjeleníti az összes könyvet. Minden könyv mellett látható:

- címe
- szerzője
- állapota
- elérhetősége

### 2.2. Új könyv hozzáadása

Új könyvet az `Add` oldalon adhatod hozzá. Add meg a szükséges mezőket:

- cím
- szerző
- kiadás éve
- kategória
- elérhetőség

A mentés után a könyv megjelenik a könyvek listájában.

### 2.3. Könyv részletei és módosítása

A könyvre kattintva megnyílik a részletes nézet. Itt szerkesztheted a könyv adatait és frissítheted az elérhetőségét.

### 2.4. Könyv törlése

A könyv részleteinél vagy a listában elérhető a törlés gomb. Figyelj arra, hogy a törlés után az adott könyv nem lesz többé elérhető a rendszerben.

## 3. Kölcsönzések kezelése

A kölcsönzéseket a könyv részleteit megjelenítő oldalon kezelheted, miután kiválasztottad a könyvet a listából.

A rendszer az alábbi kölcsönzési műveleteket támogatja:

- új kölcsönzés indítása egy elérhető könyvhöz
- aktív kölcsönzés lezárása (visszavétel)
- aktív kölcsönzés adatainak olvasása

### 3.1. Új kölcsönzés létrehozása

Új kölcsönzést a könyv részletei oldalon indíthatsz el. Add meg a következő adatokat:

- kölcsönző neve
- kölcsönző e-mail-címe
- határidő
- megjegyzés

### 3.2. Kölcsönzés lezárása

Ha egy kölcsönzött könyvet visszahoznak, válaszd a visszavétel funkciót az adott kölcsönzésnél. Ezzel a könyv elérhetővé válik újra a könyvtár számára.

## 4. Rendszer működése

A felhasználói élmény két fő szolgáltatásra épül:

- `book-service`: könyvek adatai és elérhetősége
- `loan-service`: kölcsönzési adatok és státuszok

A frontend az API gatewayen keresztül kommunikál mindkettővel, így a felület mindig friss adatokat mutat.

## 5. Funkciók áttekintése

A rendszer általános funkciói:

- könyvek listázása és részletezése
- könyvadatok létrehozása, szerkesztése és törlése
- könyv elérhetőségének kezelése
- kölcsönzések létrehozása és lezárása
- aktív kölcsönzések nyomon követése

## 6. Tippek és javaslatok

- Ha egy könyv elérhetősége `nem elérhető`, az általában azt jelenti, hogy a könyvet jelenleg kikölcsönözték.
- Ha a könyv nincs elérhető állapotban, de nincs aktív kölcsönzés hozzá, akkor a könyv egyszerűen nem elérhető.
- Könyv törlése előtt győződj meg róla, hogy nincs aktív kölcsönzéshez kapcsolva.
- Használj egyértelmű szerzői és cím mezőket a könnyebb kereséshez.

## 7. Gyakori hibák

- Ha a frontend nem jelenik meg, ellenőrizd, hogy a `frontend` és az `api-gateway` szolgáltatások futnak.
- Ha a kölcsönzés nem jön létre, ellenőrizd az `loan-service` logjait és a backend elérhetőségét.
- Ha könyvlista nem töltődik, ellenőrizd az API gateway és a book-service kapcsolatát.

## 8. Kapcsolódó beállítások

A `library-system/k8s` mappában találhatók a Kubernetes manifestek, illetve a `library-system/database/book-seed` könyvtárban a könyv seed adatok.

A rendszer használata során ezek a fájlok fontosak lehetnek, ha módosítani szeretnéd az alapértelmezett telepítést vagy az induló adatokat.
