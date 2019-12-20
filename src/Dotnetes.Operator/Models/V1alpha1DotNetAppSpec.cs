using Newtonsoft.Json;

namespace Dotnetes.Operator.Models
{
    public class V1alpha1DotNetAppSpec
    {
        [JsonProperty("image")]
        public string Image { get; set; }

        [JsonProperty("replicas")]
        public int Replicas { get; set; }
    }
}