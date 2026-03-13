## Continuous Deployment with ArgoCD

The project supports Continuous Deployment using ArgoCD.

ArgoCD monitors the GitHub repository and automatically deploys the Kubernetes manifests located in the `k8s` folder.

Deployment steps:

1. Install ArgoCD in the Kubernetes cluster
2. Connect ArgoCD to this GitHub repository
3. Apply the ArgoCD application manifest:

kubectl apply -f k8s/argocd-application.yaml

ArgoCD will automatically synchronize the application and deploy the MongoDB service.
