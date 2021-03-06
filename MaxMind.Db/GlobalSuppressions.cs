﻿// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

#region

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "<Pending>", Scope = "type", Target = "~T:MaxMind.Db.Reader.ReaderIteratorNode`1")]
[assembly: SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "<Pending>", Scope = "type", Target = "~T:MaxMind.Db.Reader.ReaderIteratorNode`1")]
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>", Scope = "member", Target = "~M:MaxMind.Db.MemoryMapBuffer.#ctor(System.String,System.Boolean,System.IO.FileInfo)")]
[assembly: SuppressMessage("Major Code Smell", "S4457:Parameter validation in \"async\"/\"await\" methods should be wrapped", Justification = "<Pending>", Scope = "member", Target = "~M:MaxMind.Db.ArrayBuffer.BytesFromStreamAsync(System.IO.Stream)~System.Threading.Tasks.Task{System.Byte[]}")]
[assembly: SuppressMessage("Style", "IDE0056:Use index operator", Justification = "<Pending>", Scope = "member", Target = "~M:MaxMind.Db.Buffer.ReadBigInteger(System.Int64,System.Int32)~System.Numerics.BigInteger")]
[assembly: SuppressMessage("Style", "IDE0056:Use index operator", Justification = "<Pending>", Scope = "member", Target = "~M:MaxMind.Db.Reader.FindAll``1(MaxMind.Db.InjectableValues,System.Int32)~System.Collections.Generic.IEnumerable{MaxMind.Db.Reader.ReaderIteratorNode{``0}}")]

#endregion
