using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace MaxMind.MaxMindDb
{
    public class Response
    {
        [JsonProperty("city")]
        public City City { get; set; }

        [JsonProperty("continent")]
        public Region Continent { get; set; }

        [JsonProperty("country")]
        public Country Country { get; set; }

        [JsonProperty("location")]
        public Location Location { get; set; }

        [JsonProperty("registered_country")]
        public Country RegisteredCountry { get; set; }

        [JsonProperty("subdivisions")]
        public List<Region> Subdivisions { get; set; }
    }
}
