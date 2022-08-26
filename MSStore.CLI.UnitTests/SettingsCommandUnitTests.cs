// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services.Telemetry;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class SettingsCommandUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public void Init()
        {
            FakeLogin();
        }

        [TestMethod]
        public async Task SettingsCommandWithNoParametersShouldReturnZeroAndShowHelp()
        {
            AddDefaultFakeAccount();

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "settings"
                });

            result.Should().Contain("Usage:");
            result.Should().Contain("settings [options]");
        }

        [TestMethod]
        public async Task SettingsCommandShouldSetTelemetrySettingToTrue()
        {
            AddDefaultFakeAccount();

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "settings",
                    "-t"
                });

            FakeTelemetryConfigurationManager
                .Verify(x => x.SaveAsync(It.Is<TelemetryConfigurations>(tc => tc.TelemetryEnabled == true), It.IsAny<CancellationToken>()));
        }

        [TestMethod]
        public async Task SettingsCommandShouldSetTelemetrySettingToFalse()
        {
            AddDefaultFakeAccount();

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "settings",
                    "-t",
                    "false"
                });

            FakeTelemetryConfigurationManager
                .Verify(x => x.SaveAsync(It.Is<TelemetryConfigurations>(tc => tc.TelemetryEnabled == false), It.IsAny<CancellationToken>()));
        }
    }
}