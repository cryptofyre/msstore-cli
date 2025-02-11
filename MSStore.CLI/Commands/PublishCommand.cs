// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using MSStore.CLI.Helpers;
using MSStore.CLI.ProjectConfigurators;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands
{
    internal class PublishCommand : Command
    {
        public PublishCommand()
            : base("publish", "Publishes your Application to the Microsoft Store.")
        {
            AddArgument(InitCommand.PathOrUrl);

            var inputDirectory = new Option<DirectoryInfo?>(
                aliases: new string[] { "--inputDirectory", "-i" },
                description: "The directory where the '.msix' or '.msixupload' file to be used for the publishing command. If not provided, the cli will try to find the best candidate based on the 'pathOrUrl' argument.",
                parseArgument: result =>
                {
                    if (result.Tokens.Count == 0)
                    {
                        return null;
                    }

                    string? directoryPath = result.Tokens.Single().Value;
                    if (!Directory.Exists(directoryPath))
                    {
                        result.ErrorMessage = "Input directory does not exist.";
                        return null;
                    }
                    else
                    {
                        return new DirectoryInfo(directoryPath);
                    }
                });

            AddOption(inputDirectory);
        }

        public new class Handler : ICommandHandler
        {
            private readonly IProjectConfiguratorFactory _projectConfiguratorFactory;
            private readonly IStoreAPIFactory _storeAPIFactory;
            private readonly TelemetryClient _telemetryClient;

            public string PathOrUrl { get; set; } = null!;

            public DirectoryInfo? InputDirectory { get; set; } = null!;

            public Handler(
                IProjectConfiguratorFactory projectConfiguratorFactory,
                IStoreAPIFactory storeAPIFactory,
                TelemetryClient telemetryClient)
            {
                _projectConfiguratorFactory = projectConfiguratorFactory ?? throw new ArgumentNullException(nameof(projectConfiguratorFactory));
                _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
                _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            }

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                var configurator = await _projectConfiguratorFactory.FindProjectConfiguratorAsync(PathOrUrl, ct);

                var props = new Dictionary<string, string>();

                if (configurator == null)
                {
                    AnsiConsole.WriteLine(CultureInfo.InvariantCulture, "We could not find a project configurator for the project at '{0}'.", PathOrUrl);
                    props["ProjType"] = "NF";
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, props, ct);
                }

                props["ProjType"] = configurator.ConfiguratorProjectType;

                var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                AnsiConsole.WriteLine($"This seems to be a {configurator.ConfiguratorProjectType} project.");

                var projectPublisher = configurator as IProjectPublisher;
                if (projectPublisher == null)
                {
                    AnsiConsole.WriteLine(CultureInfo.InvariantCulture, "We can't publish this type of project.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-5, props, ct);
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(
                    await projectPublisher.PublishAsync(PathOrUrl, null, InputDirectory, storePackagedAPI, ct), props, ct);
            }
        }
    }
}
