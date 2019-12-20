# Native .NET Hosting with Kubernetes

This repo is a prototype of tools for native .NET Hosting in Kubernetes.

## Pushing an app

Pre-requisites:
* Azure Container Registry
    * Must be logged in to docker with it
* Azure Kubernetes Service
    * Must be the *current* context in `kubectl`
* Local docker daemon configured
* Built the `src/d6s` project and have it somewhere (or use the helper `d6s.ps1` script in this repo that does `dotnet run`)
* `kubectl apply -f .\deploy\dotnetapp.crd.yaml` in this repo (temporary)

1. `dotnet new web` somewhere
1. `cd` to the new app
1. Run `d6s push --acr [YOUR ACR INSTANCE NAME] --local-build`

For now, all this does is:
* Publish the app
* Build a docker image from it
* Push it to your ACR instance
* Publish a new CRD to the AKS instance for the "app"
    * You can view it with `kubectl get dotnetapp`

## TODO:
* Include instructions to deploy the operator
* Have the operator actually do something useful with the CRD (maintain Deployments, etc.)