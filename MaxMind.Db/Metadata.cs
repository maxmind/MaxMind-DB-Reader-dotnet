#region

using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Data about the database file itself
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Metadata
    {
        /// <summary>
        ///     The major version number for the MaxMind DB binary format used by the database.
        /// </summary>
        [JsonProperty("binary_format_major_version")]
        public int BinaryFormatMajorVersion { get; private set; }

        /// <summary>
        ///     The minor version number for the MaxMind DB binary format used by the database.
        /// </summary>
        [JsonProperty("binary_format_minor_version")]
        public int BinaryFormatMinorVersion { get; private set; }

        [JsonProperty("build_epoch")]
        internal long BuildEpoch { get; private set; }

        /// <summary>
        ///     The date-time of the database build.
        /// </summary>
        [JsonIgnore]
        public DateTime BuildDate => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(BuildEpoch);

        /// <summary>
        ///     The MaxMind DB database type.
        /// </summary>
        [JsonProperty("database_type")]
        public string DatabaseType { get; private set; }

        /// <summary>
        ///     A map from locale codes to the database description in that language.
        /// </summary>
        [JsonProperty("description")]
        public Hashtable Description { get; private set; }

        /// <summary>
        ///     The IP version that the database supports. This will be 4 or 6.
        /// </summary>
        [JsonProperty("ip_version")]
        public int IPVersion { get; private set; }

        /// <summary>
        ///     A list of locale codes for languages that the database supports.
        /// </summary>
        [JsonProperty("languages")]
        public List<string> Languages { get; private set; }

        [JsonProperty("node_count")]
        internal int NodeCount { get; set; }

        [JsonProperty("record_size")]
        internal int RecordSize { get; set; }

        [JsonIgnore]
        internal int NodeByteSize => RecordSize / 4;

        internal int SearchTreeSize => NodeCount * NodeByteSize;
    }
}