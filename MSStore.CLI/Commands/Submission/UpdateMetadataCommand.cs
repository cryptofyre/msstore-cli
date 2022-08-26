// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Submission
{
    internal class UpdateMetadataCommand : Command
    {
        public UpdateMetadataCommand()
            : base("updateMetadata", "Updates the existing draft submission metadata with the provided JSON.")
        {
            var metadata = new Argument<string>(
                name: "metadata",
                description: "The updated JSON metadata representation.");

            AddArgument(SubmissionCommand.ProductIdArgument);
            AddArgument(metadata);
            AddOption(SubmissionCommand.SkipInitialPolling);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;
            private readonly TelemetryClient _telemetryClient;

            public string Metadata { get; set; } = null!;
            public bool SkipInitialPolling { get; set; }
            public string ProductId { get; set; } = null!;

            public Handler(ILogger<Handler> logger, IStoreAPIFactory storeAPIFactory, TelemetryClient telemetryClient)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
                _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            }

            public int Invoke(InvocationContext context)
            {
                throw new NotImplementedException();
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                object? updateSubmissionData = null;

                if (ProductTypeHelper.Solve(ProductId) == ProductType.Packaged)
                {
                    updateSubmissionData = await UpdateCommand.Handler.PackagedUpdateCommandAsync(_storeAPIFactory, Metadata, ProductId, _logger, ct);
                }
                else
                {
                    var submissionMetadata = JsonSerializer.Deserialize(Metadata, SourceGenerationContext.GetCustom().UpdateMetadataRequest);

                    if (submissionMetadata == null)
                    {
                        throw new MSStoreException("Invalid metadata provided.");
                    }

                    updateSubmissionData = await AnsiConsole.Status().StartAsync("Updating submission metadata", async ctx =>
                    {
                        try
                        {
                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            return await storeAPI.UpdateSubmissionMetadataAsync(ProductId, submissionMetadata, SkipInitialPolling, ct);
                        }
                        catch (Exception err)
                        {
                            _logger.LogError(err, "Error while updating submission metadata.");
                            ctx.ErrorStatus(err);
                            return null;
                        }
                    });
                }

                if (updateSubmissionData == null)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                }

                AnsiConsole.WriteLine(JsonSerializer.Serialize(updateSubmissionData, updateSubmissionData.GetType(), SourceGenerationContext.GetCustom(true)));

                return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, 0, ct);
            }
        }
    }
}
