using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace MaxMind.MaxMindDb
{
    public class Location
    {
        [JsonProperty("latitude")]
        public double Latitude { get; set; }

        [JsonProperty("longitude")]
        public double Longitude { get; set; }

        [JsonProperty("metro_code")]
        public string MetroCode { get; set; }

        [JsonProperty("time_zone")]
        public string TimeZone { get; set; }
    }
}
