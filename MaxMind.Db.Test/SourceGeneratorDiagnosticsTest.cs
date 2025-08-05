using System;
using System.Linq;
using Xunit;

namespace MaxMind.Db.Test
{
    /// <summary>
    /// Tests that verify source generator infrastructure works correctly
    /// These are conservative tests that don't make assumptions about which types have source generation
    /// </summary>
    public class SourceGeneratorDiagnosticsTest
    {
        [Fact]
        public void TestSourceGeneratorApiWorks()
        {
            // Basic API functionality test - these methods should not crash
            var systemType = typeof(string);

            // System types should never have source generation
            Assert.False(SourceGeneratorSupport.HasActivator(systemType));

            // Trying to create with unknown type should fail gracefully
            var success = SourceGeneratorSupport.TryCreateInstance(systemType, new object[0], out var instance);
            Assert.False(success);
            Assert.Null(instance);
        }

        [Fact]
        public void TestSourceGeneratorHandlesNullArguments()
        {
            // Should handle null arguments gracefully without crashing
            var success = SourceGeneratorSupport.TryCreateInstance(typeof(string), null!, out var instance);
            Assert.False(success); // Should fail gracefully
            Assert.Null(instance);
        }

        [Fact]
        public void TestSourceGeneratorHandlesEmptyArguments()
        {
            // Should handle empty argument arrays gracefully
            var success = SourceGeneratorSupport.TryCreateInstance(typeof(string), new object[0], out var instance);
            Assert.False(success); // Should fail gracefully for system types
            Assert.Null(instance);
        }

        [Fact]
        public void TestSourceGeneratorHandlesInvalidTypes()
        {
            // Test with various types that should not have source generation
            var typesToTest = new[]
            {
                typeof(int),
                typeof(DateTime),
                typeof(Exception),
                typeof(System.Collections.Generic.List<>),
                typeof(System.Collections.Generic.Dictionary<,>)
            };

            foreach (var type in typesToTest)
            {
                Assert.False(SourceGeneratorSupport.HasActivator(type),
                    $"Type {type.Name} should not have source generation");

                var success = SourceGeneratorSupport.TryCreateInstance(type, new object[0], out var instance);
                Assert.False(success, $"Creation should fail for type {type.Name}");
                Assert.Null(instance);
            }
        }

#if DEBUG
        [Fact]
        public void TestSourceGeneratorRegistrationInDebugMode()
        {
            // In debug mode, we can inspect what's registered
            var activators = SourceGeneratorSupport.GetRegisteredActivators();

            // Log for diagnostic purposes - don't assert specific counts since it varies
            Console.WriteLine($"Found {activators.Count} source-generated activators");

            if (activators.Count > 0)
            {
                // Basic validation of registered activators
                foreach (var kvp in activators)
                {
                    Assert.NotNull(kvp.Key); // Type should not be null
                    Assert.NotNull(kvp.Value); // Activator should not be null
                    Assert.True(kvp.Key.IsClass, $"Type {kvp.Key.Name} should be a class");
                }

                // Log some examples for debugging
                var examples = string.Join(", ", activators.Keys.Take(3).Select(t => t.Name));
                Console.WriteLine($"Example types: {examples}");
            }
        }
#endif
    }
}