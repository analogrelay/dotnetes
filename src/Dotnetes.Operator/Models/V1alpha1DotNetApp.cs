using k8s;
using k8s.Models;
using Newtonsoft.Json;

namespace Dotnetes.Operator.Models
{
    public class V1alpha1DotNetApp: KubernetesObject
    {
        [JsonProperty("metadata")]
        public V1ObjectMeta Metadata { get; set; }
        [JsonProperty("spec")]
        public V1alpha1DotNetAppSpec Spec { get; set; }
    }
}