```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26100.6584/24H2/2024Update/HudsonValley)
AMD Ryzen 5 5600 3.50GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.100
  [Host]   : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                         | Mean               | Error           | StdDev         | Gen0   | Gen1   | Allocated |
|------------------------------- |-------------------:|----------------:|---------------:|-------:|-------:|----------:|
| &#39;Old: Synchronous Logger&#39;      |      2,801.4063 ns |   2,938.1952 ns |    161.0524 ns | 0.0038 |      - |     120 B |
| &#39;New: Asynchronous Logger&#39;     |        325.4641 ns |      46.4964 ns |      2.5486 ns | 0.0076 | 0.0072 |     144 B |
| &#39;P/Invoke: GetPixel&#39;           | 11,854,963.5417 ns | 298,816.6207 ns | 16,379.1474 ns |      - |      - |         - |
| &#39;Utility: ColorsMatch (True)&#39;  |          0.0000 ns |       0.0000 ns |      0.0000 ns |      - |      - |         - |
| &#39;Utility: ColorsMatch (False)&#39; |          0.0248 ns |       0.1615 ns |      0.0089 ns |      - |      - |         - |
