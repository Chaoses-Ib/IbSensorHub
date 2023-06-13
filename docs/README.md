# Documentation
## Android
x86-64 is not supported since [Grpc.Core.Xamarin](https://www.nuget.org/packages/Grpc.Core.Xamarin) does not provide the x86-64 library. You should debug in a x86 emulator.

For releasing, `IbSensorHub.Android.csproj.user` should containg the following properties:
```xml
<PropertyGroup>
  <AndroidKeyStore>True</AndroidKeyStore>
  <AndroidSigningKeyStore>key.private.jks</AndroidSigningKeyStore>
  <AndroidSigningStorePass>...</AndroidSigningStorePass>
  <AndroidSigningKeyPass>...</AndroidSigningKeyPass>
</PropertyGroup>
```