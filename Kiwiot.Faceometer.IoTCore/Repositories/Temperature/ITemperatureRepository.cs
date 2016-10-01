using Windows.Foundation;

namespace Kiwiot.Faceometer.IoTCore.Repositories.Temperature
{
    public interface ITemperatureRepository
    {
        IAsyncOperation<double> GetTemperatureAsync();
    }
}
