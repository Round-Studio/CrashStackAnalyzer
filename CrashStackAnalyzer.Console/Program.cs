using CrashStackAnalyzer;

class Program
{
    static void Main()
    {
        var analyzer = new CrashAnalyzer();

        var csharpStack = @"
Failed to load blockstate create_radar:blockstates/radome.json from pack mod/create_radar
com.google.gson.JsonParseException: java.io.EOFException: End of input at line 1 column 1 path $
	at net.minecraft.util.GsonHelper.fromNullableJson(GsonHelper.java:527) ~[client-1.21.1-20240808.144430-srg.jar%23222!/:?]
	at net.minecraft.util.GsonHelper.fromJson(GsonHelper.java:532) ~[client-1.21.1-20240808.144430-srg.jar%23222!/:?]
	at net.minecraft.util.GsonHelper.parse(GsonHelper.java:594) ~[client-1.21.1-20240808.144430-srg.jar%23222!/:?]
	at net.minecraft.util.GsonHelper.parse(GsonHelper.java:602) ~[client-1.21.1-20240808.144430-srg.jar%23222!/:?]
	at net.minecraft.client.resources.model.ModelManager.lambda$loadBlockStates$12(ModelManager.java:180) ~[client-1.21.1-20240808.144430-srg.jar%23222!/:?]
	at java.base/java.util.concurrent.CompletableFuture$AsyncSupply.run(CompletableFuture.java:1768) [?:?]
	at java.base/java.util.concurrent.CompletableFuture$AsyncSupply.exec(CompletableFuture.java:1760) [?:?]
	at java.base/java.util.concurrent.ForkJoinTask.doExec(ForkJoinTask.java:387) [?:?]
	at java.base/java.util.concurrent.ForkJoinPool$WorkQueue.topLevelExec(ForkJoinPool.java:1312) [?:?]
	at java.base/java.util.concurrent.ForkJoinPool.scan(ForkJoinPool.java:1843) [?:?]
	at java.base/java.util.concurrent.ForkJoinPool.runWorker(ForkJoinPool.java:1808) [?:?]
	at java.base/java.util.concurrent.ForkJoinWorkerThread.run(ForkJoinWorkerThread.java:188) [?:?]
Caused by: java.io.EOFException: End of input at line 1 column 1 path $
	at MC-BOOTSTRAP/com.google.gson@2.10.1/com.google.gson.stream.JsonReader.nextNonWhitespace(JsonReader.java:1457) ~[gson-2.10.1.jar%23142!/:?]
	at MC-BOOTSTRAP/com.google.gson@2.10.1/com.google.gson.stream.JsonReader.doPeek(JsonReader.java:558) ~[gson-2.10.1.jar%23142!/:?]
	at MC-BOOTSTRAP/com.google.gson@2.10.1/com.google.gson.stream.JsonReader.peek(JsonReader.java:433) ~[gson-2.10.1.jar%23142!/:?]
	at MC-BOOTSTRAP/com.google.gson@2.10.1/com.google.gson.internal.bind.TypeAdapters$28.read(TypeAdapters.java:769) ~[gson-2.10.1.jar%23142!/:?]
	at MC-BOOTSTRAP/com.google.gson@2.10.1/com.google.gson.internal.bind.TypeAdapters$28.read(TypeAdapters.java:725) ~[gson-2.10.1.jar%23142!/:?]
	at MC-BOOTSTRAP/com.google.gson@2.10.1/com.google.gson.internal.bind.TypeAdapters$34$1.read(TypeAdapters.java:1007) ~[gson-2.10.1.jar%23142!/:?]
	at net.minecraft.util.GsonHelper.fromNullableJson(GsonHelper.java:525) ~[client-1.21.1-20240808.144430-srg.jar%23222!/:?]
	... 11 more
";

        var report = analyzer.Analyze(csharpStack);

        // 验证报告
        var issues = report.Validate();
        if (issues.Any())
        {
            Console.WriteLine("Issues found:");
            foreach (var issue in issues)
            {
                Console.WriteLine($"  - {issue}");
            }
        }

        // 使用扩展方法
        Console.WriteLine($"Exception Point: {report.ExceptionPoint?.GetShortDescription()}");
        Console.WriteLine($"Root Cause: {report.MainException.GetRootCause()}");

        // 导出完整报告
        Console.WriteLine("\n" + report.GetFullReport());

        // JSON 导出
        string json = analyzer.ExportToJson(report);
        File.WriteAllText("crash_report.json", json);
    }
}