using System;
using System.Threading.Tasks;
using Windows.Foundation;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using System.Diagnostics;

namespace Kiwiot.Faceometer.IoTCore.Repositories.Telemetry
{
    public sealed class AzureFunctionTelemetryRepository : ITelemetryRepository
    {
        #region Private Fields
        private string functionUrl = "";
        #endregion

        #region Private Static Functions
        private static async Task<string> HttpPost(string url, string body, string contentType = "application/json")
        {
            var client = HttpWebRequest.Create(url);
            client.Method = "POST";
            client.ContentType = contentType;
            var req = await client.GetRequestStreamAsync();
            var writer = new StreamWriter(req);
            await writer.WriteLineAsync(body);
            writer.Dispose();

            var resp = await client.GetResponseAsync();
            var reader = new StreamReader(resp.GetResponseStream());
            var response = reader.ReadToEnd();
            reader.Dispose();

            return response;
        }
        #endregion

        public void Log(string message) => Debug.WriteLine($"{DateTime.Now.ToString("[yyyy:MM:dd hh:mm:ss]")}  ({this.GetType().Name})  {message}");


        public IAsyncOperation<dynamic> SubmitTelemetryAsync(Models.Telemetry telemetry) => SubmitTelemetryAsyncTask(telemetry).AsAsyncOperation();
        private async Task<dynamic> SubmitTelemetryAsyncTask(Models.Telemetry telemetry)
        {
            Log("Serialising payload to JSON");
            var json = JsonConvert.SerializeObject(telemetry);

            Log("Sending payload to Azure Function");
            var result = await HttpPost(functionUrl, json);

            Log("Deserialising response");
            var resp = JsonConvert.DeserializeObject(result);
            return resp;
        }
    }
}
