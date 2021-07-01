using System;
using Kavita.Common.Disk;

namespace Kavita.Common.Update
{
    public interface IVerifyUpdates
    {
        bool Verify(UpdatePackage updatePackage, string packagePath);
    }
    
    public class UpdateVerification : IVerifyUpdates
    {
        private readonly IDiskService _diskService;

        public UpdateVerification(IDiskService diskService)
        {
            _diskService = diskService;
        }

        public bool Verify(UpdatePackage updatePackage, string packagePath)
        {
            using (var fileStream = _diskService.OpenReadStream(packagePath))
            {
                var hash = fileStream.SHA256Hash();

                return hash.Equals(updatePackage.Hash, StringComparison.CurrentCultureIgnoreCase);
            }
        }
    }
}