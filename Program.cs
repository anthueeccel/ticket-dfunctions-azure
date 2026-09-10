using Microsoft.Extensions.Hosting;

/// <summary>
/// Entry point for the Azure Functions .NET isolated worker.
///
/// The HostBuilder + ConfigureFunctionsWorkerDefaults call wires up the gRPC
/// server, logging, dependency injection, and automatic discovery of every
/// [Function] method (plus binding extensions such as the Durable Task
/// extension registered in extensions.json).
///
/// The Microsoft.Azure.Functions.Worker.Sdk build-time package generates the
/// functions.metadata file from those annotations at compile time. At runtime
/// the host reads that metadata and the worker stays alive to receive
/// invocations over gRPC. Without the explicit HostBuilder / host.Run(), the
/// worker process exits immediately and the Functions Host reports
/// "Failed to start language worker process".
/// </summary>
public class Program
{
    public static void Main()
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .Build();

        host.Run();
    }
}