## Continuous Deployment with ArgoCD

Ez a projekt ArgoCD-vel támogatja a folyamatos telepítést.

Az ArgoCD a GitHub repót figyeli, és a `library-system/k8s` mappában található Kubernetes manifesteket szinkronizálja a klaszterbe.

## Mit telepít az ArgoCD?

Az alkalmazásmanifest nem csak MongoDB-t telepít, hanem a teljes stacket:

- frontend
- api-gateway
- book-service
- loan-service
- book-mongodb
- loan-mongodb

Cél namespace:

```text
library-system
```

## Előfeltételek

- működő Kubernetes klaszter
- telepített ArgoCD
- `kubectl` hozzáférés a klaszterhez
- hozzáférés ehhez a GitHub repositoryhoz

## Telepítési lépések

1. Telepítsd fel kézzel az ArgoCD-t a klaszterbe.
2. Ellenőrizd, hogy az ArgoCD komponensei rendben futnak az `argocd` namespace-ben.
3. Alkalmazd az ArgoCD application manifestet:

```bash
kubectl apply -f k8s/argocd-application.yaml
```

4. Ellenőrizd, hogy létrejött a `library-system` alkalmazás az ArgoCD-ben.
5. Várd meg, amíg az alkalmazás `Synced` és `Healthy` állapotba kerül.

## Szinkronizálási viselkedés

Az alkalmazásmanifest automatikus szinkronizálást használ:

- `prune: true`
- `selfHeal: true`
- `CreateNamespace=true`

Ez azt jelenti, hogy az ArgoCD:

- automatikusan létrehozza a cél namespace-et
- eltávolítja a már nem használt erőforrásokat
- visszaállítja az eltért állapotot a Gitben tárolt konfigurációra

## Fontos megjegyzés

Az ArgoCD a `latest` tagre mutató image-eket használó Kubernetes manifesteket telepíti. Emiatt a CD folyamat akkor működik helyesen, ha a CI workflow már feltöltötte a friss image-eket a GHCR-be.
