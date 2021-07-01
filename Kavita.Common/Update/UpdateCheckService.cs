using System.Threading.Tasks;
using Kavita.Common.EnvironmentInfo;

namespace Kavita.Common.Update
{
    public interface ICheckUpdateService
    {
        Task<UpdatePackage> AvailableUpdate();
    }
    
    public class UpdateCheckService : ICheckUpdateService
    {
        private readonly IUpdatePackageProvider _updatePackageProvider;

        public UpdateCheckService(IUpdatePackageProvider updatePackageProvider)
        {
            _updatePackageProvider = updatePackageProvider;
        }

        public async Task<UpdatePackage> AvailableUpdate()
        {
            return await _updatePackageProvider.GetLatestUpdate(Configuration.GetBranch(Configuration.GetAppSettingFilename()), BuildInfo.Version);
        }
    }
}