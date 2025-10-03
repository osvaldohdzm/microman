using Micromanager;

var argsList = args.ToList();
bool stealthMode = argsList.Contains("--stealth");

string logDir = "C:\\Micromanager";
if (!Directory.Exists(logDir))
{
    Directory.CreateDirectory(logDir);
}
string logFilePath = Path.Combine(logDir, "stealth_log.txt");

if (stealthMode)
{
    try
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<Worker>();
        var host = builder.Build();

        // Log execution context to a file
        await File.AppendAllTextAsync(logFilePath, $"[{DateTime.UtcNow}] Running in stealth mode. User: {Environment.UserName}, Machine: {Environment.MachineName}{Environment.NewLine}");

        await host.RunAsync();
    }
    catch (Exception ex)
    {
        await File.AppendAllTextAsync(logFilePath, $"[{DateTime.UtcNow}] Error: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
    }
}
else
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddHostedService<Worker>();
    var host = builder.Build();

    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Application started in normal mode.");

    await host.RunAsync();
}
