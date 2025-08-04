using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using MaxMind.Db;

namespace MaxMind.Db.Test
{
    /// <summary>
    /// Simple test runner for AOT mode that bypasses xUnit's test discovery
    /// </summary>
    public static class AotTestRunner
    {
        public static int RunDirectTests()
        {
            Console.WriteLine("MaxMind.Db AOT Direct Test Runner");
            Console.WriteLine("==================================");

            // Check if we're in AOT mode
            var isAot = !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
            Console.WriteLine($"AOT Mode: {(isAot ? "Yes" : "No")}");
            Console.WriteLine();

            int passed = 0;
            int failed = 0;

            // Test 1: Basic database reading
            try
            {
                Console.Write("Test 1 - Read test database: ");
                var testDb = Path.Combine("TestData", "MaxMind-DB", "test-data", "MaxMind-DB-test-decoder.mmdb");
                if (!File.Exists(testDb))
                {
                    // Try alternate path
                    testDb = Path.Combine("..", "MaxMind.Db.Test", "TestData", "MaxMind-DB", "test-data", "MaxMind-DB-test-decoder.mmdb");
                }

                using var reader = new Reader(testDb);
                var metadata = reader.Metadata;
                if (metadata.DatabaseType == "MaxMind DB Decoder Test")
                {
                    Console.WriteLine("✓ PASS");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ FAIL - Wrong database type: {metadata.DatabaseType}");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ FAIL - {ex.Message}");
                failed++;
            }

            // Test 2: IPv4 lookup
            try
            {
                Console.Write("Test 2 - IPv4 address lookup: ");
                var testDb = Path.Combine("TestData", "MaxMind-DB", "test-data", "MaxMind-DB-test-ipv4-24.mmdb");
                if (!File.Exists(testDb))
                {
                    testDb = Path.Combine("..", "MaxMind.Db.Test", "TestData", "MaxMind-DB", "test-data", "MaxMind-DB-test-ipv4-24.mmdb");
                }

                using var reader = new Reader(testDb);
                var ip = IPAddress.Parse("1.1.1.1");
                var result = reader.Find<Dictionary<string, object>>(ip);
                if (result != null)
                {
                    Console.WriteLine("✓ PASS");
                    passed++;
                }
                else
                {
                    Console.WriteLine("✗ FAIL - No result");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ FAIL - {ex.Message}");
                failed++;
            }

            // Test 3: Custom type deserialization
            try
            {
                Console.Write("Test 3 - Custom type deserialization: ");
                var testDb = Path.Combine("TestData", "MaxMind-DB", "test-data", "GeoIP2-City-Test.mmdb");
                if (!File.Exists(testDb))
                {
                    testDb = Path.Combine("..", "MaxMind.Db.Test", "TestData", "MaxMind-DB", "test-data", "GeoIP2-City-Test.mmdb");
                }

                using var reader = new Reader(testDb);
                var ip = IPAddress.Parse("81.2.69.160");
                var result = reader.Find<TestModel>(ip);
                if (result?.City != null)
                {
                    Console.WriteLine("✓ PASS");
                    passed++;
                }
                else
                {
                    Console.WriteLine("✗ FAIL - No city data");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ FAIL - {ex.Message}");
                failed++;
            }

            // Test 4: Memory mode
            try
            {
                Console.Write("Test 4 - Memory mode: ");
                var testDb = Path.Combine("TestData", "MaxMind-DB", "test-data", "MaxMind-DB-test-decoder.mmdb");
                if (!File.Exists(testDb))
                {
                    testDb = Path.Combine("..", "MaxMind.Db.Test", "TestData", "MaxMind-DB", "test-data", "MaxMind-DB-test-decoder.mmdb");
                }

                using var reader = new Reader(testDb, FileAccessMode.Memory);
                var metadata = reader.Metadata;
                if (metadata != null)
                {
                    Console.WriteLine("✓ PASS");
                    passed++;
                }
                else
                {
                    Console.WriteLine("✗ FAIL - No metadata");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ FAIL - {ex.Message}");
                failed++;
            }

            // Summary
            Console.WriteLine();
            Console.WriteLine($"Results: {passed} passed, {failed} failed");
            return failed == 0 ? 0 : 1;
        }

        // Test model class with Constructor attribute
        private class TestModel
        {
            public Dictionary<string, object>? City { get; }

            [Constructor]
            public TestModel([Parameter("city")] Dictionary<string, object>? city)
            {
                City = city;
            }
        }
    }
}