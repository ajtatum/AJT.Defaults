using System;
using System.Collections.ObjectModel;
using System.Data;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Exceptions;
using Serilog.Sinks.MSSqlServer;
using Serilog.Sinks.SystemConsole.Themes;

namespace AJT.Defaults.Serilog
{
    /// <summary>
    /// Serilog Default Extensions
    /// </summary>
    public static class SerilogDefaultExtensions
    {
        /// <summary>
        /// Loads the default configuration used by most web applications developed by AJ Tatum.
        /// </summary>
        /// <param name="loggerConfiguration">The current loggerConfiguration</param>
        /// <param name="hostingContext">The current HostBuilderContext</param>
        /// <param name="applicationName">The application name.</param>
        /// <param name="enrichWithBuildNumber">Whether or not to enrich with Build Number. If so <see cref="IConfiguration"/> should contain the key BuildNumber.</param>
        /// <param name="useSqlServer">Whether or not to use SQL Server. If so, <see cref="IConfiguration"/> should have a connection string named LogsConnection.</param>
        /// <param name="useConsole">Whether or not to use the console.</param>
        /// <param name="useApplicationInsights">Where or not to use Application Insights. If so <see cref="IConfiguration"/> should have the key ApplicationInsights:InstrumentationKey</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Either the Connection String LogsConnection or the Application Insights Instrumentation Key could not be found.</exception>
        public static LoggerConfiguration LoadDefaultConfig(this LoggerConfiguration loggerConfiguration, HostBuilderContext hostingContext, string applicationName, bool enrichWithBuildNumber = true, bool useSqlServer = true, bool useConsole = true, bool useApplicationInsights = false)
        {
            var config = loggerConfiguration
                .ReadFrom.Configuration(hostingContext.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails()
                .Enrich.WithProperty("Application", applicationName)
                .Enrich.WithProperty("Environment", hostingContext.HostingEnvironment.EnvironmentName);

            if (enrichWithBuildNumber)
            {
                config.Enrich.WithProperty("BuildNumber", hostingContext.Configuration["BuildNumber"]);
            }

            if (useSqlServer)
            {
                var connectionString = hostingContext.Configuration.GetConnectionString("LogsConnection");

                if (!string.IsNullOrEmpty(connectionString))
                {

                    var columnOptions = new ColumnOptions
                    {
                        ClusteredColumnstoreIndex = false,
                        DisableTriggers = true,
                        AdditionalColumns = new Collection<SqlColumn>
                        {
                            new SqlColumn("Application", SqlDbType.VarChar, true, 50) {NonClusteredIndex = true},
                            new SqlColumn("Environment", SqlDbType.VarChar, true, 50),
                            new SqlColumn("BuildNumber", SqlDbType.VarChar, true, 50),
                            new SqlColumn("RequestPath", SqlDbType.VarChar, true, 255)
                        }
                    };
                    columnOptions.Store.Add(StandardColumn.LogEvent);
                    columnOptions.Store.Remove(StandardColumn.Properties);
                    columnOptions.PrimaryKey = columnOptions.Id;
                    columnOptions.Id.NonClusteredIndex = true;

                    columnOptions.Level.ColumnName = "Severity";
                    columnOptions.Level.DataLength = 15;

                    config.WriteTo.MSSqlServer(connectionString, tableName: "Logs", columnOptions: columnOptions, autoCreateSqlTable: true, batchPostingLimit: 50, period: new TimeSpan(0, 0, 5));
                }
                else
                {
                    throw new ArgumentException("Unable to find the connection string LogsConnection.");
                }
            }

            if (useConsole)
            {
                config.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {ThreadId} {EventType:x8} {Level:u3}] {Message:lj}{NewLine}{Exception}", theme: AnsiConsoleTheme.Code);
            }

            if (useApplicationInsights)
            {
                var appInsightsInstrumentKey = hostingContext.Configuration["ApplicationInsights:InstrumentationKey"];

                if (!string.IsNullOrEmpty(appInsightsInstrumentKey))
                {
                    var telemetryConfiguration = TelemetryConfiguration.CreateDefault();
                    telemetryConfiguration.InstrumentationKey = appInsightsInstrumentKey;

                    config.WriteTo.ApplicationInsights(telemetryConfiguration, TelemetryConverter.Traces);
                }
                else
                {
                    throw new ArgumentException("Unable to find ApplicationInsights:InstrumentationKey in configuration.");
                }
            }

            return config;

        }
    }
}
