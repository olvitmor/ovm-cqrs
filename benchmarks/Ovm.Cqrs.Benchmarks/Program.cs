using BenchmarkDotNet.Running;

// Run all:      dotnet run -c Release --project benchmarks/Ovm.Cqrs.Benchmarks -- --filter '*'
// Quick check:  dotnet run -c Release --project benchmarks/Ovm.Cqrs.Benchmarks -- --filter '*' --job short
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
