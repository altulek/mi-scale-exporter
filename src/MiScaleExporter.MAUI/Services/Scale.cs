using MiScaleExporter.Models;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Exceptions;
using Plugin.BLE.Android;
using System.Reflection;
using System.Text;

namespace MiScaleExporter.Services
{
    public class Scale : IScale
    {
        private ILogService _logService;
        private IDataInterpreter _dataInterpreter;

        private IAdapter _adapter;
        private TaskCompletionSource<BodyComposition> _completionSource;
        private TaskCompletionSource<List<BodyComposition>> _historyCompletionSource;

        private BodyComposition _lastSuccessfulBodyComposition;
        private List<BodyComposition> _bodyCompositionHistory = new List<BodyComposition> { };
        private byte[] _scannedData;
        private string _scaleBlutetoothAddress;
        private DateTime? _lastSuccessfulMeasure;
        private static bool _impedanceWaitFinished = false;
        private bool _impedanceWaitStarted = false;
        private int _minWeight = 10; // in kilograms
        private IDevice _device;
        private byte[] _deviceId;
        private User _user;

        private BodyComposition _receivedBodyComposition;

        public BodyComposition BodyComposition
        {
            get { return _receivedBodyComposition; }
            set { _receivedBodyComposition = value; }
        }

        public Scale(ILogService logService, IDataInterpreter dataInterpreter)
        {
            _logService = logService;

            _adapter = CrossBluetoothLE.Current.Adapter;
            _adapter.ScanTimeout = 50000;
            _adapter.ScanTimeoutElapsed += TimeOuted;
            _dataInterpreter = dataInterpreter;
        }

        public async Task<BodyComposition> GetBodyCompositonAsync(string scaleAddress, User user)
        {
            this.BodyComposition = null;
            _lastSuccessfulBodyComposition = null;
            _receivedBodyComposition = null;
            _impedanceWaitFinished = false;
            _impedanceWaitStarted = false;

            _user = user;
            _scaleBlutetoothAddress = scaleAddress;
            _completionSource = new TaskCompletionSource<BodyComposition>();
            _adapter.DeviceAdvertised += DeviceAdvertided;

            await _adapter.StartScanningForDevicesAsync();
            return await _completionSource.Task;
        }

        public async Task<List<BodyComposition>> GetHistoryAsync(string scaleAddress, byte[] deviceId, User user)
        {
            _deviceId = deviceId;
            _user = user;
            _scaleBlutetoothAddress = scaleAddress;
            _historyCompletionSource = new TaskCompletionSource<List<BodyComposition>>();
            _adapter.DeviceAdvertised += GetHistoryFromScale;
            await _adapter.StartScanningForDevicesAsync();

            return await _historyCompletionSource.Task;
        }

        public async Task<bool> ClearHistoryAsync()
        {
            bool cleared = await ClearHistoryOnScale();
            _bodyCompositionHistory = new List<BodyComposition>();
            SetPreviewHistory();
            return cleared;
        }

        private async void DeviceAdvertided(object s, DeviceEventArgs a)
        {
            var obj = a.Device.NativeDevice;
            PropertyInfo propInfo = obj.GetType().GetProperty("Address");
            string address = (string)propInfo.GetValue(obj, null);

            if (address.ToLowerInvariant() == _scaleBlutetoothAddress?.ToLowerInvariant())
            {

                try
                {
                    var device = a.Device;
                    var bodyCompositionCandidate = GetScanData(device);
                    if(bodyCompositionCandidate is not null && bodyCompositionCandidate.Weight > _minWeight)
                    {
                        this.BodyComposition = bodyCompositionCandidate;
                    }
                   
                    this.ProcessReceivedData();
                }
                catch (Exception ex)
                {
                    _logService.LogError(ex.Message);

                    if (_scannedData != null)
                    {
                        _logService.LogInfo(string.Join("; ", _scannedData));
                    }
                }
                finally
                {

                    if (this.BodyComposition != null && (this.BodyComposition.HasImpedance || _impedanceWaitFinished))
                    {
                        StopAsync().Wait();
                        _lastSuccessfulMeasure = this.BodyComposition.Date;
                        this.BodyComposition.IsValid = true;
                        _completionSource.SetResult(this.BodyComposition);
                    }
                }
            }
        }

        private async void GetHistoryFromScale(object s, DeviceEventArgs a)
        {            
            var obj = a.Device.NativeDevice;
            PropertyInfo propInfo = obj.GetType().GetProperty("Address");
            string address = (string)propInfo.GetValue(obj, null);

            if (address.ToLowerInvariant() == _scaleBlutetoothAddress?.ToLowerInvariant())
            {
                try
                {
                    var device = _device = a.Device;

                    await _adapter.StopScanningForDevicesAsync(); // nie trzeba juz skanować
                    await _adapter.ConnectToDeviceAsync(device);

                    var service = await device.GetServiceAsync(new Guid("0000181b-0000-1000-8000-00805f9b34fb"));
                    var characteristic = await service.GetCharacteristicAsync(new Guid("00002a2f-0000-3512-2118-0009af100700"));

                    characteristic.ValueUpdated += OnHistoryNotificationRecivied;

                    byte[] getDataBytes = new byte[5];
                    getDataBytes[0] = 0x01;
                    Array.Copy(_deviceId, 0, getDataBytes,1, _deviceId.Length);

                    await characteristic.StartUpdatesAsync();
                    //await characteristic.WriteAsync(new byte[] { 0x01, 0x9c, 0xb7, 0x90, 0xa2 });
                    await characteristic.WriteAsync(getDataBytes);
                    await characteristic.WriteAsync(new byte[] { 0x02 });
                }
                catch (Exception ex)
                {
                    _logService.LogError(ex.Message);
                }
                finally
                {
                    StopAsync().Wait();
                }
            }
        }

        private async Task<bool> ClearHistoryOnScale()
        {
            try
            {
                await _adapter.ConnectToDeviceAsync(_device);

                var service = await _device.GetServiceAsync(new Guid("0000181b-0000-1000-8000-00805f9b34fb"));
                var characteristic = await service.GetCharacteristicAsync(new Guid("00002a2f-0000-3512-2118-0009af100700"));


                byte[] clearDataBytes = new byte[5];
                clearDataBytes[0] = 0x04;
                Array.Copy(_deviceId, 0, clearDataBytes, 1, _deviceId.Length);

                await characteristic.StartUpdatesAsync();
                await characteristic.WriteAsync(new byte[] { 0x03 });
                //await characteristic.WriteAsync(new byte[] { 0x04, 0x9c, 0xb7, 0x90, 0xa2 });
                await characteristic.WriteAsync(clearDataBytes);

                await _adapter.DisconnectDeviceAsync(_device);
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex.Message);
                return false;
            }
            finally
            {
                
            }
        }

        private void OnHistoryNotificationRecivied(object o, CharacteristicUpdatedEventArgs args)
        {
            var bytes = args.Characteristic.Value;
            Console.WriteLine("[<<] {0}", BitConverter.ToString(bytes));

            // mamy dane z wagi
            if (bytes.Length == 13)
            {
                var bc = this._dataInterpreter.ComputeData(bytes, _user);
                if( bc != null )
                {
                    this._bodyCompositionHistory.Add(bc);
                    Console.WriteLine("Waga: {0}, BMI: {1}, data: {2}", bc.Weight, bc.BMI, bc.Date);
                }                
            }

            // koniec danych
            if (bytes.Length == 1 && bytes[0] == 0x03)
            {
                this.SetPreviewHistory();
                _historyCompletionSource.SetResult(this._bodyCompositionHistory);
            }
        }

        private void ProcessReceivedData()
        {
            if (this.BodyComposition == null)
            {
                return;
            }

            this.SetPreviews();

            _lastSuccessfulBodyComposition = this.BodyComposition;

            if (!this.BodyComposition.IsStabilized)
            {
                this.BodyComposition = null;
                return;
            }
            else
            {

                if (_lastSuccessfulMeasure != null && _lastSuccessfulMeasure >= this.BodyComposition.Date)
                {
                    this.BodyComposition = null;

                    return;
                }
                if (!_impedanceWaitStarted)
                {
                    _impedanceWaitStarted = true;
                    Task.Factory.StartNew(async () =>
                    {
                        var seconds = 5;
                        await Task.Delay(TimeSpan.FromSeconds(seconds));
                        _impedanceWaitStarted = false;
                        _impedanceWaitFinished = true;

                    });
                }
            }
        }

        private void SetPreviewHistory()
        {
            ScaleMeasurement.Instance.History = this._bodyCompositionHistory;
        }
        private void SetPreviews()
        {
            if (Preferences.Get(PreferencesKeys.ShowDebugInfo, false))
            {
                ScaleMeasurement.Instance.FoundScale = this.BodyComposition != null ? "Connected to scale: Yes" : "Connected to scale: No"; ;
                ScaleMeasurement.Instance.DebugData = (this.BodyComposition.IsStabilized ? "Stabilized: Yes" : "Stabilized: No") + " " + (this.BodyComposition.HasImpedance ? "Impedance: Yes" : "Impedance: No");
                ScaleMeasurement.Instance.RawData = string.Join("|", this.BodyComposition.ReceivedRawData);
            }

            ScaleMeasurement.Instance.Weight = this.BodyComposition.Weight.ToString("0.##") + "kg";
        }

        private BodyComposition GetScanData(IDevice device)
        {
            if (device != null)
            {
                var data = device.AdvertisementRecords
                    .Where(x => x.Type == Plugin.BLE.Abstractions.AdvertisementRecordType.ServiceData) //0x16
                    .Select(x => x.Data)
                    .FirstOrDefault();
                _scannedData = data;

                var bc = this._dataInterpreter.ComputeData(data, _user);
                if (bc is not null)
                {
                    bc.ReceivedRawData = _scannedData;
                }

                return bc;
            }

            return null;
        }

        private void CalculateBMIIfEmpty()
        {
            if (this.BodyComposition is not null && this.BodyComposition.BMI == 0 && _user.Height != 0)
            {
                var heightInMeters = (double)_user.Height / 100;
                this.BodyComposition.BMI = Math.Round(this.BodyComposition.Weight / (heightInMeters * heightInMeters), 2);
            }
        }

        public async Task CancelSearchAsync()
        {
            try
            {
                if (this.BodyComposition != null)
                {
                    this.BodyComposition.IsValid = false;
                }
                if (!_completionSource.Task.IsCompleted)
                {
                    _completionSource.SetResult(this.BodyComposition);
                }

            }
            catch (Exception ex)
            {
                _logService.LogError(ex.Message);
            }

            await StopAsync();
        }

        public void StopSearch()
        {
            StopAsync().Wait();

            if (this.BodyComposition is not null)
            {
                this.BodyComposition.IsValid = true;
            }
            else if (_lastSuccessfulBodyComposition is not null)
            {
                this.BodyComposition = _lastSuccessfulBodyComposition;
                this.BodyComposition.IsValid = true;
            }
            CalculateBMIIfEmpty();
        }

        private void TimeOuted(object s, EventArgs e)
        {
            StopAsync().Wait();
            if(this.BodyComposition != null)
            {
                _completionSource.SetResult(this.BodyComposition);
            }
            
            _historyCompletionSource.SetResult(this._bodyCompositionHistory);
        }

        private async Task StopAsync()
        {
            await _adapter.StopScanningForDevicesAsync();

            _adapter.DeviceAdvertised -= GetHistoryFromScale;
            _adapter.DeviceAdvertised -= DeviceAdvertided;
        }
    }
}