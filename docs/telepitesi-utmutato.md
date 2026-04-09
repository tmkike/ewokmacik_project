# Telepítési útmutató helyi Kubernetes klaszterre

Ez az útmutató bemutatja, hogyan telepíthető a projekt a `library-system/k8s` Kubernetes manifestjei segítségével egy helyi klaszterre.

## 1. Előkészítés

A helyi telepítéshez ezek szükségesek:

- `kubectl` telepítve és a klaszterhez kapcsolódva
- Docker Desktop Kubernetes-szel, Minikube vagy Kind
- Docker (ha a helyi image-eket szeretnéd felépíteni és betölteni)
- opcionálisan `.NET 8 SDK` a szolgáltatások helyi buildeléséhez

## 2. Környezet indítása

### Docker Desktop esetén

1. Nyisd meg a Docker Desktop beállításait.
2. Engedélyezd a Kubernetes-t.
3. Várj, amíg a klaszter állapota `Running` lesz.

### Minikube esetén

```bash
minikube start --driver=docker
```

### Kind esetén

```bash
kind create cluster
```

## 3. Image-ek előkészítése

A `library-system/k8s` manifestek alapértelmezés szerint a következő image-ekre mutatnak:

- `ghcr.io/tmkike/ewokmacik_project-frontend:latest`
- `ghcr.io/tmkike/ewokmacik_project-book-service:latest`
- `ghcr.io/tmkike/ewokmacik_project-loan-service:latest`

Ha a saját gépeden építed az image-eket, használj azonos tageket, vagy módosítsd a manifesteket a lokális image nevekkel.

### 3.1. Helyi image-ek építése

A repository gyökérkönyvtárából futtasd:

```bash
docker build -t ghcr.io/tmkike/ewokmacik_project-frontend:latest ./frontend

docker build -t ghcr.io/tmkike/ewokmacig_project-book-service:latest ./library-system/book-service

docker build -t ghcr.io/tmkike/ewokmacik_project-loan-service:latest ./library-system/loan-service
```

### 3.2. Image betöltése a klaszterbe

- Docker Desktop esetén általában nincs további lépés szükséges.
- Minikube esetén:

```bash
minikube image load ghcr.io/tmkike/ewokmacik_project-frontend:latest
minikube image load ghcr.io/tmkike/ewokmacik_project-book-service:latest
minikube image load ghcr.io/tmkike/ewokmacik_project-loan-service:latest
```

- Kind esetén:

```bash
kind load docker-image ghcr.io/tmkike/ewokmacik_project-frontend:latest
kind load docker-image ghcr.io/tmkike/ewokmacik_project-book-service:latest
kind load docker-image ghcr.io/tmkike/ewokmacik_project-loan-service:latest
```

## 4. Kubernetes manifestek telepítése

A telepítéshez használd a `kustomization.yaml`-t a `library-system/k8s` mappában. Navigálj a megfelelő könyvtárba és futtasd:

```bash
cd library-system/k8s
kubectl apply -k .
```

Ez a parancs létrehozza:

- `library-system` namespace-et
- MongoDB persistent volume claim-eket és deploymenteket
- könyv- és kölcsönzés szolgáltatásokat
- API gateway-et és frontend-et

## 5. Szükséges szolgáltatások és portok

A telepítés után az alábbi szolgáltatások és portok érhetők el:

- Frontend: `http://localhost:4200`
- API gateway: `http://localhost:3000`
- Book-service: `http://localhost:3001`
- Loan-service: `http://localhost:3002`

> Figyelem: a szolgáltatások által használt portok csak akkor érhetők el a helyi gépen, ha a `Service` típus `NodePort` vagy `LoadBalancer` és a klaszter biztosít hozzáférést. Docker Desktop Kubernetes esetén általában elérhető a `localhost`.

## 6. Ellenőrzés

Ellenőrizd, hogy a podok futnak:

```bash
kubectl get pods -n library-system
```

A szolgáltatások listája:

```bash
kubectl get svc -n library-system
```

## 7. Hibakeresés

- Nézd meg a podok eseményeit:

```bash
kubectl describe pod <pod-név> -n library-system
```

- Ellenőrizd a naplókat:

```bash
kubectl logs <pod-név> -n library-system
```

- Ha a manifestekben más image neveket használsz, frissítsd azokat a deploy fájlokban, vagy adj meg helyes lokális image neveket.

## 8. Tisztítás

A teljes rendszer eltávolításához futtasd:

```bash
kubectl delete -k .
kubectl delete namespace library-system
```
