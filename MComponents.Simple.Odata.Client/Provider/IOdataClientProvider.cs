using Simple.OData.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MComponents.Simple.Odata.Client
{
    public interface IOdataClientProvider
    {
        public ODataClient Client { get; }
    }
}
