#!/bin/bash

# Source Generator Compilation Performance Test
# Compares compilation time between origin/main and optimized greg/aot

set -e

echo "=== Source Generator Compilation Performance Test ==="
echo "Date: $(date)"
echo "CPU: $(grep 'model name' /proc/cpuinfo | head -1 | cut -d: -f2 | xargs)"
echo "Memory: $(free -h | grep Mem | awk '{print $2}')"
echo "Using taskset to pin to CPU core 0 for consistent results"
echo

# Function to perform timed compilation test
compile_test() {
    local branch_name="$1"
    local description="$2"
    local iterations=5
    
    echo "Testing: $description ($branch_name)"
    echo "Running $iterations iterations..."
    
    local times=()
    local total_time=0
    local success_count=0
    
    for i in $(seq 1 $iterations); do
        # Clean build artifacts
        taskset -c 0 dotnet clean > /dev/null 2>&1
        find . -type d \( -name "obj" -o -name "bin" \) -exec rm -rf {} + 2>/dev/null || true
        
        # Time the build with source generators
        start_time=$(date +%s.%N)
        if taskset -c 0 timeout 120 dotnet build MaxMind.Db.Benchmark --configuration Release --verbosity quiet > /dev/null 2>&1; then
            end_time=$(date +%s.%N)
            iteration_time=$(echo "$end_time - $start_time" | bc -l)
            times+=($iteration_time)
            total_time=$(echo "$total_time + $iteration_time" | bc -l)
            success_count=$((success_count + 1))
            printf "  Iteration %d: %.3fs\n" $i $iteration_time
        else
            echo "  Iteration $i: FAILED"
        fi
    done
    
    if [ $success_count -gt 0 ]; then
        local average=$(echo "scale=3; $total_time / $success_count" | bc -l)
        
        # Calculate standard deviation
        local variance=0
        for time in "${times[@]}"; do
            local diff=$(echo "$time - $average" | bc -l)
            local sq_diff=$(echo "$diff * $diff" | bc -l)
            variance=$(echo "$variance + $sq_diff" | bc -l)
        done
        variance=$(echo "scale=6; $variance / $success_count" | bc -l)
        local stddev=$(echo "scale=3; sqrt($variance)" | bc -l)
        
        printf "  Average: %.3fs ± %.3fs (n=%d)\n" $average $stddev $success_count
        
        # Store result for comparison
        if [ "$branch_name" = "origin/main" ]; then
            baseline_time=$average
        else
            optimized_time=$average
        fi
    else
        echo "  All builds failed!"
    fi
    
    echo
}

# Store current branch
current_branch=$(git branch --show-current)

# Test baseline (origin/main)
echo "=== Baseline Test (origin/main) ==="
git checkout origin/main > /dev/null 2>&1
compile_test "origin/main" "Original Source Generator"

# Test optimized version (greg/aot)  
echo "=== Optimized Test (greg/aot) ==="
git checkout greg/aot > /dev/null 2>&1
compile_test "greg/aot" "Performance Optimized Source Generator"

# Calculate improvement
if [ -n "${baseline_time:-}" ] && [ -n "${optimized_time:-}" ]; then
    echo "=== Performance Comparison ==="
    printf "Baseline (origin/main):  %.3fs\n" $baseline_time
    printf "Optimized (greg/aot):    %.3fs\n" $optimized_time
    
    local improvement=$(echo "scale=3; ($baseline_time - $optimized_time) / $baseline_time * 100" | bc -l)
    local speedup=$(echo "scale=2; $baseline_time / $optimized_time" | bc -l)
    
    if (( $(echo "$improvement > 0" | bc -l) )); then
        printf "Improvement: %.1f%% faster (%.2fx speedup)\n" $improvement $speedup
    else
        local regression=$(echo "scale=1; ($optimized_time - $baseline_time) / $baseline_time * 100" | bc -l)
        printf "Regression: %.1f%% slower\n" $regression
    fi
fi

# Restore original branch
git checkout "$current_branch" > /dev/null 2>&1

echo
echo "=== Test Complete ==="