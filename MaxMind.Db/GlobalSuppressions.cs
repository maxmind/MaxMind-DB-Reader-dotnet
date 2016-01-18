// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

#region

using System.Diagnostics.CodeAnalysis;

#endregion

[assembly: SuppressMessage("Style", "IDE0005:Using directive is unnecessary.", Justification = "<Pending>")]
[assembly:
    SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "MaxMind",
        Scope = "member", Target = "MaxMind.Db.Decoder.#CtrlData(System.Int64,System.Int32&,System.Int64&)")]
[assembly:
    SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors", Scope = "type",
        Target = "MaxMind.Db.InvalidDatabaseException")]
[assembly:
    SuppressMessage("Microsoft.Naming", "CA1724:TypeNamesShouldNotMatchNamespaces", Scope = "type",
        Target = "MaxMind.Db.Metadata")]
[assembly:
    SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Scope = "member",
        Target = "MaxMind.Db.Reader.#Find`1(System.Net.IPAddress,MaxMind.Db.InjectableValues)")]
[assembly:
    SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Scope = "member",
        Target = "MaxMind.Db.ParameterAttribute.#.ctor(System.String,System.Boolean)")]
[assembly: SuppressMessage("Microsoft.Usage", "CA2243:AttributeStringLiteralsShouldParseCorrectly")]