﻿using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using GarminWeightScaleUploader.Lib.Models;
using MiScaleExporter.Models;
using Newtonsoft.Json;
 

namespace MiScaleExporter.Services;

public class GarminService : IGarminService
{
    private HttpClient _httpClient;
    private ILogService _logService;

    private readonly string _tmpDir;

    public GarminService(ILogService logService)
    {
        _logService = logService;
        _httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        _tmpDir = Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tmp")).FullName;
    }

    public async Task<GarminApiResponse> UploadAsync(BodyComposition bodyComposition, DateTime time, string email, string password)
    {
        if(Preferences.Get(PreferencesKeys.UseExternalAPI, false))
        {
            return await UploadViaExternalAPIAsync(bodyComposition, time, email, password);
        }
        else
        {
            return await UploadViaDirectCallToGarminAsync(bodyComposition, time, email, password);
        }
    }

    private async Task<GarminApiResponse> UploadViaDirectCallToGarminAsync(BodyComposition bodyComposition, DateTime time, string email, string password)
    {
        var result = new GarminApiResponse();
        try
        {
           
            var userProfileSettings = new UserProfileSettings
            {
                Age = 40,
                Height = 180,
            };
            var scaleDTO = new GarminWeightScaleDTO
            {
                TimeStamp = time,
                Weight = Convert.ToSingle(bodyComposition.Weight),
                PercentFat = Convert.ToSingle(bodyComposition.Fat),
                PercentHydration = Convert.ToSingle(bodyComposition.WaterPercentage),
                BoneMass = Convert.ToSingle(bodyComposition.BoneMass),
                MuscleMass = Convert.ToSingle(bodyComposition.MuscleMass),
                VisceralFatRating = Convert.ToByte(bodyComposition.VisceralFat),
                VisceralFatMass = Convert.ToSingle(bodyComposition.VisceralFat),
                PhysiqueRating = Convert.ToByte(bodyComposition.BodyType),
                MetabolicAge = Convert.ToByte(bodyComposition.MetabolicAge),
                BodyMassIndex = Convert.ToSingle(bodyComposition.BMI),
                Email = email,
                Password = password
            };

            var data = scaleDTO with { Email = email, Password = password };
            var garminApiReponse = await GarminWeightScaleUploader.Lib.GarminWeightScaleUploader.UploadAsync(data, userProfileSettings);
            result.IsSuccess = garminApiReponse;
            return result;
        }
        catch(Exception ex)
        {
            _logService.LogError(ex.Message);
            result.Message = ex.Message;
            return result;
        }
       
    }

    private async Task<GarminApiResponse> UploadViaExternalAPIAsync(BodyComposition bodyComposition, DateTime time, string email, string password)
    {
        var unixTime = ((DateTimeOffset) time).ToUnixTimeSeconds();
        var request = new GarminBodyCompositionRequest
        {
            Email = email,
            Password = password,
            Weight = bodyComposition.Weight,
            BoneMass = bodyComposition.BoneMass,
            MuscleMass = bodyComposition.MuscleMass,
            MetabolicAge = bodyComposition.MetabolicAge,
            PercentFat = bodyComposition.Fat,
            VisceralFatRating = bodyComposition.VisceralFat,
            BodyMassIndex = bodyComposition.BMI,
            PercentHydration = bodyComposition.WaterPercentage,
            PhysiqueRating = bodyComposition.BodyType,
            TimeStamp = unixTime
        };
       return await UploadToGarminCloud(request);
    }

    private async Task<GarminApiResponse> UploadToGarminCloud(GarminBodyCompositionRequest request)
    {
        var result = new GarminApiResponse();
        try
        {

        var dataAsString = JsonConvert.SerializeObject(request);
        var content = new StringContent(dataAsString, Encoding.UTF8, "application/json");

        var response = await PostAsync("/upload", content);
        result.IsSuccess = response.IsSuccessStatusCode;
        
        using (var stream = await response.Content.ReadAsStreamAsync())
        using (var reader = new StreamReader(stream))
        {
            result.Message = await reader.ReadToEndAsync();
            return result;
        }
        }
        catch (Exception ex)
        {
            _logService.LogError(ex.Message);
            result.Message = ex.Message;
            return result;
        }
    }
    
    private async Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content)
    {
        var baseAddress = Preferences.Get(PreferencesKeys.ApiServerAddressOverride, SettingKeys.ApiServerAddress);
        var response = await _httpClient.PostAsync($"{baseAddress}{requestUri}", content);
        return response;
    }
}