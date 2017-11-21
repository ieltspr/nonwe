using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teg.Its.Metering.Core;
using Wec.Its.Metering.LGIntervalLoadCommon;

namespace Wec.Its.Metering.LGIntervalLoadDomain.DAL
{
    public class MeterInfoQuery 
    {
        //private AMROpen_CISEntities _context;        

        //public MeterInfoQuery(AMROpen_CISEntities context)
        //{
        //    _context = context;
        //}
        private AMROpen_CISJustinEntities _context;
        public MeterInfoQuery(AMROpen_CISJustinEntities context)
        {
            _context = context;
        }

        public List<GetAllMeterInfoEntity> GetMeterInfoIdData1()
        { 
            List<GetAllMeterInfoEntity> listallm = new List<GetAllMeterInfoEntity>();
            try
            {
                listallm = _context.GetAllMeters().ToList();                
                return listallm;
            }
            catch (Exception ex)
            {
                Logger.Write("Error : " + ex.Message + ex.StackTrace.ToString());
                return listallm;
            }            
        }
        public List<LGIntervalLoad_GetMeterInfo_Result> LGIntervalLoad_GetMeterInfo(int ServiceTypeID , int ResonTypeID , int FrequencyTypeID)
        {            
            List<LGIntervalLoad_GetMeterInfo_Result> listgetmtrfilter = new List<LGIntervalLoad_GetMeterInfo_Result>();
            try
            {
                listgetmtrfilter = _context.LGIntervalLoad_GetMeterInfo(ServiceTypeID, ResonTypeID, FrequencyTypeID).ToList(); 
                return listgetmtrfilter;        
            }
            catch (Exception ex)
            {
                Logger.Write("Error : " + ex.Message + ex.StackTrace.ToString());
                return listgetmtrfilter;
            }
        }

        public List<AMIIntervalQualityType> GetQualityTypes()
        {                           
            var qualitytype = _context.AMIIntervalQualityTypes.Where(qtype => qtype.AMISystemId == 10).ToList();
            return qualitytype;             
        }

        public List<AMIIntervalReadType> GetReadTypes()
        {
            var readtype = _context.AMIIntervalReadTypes.Where(rtype => rtype.AMISystemId == 10).ToList();
            return readtype;
        }

        /*
       public List<MeterInfoClassFile>  GetMeterInfoIdData()
       
        {
            try
            {
                List<MeterInfoClassFile> mtrd = new List<MeterInfoClassFile>();
                 var t = (from c in _context.MeterInfoIds                         
                          join s in _context.MeterInfoes
                          on c.MeterInfoId1 equals s.MeterInfoId
                          join i in _context.IntervalChannels
                          on c.MeterInfoId1 equals i.MeterInfoId                         
                          join ir in _context.IntervalReasons
                          on i.IntervalChannelId equals ir.IntervalChannelId
                          from r in _context.IntervalChannelTrackings
                          where r.IntervalChannelId == i.IntervalChannelId  && s.AMISystemId == 10
                          //join r in _context.IntervalChannelTrackings
                          //on i.IntervalChannelId equals r.IntervalChannelId
                          //where s.AMISystemId == 10 //&&  s.ServiceTypeId == 1 //&& i.IntervalChannelId == intervalchannelid
                 select new MeterInfoClassFile
                          {
                              MeterInfoId = c.MeterInfoId1,
                              UtilityId = c.UtilityId,
                              MeterNumber = c.MeterNumber,
                              PremiseId = c.PremiseId,
                              PremiseServiceSequence = c.PremiseServiceSequence,
                              Active = c.Active,
                              IntervalChanelId = i.IntervalChannelId,
                              LastLoadDateTime = r.LastLoadDateTime,
                              FrequncyTypeId = i.FrequencyTypeId,
                              ServiceTypeId = s.ServiceTypeId,
                              IntervalReasonTypeId = ir.IntervalReasonTypeId
                          }).ToList();
                 mtrd = t.ToList();

                 //select mi.*  , m.ServiceTypeId , ir.IntervalReasonTypeId  ,  i.FrequencyTypeId from MeterInfoId mi
                 //inner join MeterInfo m on m.MeterInfoId = mi.MeterInfoId
                 //inner join IntervalChannel i on i.MeterInfoId = mi.MeterInfoId-- and mi.MeterInfoId = 102409
                 //inner join IntervalChannelTracking r on r.IntervalChannelId = i.IntervalChannelId
                 //inner join IntervalReason ir on ir.IntervalChannelId = i.IntervalChannelId--and i.IntervalChannelId = 69914
                 //and m.AMISystemId = 10

                 return mtrd;

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        */
    }
}
