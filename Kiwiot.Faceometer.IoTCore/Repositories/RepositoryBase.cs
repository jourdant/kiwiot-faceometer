using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiwiot.Faceometer.IoTCore.Repositories
{
    public sealed class RepositoryBase
    {
        public void Log(string message) =>  Debug.WriteLine($"{DateTime.Now.ToString("[yyyy:MM:dd hh:mm:ss]")}  ({this.GetType().Name})  {message}");
    }
}
