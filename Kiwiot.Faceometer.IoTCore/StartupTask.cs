using System;
using Windows.ApplicationModel.Background;
using System.Threading.Tasks;
using Kiwiot.Faceometer.IoTCore.Repositories.Photo;
using Kiwiot.Faceometer.IoTCore.Repositories.Temperature;
using Kiwiot.Faceometer.IoTCore.Models;
using System.Diagnostics;
using System.Linq;
using Kiwiot.Faceometer.IoTCore.Repositories.Telemetry;

namespace Kiwiot.Faceometer.IoTCore
{
    public sealed class StartupTask : IBackgroundTask
    {
        int refreshTime = 300;

        public void Log(string message) => Debug.WriteLine($"{DateTime.Now.ToString("[yyyy:MM:dd hh:mm:ss]")}  ({this.GetType().Name})  {message}");

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            //deferrals allow the background task to survive even while async
            //more info: http://aka.ms/backgroundtaskdeferral
            Log($"Initialising background task thread deferral");
            var deferral = taskInstance.GetDeferral();

            //setup
            Log($"Initialising repositories");
            IPhotoRepository photoRepository = new WebcamPhotoRepository();
            ITemperatureRepository temperatureRepository = new FEZHATTemperatureRepository();
            ITelemetryRepository telemetryRepository = new AzureFunctionTelemetryRepository();

            //app logic
            while (true)
            {
                try
                {
                    //retrieve data
                    var photo = await photoRepository.GetPhotoBytesAsync();
                    var temperature = await temperatureRepository.GetTemperatureAsync();

                    //prepare and send payload
                    var telemetry = new Telemetry() { Temperature = temperature, Image = Convert.ToBase64String(photo.ToArray()) };
                    var resp = await telemetryRepository.SubmitTelemetryAsync(telemetry);

                    //check response for instructions from the cloud
                    if (resp.RefreshTime != null && refreshTime != (int)resp.RefreshTime)
                    {
                        Log($"Updating refresh time to: {(int)resp.RefreshTime} seconds");
                        refreshTime = resp.RefreshTime;
                    }
                }
                catch (Exception ex)
                {
                    Log($"Exception occurred. Details: {ex.ToString()}");
                }

                //replacement for Thread.Sleep in UWP
                Log($"Waiting for {refreshTime} seconds");
                await Task.Delay(TimeSpan.FromSeconds(refreshTime));
            }

            deferral.Complete();
        }
    }
}
