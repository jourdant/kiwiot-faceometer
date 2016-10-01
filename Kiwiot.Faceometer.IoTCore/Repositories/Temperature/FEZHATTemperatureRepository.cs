using GHIElectronics.UWP.Shields;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Kiwiot.Faceometer.IoTCore.Repositories.Temperature
{
    public sealed class FEZHATTemperatureRepository : ITemperatureRepository
    {
        #region Private Fields
        FEZHAT mainboard;
        #endregion

        #region Constructors
        public FEZHATTemperatureRepository()
        {
            Log("Initialising FEZHAT mainboard");
            mainboard = FEZHAT.CreateAsync().Result;
        }
        #endregion

        public void Log(string message) => Debug.WriteLine($"{DateTime.Now.ToString("[yyyy:MM:dd hh:mm:ss]")}  ({this.GetType().Name})  {message}");

        public IAsyncOperation<double> GetTemperatureAsync() => GetTemperatureAsyncTask().AsAsyncOperation();
        private async Task<double> GetTemperatureAsyncTask()
        {
            Log("Retrieving temperature");
            return mainboard.GetTemperature();
        }
    }
}
