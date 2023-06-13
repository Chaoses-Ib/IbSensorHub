using Avalonia.Threading;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace IbSensorHub
{
    internal class SensorService : Grpc.SensorService.SensorServiceBase
    {
        private bool IsSupported(Action f)
        {
            try
            {
                f();
            }
            catch (FeatureNotSupportedException)
            {
                return false;
            }
            catch (FeatureNotEnabledException ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
            catch (PermissionException ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
            catch (NotImplementedInReferenceAssemblyException)
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
            return true;
        }

        private async Task<bool> IsSupportedAsync(Func<Task> f)
        {
            try
            {
                await f();
            }
            catch (FeatureNotSupportedException)
            {
                return false;
            }
            catch (FeatureNotEnabledException ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
            catch (PermissionException ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
            catch (NotImplementedInReferenceAssemblyException)
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
            return true;
        }

        public override async Task<Grpc.DeviceInfo> GetDeviceInfo(Empty request, ServerCallContext context)
        {
            var info = new Grpc.DeviceInfo
            {
                HostName = Dns.GetHostName(),
            };
            if (IsSupported(Accelerometer.Stop))
            {
                info.SensorTypes.Add(Grpc.SensorType.Accelerometer);
                info.SensorTypes.Add(Grpc.SensorType.Shake);
            }
            if (IsSupported(Barometer.Stop))
            {
                info.SensorTypes.Add(Grpc.SensorType.Barometer);
            }
            if (IsSupported(Compass.Stop))
            {
                info.SensorTypes.Add(Grpc.SensorType.Compass);
            }
            if (await IsSupportedAsync(() => Dispatcher.UIThread.InvokeAsync(Geolocation.GetLastKnownLocationAsync)))
            {
                // Xamarin.Essentials.PermissionException: Permission request must be invoked on main thread.

                info.SensorTypes.Add(Grpc.SensorType.Geolocation);
            }
            if (IsSupported(Gyroscope.Stop))
            {
                info.SensorTypes.Add(Grpc.SensorType.Gyroscope);
            }
            if (IsSupported(Magnetometer.Stop))
            {
                info.SensorTypes.Add(Grpc.SensorType.Magnetometer);
            }
            if (IsSupported(OrientationSensor.Stop))
            {
                info.SensorTypes.Add(Grpc.SensorType.Orientation);
            }
            return info;
        }

        public override async Task ReadSensorChanges(Grpc.Sensor request, IServerStreamWriter<Grpc.SensorData> responseStream, ServerCallContext context)
        {
            // TODO: Reference counts

            Action onCompleted = () => { };
            try
            {
                switch (request.Type)
                {
                    case Grpc.SensorType.Accelerometer:
                        {
                            EventHandler<AccelerometerChangedEventArgs> onReadingChanged = (sender, e) =>
                            {
                                var data = new Grpc.SensorData
                                {
                                    Time = Timestamp.FromDateTime(DateTime.UtcNow),
                                };
                                data.Values.Add(e.Reading.Acceleration.X);
                                data.Values.Add(e.Reading.Acceleration.Y);
                                data.Values.Add(e.Reading.Acceleration.Z);
                                responseStream.WriteAsync(data).GetAwaiter().GetResult();
                            };
                            Accelerometer.ReadingChanged += onReadingChanged;
                            if (Accelerometer.IsMonitoring is false)
                            {
                                Accelerometer.Start(SensorSpeed.UI);
                            }
                            onCompleted += () =>
                            {
                                Accelerometer.Stop();
                                Accelerometer.ReadingChanged -= onReadingChanged;
                            };
                            break;
                        }
                    case Grpc.SensorType.Shake:
                        {
                            EventHandler onReadingChanged = (sender, e) =>
                            {
                                var data = new Grpc.SensorData
                                {
                                    Time = Timestamp.FromDateTime(DateTime.UtcNow),
                                };
                                responseStream.WriteAsync(data).GetAwaiter().GetResult();
                            };
                            Accelerometer.ShakeDetected += onReadingChanged;
                            if (Accelerometer.IsMonitoring is false)
                            {
                                Accelerometer.Start(SensorSpeed.UI);
                            }
                            onCompleted += () =>
                            {
                                Accelerometer.Stop();
                                Accelerometer.ShakeDetected -= onReadingChanged;
                            };
                            break;
                        }
                    case Grpc.SensorType.Barometer:
                        {
                            EventHandler<BarometerChangedEventArgs> onReadingChanged = (sender, e) =>
                            {
                                var data = new Grpc.SensorData
                                {
                                    Time = Timestamp.FromDateTime(DateTime.UtcNow),
                                };
                                // TODO: double
                                data.Values.Add((float)e.Reading.PressureInHectopascals);
                                responseStream.WriteAsync(data).GetAwaiter().GetResult();
                            };
                            Barometer.ReadingChanged += onReadingChanged;
                            if (Barometer.IsMonitoring is false)
                            {
                                Barometer.Start(SensorSpeed.UI);
                            }
                            onCompleted += () =>
                            {
                                Barometer.Stop();
                                Barometer.ReadingChanged -= onReadingChanged;
                            };
                            break;
                        }
                    case Grpc.SensorType.Compass:
                        {
                            EventHandler<CompassChangedEventArgs> onReadingChanged = (sender, e) =>
                            {
                                var data = new Grpc.SensorData
                                {
                                    Time = Timestamp.FromDateTime(DateTime.UtcNow),
                                };
                                // TODO: double
                                data.Values.Add((float)e.Reading.HeadingMagneticNorth);
                                responseStream.WriteAsync(data).GetAwaiter().GetResult();
                            };
                            Compass.ReadingChanged += onReadingChanged;
                            if (Compass.IsMonitoring is false)
                            {
                                Compass.Start(SensorSpeed.UI);
                            }
                            onCompleted += () =>
                            {
                                Compass.Stop();
                                Compass.ReadingChanged -= onReadingChanged;
                            };
                            break;
                        }
                    case Grpc.SensorType.Geolocation:
                        {
                            var locationRequest = new GeolocationRequest(GeolocationAccuracy.Default, TimeSpan.FromSeconds(60));
                                
                            var location = await Geolocation.GetLastKnownLocationAsync();
                            while (true)
                            {
                                if (location is not null)
                                {
                                    var data = new Grpc.SensorData
                                    {
                                        Time = Timestamp.FromDateTimeOffset(location.Timestamp),
                                    };
                                    data.Values.Add((float)location.Latitude);
                                    data.Values.Add((float)location.Longitude);
                                    data.Values.Add((float)(location.Altitude ?? double.NaN));
                                    data.Values.Add((float)(location.Speed ?? double.NaN));
                                    data.Values.Add((float)(location.Course ?? double.NaN));
                                    data.Values.Add((float)(location.Accuracy ?? double.NaN));
                                    data.Values.Add((float)(location.VerticalAccuracy ?? double.NaN));
                                    await responseStream.WriteAsync(data);
                                }

                                location = await Geolocation.GetLocationAsync(locationRequest);
                            }
                            break;
                        }
                    case Grpc.SensorType.Gyroscope:
                        {
                            EventHandler<GyroscopeChangedEventArgs> onReadingChanged = (sender, e) =>
                            {
                                var data = new Grpc.SensorData
                                {
                                    Time = Timestamp.FromDateTime(DateTime.UtcNow),
                                };
                                data.Values.Add(e.Reading.AngularVelocity.X);
                                data.Values.Add(e.Reading.AngularVelocity.Y);
                                data.Values.Add(e.Reading.AngularVelocity.Z);
                                responseStream.WriteAsync(data).GetAwaiter().GetResult();
                            };
                            Gyroscope.ReadingChanged += onReadingChanged;
                            if (Gyroscope.IsMonitoring is false)
                            {
                                Gyroscope.Start(SensorSpeed.UI);
                            }
                            onCompleted += () =>
                            {
                                Gyroscope.Stop();
                                Gyroscope.ReadingChanged -= onReadingChanged;
                            };
                            break;
                        }
                    case Grpc.SensorType.Magnetometer:
                        {
                            EventHandler<MagnetometerChangedEventArgs> onReadingChanged = (sender, e) =>
                            {
                                var data = new Grpc.SensorData
                                {
                                    Time = Timestamp.FromDateTime(DateTime.UtcNow),
                                };
                                data.Values.Add(e.Reading.MagneticField.X);
                                data.Values.Add(e.Reading.MagneticField.Y);
                                data.Values.Add(e.Reading.MagneticField.Z);
                                responseStream.WriteAsync(data).GetAwaiter().GetResult();
                            };
                            Magnetometer.ReadingChanged += onReadingChanged;
                            if (Magnetometer.IsMonitoring is false)
                            {
                                Magnetometer.Start(SensorSpeed.UI);
                            }
                            onCompleted += () =>
                            {
                                Magnetometer.Stop();
                                Magnetometer.ReadingChanged -= onReadingChanged;
                            };
                            break;
                        }
                    case Grpc.SensorType.Orientation:
                        {
                            EventHandler<OrientationSensorChangedEventArgs> onReadingChanged = (sender, e) =>
                            {
                                var data = new Grpc.SensorData
                                {
                                    Time = Timestamp.FromDateTime(DateTime.UtcNow),
                                };
                                data.Values.Add(e.Reading.Orientation.X);
                                data.Values.Add(e.Reading.Orientation.Y);
                                data.Values.Add(e.Reading.Orientation.Z);
                                data.Values.Add(e.Reading.Orientation.W);
                                responseStream.WriteAsync(data).GetAwaiter().GetResult();
                            };
                            OrientationSensor.ReadingChanged += onReadingChanged;
                            if (OrientationSensor.IsMonitoring is false)
                            {
                                OrientationSensor.Start(SensorSpeed.UI);
                            }
                            onCompleted += () =>
                            {
                                OrientationSensor.Stop();
                                OrientationSensor.ReadingChanged -= onReadingChanged;
                            };
                            break;
                        }
                }
                await Task.Delay(Timeout.Infinite);
            }
            catch (FeatureNotSupportedException ex)
            {
                // Feature not supported on device
                Debug.WriteLine(ex);
            }
            catch (NotImplementedInReferenceAssemblyException)
            {
#if DEBUG
                // Mock sensor data
                try
                {
                    var rnd = new Random();
                    while (true)
                    {
                        var data = new Grpc.SensorData
                        {
                            Time = Timestamp.FromDateTime(DateTime.UtcNow),
                        };
                        data.Values.Add((float)rnd.NextDouble() * 360);
                        for (int i = 0; i < 6; i++)
                        {
                            data.Values.Add((float)rnd.NextDouble());
                        }
                        await responseStream.WriteAsync(data);
                        await Task.Delay(100);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
#endif
            }
            catch (RpcException ex)
            {
                Debug.WriteLine(ex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                onCompleted();
            }
        }
    }
}
