using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MaxMind.Db.Test
{
    // Custom entry point that checks for AOT mode
    public class Program  
    {
        public static int Main(string[] args)
        {
            // Check for explicit AOT test flag
            if (args.Contains("--aot-test"))
            {
                return AotTestRunner.RunDirectTests();
            }

            // Auto-detect AOT mode
            if (!RuntimeFeature.IsDynamicCodeSupported)
            {
                Console.WriteLine("NativeAOT mode detected - running direct tests");
                Console.WriteLine("(xUnit v3 test discovery is not yet AOT-compatible)");
                Console.WriteLine();
                return AotTestRunner.RunDirectTests();
            }

            // In JIT mode, try to run xUnit tests
            // This would normally be handled by the auto-generated entry point
            // but we're overriding it for AOT compatibility
            Console.WriteLine("JIT mode - please run tests using 'dotnet test' instead");
            return 1;
        }
    }
}