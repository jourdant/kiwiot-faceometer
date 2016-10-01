using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Kiwiot.Faceometer.IoTCore.Repositories.Telemetry
{
    public interface ITelemetryRepository
    {
        IAsyncOperation<dynamic> SubmitTelemetryAsync(Models.Telemetry telemetry);
    }
}
