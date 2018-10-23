﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client.Exceptions;
using Microsoft.Azure.IoTSolutions.DeviceSimulation.Services.DataStructures;
using Microsoft.Azure.IoTSolutions.DeviceSimulation.Services.Diagnostics;
using Microsoft.Azure.IoTSolutions.DeviceSimulation.Services.Exceptions;
using Microsoft.Azure.IoTSolutions.DeviceSimulation.Services.Models;
using Microsoft.Azure.IoTSolutions.DeviceSimulation.Services.Simulation;

namespace Microsoft.Azure.IoTSolutions.DeviceSimulation.SimulationAgent.DeviceConnection
{
    /// <summary>
    /// Establish a connection to Azure IoT Hub
    /// </summary>
    public class Connect : IDeviceConnectionLogic
    {
        private readonly IScriptInterpreter scriptInterpreter;
        private readonly ILogger log;
        private readonly IInstance instance;
        private string deviceId;
        private DeviceModel deviceModel;
        private IDeviceConnectionActor deviceContext;
        private ISimulationContext simulationContext;

        public Connect(
            IScriptInterpreter scriptInterpreter,
            ILogger logger,
            IInstance instance)
        {
            this.scriptInterpreter = scriptInterpreter;
            this.log = logger;
            this.instance = instance;
        }

        public void Init(IDeviceConnectionActor context, string deviceId, DeviceModel deviceModel)
        {
            this.instance.InitOnce();

            this.deviceContext = context;
            this.simulationContext = context.SimulationContext;
            this.deviceId = deviceId;
            this.deviceModel = deviceModel;

            this.instance.InitComplete();
        }

        public async Task RunAsync()
        {
            this.instance.InitRequired();

            this.log.Debug("Connecting...", () => new { this.deviceId });
            var st = DateTimeOffset.UtcNow;
            var start = st.ToUnixTimeMilliseconds();

            StringBuilder sb = new StringBuilder();
            var msg = string.Empty;

            try
            {
                // Ensure pending task are stopped
                this.deviceContext.DisposeClient();

                msg = $"Connecting, {this.deviceId}, {this.deviceContext.Connected}, {st}, {0}";
                sb.Append(msg + "\n");

                this.deviceContext.Client = this.simulationContext.Devices.GetClient(
                    this.deviceContext.Device,
                    this.deviceModel.Protocol,
                    this.scriptInterpreter);

                await this.deviceContext.Client.ConnectAsync();

                var resTime = DateTimeOffset.UtcNow;
                var responseTime = resTime.ToUnixTimeMilliseconds();
                var timeSpentMsecs = responseTime - start;
                msg = $"Connected, {this.deviceId}, {this.deviceContext.Connected}, {resTime}, {timeSpentMsecs}";
                sb.Append(msg + "\n");

                await this.deviceContext.Client.RegisterMethodsForDeviceAsync(
                    this.deviceModel.CloudToDeviceMethods,
                    this.deviceContext.DeviceState,
                    this.deviceContext.DeviceProperties);

                await this.deviceContext.Client.RegisterDesiredPropertiesUpdateAsync(this.deviceContext.DeviceProperties);

                resTime = DateTimeOffset.UtcNow;
                responseTime = resTime.ToUnixTimeMilliseconds();
                timeSpentMsecs = responseTime - start;
                this.log.Debug("Device connected", () => new { timeSpentMsecs, this.deviceId });
                this.deviceContext.HandleEvent(DeviceConnectionActor.ActorEvents.Connected);

                msg = $"PropsUpdated, {this.deviceId}, {this.deviceContext.Connected}, {resTime}, {timeSpentMsecs}";
                sb.Append(msg + "\n");
            }
            catch (DeviceAuthFailedException e)
            {
                var resTime = DateTimeOffset.UtcNow;
                var responseTime = resTime.ToUnixTimeMilliseconds();
                var timeSpentMsecs = responseTime - start;
                this.log.Error("Invalid connection credentials", () => new { timeSpentMsecs, this.deviceId, e });
                msg = $"DeviceAuthFailedException, {this.deviceId}, {this.deviceContext.Connected}, {resTime}, {timeSpentMsecs}";
                sb.Append(msg + "\n");

                this.deviceContext.HandleEvent(DeviceConnectionActor.ActorEvents.AuthFailed);
            }
            catch (DeviceNotFoundException e)
            {
                var resTime = DateTimeOffset.UtcNow;
                var responseTime = resTime.ToUnixTimeMilliseconds();
                var timeSpentMsecs = responseTime - start;
                this.log.Error("Device not found", () => new { timeSpentMsecs, this.deviceId, e });
                msg = $"DeviceNotFoundException, {this.deviceId}, {this.deviceContext.Connected}, {resTime}, {timeSpentMsecs}";
                sb.Append(msg + "\n");

                this.deviceContext.HandleEvent(DeviceConnectionActor.ActorEvents.DeviceNotFound);
            }
            catch (Exception e)
            {
                var resTime = DateTimeOffset.UtcNow;
                var responseTime = resTime.ToUnixTimeMilliseconds();
                var timeSpentMsecs = responseTime - start;
                this.log.Error("Connection error", () => new { timeSpentMsecs, this.deviceId, e });
                msg = $"ConnectionException + {e.Message}, {this.deviceId}, {this.deviceContext.Connected}, {resTime}, {timeSpentMsecs}";
                sb.Append(msg + "\n");

                this.deviceContext.HandleEvent(DeviceConnectionActor.ActorEvents.ConnectionFailed);
            }

            try
            {
                var filePath = "/tmp/share/device-connection-log.csv";
                this.log.LogToFile(filePath, sb.ToString());
            }
            catch
            {
                try
                {
                    var filePath = "/tmp/share/device-connection-log-1.csv";
                    this.log.LogToFile(filePath, sb.ToString());
                }
                catch (Exception ex)
                {
                    this.log.Write("Failed to log to file" + ex.Message);
                }
            }
        }
    }
}
