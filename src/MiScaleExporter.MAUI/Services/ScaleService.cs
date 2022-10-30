﻿using MiScaleExporter.Models;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MiScaleExporter.Services
{
    public class ScaleService : IScaleService
    {
        private IAdapter _adapter;
        private IDevice _scaleDevice;
        private Scale _scale;
        private User _user;
        private TaskCompletionSource<BodyComposition> _completionSource;
        private BodyComposition bodyComposition;
        private ILogService _logService;
        private byte[] _scannedData;
        private byte[] _previousErrorData;

        public ScaleService(ILogService logService)
        {
            _logService = logService;
            _adapter = CrossBluetoothLE.Current.Adapter;
            _adapter.ScanTimeout = 50000;
            _adapter.ScanTimeoutElapsed += TimeOuted;
        }

        public async Task<Models.BodyComposition> GetBodyCompositonAsync(Scale scale, Models.User user)
        {
            bodyComposition = null;
            _completionSource = new TaskCompletionSource<BodyComposition>();

            _scale = scale;
            _user = user;
            _adapter.DeviceAdvertised += DeviceAdvertided;
            await _adapter.StartScanningForDevicesAsync();
            return await _completionSource.Task;
        }

        public async Task CancelSearchAsync()
        {
            try
            {
                if(bodyComposition != null)
                {
                    bodyComposition.IsValid = false;
                }
                if (!_completionSource.Task.IsCompleted)
                {
                    _completionSource.SetResult(bodyComposition);
                }
               
            }
            catch (Exception ex)
            {
                _logService.LogError(ex.Message);
            }

            await StopAsync();
        }

        private void TimeOuted(object s, EventArgs e)
        {
            StopAsync().Wait();
            _completionSource.SetResult(bodyComposition);
        }

        private void DeviceAdvertided(object s, DeviceEventArgs a)
        {
            var obj = a.Device.NativeDevice;
            PropertyInfo propInfo = obj.GetType().GetProperty("Address");
            string address = (string)propInfo.GetValue(obj, null);

            if (address.ToLowerInvariant() == _scale.Address?.ToLowerInvariant())
            {

                try
                {
                    _scaleDevice = a.Device;
                    if (!GetScanData())
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError(ex.Message);

                    if (_scannedData != null)
                    {
                        _logService.LogInfo(String.Join("; ", _scannedData));
                    }
                }
                finally
                {

                    if (bodyComposition != null)
                    {
                        StopAsync().Wait();
                        bodyComposition.IsValid = true;
                        _completionSource.SetResult(bodyComposition);
                    }
                }
            }

        }

        private bool GetScanData()
        {
            if (_scaleDevice != null)
            {
                var data = _scaleDevice.AdvertisementRecords
                    .Where(x => x.Type == Plugin.BLE.Abstractions.AdvertisementRecordType.ServiceData) //0x16
                    .Select(x => x.Data)
                    .FirstOrDefault();
                _scannedData = data;


                var bc = ComputeData(data);

                return bc != null;
            }

            return false;
        }

        private BodyComposition ComputeData(byte[] data)
        {
            switch (_user.ScaleType)
            {
                case ScaleType.MiBodyCompositionScale:

                    var miBodyCompositionScale = new MiScaleBodyComposition.MiScale();
                    var user = new MiScaleBodyComposition.User(_user.Height, _user.Age, (MiScaleBodyComposition.Sex)(byte)_user.Sex);
                    var isStabilized = miBodyCompositionScale.Istabilized(data, user);
                    var hasImpedance = miBodyCompositionScale.HasImpedance(data, user);
                    var doNotWaitForImpedance = Preferences.Get(PreferencesKeys.DoNotWaitForImpedance, false);

                    if (isStabilized && (doNotWaitForImpedance || hasImpedance))
                    {
                        if (Preferences.Get(PreferencesKeys.ShowReceivedByteArray, false))
                        {
                            _logService.LogInfo(String.Join("; ", data));
                        }
                        var bc = miBodyCompositionScale.GetBodyComposition(data, user);
                        bodyComposition = new BodyComposition
                        {
                            Weight = bc.Weight,
                            BMI = bc.BMI,
                            ProteinPercentage = bc.ProteinPercentage,
                            IdealWeight = bc.IdealWeight,
                            BMR = bc.BMR,
                            BoneMass = bc.BoneMass,
                            Fat = bc.Fat,
                            MetabolicAge = bc.MetabolicAge,
                            MuscleMass = bc.MuscleMass,
                            VisceralFat = bc.VisceralFat,
                            WaterPercentage = bc.Water,
                            BodyType = bc.BodyType,
                        };
                        return bodyComposition;
                    }
                    else
                    {
                        if (Preferences.Get(PreferencesKeys.ShowUnstabilizedData, false))
                        {
                            var dataString = String.Join("; ", data);
                            var previousDataString = String.Join("; ", _previousErrorData ?? new byte[0]);
                            if (previousDataString != dataString)
                            {
                                _logService.LogInfo("received data but it is unstable, or wrong scale type is selected\n" + dataString);
                                _previousErrorData = data;
                            }
                        }
                        return null;
                    }
                case ScaleType.MiSmartScale:
                    var legacyMiscale = new MiScaleBodyComposition.LegacyMiScale();

                    if (legacyMiscale.Istabilized(data))
                    {
                        if (Preferences.Get(PreferencesKeys.ShowReceivedByteArray, false))
                        {
                            _logService.LogInfo(String.Join("; ", data));
                        }

                        var legacyResult = legacyMiscale.GetWeight(data, _user.Height);

                        bodyComposition = new BodyComposition
                        {
                            Weight = legacyResult.Weight,
                            BMI = legacyResult.BMI,
                        };

                        return bodyComposition;
                    }
                    else
                    {
                        if (Preferences.Get(PreferencesKeys.ShowUnstabilizedData, false))
                        {
                            var dataString = String.Join("; ", data);
                            var previousDataString = String.Join("; ", _previousErrorData ?? new byte[0]);
                            if (previousDataString != dataString)
                            {
                                _logService.LogInfo("received data but it is unstable, or wrong scale type is selected\n" + dataString);
                                _previousErrorData = data;
                            }
                        }

                        return null;
                    }
                default:
                    throw new NotImplementedException();
            }


        }

        private async Task StopAsync()
        {
            await _adapter.StopScanningForDevicesAsync();

            _adapter.DeviceAdvertised -= DeviceAdvertided;
        }

    }
}