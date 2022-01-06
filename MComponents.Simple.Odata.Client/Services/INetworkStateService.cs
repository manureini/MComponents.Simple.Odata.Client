using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MComponents.Simple.Odata.Client.Services
{
    public interface INetworkStateService
    {
        public bool IsOnline { get; }
    }
}
