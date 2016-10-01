#r "Newtonsoft.Json"
#r "System.Drawing"
#r "Microsoft.WindowsAzure.Storage"

using System;
using System.Net;
using System.Net.Http.Headers;
using System.Drawing;
using System.Drawing.Imaging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

public static string AzureStorageConnectionString => Environment.GetEnvironmentVariable($"CUSTOMCONNSTR_{nameof(AzureStorageConnectionString)}");
public static string PowerBIURL => Environment.GetEnvironmentVariable($"APPSETTING_{nameof(PowerBIURL)}");
public static string CognitiveServicesKey => Environment.GetEnvironmentVariable($"APPSETTING_{nameof(CognitiveServicesKey)}");
private static TraceWriter log;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter l)
{
    log = l;
    var timestamp = DateTime.Now;

    //grab request body as text
    var jsonContent = await req.Content.ReadAsStringAsync();
    log.Info($"Incoming message: {jsonContent}");

    log.Info("Deserialising json");
    var jsonSettings = new Newtonsoft.Json.JsonSerializerSettings();
    jsonSettings.Converters.Add(new ImageConverter());
    var data = JsonConvert.DeserializeObject<Telemetry>(jsonContent, jsonSettings);
    
    log.Info("Uploading image to cognitive services");
    var faces = await GetFaces(data.Image);
    log.Info($"Faces detected: {faces.Count}");

    log.Info("Uploading image to blob storage");
    var fileName = $"{timestamp.ToString("yyyyMMdd_hhmmss")}_webcam.jpg";
    var blobUrl = await UploadBlob(data.Image, fileName);
    log.Info($"Blob url: {blobUrl}");

    //build return object
    var ret = new {
        Timestamp = timestamp,
        Device = data.Device,
        Temperature = data.Temperature,
        Faces = faces.Count,
        Url = blobUrl,
        RefreshTime = 300
    };

    log.Info("Reporting telemetry to Power BI");
    await HttpPost(PowerBIURL, JsonConvert.SerializeObject(new List<object>() {ret}));

    //return http response back to mcu
    return req.CreateResponse(HttpStatusCode.OK, ret);
}

//object to be deserialised from mcu
public class Telemetry {
    public string Device {get;set;}
    public double Temperature {get; set; }
    public Bitmap Image { get; set; }
}

//handles uploading an Image object into azure blob storage
public static async Task<string> UploadBlob(Image image, string fileName) 
{
    using (var ms = new MemoryStream())
    {
        //set up storage account and container
        var storageAccount = CloudStorageAccount.Parse(AzureStorageConnectionString).CreateCloudBlobClient();
        var container = storageAccount.GetContainerReference("images");
        container.CreateIfNotExists();
        container.SetPermissionsAsync(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
        
        //upload blob and set metadata
        var blob = container.GetBlockBlobReference(fileName);
        image.Save(ms, ImageFormat.Jpeg);
        ms.Position = 0;
        blob.UploadFromStream(ms); 
        blob.Properties.ContentType = "image/jpeg";
        await blob.SetPropertiesAsync(); 
        
        return blob.Uri.ToString();
    }
}

//a quick and simple method for POSTing json and receiving a text based response back
public static async Task<string> HttpPost(string url, string body, string contentType = "application/json")
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

//handles interfacing with oxford api/cognitive services
public class Face { public string FaceId { get; set; } }
public static async Task<List<Face>> GetFaces(Image image)
{
    var client = HttpWebRequest.Create("https://api.projectoxford.ai/face/v1.0/detect");
    client.Method = "POST";
    client.ContentType = "application/octet-stream";
    client.Headers.Add("Ocp-Apim-Subscription-Key", CognitiveServicesKey);
    image.Save(await client.GetRequestStreamAsync(), ImageFormat.Jpeg);

    var resp = await client.GetResponseAsync();
    var reader = new StreamReader(resp.GetResponseStream());
    var response = reader.ReadToEnd();
    reader.Dispose();
    
    log.Info($"Cognitive services response: {response}");
    return JsonConvert.DeserializeObject<List<Face>>(response);
} 

//allows us to deserialise b64 string into image seamlessly
public class ImageConverter : Newtonsoft.Json.JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(Bitmap) || objectType == typeof(Image);
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) => (Bitmap)Bitmap.FromStream(new MemoryStream(Convert.FromBase64String((string)reader.Value)));
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) { throw new NotImplementedException(); }
}