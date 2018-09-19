/*
 * Licensed to the OpenSkywalking under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SkyWalking.Config;
using SkyWalking.Logging;
using SkyWalking.NetworkProtocol;

namespace SkyWalking.Transport.Grpc
{
    public class GrpcInstrumentationClient : IInstrumentationClient
    {
        private readonly ConnectionManager _connectionManager;
        private readonly IInstrumentationLogger _logger;
        private readonly GrpcConfig _config;

        public GrpcInstrumentationClient(ConnectionManager connectionManager, IConfigAccessor configAccessor, IInstrumentationLoggerFactory loggerFactory)
        {
            _connectionManager = connectionManager;
            _config = configAccessor.Get<GrpcConfig>();
            _logger = loggerFactory.CreateLogger(typeof(GrpcInstrumentationClient));
        }

        public async Task<NullableValue> RegisterApplicationAsync(string applicationCode, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!_connectionManager.Ready)
            {
                return NullableValue.Null;
            }

            var connection = _connectionManager.GetConnection();

            var client = new ApplicationRegisterService.ApplicationRegisterServiceClient(connection);

            try
            {
                var applicationMapping = await client.applicationCodeRegisterAsync(new Application {ApplicationCode = applicationCode},
                    null, _config.GetTimeout(), cancellationToken);

                return new NullableValue(applicationMapping?.Application?.Value ?? 0);
            }
            catch (Exception ex)
            {
                _logger.Error("Register application error.", ex);
                _connectionManager.Failure(ex);
                return NullableValue.Null;
            }
        }

        public async Task<NullableValue> RegisterApplicationInstanceAsync(int applicationId, Guid agentUUID, long registerTime, AgentOsInfoRequest osInfoRequest,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!_connectionManager.Ready)
            {
                return NullableValue.Null;
            }

            var connection = _connectionManager.GetConnection();

            var client = new InstanceDiscoveryService.InstanceDiscoveryServiceClient(connection);

            try
            {
                var applicationInstance = new ApplicationInstance
                {
                    ApplicationId = applicationId,
                    AgentUUID = agentUUID.ToString("N"),
                    RegisterTime = registerTime,
                    Osinfo = new OSInfo
                    {
                        OsName = osInfoRequest.OsName,
                        Hostname = osInfoRequest.HostName,
                        ProcessNo = osInfoRequest.ProcessNo
                    }
                };

                applicationInstance.Osinfo.Ipv4S.AddRange(osInfoRequest.IpAddress);

                var applicationInstanceMapping = await client.registerInstanceAsync(applicationInstance, null, _config.GetTimeout(), cancellationToken);
                return new NullableValue(applicationInstanceMapping.ApplicationInstanceId);
            }
            catch (Exception ex)
            {
                _logger.Error("Register application instance error.", ex);
                _connectionManager.Failure(ex);
                return NullableValue.Null;
            }
        }

        public async Task HeartbeatAsync(int applicationInstance, long heartbeatTime, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!_connectionManager.Ready)
            {
                return;
            }

            var connection = _connectionManager.GetConnection();

            var client = new InstanceDiscoveryService.InstanceDiscoveryServiceClient(connection);

            var heartbeat = new ApplicationInstanceHeartbeat
            {
                ApplicationInstanceId = applicationInstance,
                HeartbeatTime = heartbeatTime
            };
            try
            {
                await client.heartbeatAsync(heartbeat, null, _config.GetTimeout(), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Error("Heartbeat error.", ex);
                _connectionManager.Failure(ex);
            }
        }

        public Task CollectAsync(IEnumerable<TraceSegmentRequest> request, CancellationToken cancellationToken = default(CancellationToken))
        {
           return Task.CompletedTask;
        }
    }
}