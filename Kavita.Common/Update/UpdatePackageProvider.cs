using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Kavita.Common.EnvironmentInfo;

namespace Kavita.Common.Update
{
    public interface IUpdatePackageProvider
    {
        Task<UpdatePackage> GetLatestUpdate(string branch, Version currentVersion);
        Task<List<UpdatePackage>> GetRecentUpdates(string branch, Version currentVersion);
    }
    
    public class UpdatePackageProvider
    {
        private readonly HttpClient _httpClient;
        private readonly IPlatformInfo _platformInfo;
        private static string UpdateApi = "http://update.kavitareader.com"; // TODO: Move this into a central point

        public UpdatePackageProvider(HttpClient httpClient, IPlatformInfo platformInfo)
        {
            _httpClient = httpClient;
            _platformInfo = platformInfo;
        }
        public async Task<UpdatePackage> GetLatestUpdate(string branch, Version currentVersion)
        {
            var update = await (UpdateApi + "/update/")
                .AppendPathSegment(branch)
                .SetQueryParams(new
                {
                    version = currentVersion,
                    os = OsInfo.Os.ToString().ToLowerInvariant(),
                    arch = RuntimeInformation.OSArchitecture,
                    runtime = PlatformInfo.Platform.ToString().ToLowerInvariant(),
                    runtimeVer = _platformInfo.Version
                })
                .GetJsonAsync<UpdatePackageAvailable>();
            

            if (update is not {Available: true})
            {
                return null;
            }

            return update.UpdatePackage;
        }

        public async Task<List<UpdatePackage>> GetRecentUpdates(string branch, Version currentVersion)
        {
            var updates = await (UpdateApi + "/update/" + branch + "/changes")
                .SetQueryParams(new
                {
                    version = currentVersion,
                    os = OsInfo.Os.ToString().ToLowerInvariant(),
                    arch = RuntimeInformation.OSArchitecture,
                    runtime = PlatformInfo.Platform.ToString().ToLowerInvariant(),
                    runtimeVer = _platformInfo.Version
                })
                .GetJsonAsync<List<UpdatePackage>>();

            return updates;
        }
    }
}