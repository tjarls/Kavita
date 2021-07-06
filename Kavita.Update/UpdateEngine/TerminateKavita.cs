using Kavita.Common.Processes;
using Microsoft.Extensions.Logging;

namespace Kavita.Update.UpdateEngine
{
    public interface ITerminateKavita
    {
        void Terminate(int processId);
    }
    public class TerminateKavita : ITerminateKavita
    {
        private readonly IProcessProvider _processProvider;
        private readonly ILogger<TerminateKavita> _logger;

        public TerminateKavita(IProcessProvider processProvider, ILogger<TerminateKavita> logger)
        {
            _processProvider = processProvider;
            _logger = logger;
        }
        
        public void Terminate(int processId)
        {
            _logger.LogInformation("Killing all running processes");
                
            _processProvider.KillAll(ProcessProvider.KAVITA_PROCESS_NAME);

            _processProvider.Kill(processId);
        }
    }
}