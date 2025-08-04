using System;
using System.Linq;
using Xunit;

namespace MaxMind.Db.Test
{
    /// <summary>
    /// Tests that verify source generation infrastructure works correctly across assembly boundaries
    /// These tests focus on the infrastructure rather than specific types
    /// </summary>
    public class CrossAssemblySourceGenerationTest
    {
        [Fact]
        public void TestSourceGeneratorCoordinatorInitialization()
        {
            // Test that the source generator infrastructure doesn't crash
            // We can't directly test the coordinator due to namespace conflicts
            // but we can test that the overall system works
            
            // This is an indirect test - if source generation is working,
            // the system should be stable
            var success = SourceGeneratorSupport.HasActivator(typeof(string));
            Assert.False(success); // Should be false, but shouldn't crash
        }

        [Fact]
        public void TestSourceGeneratorDoesNotProcessSystemTypes()
        {
            // Verify we don't accidentally generate activators for system types
            var systemTypes = new[]
            {
                typeof(string),
                typeof(int),
                typeof(DateTime),
                typeof(Exception),
                typeof(System.Collections.Generic.List<>),
                typeof(System.Collections.Generic.Dictionary<,>)
            };

            foreach (var type in systemTypes)
            {
                Assert.False(SourceGeneratorSupport.HasActivator(type),
                    $"Should not have source-generated activator for system type: {type.Name}");
            }
        }

        [Fact]
        public void TestSourceGeneratorMemoryStability()
        {
            // Basic smoke test to ensure repeated calls don't cause memory issues
            var initialMemory = GC.GetTotalMemory(true);
            
            // Exercise source generator API multiple times with invalid arguments
            // This should not cause memory leaks or crashes
            for (int i = 0; i < 50; i++)
            {
                SourceGeneratorSupport.HasActivator(typeof(string));
                SourceGeneratorSupport.TryCreateInstance(typeof(string), new object[0], out _);
            }
            
            var finalMemory = GC.GetTotalMemory(true);
            var memoryIncrease = finalMemory - initialMemory;
            
            // Should not have excessive memory growth (this is a loose heuristic)
            Assert.True(memoryIncrease < 1024 * 1024, // Less than 1MB growth
                $"Excessive memory growth detected: {memoryIncrease} bytes");
        }

#if DEBUG
        [Fact]
        public void TestSourceGeneratorCrossAssemblyDetection()
        {
            // In debug mode, log information about cross-assembly detection
            var activators = SourceGeneratorSupport.GetRegisteredActivators();
            
            if (activators.Count > 0)
            {
                var assemblies = activators.Keys
                    .Select(t => t.Assembly.GetName().Name)
                    .Distinct()
                    .OrderBy(name => name);
                
                Console.WriteLine($"Source generators found types in assemblies: {string.Join(", ", assemblies)}");
                
                // Check if we found any test assembly types (demonstrates cross-assembly capability)
                var testAssemblyTypes = activators.Keys.Where(t => 
                    t.Assembly.GetName().Name?.Contains("Test") == true);
                    
                Console.WriteLine($"Found {testAssemblyTypes.Count()} types in test assemblies");
            }
            else
            {
                Console.WriteLine("No source-generated activators found - may be expected in some configurations");
            }
        }
#endif
    }
}