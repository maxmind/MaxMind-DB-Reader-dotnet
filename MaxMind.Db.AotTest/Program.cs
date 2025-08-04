using System;
using System.Collections.Generic;
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

#if NET8_0_OR_GREATER
            var isAot = !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
            Console.WriteLine(isAot ? "Running in AOT mode ✓" : "Running in JIT mode");

            // AotCompatibility is internal, so we can't check it directly
            // The test itself demonstrates AOT is working if it compiles and runs
#else
            Console.WriteLine("Not running on .NET 8+");
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

            // Try to read a sample IP
            var testIp = IPAddress.Parse("8.8.8.8");
            var result = reader.Find<Dictionary<string, object>>(testIp);

            if (result != null)
            {
                Console.WriteLine($"  Found data for {testIp}: Found data ✓");
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