#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "System.Drawing"

using System.Linq;
using System.Drawing;
using System.IO;
using Newtonsoft.Json;

public static string AzureStorageConnectionString => Environment.GetEnvironmentVariable($"APPSETTING_FACEOMETER_AZURESTORAGECONNECTIONSTRING");
public static string PowerBIURL => Environment.GetEnvironmentVariable($"APPSETTING_FACEOMETER_POWERBIURL");
public static string CognitiveServicesFaceKey => Environment.GetEnvironmentVariable($"APPSETTING_FACEOMETER_COGNITIVESERVICESFACEKEY");
public static string CognitiveServicesEmotionKey => Environment.GetEnvironmentVariable($"APPSETTING_FACEOMETER_COGNITIVESERVICESEMOTIONKEY");
public static int RefreshTime => System.Convert.ToInt32(Environment.GetEnvironmentVariable($"APPSETTING_FACEOMETER_REFRESHTIME") ?? "300");
private static TraceWriter log;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter l)
{
    log = l;
    var timestamp = DateTime.Now;

    //grab request body as text
    var jsonContent = await req.Content.ReadAsStringAsync();
    //log.Info($"Incoming message: {jsonContent}");
    log.Info("Deserialising...\r\n");
    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Telemetry>(jsonContent);

    MemoryStream ms = new MemoryStream(data.Image);
    Image returnImage = Image.FromStream(ms);
    returnImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
    MemoryStream ms2 = new MemoryStream();
    returnImage.Save(ms2, System.Drawing.Imaging.ImageFormat.Jpeg);
    data.Image = ms2.ToArray();



    log.Info($"Image size: {System.Math.Round(1.0*data.Image.Length/1024/1024, 2)}MB");
    log.Info("Uploading image to blob storage");
    var fileName = $"{timestamp.ToString("yyyyMMdd_hhmmss")}_webcam.jpg";
    fileName = System.String.Join("_", fileName.Split(System.IO.Path.GetInvalidFileNameChars(), System.StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');

    var blobUrl = await UploadImageToBlob(data.Image, $"{data.Device}/{fileName}");
    log.Info($"Blob url: {blobUrl}\r\n");

    log.Info("Uploading image to cognitive services");
    var emotionApiUrl = "https://api.projectoxford.ai/emotion/v1.0/recognize";
    var emotionApiData = $"{{\"url\": \"{blobUrl}\"}}";
    var emotionApiHeaders = new Dictionary<string, object>();
    emotionApiHeaders.Add("Ocp-Apim-Subscription-Key", CognitiveServicesEmotionKey);
    var faceresp = await HttpPost(emotionApiUrl, emotionApiData, emotionApiHeaders);
    log.Info(faceresp);
    var faces = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Face>>(faceresp);

    //calculate emotions
    var averageAnger = faces.Select(x => x.Scores.Anger).Average();
    var averageContempt = faces.Select(x => x.Scores.Contempt).Average();
    var averageDisgust = faces.Select(x => x.Scores.Disgust).Average();
    var averageFear = faces.Select(x => x.Scores.Fear).Average();
    var averageHappiness = faces.Select(x => x.Scores.Happiness).Average();
    var averageNeutral = faces.Select(x => x.Scores.Neutral).Average();
    var averageSadness = faces.Select(x => x.Scores.Sadness).Average();
    var averageSurprise = faces.Select(x => x.Scores.Surprise).Average();

    // var faces = await GetFaces(data.Image);
    log.Info($"Faces detected: {faces.Count}\r\n");

    //build timestamp
    var tz = System.TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
    var ts = System.TimeZoneInfo.ConvertTimeFromUtc(System.DateTime.UtcNow, tz);
    
    //build return object
    var ret = new {
        Timestamp = ts,
        Device = data.Device,
        AverageAnger = System.Math.Round(averageAnger, 4),
        AverageContempt = System.Math.Round(averageContempt, 4),
        AverageDisgust = System.Math.Round(averageDisgust, 4),
        AverageFear = System.Math.Round(averageFear, 4),
        AverageHappiness = System.Math.Round(averageHappiness, 4),
        AverageNeutral = System.Math.Round(averageNeutral, 4),
        AverageSadness = System.Math.Round(averageSadness, 4),
        AverageSurprise = System.Math.Round(averageSurprise, 4),
        Faces = faces.Count,
        Url = blobUrl,
        RefreshTime = RefreshTime,
        AverageEngagement = System.Math.Round((((2*averageHappiness)+averageNeutral)/2) + averageSurprise, 4)*100,
        MaxEngagement = 100,
        MinEngagement = 0,
        TargetEngagement = 0
    };

    log.Info("Reporting telemetry to Power BI\r\n");
    await HttpPost(PowerBIURL, Newtonsoft.Json.JsonConvert.SerializeObject(new List<object>() {ret}));

    //return http response back to mcu
    return req.CreateResponse(System.Net.HttpStatusCode.OK, ret);
}

//object to be deserialised from mcu
public class Telemetry {
    public string Device {get;set;}
    public byte[] Image { get; set; }
}

//handles uploading an Image object into azure blob storage
public static async Task<string> UploadImageToBlob(byte[] image, string fileName) 
{
    //set up storage account and container
    var storageAccount = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse(AzureStorageConnectionString).CreateCloudBlobClient();
    var container = storageAccount.GetContainerReference("images");
    container.CreateIfNotExists(); 
    await container.SetPermissionsAsync(new Microsoft.WindowsAzure.Storage.Blob.BlobContainerPermissions { PublicAccess = Microsoft.WindowsAzure.Storage.Blob.BlobContainerPublicAccessType.Blob });
    
    //upload blob and set metadata
    var blob = container.GetBlockBlobReference(fileName);
    using (var ms = new MemoryStream(image))
    {
        ms.Position = 0;
        blob.UploadFromStream(ms); 
        blob.Properties.ContentType = "image/jpeg";
        await blob.SetPropertiesAsync(); 
    }

    //return blob url
    return blob.Uri.ToString();
}

//a quick and simple method for POSTing json and receiving a text based response back
public static async Task<string> HttpPost(string url, string body, Dictionary<string, object> headers = null, string contentType = "application/json")
{
    if (url == string.Empty) {
        log.Info("Url is empty. Skipping...");
        return string.Empty;
    }

    var client = System.Net.HttpWebRequest.Create(url);
    client.Method = "POST";
    client.ContentType = contentType;
    if (headers != null) foreach (var header in headers) client.Headers.Add(header.Key, header.Value.ToString());

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
public class Face { 
    public string FaceId { get; set; }  
    public EmotionScore Scores {get;set;}
}
public class EmotionScore { 
    public double Anger { get; set; } 
    public double Contempt { get; set; } 
    public double Disgust { get; set; } 
    public double Fear { get; set; } 
    public double Happiness { get; set; } 
    public double Neutral { get; set; } 
    public double Sadness { get; set; } 
    public double Surprise { get; set; } 
}

public static async Task<List<Face>> GetFaces(byte[] image)
{
    var client = System.Net.HttpWebRequest.Create("https://api.projectoxford.ai/face/v1.0/detect");
    client.Method = "POST";
    client.ContentType = "application/octet-stream";
    client.Headers.Add("Ocp-Apim-Subscription-Key", CognitiveServicesFaceKey);
    using (var ms = new MemoryStream(image)) ms.CopyTo((await client.GetRequestStreamAsync()));

    var resp = await client.GetResponseAsync();
    var reader = new StreamReader(resp.GetResponseStream());
    var response = reader.ReadToEnd();
    reader.Dispose();
    
    log.Info($"Cognitive services response: {response}");
    return Newtonsoft.Json.JsonConvert.DeserializeObject<List<Face>>(response);
}

public static async Task<List<Face>> GetFaceEmotions(byte[] image)
{
    var client = System.Net.HttpWebRequest.Create("https://api.projectoxford.ai/emotion/v1.0/detect");
    client.Method = "POST";
    client.ContentType = "application/octet-stream";
    client.Headers.Add("Ocp-Apim-Subscription-Key", CognitiveServicesFaceKey);
    using (var ms = new MemoryStream(image)) ms.CopyTo((await client.GetRequestStreamAsync()));

    var resp = await client.GetResponseAsync();
    var reader = new StreamReader(resp.GetResponseStream());
    var response = reader.ReadToEnd();
    reader.Dispose();
    
    log.Info($"Cognitive services response: {response}");
    return Newtonsoft.Json.JsonConvert.DeserializeObject<List<Face>>(response);
}