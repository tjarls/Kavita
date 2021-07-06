using System;
using System.IO;
using Kavita.Common.Extensions;
using Kavita.Common.Processes;
using Microsoft.Extensions.Logging;

namespace Kavita.Update.UpdateEngine
{
    public interface IStartKavita
    {
        void Start(string installationFolder);
    }
    public class StartKavita : IStartKavita
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IProcessProvider _processProvider;
        private readonly ILogger<StartKavita> _logger;

        public StartKavita(IServiceProvider serviceProvider, IProcessProvider processProvider,
            ILogger<StartKavita> logger)
        {
            _serviceProvider = serviceProvider;
            _processProvider = processProvider;
            _logger = logger;
        }
        
        public void Start(string installationFolder)
        {
            _logger.LogInformation("Starting Kavita");
            StartWinform(installationFolder);
            return;
            // if (appType == AppType.Service)
            // {
            //     try
            //     {
            //         StartService();
            //     }
            //     catch (InvalidOperationException e)
            //     {
            //         _logger.LogWarning(e, "Couldn't start Kavita Service (Most likely due to permission issues). Falling back to console");
            //         StartConsole(installationFolder);
            //     }
            // }
            // else if (appType == AppType.Console)
            // {
            //     StartConsole(installationFolder);
            // }
            // else
            // {
            //     StartWinform(installationFolder);
            // }
        }

        private void StartService()
        {
            _logger.LogInformation("Starting Kavita service");
            //_serviceProvider.Start(ServiceProvider.SERVICE_NAME);
            throw new NotImplementedException("Kavita service is not supported");
        }

        private void StartWinform(string installationFolder)
        {
            Start(installationFolder, "Kavita".ProcessNameToExe());
        }

        private void StartConsole(string installationFolder)
        {
            throw new NotImplementedException("Kavita Console is not supported");
            //Start(installationFolder, "Kavita.Console".ProcessNameToExe());
        }

        private void Start(string installationFolder, string fileName)
        {
            _logger.LogInformation("Starting {0}", fileName);
            var path = Path.Combine(installationFolder, fileName);

            // if (!_startupContext.Flags.Contains(StartupContext.NO_BROWSER))
            // {
            //     _startupContext.Flags.Add(StartupContext.NO_BROWSER);
            // }

            _processProvider.SpawnNewProcess(path);
        }
    }
}