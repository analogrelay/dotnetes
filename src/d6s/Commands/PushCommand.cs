using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Dotnetes.CommandLine.Tools;
using McMaster.Extensions.CommandLineUtils;

namespace Dotnetes.CommandLine.Commands
{
    [Command("push", Description = "Push an app to a dotnetes instance.")]
    internal class PushCommand
    {
        [Option("-p|--project <PROJECT_PATH>", Description = "The path to the project. Defaults to the current directory.")]
        public string ProjectPath { get; set; }

        [Option("--keep-temp", Description = "Keep the temporary work directory around.")]
        public bool KeepTempDirectory { get; set; }

        [Option("--local-build", Description = "Build the container using the local docker daemon.")]
        public bool LocalBuild { get; set; }

        [Option("--skip-push", Description = "Skips pushing the image to ACR.")]
        public bool SkipPush { get; set; }

        [Option("--acr", Description = "The name of the ACR registry.")]
        public string Acr { get; set; }

        public async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            var context = DotnetesContext.Default;
            console.WriteLine($"Using dotnetes context: {context.Name}");

            var acr = context.ContainerRegistry;
            if (!String.IsNullOrEmpty(Acr))
            {
                acr = Acr;
            }

            if (!acr.EndsWith(".azurecr.io"))
            {
                acr = $"{acr}.azurecr.io";
            }

            var path = ResolveProjectPath(ProjectPath);
            console.WriteLine($"Preparing to push project: {path} ...");

            // TODO: This is super wrong. We need to use MSBuild to discover this or use it to gen the dockerfile?
            var appName = Path.GetFileNameWithoutExtension(path);
            var appDll = $"{appName}.dll";

            var workDir = Path.Combine(Path.GetTempPath(), $"d6s_{Guid.NewGuid().ToString("N")}");
            try
            {
                // Step 1: Publish the app
                var publishDir = Path.Combine(workDir, "publish");

                var exitCode = await DotNet.Default.PublishAsync(path, outputDirectory: publishDir);
                if (exitCode != 0)
                {
                    console.Error.WriteLine("Failed to publish app!");
                    return 1;
                }

                // Step 2: Make a container
                if (!LocalBuild)
                {
                    console.Error.WriteLine("ACR Build not yet implemented.");
                    return 1;
                }
                // Drop the dockerfile
                var dockerfile = Path.Combine(publishDir, "Dockerfile");
                using (var writer = new StreamWriter(dockerfile))
                {
                    await writer.WriteLineAsync("FROM mcr.microsoft.com/dotnet/core/aspnet:3.1");
                    await writer.WriteLineAsync("ENV ASPNETCORE_URLS http://*:80");
                    await writer.WriteLineAsync("WORKDIR /app");
                    await writer.WriteLineAsync("COPY . /app");
                    await writer.WriteLineAsync($"ENTRYPOINT [ \"dotnet\", \"{appDll}\" ]");
                }

                // Docker build that thing!
                // TODO: Better tagging?
                var containerRef = new ContainerRef($"{acr}/{appName.ToLowerInvariant()}/app", Guid.NewGuid().ToString("N"));
                if (await Docker.Default.BuildAsync(publishDir, tag: containerRef.ToString(), labels: new Dictionary<string, string>()
                {
                    { "dotnetes", "1" }
                }) != 0)
                {
                    console.Error.WriteLine("Failed to build app image.");
                    return 1;
                }

                // Step 3: Publish to ACR
                if (!SkipPush)
                {
                    if (await Docker.Default.PushAsync(containerRef.ToString()) != 0)
                    {
                        console.Error.WriteLine("Failed to publish image to dotnetes.");
                        return 1;
                    }
                }

                // Step 4: Push the deployment to K8s
                var k8sAppName = MakeK8sSafe(appName);

                // Step 4.1: Drop all the k8s content to a directory
                var k8sDir = Path.Combine(workDir, "k8s");
                if (!Directory.Exists(k8sDir))
                {
                    Directory.CreateDirectory(k8sDir);
                }

                var k8sFiles = new[] { "deployment.yaml", "service.yaml", "ingress.yaml" };
                foreach (var k8sFile in k8sFiles)
                {
                    await ResourceHelper.DropResourceFileAsync(k8sFile, Path.Combine(k8sDir, k8sFile));
                }

                // Step 4.2: Generate kustomization.yaml and patch for ingress
                using (var writer = new StreamWriter(Path.Combine(k8sDir, "patch-ingress.yaml")))
                {
                    await writer.WriteLineAsync("- op: replace");
                    await writer.WriteLineAsync("  path: /spec/rules/0/http/paths/0/path");
                    await writer.WriteLineAsync($"  value: /{k8sAppName}");
                }

                await GenerateKustomizationAsync(
                    Path.Combine(k8sDir, "kustomization.yaml"),
                    context.Namespace,
                    k8sAppName,
                    containerRef,
                    resources: k8sFiles,
                    patchesJson6902: new[] { 
                        new KustomizePatch(
                            group: "networking.k8s.io",
                            version: "v1beta1",
                            kind: "Ingress",
                            name: "ingress",
                            path: "patch-ingress.yaml")
                    });

                // Step 4.3: Apply!
                console.WriteLine("Applying Kubernetes Resources...");
                await Kubectl.Default.ApplyKustomizationAsync(k8sDir);

                var oldFg = console.ForegroundColor;
                try
                {
                    console.ForegroundColor = ConsoleColor.Green;
                    console.WriteLine($"Deployed to Kubernetes! Use 'kubectl get deployment -w --namespace={context.Namespace}' and wait for deployments to be ready");
                    console.WriteLine($"Your service will be available at 'http://{context.DnsName}/{k8sAppName}'");
                }
                finally
                {
                    console.ForegroundColor = oldFg;
                }
            }
            finally
            {
                if (!KeepTempDirectory && Directory.Exists(workDir))
                {
                    try
                    {
                        Directory.Delete(workDir, recursive: true);
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // Don't worry about it.
                    }
                }
            }
            return 0;
        }

        private async Task GenerateKustomizationAsync(string path, string ns, string appName, ContainerRef container, IReadOnlyList<string> resources, IReadOnlyList<KustomizePatch> patchesJson6902)
        {
            await using var writer = new StreamWriter(path);
            await writer.WriteLineAsync("apiVersion: kustomize.config.k8s.io/v1beta1");
            await writer.WriteLineAsync("kind: Kustomization");
            await writer.WriteLineAsync($"namespace: {ns}");
            await writer.WriteLineAsync("images:");
            await writer.WriteLineAsync("  - name: appcontainer # match images with this name");
            await writer.WriteLineAsync($"    newTag: {container.Tag} # override the tag");
            await writer.WriteLineAsync($"    newName: {container.Repository} # override the name");
            await writer.WriteLineAsync("commonLabels:");
            await writer.WriteLineAsync($"    \"cloud.dot.net/app\": {appName} # Kustomize will replace this");
            await writer.WriteLineAsync($"namePrefix: {appName}-");
            if (resources.Count > 0)
            {
                await writer.WriteLineAsync("resources:");
                foreach (var resource in resources)
                {
                    await writer.WriteLineAsync($"  - {resource}");
                }
            }
            if (patchesJson6902.Count > 0)
            {
                await writer.WriteLineAsync("patchesJson6902:");
                foreach (var patch in patchesJson6902)
                {
                    await writer.WriteLineAsync("  - target:");
                    await writer.WriteLineAsync($"      group: {patch.Group}");
                    await writer.WriteLineAsync($"      version: {patch.Version}");
                    await writer.WriteLineAsync($"      kind: {patch.Kind}");
                    await writer.WriteLineAsync($"      name: {patch.Name}");
                    await writer.WriteLineAsync($"    path: {patch.Path}");
                }
            }
        }

        private string MakeK8sSafe(string appName)
        {
            return appName.ToLowerInvariant();
        }

        private static string ResolveProjectPath(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                projectPath = Directory.GetCurrentDirectory();
            }

            if (!File.Exists(projectPath))
            {
                // Try to resolve the default project
                if (!Directory.Exists(projectPath))
                {
                    throw new CommandLineException($"Project does not exist: {projectPath}");
                }

                projectPath = FindProject(projectPath);
            }

            return projectPath;
        }

        private static string FindProject(string projectPath)
        {
            foreach (var file in Directory.EnumerateFiles(projectPath))
            {
                var extension = Path.GetExtension(file);
                if (extension.EndsWith("proj"))
                {
                    return file;
                }
            }
            throw new CommandLineException($"Could not find any project files in {projectPath}");
        }
    }

    internal struct ContainerRef
    {
        public ContainerRef(string repository, string tag)
        {
            Repository = repository;
            Tag = tag;
        }

        public string Repository { get; }
        public string Tag { get; }

        public override string ToString() => $"{Repository}:{Tag}";

    }

    internal struct KustomizePatch
    {
        public KustomizePatch(string group, string version, string kind, string name, string path)
        {
            Group = group;
            Version = version;
            Kind = kind;
            Name = name;
            Path = path;
        }

        public string Group { get; }
        public string Version { get; }
        public string Kind { get; }
        public string Name { get; }
        public string Path { get; }
    }
}
