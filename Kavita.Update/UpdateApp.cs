using System;
using System.ComponentModel;
using System.IO;
using Kavita.Common.Processes;
using Microsoft.Extensions.Logging;

namespace Kavita.Update
{
    public class UpdateApp
    {
        private readonly UpdateEngine.InstallUpdateService _installUpdateService;
        private readonly IProcessProvider _processProvider;
        
        private readonly ILoggerFactory _logfactory;
        private static ILogger _logger;

        public UpdateApp(UpdateEngine.InstallUpdateService installUpdateService, IProcessProvider processProvider)
        {
            _installUpdateService = installUpdateService;
            _processProvider = processProvider;
            _logfactory = new LoggerFactory();
            _logger = new Logger<UpdateApp>(_logfactory);
        }

        public static void Main(string[] args)
        {
            try
            {
                //var startupContext = new StartupContext(args);
                //TODO: NzbDroneLogger.Register(startupContext, true, true);

                //_logger.LogInformation("Starting Kavita Update Client");
                System.Console.WriteLine("Starting Kavita Update Client");

                // _container = UpdateContainerBuilder.Build(startupContext);
                // _container.Resolve<InitializeLogger>().Initialize();
                // _container.Resolve<UpdateApp>().Start(args);
                
                // What this needs to do is open the API/temp directory and check if the update/ folder exists
                // if so, backup current directory to temp/backup/
                // copy the files over for
                // then delete backup and return
                // if exception, perform rollback
                // I need DI here
                UpdateApp.Start(args);

                //_logger.LogInformation("Update completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "An error has occurred while applying update package");
            }
        }
        
        public static void Start(string[] args)
        {
            //var startupContext = ParseArgs(args);
            var targetFolder = Directory.GetCurrentDirectory();

            _installUpdateService.Start(targetFolder, ParseProcessId(args[0]));
        }
        
        private int ParseProcessId(string arg)
        {
            int id;
            if (!int.TryParse(arg, out id) || id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(arg), "Invalid process ID");
            }

            _logger.LogDebug("Kavita process ID: {0}", id);
            return id;
        }
        
        
    }
}