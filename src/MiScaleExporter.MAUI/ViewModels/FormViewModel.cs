﻿using MiScaleExporter.MAUI;
using MiScaleExporter.MAUI.Utils;
using MiScaleExporter.Models;
using MiScaleExporter.Services;
using System.Globalization;

namespace MiScaleExporter.MAUI.ViewModels
{
    public class FormViewModel : BaseViewModel, IFormViewModel
    {
        private readonly IGarminService _garminService;

        public FormViewModel(IGarminService garminService)
        {
            _garminService = garminService;
            Title = "Garmin Body Composition Form";
            Date = DateTime.Now;
            Time = DateTime.Now.TimeOfDay;
            UploadCommand = new Command(OnUpload, ValidateSave);
            this.PropertyChanged +=
                (_, __) => UploadCommand.ChangeCanExecute();
        }

        public async Task LoadPreferencesAsync()
        {
            this._email = Preferences.Get(PreferencesKeys.GarminUserEmail, string.Empty);
            this._password = await SecureStorage.GetAsync(PreferencesKeys.GarminUserPassword);

            this.ShowEmail = string.IsNullOrWhiteSpace(_email);
            this.ShowPassword = string.IsNullOrWhiteSpace(_password);
        }
        private bool ValidateSave()
        {
            return !String.IsNullOrWhiteSpace(_email)
                   && !String.IsNullOrWhiteSpace(_password);
        }

        public void AutoUpload()
        {
            if (!string.IsNullOrWhiteSpace(_email)
                 && !string.IsNullOrWhiteSpace(_password))
            {
                OnUpload();
            }
        }

        private async void OnUpload()
        {
            this.IsBusyForm = true;
            var response = await this._garminService.UploadAsync(this.PrepareRequest(), Date.Date.Add(Time), _email, _password);
            var message = response.IsSuccess ? "Uploaded" : response.Message;
            await Application.Current.MainPage.DisplayAlert("Response", message, "OK");
            this.IsBusyForm = false;
            // This will pop the current page off the navigation stack
            await Shell.Current.GoToAsync("..?autoUpload=false");
        }

        private BodyComposition PrepareRequest()
        {
            var bc = new BodyComposition
            {
                Fat = DoubleValueParser.ParseValueFromUsersCulture(_fat) ?? 0,
                BodyType = _bodyType ?? 0,
                Weight = DoubleValueParser.ParseValueFromUsersCulture(_weight) ?? 0,
                BoneMass = DoubleValueParser.ParseValueFromUsersCulture(_boneMass) ?? 0,
                MuscleMass = DoubleValueParser.ParseValueFromUsersCulture(_muscleMass) ?? 0,
                MetabolicAge = DoubleValueParser.ParseValueFromUsersCulture(_metabolicAge) ?? 0,
                ProteinPercentage = DoubleValueParser.ParseValueFromUsersCulture(_proteinPercentage) ?? 0,
                VisceralFat = DoubleValueParser.ParseValueFromUsersCulture(_visceralFat) ?? 0,
                BMI = DoubleValueParser.ParseValueFromUsersCulture(_bmi) ?? 0,
                BMR = DoubleValueParser.ParseValueFromUsersCulture(_bmr) ?? 0,
                WaterPercentage = DoubleValueParser.ParseValueFromUsersCulture(_waterPercentage) ?? 0,
            };
            return bc;
        }

        public void LoadBodyComposition()
        {
            if (App.BodyComposition is null) return;

            Weight = App.BodyComposition.Weight.ToString();
            BMI = App.BodyComposition.BMI.ToString();
            BoneMass = App.BodyComposition.BoneMass.ToString();
            MuscleMass = App.BodyComposition.MuscleMass.ToString();
            IdealWeight = App.BodyComposition.IdealWeight.ToString();
            BMR = App.BodyComposition.BMR.ToString(); ;
            MetabolicAge = App.BodyComposition.MetabolicAge.ToString(); 
            ProteinPercentage = App.BodyComposition.ProteinPercentage.ToString();
            VisceralFat = App.BodyComposition.VisceralFat.ToString();
            Fat = App.BodyComposition.Fat.ToString();
            WaterPercentage = App.BodyComposition.WaterPercentage.ToString();
            BodyType = App.BodyComposition.BodyType;
            IsAutomaticCalculation = true;
        }

        public Command UploadCommand { get; }

        private string _weight;

        public string Weight
        {
            get => _weight;
            set => SetProperty(ref _weight, DoubleValueParser.CheckValue(value));
        }

        private string _bmi;

        public string BMI
        {
            get => _bmi;
            set => SetProperty(ref _bmi, DoubleValueParser.CheckValue(value));
        }

        private string _idealWeight;

        public string IdealWeight
        {
            get => _idealWeight;
            set => SetProperty(ref _idealWeight, DoubleValueParser.CheckValue(value));
        }

        private string _metabolicAge;

        public string MetabolicAge
        {
            get => _metabolicAge;
            set => SetProperty(ref _metabolicAge, DoubleValueParser.CheckValue(value));
        }

        private string _proteinPercentage;

        public string ProteinPercentage
        {
            get => _proteinPercentage;
            set => SetProperty(ref _proteinPercentage, DoubleValueParser.CheckValue(value));
        }

        private string _bmr;

        public string BMR
        {
            get => _bmr;
            set => SetProperty(ref _bmr, DoubleValueParser.CheckValue(value));
        }

        private string _fat;

        public string Fat
        {
            get => _fat;
            set => SetProperty(ref _fat, DoubleValueParser.CheckValue(value));
        }

        private string _muscleMass;

        public string MuscleMass
        {
            get => _muscleMass;
            set => SetProperty(ref _muscleMass, DoubleValueParser.CheckValue(value));
        }

        private string _boneMass;

        public string BoneMass
        {
            get => _boneMass;
            set => SetProperty(ref _boneMass, DoubleValueParser.CheckValue(value));
        }

        private string _visceralFat;

        public string VisceralFat
        {
            get => _visceralFat;
            set => SetProperty(ref _visceralFat, DoubleValueParser.CheckValue(value));
        }

        private int? _bodyType;

        public int? BodyType
        {
            get => _bodyType;
            set => SetProperty(ref _bodyType, value);
        }

        private string _waterPercentage;

        public string WaterPercentage
        {
            get => _waterPercentage;
            set => SetProperty(ref _waterPercentage, DoubleValueParser.CheckValue(value));
        }

        private string _email;

        private string _password;

        private DateTime _date;

        public DateTime Date
        {
            get => _date;
            set => SetProperty(ref _date, value);
        }

        private TimeSpan _time;

        public TimeSpan Time
        {
            get => _time;
            set => SetProperty(ref _time, value);
        }

        private bool _isAutomaticCalculation;

        public bool IsAutomaticCalculation
        {
            get => _isAutomaticCalculation;
            set => SetProperty(ref _isAutomaticCalculation, value);
        }

        private bool _isBusyForm;

        public bool IsBusyForm
        {
            get => _isBusyForm;
            set => SetProperty(ref _isBusyForm, value);
        }

        public string Email
        {
            get => _email;
            set
            {
                SetProperty(ref _email, value);
                UploadCommand?.ChangeCanExecute();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                SetProperty(ref _password, value);
                UploadCommand?.ChangeCanExecute();
            }
        }

        private bool _showEmail;
        private bool _showPassword;

        public bool ShowEmail
        {
            get => _showEmail;
            set => SetProperty(ref _showEmail, value);
        }

        public bool ShowPassword
        {
            get => _showPassword;
            set => SetProperty(ref _showPassword, value);
        }

    }
}