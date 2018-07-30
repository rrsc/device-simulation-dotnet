﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;

namespace Microsoft.Azure.IoTSolutions.DeviceSimulation.SimulationAgent.DeviceProperties
{
    public interface IDevicePropertiesLogic
    {
        void Init(IDevicePropertiesActor deviceContext, string deviceId);
        Task RunAsync();
    }
}
