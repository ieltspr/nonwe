using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wec.Its.Metering.LGIntervalLoadDomain.DAL
{
    using System;
    using System.Collections.Generic;
    public class MeterInfoClassFile
    {

        public int MeterInfoId { get; set; }
        public short UtilityId { get; set; }
        public string MeterNumber { get; set; }
        public int PremiseId { get; set; }
        public short PremiseServiceSequence { get; set; }
        public bool Active { get; set; }
        public int IntervalChannelId { get; set; }

        public DateTime? LastLoadDateTime { get; set; }

        public int FrequncyTypeId { get; set; }

        public short? ServiceTypeId { get; set; }

        public int IntervalReasonTypeId { get; set; }
        
    }
}
