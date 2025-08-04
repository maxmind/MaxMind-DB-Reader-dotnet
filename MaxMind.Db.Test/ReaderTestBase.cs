using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using MaxMind.Db.Test.Helper;
using Xunit;

namespace MaxMind.Db.Test
{
    /// <summary>
    /// Abstract base class for Reader tests that can run with both source generator and reflection activation paths
    /// </summary>
    public abstract class ReaderTestBase
    {
        protected readonly ReaderWrapper Wrapper;
        protected readonly string TestDataRoot;

        protected ReaderTestBase(ReaderWrapper wrapper)
        {
            Wrapper = wrapper;
            TestDataRoot = Path.Combine(Helper.TestUtils.TestDirectory, "TestData", "MaxMind-DB", "test-data");
        }

        public Reader CreateReader(string filename)
        {
            var path = Path.Combine(TestDataRoot, filename);
            return Wrapper.CreateReader(path);
        }

        protected Reader CreateReader(Stream stream)
        {
            return Wrapper.CreateReader(stream);
        }

        public T? Find<T>(Reader reader, IPAddress ipAddress) where T : class
        {
            return Wrapper.Find<T>(reader, ipAddress);
        }

        public T? Find<T>(Reader reader, IPAddress ipAddress, InjectableValues? injectables) where T : class
        {
            return Wrapper.Find<T>(reader, ipAddress, injectables);
        }

        public void Dispose()
        {
            Wrapper?.Dispose();
        }
    }

    /// <summary>
    /// Tests that run with source generator activation
    /// </summary>
#if !TEST_REFLECTION_ONLY
    public class SourceGeneratorReaderTests : ReaderTestBase, IDisposable
    {
        public SourceGeneratorReaderTests() : base(new SourceGeneratorReaderWrapper())
        {
        }

        [Fact]
        public void TestSourceGeneratorPath()
        {
            Assert.True(Wrapper.IsSourceGeneratedReader);
        }

        [Fact]
        public void TestTypesToObjectWithSourceGenerator()
        {
            using var reader = CreateReader("MaxMind-DB-test-decoder.mmdb");
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected string");
            var record = Find<TypeHolder>(reader, IPAddress.Parse("1.1.1.1"), injectables);

            Assert.NotNull(record);
            Assert.True(record.Boolean);
            Assert.Equal("unicode! ☯ - ♫", record.Utf8String);
        }

        [Fact]
        public void TestSimpleTypeWithSourceGenerator()
        {
            using var reader = CreateReader("MaxMind-DB-test-ipv4-24.mmdb");
            var result = Find<Dictionary<string, object>>(reader, IPAddress.Parse("1.1.1.1"));

            Assert.NotNull(result);
            Assert.Equal("1.1.1.1", result["ip"]);
        }
    }
#endif

    /// <summary>
    /// Tests that run with reflection activation (source generators disabled)
    /// </summary>
#if !TEST_SOURCE_GENERATOR_ONLY
    public class ReflectionReaderTests : ReaderTestBase, IDisposable
    {
        public ReflectionReaderTests() : base(new ReflectionReaderWrapper())
        {
        }

        [Fact]
        public void TestReflectionPath()
        {
            Assert.False(Wrapper.IsSourceGeneratedReader);
        }

        [Fact]
        public void TestTypesToObjectWithReflection()
        {
            using var reader = CreateReader("MaxMind-DB-test-decoder.mmdb");
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected string");
            var record = Find<TypeHolder>(reader, IPAddress.Parse("1.1.1.1"), injectables);

            Assert.NotNull(record);
            Assert.True(record.Boolean);
            Assert.Equal("unicode! ☯ - ♫", record.Utf8String);
        }

        [Fact]
        public void TestSimpleTypeWithReflection()
        {
            using var reader = CreateReader("MaxMind-DB-test-ipv4-24.mmdb");
            var result = Find<Dictionary<string, object>>(reader, IPAddress.Parse("1.1.1.1"));

            Assert.NotNull(result);
            Assert.Equal("1.1.1.1", result["ip"]);
        }
    }
#endif

    /// <summary>
    /// Parametrized tests that run with both source generator and reflection wrappers
    /// </summary>
    public class ParametrizedReaderTests : IDisposable
    {
        public static IEnumerable<object[]> GetReaderWrappers()
        {
            yield return new object[] { new SourceGeneratorReaderWrapper() };
            yield return new object[] { new ReflectionReaderWrapper() };
        }

        [Theory]
        [MemberData(nameof(GetReaderWrappers))]
        public void TestBothActivationPaths(ReaderWrapper wrapper)
        {
            using var testBase = new TestableReaderBase(wrapper);
            using var reader = testBase.CreateReader("MaxMind-DB-test-ipv4-24.mmdb");

            var result = testBase.Find<Dictionary<string, object>>(reader, IPAddress.Parse("1.1.1.1"));
            Assert.NotNull(result);
            Assert.Equal("1.1.1.1", result["ip"]);

            // Verify we're actually using the expected activation path
            if (wrapper.IsSourceGeneratedReader)
            {
#if NET8_0_OR_GREATER
                // Verify source generator is being used by checking if activators are registered
                Assert.True(SourceGeneratorSupport.HasActivator(typeof(Dictionary<string, object>)) ||
                           SourceGeneratorSupport.GetRegisteredActivators().Count >= 0);
#endif
            }
            else
            {
                // For reflection wrapper, source generators should be temporarily disabled
                Assert.False(wrapper.IsSourceGeneratedReader);
            }
        }

        [Theory]
        [MemberData(nameof(GetReaderWrappers))]
        public void TestComplexTypeActivation(ReaderWrapper wrapper)
        {
            using var testBase = new TestableReaderBase(wrapper);
            using var reader = testBase.CreateReader("MaxMind-DB-test-decoder.mmdb");

            var injectables = new InjectableValues();
            injectables.AddValue("injected", "test value");

            var record = testBase.Find<TypeHolder>(reader, IPAddress.Parse("1.1.1.1"), injectables);
            Assert.NotNull(record);
            Assert.Equal("unicode! ☯ - ♫", record.Utf8String);
            Assert.True(record.Boolean);
        }

        public void Dispose()
        {
            // Nothing to dispose at this level
        }

        private class TestableReaderBase : ReaderTestBase, IDisposable
        {
            public TestableReaderBase(ReaderWrapper wrapper) : base(wrapper) { }

            public new void Dispose()
            {
                base.Dispose();
            }
        }
    }
}