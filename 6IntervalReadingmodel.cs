using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wec.Its.Metering.LGIntervalLoadDomain.DAL
{
    public class IntervalReadingmodel
    {        
        public int IntervalChanelId { get; set; }
        public DateTime ReadDateTime { get; set; }
        public bool DST { get; set; }
        public decimal ReadingValue { get; set; }

        public AMIIntervalQualityType QualityType { get; set; }
               
    }
}
