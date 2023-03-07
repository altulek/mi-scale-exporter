﻿using MiScaleExporter.Models;
using MiScaleExporter.Services;
using MiScaleExporter.Permission;

namespace MiScaleExporter.MAUI.ViewModels
{
    public class ScaleViewModel : BaseViewModel, IScaleViewModel
    {
        private readonly IScale _scale;
        private readonly ILogService _logService;

        private string _address;
        private int _age;
        private int _height;
        private Models.Sex _sex;
        private ScaleType _scaleType;

        public ScaleViewModel(IScale scale, ILogService logService)
        {
            _scale = scale;
            _logService = logService;

            Title = "Mi Scale Data";
            CancelCommand = new Command(OnCancel);
            StopCommand = new Command(OnStop);
        }

        public async Task CheckPreferencesAsync()
        {
            ScaleMeasurement.Instance.Weight = "";
            App.BodyComposition = null;
            var hasPermissions = await CheckPermissions();

            if(hasPermissions)
            {
                await this.LoadPreferencesAsync();
                if (!string.IsNullOrWhiteSpace(_address))
                {
                    OnScan();
                }
                else
                {
                   // await App.Current.MainPage.Navigation.PopAsync();
                    await Shell.Current.GoToAsync($"//Settings");
                }
            }
           
        }

        public async Task LoadPreferencesAsync()
        {
            DateTime birthday = Preferences.Get(PreferencesKeys.UserBirthday, DateTime.Now.AddYears(-30));
            int age = DateTime.Now.Year - birthday.Year;
            if (birthday.Date > DateTime.Now.AddYears(-age)) age--;

            this._age = age;
            this._height = Preferences.Get(PreferencesKeys.UserHeight, 170);
            this._sex = (Models.Sex)Preferences.Get(PreferencesKeys.UserSex, (byte)Models.Sex.Male);
            this._address = Preferences.Get(PreferencesKeys.MiScaleBluetoothAddress, string.Empty);
            this._scaleType = (ScaleType)Preferences.Get(PreferencesKeys.ScaleType, (byte)ScaleType.MiBodyCompositionScale);
        }

        private async void OnScan()
        {
            await StartScan();
        }

        private async Task<bool> CheckPermissions()
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                double version = 0;
                double.TryParse(DeviceInfo.VersionString, out version);

                if (version >= 12)
                {
                    if (await GetBluetoothPermissionStatusAsync() != PermissionStatus.Granted)
                    {
                        await Application.Current.MainPage.DisplayAlert("Problem", "Permission to use Bluetooth is required to scan.",
                           "OK");
                        return false;
                    }

                    if (await GetLocationPermissionStatusAsync() != PermissionStatus.Granted)
                    {
                        await Application.Current.MainPage.DisplayAlert("Problem", "Permission to use Location (Bluetooth) is required to scan.",
                            "OK");
                        return false;
                    }
                }
                else
                {
                    if (await GetLocationPermissionStatusAsync() != PermissionStatus.Granted)
                    {
                        await Application.Current.MainPage.DisplayAlert("Problem", "Permission to use  Location (Bluetooth) is required to scan.",
                            "OK");
                        return false;
                    }
                }

            }

            return true;

        }

        private async Task StartScan()
        {
            ScanningLabel = string.Empty;
            ScaleMeasurement.Instance.Weight = "0";
            this.IsBusyForm = true;
            await this._scale.GetBodyCompositonAsync(_address, new User { Sex = _sex, Age = _age, Height = _height, ScaleType = _scaleType });
            this.OnStop();
        }

        private async void OnStop()
        {
            this._scale.StopSearch();
            this.IsBusyForm = false;
            if (this._scale.BodyComposition is null || !this._scale.BodyComposition.IsValid)
            {
                var msg = "Data could not be obtained. try again";
                await Application.Current.MainPage.DisplayAlert("Problem", msg,
                    "OK");
                _logService.LogError(msg);
                ScanningLabel = "Not found";
            }
            else
            {
                App.BodyComposition = this._scale.BodyComposition;
                await Shell.Current.GoToAsync($"//FormPage?autoUpload={Preferences.Get(PreferencesKeys.OneClickScanAndUpload, false)}");
            }
        }

        private async void OnCancel()
        {
            await this._scale.CancelSearchAsync();
            this.IsBusyForm = false;
        }

        private async Task<PermissionStatus> GetLocationPermissionStatusAsync()
        {
            var locationPermissionStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (locationPermissionStatus != PermissionStatus.Granted)
            {
                locationPermissionStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            return locationPermissionStatus;
        }

        private async Task<PermissionStatus> GetBluetoothPermissionStatusAsync()
        {
            var bluetoothPermission = DependencyService.Get<IBluetoothConnectPermission>();
            var status = await bluetoothPermission.CheckStatusAsync();
            if (status != PermissionStatus.Granted)
            {
                status = await bluetoothPermission.RequestAsync();
            }
            return status;
        }

        public Command CancelCommand { get; }
        public Command StopCommand { get; }

        private string _scanningLabel;

        public string ScanningLabel
        {
            get => _scanningLabel;
            set => SetProperty(ref _scanningLabel, value);
        }

        private bool _isBusyForm;

        public bool IsBusyForm
        {
            get => _isBusyForm;
            set => SetProperty(ref _isBusyForm, value);
        }
    }
}