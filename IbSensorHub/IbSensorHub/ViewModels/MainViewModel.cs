using ReactiveUI;
using System.Linq;
using System.Net;
using Grpc.Core;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System;
using IbSensorHub.ViewModels.Sensors;

namespace IbSensorHub.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public string HostName => Dns.GetHostName();

        private List<LocalAddress> _localAddresses;
        public List<LocalAddress> LocalAddresses
        {
            get => _localAddresses;
            set => this.RaiseAndSetIfChanged(ref _localAddresses, value);
        }

        private int _port;
        public int Port
        {
            get => _port;
            set => this.RaiseAndSetIfChanged(ref _port, value);
        }

        public string TargetAddress { get; set; }

        AccelerometerViewModel Accelerometer { get; set; } = new();
        GyroscopeViewModel Gyroscope { get; set; } = new();
        CompassViewModel Compass { get; set; } = new();
        GeolocationViewModel Geolocation { get; set; } = new();

        public MainViewModel()
        {
            var server = new Server
            {
                Services =
                {
                    Grpc.SensorService.BindService(new SensorService()),
                },
                Ports =
                {
                    new ServerPort("0.0.0.0", ServerPort.PickUnused, ServerCredentials.Insecure)
                }
            };
            server.Start();
            _port = server.Ports.First().BoundPort;
            _localAddresses = GetLocalAddresses();
            TargetAddress = $"localhost:{Port}";
        }

        public class LocalAddress
        {
            public string NetworkInterface { get; set; }
            public string Address { get; set; }
        }

        private List<LocalAddress> GetLocalAddresses()
        {
            /*
            string.Join("\n", Dns.GetHostEntry(HostName)
                .AddressList
                // Filter out IPv6 addresses (and others)
                .Where(addr => addr.AddressFamily is System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(addr => $"{addr}:{Port}")
                );
            */
            // The above approach doesn't work on Android, where only 127.0.0.1 is in the list.

            string interfaceDescription = string.Empty;
            var result = new List<IPAddress>();
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n is {
                        NetworkInterfaceType: not NetworkInterfaceType.Loopback,
                        OperationalStatus: OperationalStatus.Up
                    })
                    .Select(n => (n.GetIPProperties().UnicastAddresses, n))
                    .Where(v => v.UnicastAddresses.Any(addr => addr.Address.AddressFamily is AddressFamily.InterNetwork))
                    .OrderBy(v => v.n.NetworkInterfaceType switch
                    {
                        // TODO: Virtual NICs
                        NetworkInterfaceType.Wireless80211 => 0,
                        NetworkInterfaceType.Ethernet => 1,
                        _ => 2
                    })
                    .Select(v => new LocalAddress {
                        Address = v.UnicastAddresses.First(addr => addr.Address.AddressFamily is AddressFamily.InterNetwork).Address.ToString(),
                        NetworkInterface = $"{v.n.Name} ({v.n.Description})"
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to find IP addresses: {ex.Message}");
                return new();
            }
        }

        private bool _connected;
        public bool Connected
        {
            get => _connected;
            set => this.RaiseAndSetIfChanged(ref _connected, value);
        }
        private CancellationTokenSource? _readSensorCts;

        public async Task Connect()
        {
            if (string.IsNullOrWhiteSpace(TargetAddress))
            {
                return;
            }

            var channel = new Channel(TargetAddress, ChannelCredentials.Insecure);
            var client = new Grpc.SensorService.SensorServiceClient(channel);

            Grpc.DeviceInfo info;
            try
            {
                info = await client.GetDeviceInfoAsync(new());
                Debug.WriteLine(info);
            }
            catch (RpcException e)
            {
                Debug.WriteLine(e);
                return;
            }

            _readSensorCts = new();
            Connected = true;

            var sensorViewModels = new List<SensorViewModel>();
            var streamingCalls = new List<AsyncServerStreamingCall<Grpc.SensorData>>();
            var tryReadSensor = (Grpc.SensorType type, SensorViewModel viewModel) =>
            {
                if (info.SensorTypes.Contains(type))
                {
                    sensorViewModels.Add(viewModel);
                    streamingCalls.Add(client.ReadSensorChanges(new Grpc.Sensor
                    {
                        Type = type
                    }));
                };
            };
            tryReadSensor(Grpc.SensorType.Accelerometer, Accelerometer);
            tryReadSensor(Grpc.SensorType.Gyroscope, Gyroscope);
            tryReadSensor(Grpc.SensorType.Compass, Compass);
            tryReadSensor(Grpc.SensorType.Geolocation, Geolocation);

            var moveNextTasks = streamingCalls.Select(call => call.ResponseStream.MoveNext(_readSensorCts.Token)).ToArray();
            while (_readSensorCts.IsCancellationRequested is false)
            {
                var task = await Task.WhenAny(moveNextTasks);
                if (await task)
                {
                    // Renew the completed task
                    var index = Array.IndexOf(moveNextTasks, task);
                    moveNextTasks[index] = streamingCalls[index].ResponseStream.MoveNext(_readSensorCts.Token);

                    var current = streamingCalls[index].ResponseStream.Current;
                    sensorViewModels[index].UpdateValues(current.Values);
                }
                else
                {
                    Debug.WriteLine("Stream closed unexpectedly");
                }
            }
        }

        public void Disconnect()
        {
            _readSensorCts.Cancel();
            _readSensorCts = null;
            Connected = false;
        }
    }
}