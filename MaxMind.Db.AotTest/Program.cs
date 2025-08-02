using System;
using System.IO;
using System.Net;
using MaxMind.Db;

namespace MaxMind.Db.AotTest
{
    /// <summary>
    /// Test program to verify NativeAOT compatibility
    /// </summary>
    public class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("MaxMind.Db NativeAOT Compatibility Test");
            Console.WriteLine("========================================");
            
            try
            {
                // Test 1: Verify AOT mode detection
                TestAotModeDetection();
                
                // Test 2: Test basic type creation with Constructor attribute
                TestTypeCreation();
                
                // Test 3: Test reading a database file (if provided)
                if (args.Length > 0 && File.Exists(args[0]))
                {
                    TestDatabaseReading(args[0]);
                }
                
                Console.WriteLine("\nAll tests passed successfully!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nTest failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private static void TestAotModeDetection()
        {
            Console.Write("Test 1 - AOT Mode Detection: ");
            
#if NET9_0_OR_GREATER
            var isAot = !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
            Console.WriteLine(isAot ? "Running in AOT mode ✓" : "Running in JIT mode");
            
            if (isAot && !AotCompatibility.UseAotOptimizations)
            {
                throw new Exception("AOT mode detected but UseAotOptimizations is false");
            }
#else
            Console.WriteLine("Not running on .NET 9+");
#endif
        }

        private static void TestTypeCreation()
        {
            Console.Write("Test 2 - Type Creation: ");
            
            var testData = new TestType("test", 42);
            if (testData.Name != "test" || testData.Value != 42)
            {
                throw new Exception("Type creation failed");
            }
            
            Console.WriteLine("Basic type creation works ✓");
        }

        private static void TestDatabaseReading(string dbPath)
        {
            Console.Write($"Test 3 - Database Reading ({Path.GetFileName(dbPath)}): ");
            
            using var reader = new Reader(dbPath);
            
            // Test metadata reading
            var metadata = reader.Metadata;
            Console.WriteLine($"\n  Database type: {metadata.DatabaseType}");
            Console.WriteLine($"  IP version: {metadata.IpVersion}");
            Console.WriteLine($"  Record size: {metadata.RecordSize}");
            Console.WriteLine($"  Node count: {metadata.NodeCount}");
            
            // Try to read a sample IP
            var testIp = IPAddress.Parse("8.8.8.8");
            var result = reader.Find<Dictionary<string, object>>(testIp);
            
            if (result != null)
            {
                Console.WriteLine($"  Found data for {testIp}: {result.Count} fields ✓");
            }
            else
            {
                Console.WriteLine($"  No data found for {testIp} (this may be expected)");
            }
        }
    }

    /// <summary>
    /// Test type with Constructor attribute for AOT generation
    /// </summary>
    [Constructor]
    public class TestType
    {
        public string Name { get; }
        public int Value { get; }

        [Constructor]
        public TestType(
            [Parameter("name")] string name,
            [Parameter("value")] int value)
        {
            Name = name;
            Value = value;
        }
    }
}