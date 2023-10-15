using Serilog;
using System.Diagnostics;

namespace AssetRipper.AnalyzeUnityPackages.Helper;

public static class Logger
{
	private static Serilog.Core.Logger logger;

	public static void Init()
	{
		logger = new LoggerConfiguration()
#if DEBUG
			.MinimumLevel.Debug()
#else
			.MinimumLevel.Information()
#endif
			.WriteTo.Async(a => a.ColoredConsole(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message}{NewLine}{Exception}"))
			.CreateLogger();
	}

	[Conditional("DEBUG")]
	public static void Debug(string msg) => logger.Debug(msg);

	public static void Info(string msg) => logger.Information(msg);

	public static void Warning(string msg) => logger.Warning(msg);

	public static void Error(string msg) => logger.Error(msg);

	public static void Error(Exception ex, string msg) => logger.Error(ex, msg);
}
