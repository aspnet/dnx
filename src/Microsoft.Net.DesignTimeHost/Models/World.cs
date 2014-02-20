using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Net.DesignTimeHost.Models
{
    public class World
    {

        IList<string> Warnings { get; set; }
        IList<string> Errors { get; set; }
    }
}
