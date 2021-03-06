using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qwack.Core.Basic;
using Qwack.Dates;
using Qwack.Excel.Utils;
using Qwack.Futures;
using Qwack.Models.Models;
using Qwack.Providers.Json;
using Qwack.Utils;

namespace Qwack.Excel
{
    public static class ContainerStores
    {
        private const string _calendarJSONFile = "Calendars.json";
        private const string _futureSettingsFile = "futuresettings.json";
        private const string _currenciesFile = "currencies.json";

        static ContainerStores()
        {
            try
            {
                GlobalContainer = ((IServiceCollection)new ServiceCollection())
                    .AddQwackLogging()
                    .AddCalendarsFromJson(GetCalendarFilename())
                    .AddFutureSettingsFromJson(GetFutureSettingsFile())
                    .AddCurrenciesFromJson(GetCurrenciesFilename())
                    .AddSingleton(typeof(IObjectStore<>), typeof(ExcelObjectStore<>))
                    .BuildServiceProvider();

                SessionContainer = GlobalContainer.CreateScope().ServiceProvider;

                SessionContainer.GetRequiredService<IFutureSettingsProvider>();

                PnLAttributor = new PnLAttributor();
            }
            catch(Exception ex)
            {
                if(Directory.Exists(@"C:\Temp"))
                {
                    File.WriteAllText($@"C:\Temp\QwackInitializationError_{DateTime.Now:yyyyMMdd_HHmmss}.txt", ex.ToString());
                }
            }
        }
        
        public static IServiceProvider GlobalContainer { get; internal set; }
        public static IServiceProvider SessionContainer { get;set;}
        public static ICurrencyProvider CurrencyProvider => GlobalContainer.GetRequiredService<ICurrencyProvider>();
        public static ICalendarProvider CalendarProvider => GlobalContainer.GetRequiredService<ICalendarProvider>();
        public static IFutureSettingsProvider FuturesProvider => GlobalContainer.GetRequiredService<IFutureSettingsProvider>();
        public static ILogger GetLogger<T>() => GlobalContainer.GetRequiredService<ILoggerFactory>().CreateLogger<T>();

        public static IPnLAttributor PnLAttributor { get; set; } 

        private static string GetFutureSettingsFile() => Path.Combine(GetRunningDirectory(), _futureSettingsFile);

        private static string GetRunningDirectory()
        {
            var codeBaseUrl = new Uri(Assembly.GetExecutingAssembly().CodeBase);
            var codeBasePath = Uri.UnescapeDataString(codeBaseUrl.AbsolutePath);
            var dirPath = Path.GetDirectoryName(codeBasePath);
            return dirPath;
        }

        private static string GetCalendarFilename() => Path.Combine(GetRunningDirectory(), _calendarJSONFile);
        private static string GetCurrenciesFilename() => Path.Combine(GetRunningDirectory(), _currenciesFile);

        public static IObjectStore<T> GetObjectCache<T>() => SessionContainer.GetService<IObjectStore<T>>();
        public static T GetObjectFromCache<T>(string name) => SessionContainer.GetService<IObjectStore<T>>().GetObject(name).Value;
        public static void PutObjectToCache<T>(string name, T obj) => SessionContainer.GetService<IObjectStore<T>>().PutObject(name, new SessionItem<T> { Name = name, Value = obj, Version = 1 });
    }
}
