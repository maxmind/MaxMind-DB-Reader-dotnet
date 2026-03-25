// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "Constructor result intentionally discarded inside Assert.Throws", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.NullStreamThrowsArgumentNullException")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "Constructor result intentionally discarded inside Assert.Throws", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.TestEmptyStream")]
[assembly: SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "Assertions are in helper methods called from test body", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.Test")]
[assembly: SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "Assertions are in helper methods called from test body", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.TestAsync~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "Assertions are in helper methods called from test body", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.TestDecodingToConcurrentDictionary")]
[assembly: SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "Assertions are in helper methods called from test body", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.TestDecodingToDictionary")]
[assembly: SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "Assertions are in helper methods called from test body", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.TestDecodingToGenericIDictionary")]
[assembly: SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "Assertions are in helper methods called from test body", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.TestNonSeekableStream")]
[assembly: SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "Assertions are in helper methods called from test body", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.TestNonSeekableStreamAsync~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "Assertions are in helper methods called from test body", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.TestStream")]
[assembly: SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "Assertions are in helper methods called from test body", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.TestStreamAsync~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "Test validates no exception is thrown during concurrent reader construction", Scope = "member", Target = "~M:MaxMind.Db.Test.ThreadingTest.TestManyOpens(MaxMind.Db.FileAccessMode)")]
[assembly: SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "Test uses inline throw instead of Assert; analyzer cannot detect non-Assert validation", Scope = "member", Target = "~M:MaxMind.Db.Test.ThreadingTest.TestParallelFor(MaxMind.Db.FileAccessMode)")]
[assembly: SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "Method is async; Roslynator false positive on test method naming", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.NullStreamThrowsArgumentNullExceptionAsync")]
[assembly: SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "Method is async; Roslynator false positive on test method naming", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.TestEmptyStreamAsync")]
[assembly: SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments", Justification = "Constant arrays in test code; allocation perf is irrelevant", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.TestIPV4(MaxMind.Db.Reader,System.String)")]
[assembly: SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments", Justification = "Constant arrays in test code; allocation perf is irrelevant", Scope = "member", Target = "~M:MaxMind.Db.Test.ReaderTest.TestIPV6(MaxMind.Db.Reader,System.String)")]
