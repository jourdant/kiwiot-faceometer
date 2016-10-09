#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"

public static string AzureStorageConnectionString => Environment.GetEnvironmentVariable($"CUSTOMCONNSTR_FACEOMETER_AZURESTORAGECONNECTIONSTRING");
public static string PowerBIURL => Environment.GetEnvironmentVariable($"APPSETTING_FACEOMETER_POWERBIURL");
public static string CognitiveServicesKey => Environment.GetEnvironmentVariable($"APPSETTING_FACEOMETER_COGNITIVESERVICESKEY");
public static int RefreshTime => System.Convert.ToInt32(Environment.GetEnvironmentVariable($"APPSETTING_FACEOMETER_REFRESHTIME") ?? "300");
private static TraceWriter log;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter l)
{
    log = l;
    var timestamp = DateTime.Now;

    //grab request body as text
    var jsonContent = await req.Content.ReadAsStringAsync();
    log.Info($"Incoming message: {jsonContent}");
    log.Info("Deserialising...\r\n");
    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Telemetry>(jsonContent);
    
    log.Info("Uploading image to cognitive services");
    var faces = await GetFaces(data.Image);
    log.Info($"Faces detected: {faces.Count}\r\n");

    log.Info("Uploading image to blob storage");
    var fileName = $"{timestamp.ToString("yyyyMMdd_hhmmss")}_webcam.jpg";
    fileName = System.String.Join("_", fileName.Split(System.IO.Path.GetInvalidFileNameChars(), System.StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    var blobUrl = await UploadImageToBlob(data.Image, $"{data.Device}/{fileName}");
    log.Info($"Blob url: {blobUrl}\r\n");

    //build return object
    var ret = new {
        Timestamp = timestamp,
        Device = data.Device,
        Temperature = data.Temperature,
        Faces = faces.Count,
        Url = blobUrl,
        RefreshTime = RefreshTime
    };

    log.Info("Reporting telemetry to Power BI\r\n");
    await HttpPost(PowerBIURL, Newtonsoft.Json.JsonConvert.SerializeObject(new List<object>() {ret}));

    //return http response back to mcu
    return req.CreateResponse(System.Net.HttpStatusCode.OK, ret);
}

//object to be deserialised from mcu
public class Telemetry {
    public string Device {get;set;}
    public double Temperature {get; set; }
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
public static async Task<string> HttpPost(string url, string body, string contentType = "application/json")
{
    if (url == string.Empty) {
        log.Info("Url is empty. Skipping...");
        return string.Empty;
    }

    var client = System.Net.HttpWebRequest.Create(url);
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
public static async Task<List<Face>> GetFaces(byte[] image)
{
    var client = System.Net.HttpWebRequest.Create("https://api.projectoxford.ai/face/v1.0/detect");
    client.Method = "POST";
    client.ContentType = "application/octet-stream";
    client.Headers.Add("Ocp-Apim-Subscription-Key", CognitiveServicesKey);
    using (var ms = new MemoryStream(image)) ms.CopyTo((await client.GetRequestStreamAsync()));

    var resp = await client.GetResponseAsync();
    var reader = new StreamReader(resp.GetResponseStream());
    var response = reader.ReadToEnd();
    reader.Dispose();
    
    log.Info($"Cognitive services response: {response}");
    return Newtonsoft.Json.JsonConvert.DeserializeObject<List<Face>>(response);
}