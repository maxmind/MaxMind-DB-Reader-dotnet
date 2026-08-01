namespace MaxMind.Db.NetStandard.TestModels
{
    /// <summary>
    ///     A generated model used to verify .NET Standard package consumers.
    /// </summary>
    public sealed class NetStandardModel
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="NetStandardModel"/> class.
        /// </summary>
        /// <param name="utf8String">The decoded UTF-8 string.</param>
        [Constructor]
        public NetStandardModel([MapKey("utf8_string")] string utf8String)
        {
            Utf8String = utf8String;
        }

        /// <summary>
        ///     Gets the decoded UTF-8 string.
        /// </summary>
        public string Utf8String { get; }
    }
}
