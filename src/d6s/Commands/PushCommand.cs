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
            if(!String.IsNullOrEmpty(Acr))
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
                var dockerfileContent = await ResourceHelper.ReadResourceFileAsync("Default.dockerfile");
                dockerfileContent = dockerfileContent.Replace("!APPDLL!", appDll);
                await File.WriteAllTextAsync(dockerfile, dockerfileContent);

                // Docker build that thing!
                // TODO: Better tagging?
                var tag = $"{acr}/{appName.ToLowerInvariant()}/app:{Guid.NewGuid().ToString("N")}";
                if (await Docker.Default.BuildAsync(publishDir, tag: tag, labels: new Dictionary<string, string>()
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
                    if (await Docker.Default.PushAsync(tag) != 0)
                    {
                        console.Error.WriteLine("Failed to publish image to dotnetes.");
                        return 1;
                    }
                }

                // Step 4: Push the deployment to K8s
                var k8sAppName = MakeK8sSafe(appName);
                var dotnetAppContent = await ResourceHelper.ReadResourceFileAsync("dotnetapp.yml");
                dotnetAppContent = dotnetAppContent
                    .Replace("!APPNAME!", k8sAppName)
                    .Replace("!APPIMAGE!", tag);
                var dotnetAppYaml = Path.Combine(publishDir, "deploy.app.yml");
                await File.WriteAllTextAsync(dotnetAppYaml, dotnetAppContent);
                if (await Kubectl.Default.ApplyAsync(dotnetAppYaml) != 0)
                {
                    console.Error.WriteLine("Failed to push app to dotnetes.");
                    return 1;
                }

                var oldFg = console.ForegroundColor;
                console.ForegroundColor = ConsoleColor.Green;
                var message = $"Your app '{k8sAppName}' has been pushed. Use 'kubectl get dotnetapps -w' to view the current status as it starts.";
                var banner = new string('=', message.Length);
                console.WriteLine(banner);
                console.WriteLine(message);
                console.WriteLine(banner);
                console.ForegroundColor = oldFg;
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
}
