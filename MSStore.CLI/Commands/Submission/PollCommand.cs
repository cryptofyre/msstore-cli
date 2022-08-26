// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API.Models;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Submission
{
    internal class PollCommand : Command
    {
        public PollCommand()
            : base("poll", "Polls until the existing submission is PUBLISHED or FAILED.")
        {
            AddArgument(SubmissionCommand.ProductIdArgument);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;
            private readonly TelemetryClient _telemetryClient;
            private readonly IConsoleReader _consoleReader;
            private readonly IBrowserLauncher _browserLauncher;

            public string ProductId { get; set; } = null!;

            public Handler(ILogger<Handler> logger, IStoreAPIFactory storeAPIFactory, TelemetryClient telemetryClient, IConsoleReader consoleReader, IBrowserLauncher browserLauncher)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
                _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
                _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
                _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            }

            public int Invoke(InvocationContext context)
            {
                throw new NotImplementedException();
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                IStorePackagedAPI? storePackagedAPI = null;
                DevCenterSubmission? submission = null;

                var publishingStatus = await AnsiConsole.Status().StartAsync<object?>("Polling submission status", async ctx =>
                {
                    try
                    {
                        if (ProductTypeHelper.Solve(ProductId) == ProductType.Packaged)
                        {
                            storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            var application = await storePackagedAPI.GetApplicationAsync(ProductId, ct);

                            if (application?.Id == null)
                            {
                                ctx.ErrorStatus($"Could not find application with ID '{ProductId}'");
                                return -1;
                            }

                            submission = await storePackagedAPI.GetAnySubmissionAsync(application, ctx, _logger, ct);

                            if (submission?.Id == null)
                            {
                                ctx.ErrorStatus($"Could not find submission for application with ID '{ProductId}'");
                                return -1;
                            }

                            var lastSubmissionStatus = await storePackagedAPI.PollSubmissionStatusAsync(ProductId, submission.Id, false, _logger, ct: ct);

                            ctx.SuccessStatus();

                            return lastSubmissionStatus;
                        }
                        else
                        {
                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            var status = await storeAPI.GetModuleStatusAsync(ProductId, ct);

                            if (status?.ResponseData?.OngoingSubmissionId == null ||
                                status.ResponseData.OngoingSubmissionId.Length == 0)
                            {
                                ctx.ErrorStatus($"Could not find ongoing submission for application with ID '{ProductId}'");

                                return null;
                            }

                            ResponseWrapper<SubmissionStatus>? lastSubmissionStatus = null;
                            await foreach (var submissionStatus in storeAPI.PollSubmissionStatusAsync(ProductId, status.ResponseData.OngoingSubmissionId, false, ct))
                            {
                                AnsiConsole.MarkupLine($"Submission Status - [green]{submissionStatus.ResponseData?.PublishingStatus}[/]");
                                if (submissionStatus.Errors?.Any() == true)
                                {
                                    var table = new Table
                                    {
                                        Title = new TableTitle($":red_exclamation_mark: [b]Submission Errors[/]")
                                    };
                                    table.AddColumns("Code", "Details");
                                    foreach (var error in submissionStatus.Errors)
                                    {
                                        table.AddRow($"[bold u]{error.Code}[/]", error.Message!);
                                    }

                                    AnsiConsole.Write(table);
                                }

                                AnsiConsole.WriteLine();

                                lastSubmissionStatus = submissionStatus;
                            }

                            ctx.SuccessStatus();

                            return lastSubmissionStatus;
                        }
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while polling submission status");
                        ctx.ErrorStatus(err);
                    }

                    return null;
                });

                if (publishingStatus is DevCenterSubmissionStatusResponse lastSubmissionStatus)
                {
                    if (storePackagedAPI == null ||
                        submission?.Id == null)
                    {
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                    }

                    return await _telemetryClient.TrackCommandEventAsync<Handler>(
                        ProductId,
                        await storePackagedAPI.HandleLastSubmissionStatusAsync(lastSubmissionStatus, ProductId, submission.Id, _consoleReader, _browserLauncher, _logger, ct),
                        ct);
                }
                else if (publishingStatus is ResponseWrapper<SubmissionStatus> lastSubmissionStatusWrapper)
                {
                    if (lastSubmissionStatusWrapper.ResponseData?.PublishingStatus == PublishingStatus.FAILED)
                    {
                        AnsiConsole.WriteLine("Submission has failed.");

                        if (lastSubmissionStatusWrapper.Errors != null)
                        {
                            foreach (var error in lastSubmissionStatusWrapper.Errors)
                            {
                                _logger.LogError("Could not retrieve submission. Please try again.");
                                return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                            }
                        }

                        return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                    }
                    else
                    {
                        AnsiConsole.WriteLine("Submission commit success!");

                        return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, 0, ct);
                    }
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
            }
        }
    }
}
