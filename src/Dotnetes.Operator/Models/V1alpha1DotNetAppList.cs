using System.Collections.Generic;
using k8s;
using k8s.Models;
using Newtonsoft.Json;

namespace Dotnetes.Operator.Models
{
    public class V1alpha1DotNetAppList: KubernetesObject
    {
        [JsonProperty("items")]
        public IList<V1alpha1DotNetApp> Items { get; set; }
        [JsonProperty("metadata")]
        public V1ListMeta Metadata { get; set; }
    }
}
