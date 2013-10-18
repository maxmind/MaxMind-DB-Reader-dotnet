using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace MaxMind.MaxMindDb
{
    /// <summary>
    /// Data about the database file itself
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Metadata
    {
        [JsonProperty("binary_format_major_version")]
        public int BinaryFormatMajorVersion { get; set; }

        [JsonProperty("binary_format_minor_version")]
        public int BinaryFormatMinorVersion { get; set; }

        [JsonProperty("build_epoch")]
        public long BuildEpoch { get; set; }

        [JsonIgnore]
        public DateTime Build
        {
            get
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(this.BuildEpoch);
            }
        }

        [JsonProperty("database_type")]
        public string DatabaseType { get; set; }

        [JsonProperty("description")]
        public Hashtable Description { get; set; }

        [JsonProperty("ip_version")]
        public int IpVersion { get; set; }

        [JsonProperty("languages")]
        public List<string> Languages { get; set; }

        [JsonProperty("node_count")]
        public long NodeCount { get; set; }

        [JsonProperty("record_size")]
        public long RecordSize { get; set; }

        [JsonIgnore]
        public long NodeByteSize
        {
            get
            {
                return this.RecordSize / 4;
            }
        }

        public long SearchTreeSize
        {
            get
            {
                return this.NodeCount * this.NodeByteSize;
            }
        }
    }
}
