// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;

namespace MSStore.CLI.ProjectConfigurators
{
    internal interface IProjectPackager
    {
        Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct);
    }
}
