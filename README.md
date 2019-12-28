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

1. `dotnet new web` somewhere
1. `cd` to the new app
1. Run `d6s push --acr [YOUR ACR INSTANCE NAME] --local-build`

The command will:
* Locally publish the app as a framework-dependent app
* Generate a docker image for your app with a random unique tag
* Generate Kubernetes YAML descriptions:
    * A Deployment that creates 3 replicas of a pod containing only your app container
    * A Service referencing those pods
    * An Ingress making that service available at `/[k8s-safe project name]`

## Small TODOS
* Allow customizing ingress path
* Allow customizing app name, it must be "DNS safe" (alphanumeric, `-` and `.`)
* Allow remote build on ACR (right now the `--local-build` switch is required)
* Config management. Right now it's mostly hardcoded in `DotnetesContext` but the idea is that type is loaded from some project/user/environment-level config.

## Big TODOS
* Build a service that can handle the whole thing? I.e. just publish the code and the rest happens in the ☁ Cloud ☁!
* Add support for more k8s customization (customizing the podspec, adding other resources, etc.)
