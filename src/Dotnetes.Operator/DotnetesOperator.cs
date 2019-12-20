using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Dotnetes.Operator.Models;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Newtonsoft.Json.Linq;

namespace Dotnetes.Operator
{
    internal class DotnetesOperator
    {
        private readonly IOptions<KubernetesOptions> _k8SOptions;
        private readonly ILogger<DotnetesOperator> _logger;
        private readonly Kubernetes _k8s;

        public DotnetesOperator(IOptions<KubernetesOptions> k8sOptions, ILogger<DotnetesOperator> logger)
        {
            _k8SOptions = k8sOptions;
            _logger = logger;

            // Initialize k8s
            _k8s = InitializeKubernetesClient();
        }

        public async Task RunAsync()
        {
            // Dump all namespaces
            var namespaces = await _k8s.ListNamespaceAsync();

            foreach (var ns in namespaces.Items)
            {
                await RunForNamespaceAsync(ns);
            }
        }

        private async Task RunForNamespaceAsync(V1Namespace ns)
        {
            _logger.LogTrace(new EventId(0, "ScanningNamespace"), "Scanning {Namespace} for relevant resources...", ns.Metadata.Name);

            var appList = (
                (JObject)(await _k8s.ListNamespacedCustomObjectAsync(Constants.ApiGroup, Constants.ApiVersion, ns.Metadata.Name, Constants.DotNetAppPlural)))
                .ToObject<V1alpha1DotNetAppList>();

            foreach (var app in appList.Items)
            {
                _logger.LogDebug(new EventId(0, "ScanningApp"), "Scanning app {AppName} for changes...", app.Metadata.Name);

                await UpdateDeployment(ns, app);
                await UpdateService(ns, app);
            }
        }

        private async Task UpdateService(V1Namespace ns, V1alpha1DotNetApp app)
        {
            // Check for the relevant service

            // TODO: Use label selectors
            var serviceName = $"{app.Metadata.Name}-d6s";

            V1Service service;
            try
            {
                service = await _k8s.ReadNamespacedServiceAsync(serviceName, ns.Metadata.Name);
            }
            catch (HttpOperationException hopex) when (hopex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                service = null;
            }

            var target = CreateTargetService(app);

            if (service == null)
            {
                _logger.LogDebug(new EventId(0, "CreatingService"), "Creating new service");
                await _k8s.CreateNamespacedServiceAsync(target, ns.Metadata.Name);
            }
            else
            {
                // TODO: Patch it? Detect changes?
                _logger.LogDebug(new EventId(0, "ServiceAlreadyExists"), "Service already exists. TODO: Update it.");
            }
        }

        private async Task UpdateDeployment(V1Namespace ns, V1alpha1DotNetApp app)
        {
            // Check for the relevant deployment

            // TODO: Use label selectors
            var deploymentName = $"{app.Metadata.Name}-d6s";

            V1Deployment deployment;
            try
            {
                deployment = await _k8s.ReadNamespacedDeploymentAsync(deploymentName, ns.Metadata.Name);
            }
            catch (HttpOperationException hopex) when (hopex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                deployment = null;
            }

            var target = CreateTargetDeployment(app);

            if (deployment == null)
            {
                _logger.LogDebug(new EventId(0, "CreatingDeployment"), "Creating new deployment");
                await _k8s.CreateNamespacedDeploymentAsync(target, ns.Metadata.Name);
            }
            else
            {
                // TODO: Patch it? Detect changes?
                _logger.LogDebug(new EventId(0, "DeploymentAlreadyExists"), "Deployment already exists. TODO: Update it.");
            }
        }

        private V1Deployment CreateTargetDeployment(V1alpha1DotNetApp app)
        {
            var labels = CreateLabels(app);
            var podSelector = new V1LabelSelector(matchLabels: labels);
            var container = new V1Container(
                name: $"{app.Metadata.Name}-app",
                image: app.Spec.Image);
            var podSpec = new V1PodSpec(new[] { container });
            var podTemplate = new V1PodTemplateSpec(spec: podSpec, metadata: new V1ObjectMeta(labels: labels));

            return new V1Deployment(
                apiVersion: $"{V1Deployment.KubeGroup}/{V1Deployment.KubeApiVersion}",
                kind: V1Deployment.KubeKind,
                metadata: new V1ObjectMeta(name: $"{app.Metadata.Name}-d6s", labels: labels),
                spec: new V1DeploymentSpec(selector: podSelector, template: podTemplate));
        }

        private V1Service CreateTargetService(V1alpha1DotNetApp app)
        {
            var labels = CreateLabels(app);

            return new V1Service(
                apiVersion: V1Service.KubeApiVersion,
                kind: V1Service.KubeKind,
                metadata: new V1ObjectMeta(name: $"{app.Metadata.Name}-d6s", labels: labels),
                spec: new V1ServiceSpec(
                    selector: labels,
                    type: "ClusterIP",
                    ports: new[] {
                        new V1ServicePort(80, protocol: "TCP")
                    }));
        }

        private static Dictionary<string, string> CreateLabels(V1alpha1DotNetApp app)
        {
            return new Dictionary<string, string>()
            {
                { "dotnetes.dot.net/dotnetes", "1" },
                { "dotnetes.dot.net/app", app.Metadata.Name }
            };
        }

        private Kubernetes InitializeKubernetesClient()
        {
            var config = _k8SOptions.Value.ClusterAuthentication switch
            {
                ClusterAuthenticationMode.LocalConfigFile => KubernetesClientConfiguration.BuildConfigFromConfigFile(_k8SOptions.Value.ConfigFilePath),
                ClusterAuthenticationMode.InCluster => InClusterConfig(),
                var x => throw new InvalidOperationException($"Unknown Cluster Authentication type: {x}"),
            };
            _logger.LogDebug(
                new EventId(0, "KubernetesConfiguration"),
                "Using Kubernetes Cluster at {Host}, Authenticated as {Username}",
                config.Host, config.Username);
            return new Kubernetes(config);
        }

        private const string ServiceAccountPath = "/var/run/secrets/kubernetes.io/serviceaccount";
        private const string ServiceAccountTokenKeyFileName = "token";
        private const string ServiceAccountRootCAKeyFileName = "ca.crt";
        private static KubernetesClientConfiguration InClusterConfig()
        {
            var token = File.ReadAllText(Path.Combine(ServiceAccountPath, ServiceAccountTokenKeyFileName));
            var rootCAFile = Path.Combine(ServiceAccountPath, ServiceAccountRootCAKeyFileName);
            var host = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
            var port = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_PORT");

            return new KubernetesClientConfiguration
            {
                Host = new UriBuilder("https", host, Convert.ToInt32(port)).ToString(),
                AccessToken = token,
                SslCaCerts = CertUtils.LoadPemFileCert(rootCAFile)
            };
        }
    }
}
