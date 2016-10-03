using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace Kiwiot.Faceometer.IoTCore.Repositories.Photo
{
    public sealed class WebcamPhotoRepository : IPhotoRepository
    {
        #region Private Fields
        MediaCapture webcam;
        MediaCaptureInitializationSettings captureInitSettings = new MediaCaptureInitializationSettings()
        {
            StreamingCaptureMode = StreamingCaptureMode.Video,
            PhotoCaptureSource = PhotoCaptureSource.VideoPreview
        };
        DeviceInformationCollection videoDevices;
        DeviceInformationCollection audioDevices;
        #endregion

        #region Constructors
        public WebcamPhotoRepository()
        {
            Log("Initialising camera devices");
            webcam = new MediaCapture();
            webcam.Failed += (sender, args) => Log($"Webcam failed to initialise.\r\nCode: {args.Code}\r\nMessage: {args.Message}");
            webcam.InitializeAsync(captureInitSettings).AsTask().Wait();
        }
        #endregion

        public void Log(string message) => Debug.WriteLine($"{DateTime.Now.ToString("[yyyy:MM:dd hh:mm:ss]")}  ({this.GetType().Name})  {message}");


        public IAsyncOperation<IEnumerable<byte>> GetPhotoBytesAsync() => GetPhotoBytesAsyncTask().AsAsyncOperation();
        private async Task<IEnumerable<byte>> GetPhotoBytesAsyncTask()
        {
            using (var captureStream = new InMemoryRandomAccessStream())
            {
                await webcam.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), captureStream);

                var reader = new DataReader(captureStream.GetInputStreamAt(0));
                var bytes = new byte[captureStream.Size];
                await reader.LoadAsync((uint)captureStream.Size);
                reader.ReadBytes(bytes);

                return bytes;
            }
        }
    }
}
