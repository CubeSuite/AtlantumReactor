using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtlantumReactor.Patches
{
    public static class ReactorSettings
    {
        public const string name = "Atlantum Reactor";
        public const int explosionDelay = 5;
        public const int itemLimit = 5000;
        public const int energyLimit = (int)1e7;
        public const int basePower = 20000;
        public const float coolantMultiplier = 0.025f;
        
        public static float basePowerMW => basePower / 1000f;
        public static float perCoolantBoost => basePower * coolantMultiplier;
        
    }
}
