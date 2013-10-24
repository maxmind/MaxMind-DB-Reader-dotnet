using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MaxMind.DB
{
    /// <summary>
    /// Data about the database file itself
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    internal class Metadata
    {
        [JsonProperty("binary_format_major_version")]
        internal int BinaryFormatMajorVersion { get; set; }

        [JsonProperty("binary_format_minor_version")]
        internal int BinaryFormatMinorVersion { get; set; }

        [JsonProperty("build_epoch")]
        internal long BuildEpoch { get; set; }

        [JsonIgnore]
        internal DateTime Build
        {
            get
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(BuildEpoch);
            }
        }

        [JsonProperty("database_type")]
        internal string DatabaseType { get; set; }

        [JsonProperty("description")]
        internal Hashtable Description { get; set; }

        [JsonProperty("ip_version")]
        internal int IpVersion { get; set; }

        [JsonProperty("languages")]
        internal List<string> Languages { get; set; }

        [JsonProperty("node_count")]
        internal int NodeCount { get; set; }

        [JsonProperty("record_size")]
        internal int RecordSize { get; set; }

        [JsonIgnore]
        internal int NodeByteSize
        {
            get
            {
                return RecordSize / 4;
            }
        }

        internal int SearchTreeSize
        {
            get
            {
                return NodeCount * this.NodeByteSize;
            }
        }
    }
}
