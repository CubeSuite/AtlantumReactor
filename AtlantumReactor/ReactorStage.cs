using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtlantumReactor
{
    public enum ReactorStage
    {
        Idle,
        Cooling,
        Heating,
        Charging,
        Kickstarting,
        Ready,
        Ignited
    }
}
