using System.Collections.Generic;
using Windows.Foundation;

namespace Kiwiot.Faceometer.IoTCore.Repositories.Photo
{
    public interface IPhotoRepository
    {
        IAsyncOperation<IEnumerable<byte>> GetPhotoBytesAsync();
    }
}
