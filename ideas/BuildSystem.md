# D6S: Build System

## Goals

* Take an existing .NET Core app and, with minimal-to-no additional configuration, appropriately configure k8s to host it
* Allow a natural (to .NET developers) way to configure additional infrastructure

## Opinions

* MSBuild is the way. But we're not wedded to the syntax, XML files, etc. It integrates best with existing tooling
* .NET developers can understand containers, but we should provide good defaults

## Cloud Project Metadata

We configure projects in `.csproj`, so the cloud-related metadata should be no different. MSBuild items,properties, etc. in the csproj are used to configure the app:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>netcoreapp3.1</TargetFramework>
  </PropertyGroup>

</Project>

```

## Gestures

### Produce a Docker image from a .NET app

```shell
$ dotnet cloud pack
```

1. Locally publishes a Framework-Dependent Deployment of the app
2. Selects an appropriate base image based on TFM, SDK and FrameworkReferences:
    * Example: `netcoreapp3.1` + `Microsoft.AspNetCore.App` => `mcr.microsoft.com/dotnet/core/aspnet:3.1`
3. Copies the published app into the image, basically a Dockerfile like:

```dockerfile
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1
WORKDIR /app
COPY . /app
ENTRYPOINT [ "dotnet", "./[APPNAME]" ]
```

4. Generates a tag for the image based on the hash of the inputs?

#### Open Question
* Perhaps this can be done without an active Docker daemon? Glenn did a thing that builds OCI images natively?

### Generate Helm Chart from a .NET app

**NOTE**: The use of a Helm Chart is just a convenient way to produce a set of K8s resources. It could just create some raw YAML.

```shell
$ dotnet cloud generate
```

The generated chart is parameterized on the specific image name (since it differs from build to build). It contains:

1. A Deployment that launches the app container image
1. A `LoadBalancer` Service that directs HTTP traffic
1. An Ingress mapping (if configured) to route.