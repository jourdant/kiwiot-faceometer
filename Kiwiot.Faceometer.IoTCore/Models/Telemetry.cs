using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiwiot.Faceometer.IoTCore.Models
{
    public sealed class Telemetry
    {
        public string Timestamp => DateTime.Now.ToString("o");
        public string Device => Windows.Networking.Connectivity.NetworkInformation.GetHostNames().FirstOrDefault().RawName;

        public double Temperature { get; set; }
        public string Image { get; set; }
    }
}
