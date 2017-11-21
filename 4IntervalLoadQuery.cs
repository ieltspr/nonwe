using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Text;
using Teg.Its.Metering.Core;
using Wec.Its.Metering.LGIntervalLoadCommon;
using static Wec.Its.Metering.LGIntervalLoadCommon.Enums;

namespace Wec.Its.Metering.LGIntervalLoadDomain.DAL
{
    public class IntervalLoadQuery 
    {               
        private AMROpen_CISJustinEntities _context;
        public IntervalLoadQuery(AMROpen_CISJustinEntities context)
        {
            _context = context;
        }       
      
        public void InsertIntervalreadingByDevice(List<IntervalReadingmodel> interreadList)
        {
            try
            {
                foreach (IntervalReadingmodel model in interreadList)
                {
                    var t = new IntervalReading
                    {
                        IntervalChannelId = model.IntervalChanelId,
                        ReadDateTime = model.ReadDateTime,
                        DST = model.DST,
                        Reading = model.ReadingValue
                    };
                    var duplicates = _context.IntervalReadings.Where(i => i.IntervalChannelId == t.IntervalChannelId && i.ReadDateTime == t.ReadDateTime && i.DST == t.DST).Include(r => r.AMIIntervalQualityTypes).ToList();
                    if (duplicates.Count < 1)
                    {
                        t.AMIIntervalQualityTypes.Add(model.QualityType);
                        _context.AMIIntervalQualityTypes.Attach(model.QualityType);
                        _context.Set<IntervalReading>().Add(t);
                    }
                    else
                    {
                        //if duplicate has qualit type 3.8.0 or 3.8.10 
                            //If model quality type is 3.0.0 or 2.0.0 or 3.6.0
                                //Update duplicate[0].Reading = model.ReadingValue
                                //Update duplicate[0].AmiIntervalQualityTypes[0] = 
                    }
                    //
                    //duplicates[0].AMIIntervalQualityTypes.FirstOrDefault();
                    //Logger.Write("Saving the Reading value in DB  - " + model.ReadingValue);
                }

                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                Logger.Write("Error" + ex.Message + ex.StackTrace.ToString());
                throw;
            }
        }
        public void InsertUpdateIntervalChannelTracking(int intervalchannelid, DateTime lastloadDT)
        {
            IntervalChannelTracking ctr = (from s in _context.IntervalChannelTrackings
                                           where s.IntervalChannelId == intervalchannelid
                                           select s).FirstOrDefault();       
            if (ctr != null)
            {   // update      
                ctr.LastLoadDateTime = lastloadDT;               
                _context.IntervalChannelTrackings.Attach(ctr);
                _context.Entry(ctr).State = EntityState.Modified;
            }
            else
            { // insert
                var ct = new IntervalChannelTracking
                {
                    IntervalChannelId = intervalchannelid,
                    LastLoadDateTime = lastloadDT,
                    LastSendDateTime = null
                };
                _context.Set<IntervalChannelTracking>().Add(ct);
            }
            _context.SaveChanges();
            //Logger.Write("Saving the Interval Channel Tracking value in DB  - " + intervalchannelid);
        }       
    }
}
