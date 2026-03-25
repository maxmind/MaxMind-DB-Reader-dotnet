// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

#region

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "ReaderIteratorNode<T> is part of the public FindAll API contract", Scope = "type", Target = "~T:MaxMind.Db.Reader.ReaderIteratorNode`1")]
[assembly: SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Struct is used for iteration results, not equality comparison", Scope = "type", Target = "~T:MaxMind.Db.Reader.ReaderIteratorNode`1")]
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Platform-specific code paths handled via exception catch in constructor", Scope = "member", Target = "~M:MaxMind.Db.MemoryMapBuffer.#ctor(System.String,System.Boolean,System.IO.FileInfo)")]
[assembly: SuppressMessage("Style", "IDE0056:Use index operator", Justification = "Index operator requires System.Index, unavailable on netstandard2.0", Scope = "member", Target = "~M:MaxMind.Db.MemoryMapBuffer.ReadBigInteger(System.Int64,System.Int32)~System.Numerics.BigInteger")]
[assembly: SuppressMessage("Style", "IDE0056:Use index operator", Justification = "Index operator requires System.Index, unavailable on netstandard2.0", Scope = "member", Target = "~M:MaxMind.Db.Reader.FindAll``1(MaxMind.Db.InjectableValues,System.Int32)~System.Collections.Generic.IEnumerable{MaxMind.Db.Reader.ReaderIteratorNode{``0}}")]

#endregion
