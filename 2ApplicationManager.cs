using IecCimFileFormat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Teg.Its.Metering.Core;
using Teg.Its.Metering.Core.Extensions; 
using Wec.Its.Metering.LGIntervalLoadCommon;
using Wec.Its.Metering.LGIntervalLoadCommon.Logging;
using Wec.Its.Metering.LGIntervalLoadDomain.DAL;
using static Wec.Its.Metering.LGIntervalLoadCommon.Enums;

namespace Wec.Its.Metering.LGIntervalLoadDomain.Manager
{
    public class ApplicationManager
    {
        public string mode = string.Empty;
        public long processedDevices = 0; 
        public long expectedDevices = 0;
        public List<IntervalLoadError> Errors = new List<IntervalLoadError>();
        public bool SecFlag = false;

        public bool dstSecondHourHit = false;
        public bool isAmbigious00 = false;
        public bool isAmbigious15 = false;
        public bool isAmbigious30 = false;
        public bool isAmbigious45 = false;

        public TimeZone z = TimeZone.CurrentTimeZone;
        public DaylightTime t;
        
        public Dictionary<string, List<GetAllMeterInfoEntity>> MeterInfoCache { get; set; }

        public List<AMIIntervalQualityType> AMIIntervalQaulityTypelist { get; set; }

        public List<AMIIntervalReadType> AMIIntervalReadTypelist { get; set; }

        public List<LGIntervalLoad_GetMeterInfo_Result> AllFilterMeterInfo { get; set; }

        public TimeSpan interval00 = new TimeSpan(01, 00, 00);
        public TimeSpan interval15 = new TimeSpan(01, 15, 00);
        public TimeSpan interval30 = new TimeSpan(01, 30, 00);
        public TimeSpan interval45 = new TimeSpan(01, 45, 00);

        #region Constructor
        public ApplicationManager() { }
        #endregion

        public void RunApplication()
        {
            try
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                Logger.Write("Begin to process");

                ReadingCommandLine();

                if (!VerifyDSTLogic(DateTimeOffset.Now))
                {
                    return;
                }
                ProcessFiles(AppConfigSettings.InputFilePathRoot);

                sw.Stop();
                Logger.Write(string.Format("Application run time: {0}", sw.Elapsed.ToString()));
                Logger.Write("Stop");
                ErrorReportManager errorRpt = new ErrorReportManager();
                if (Errors != null)
                {
                    errorRpt.CreateReport(Errors, expectedDevices, processedDevices);
                }
            }
            catch (Exception ex)
            {
                if (AppConfigSettings.PageOnError)
                    Logger.Write(ex.ToString(), TraceEventType.Critical);
                else
                    Logger.Write(ex.ToString(), TraceEventType.Error);

            }
        }

        public void ReadingCommandLine()
        {
            try
            {
                Logger.Write(string.Format("{0} Command line options:{1}  {2}", CommandLineOptions.mode, CommandLineOptions.loadType, new CommandLineOptions().PropertiesToString()));

                ModelName.LoadType = CommandLineOptions.loadType;

                switch (CommandLineOptions.loadType)
                {
                    case LoadTypeCode.BillingElectric15Min:  //BillingElectric15Min
                        ModelName.ServiceTypeID = 0;
                        ModelName.IntervalReasonTypeID = 1;
                        ModelName.FrequencyTypeID = 2;
                        break;
                    case LoadTypeCode.BillingElectricHourly: // "BillingElectricHourly":
                        ModelName.ServiceTypeID = 0;
                        ModelName.IntervalReasonTypeID = 1;
                        ModelName.FrequencyTypeID = 1;
                        break;
                    case LoadTypeCode.ResearchElectricHourly: //"ResearchElectricHourly":
                        ModelName.ServiceTypeID = 0;
                        ModelName.IntervalReasonTypeID = 2;
                        ModelName.FrequencyTypeID = 1;
                        break;
                    case LoadTypeCode.TransportGas: //"TransportGas":
                        ModelName.Loadtypeid = "TG,IN";
                        ModelName.ServiceTypeID = 1;
                        ModelName.IntervalReasonTypeID = 1;
                        ModelName.FrequencyTypeID = 1;
                        break;
                    case LoadTypeCode.GasChoice: //"GasChoice":
                        ModelName.Loadtypeid = "GC";
                        ModelName.ServiceTypeID = 1;
                        ModelName.IntervalReasonTypeID = 1;
                        ModelName.FrequencyTypeID = 1;
                        break;
                    default:
                        throw new Exception("LoadType is invalid.  Please pass valid LoadType.");
                }
            }
            finally
            {
                Logger.Write("Load Type Code :  " + CommandLineOptions.loadType);
            }
        }

        public bool VerifyDSTLogic(DateTimeOffset dateTimeOffset)
        {
            DaylightTime daylightTime = TimeZone.CurrentTimeZone.GetDaylightChanges(dateTimeOffset.Year);
            if (daylightTime.End.Date == dateTimeOffset.Date)
            {
                if (daylightTime.End.Hour - 1 == dateTimeOffset.Hour && dateTimeOffset.Offset.Hours == -6)
                {
                    return false;
                }
            }
            return true;
        }
        public void ProcessDevices()
        {
            string fdir = AppConfigSettings.InputFilePathRoot;
            if (Directory.Exists(fdir))
            {
                ProcessFiles(fdir);
            }
        }

        public void ProcessFiles(string targetDirectory)
        {
            // Process the list of files found in the directory.
            string[] fileEntries = Directory.GetFiles(targetDirectory);
     
            CacheExistingDevices();
            foreach (string fileName in fileEntries)
            {
                if (fileName.Contains(ModelName.LoadType.ToString()))
                {
                    if (ProcessInputFiles(fileName))
                    {
                        File.Move(fileName, String.Concat(AppConfigSettings.CompletedFilePathRoot + Path.GetFileName(fileName)));
                        Logger.Write("Move the File : - " + fileName);
                    }
                }
            }
            Logger.Write("Reading the file - complete");
        }

        public void CacheExistingDevices()
        {
            Logger.Write("Caching devices...");
            using (var context = new DAL.AMROpen_CISJustinEntities())
            {
                var meterquery = new DAL.MeterInfoQuery(context);
                //AllMeterInfo = meterquery.GetMeterInfoIdData1();
                AllFilterMeterInfo = meterquery.LGIntervalLoad_GetMeterInfo(ModelName.ServiceTypeID, ModelName.IntervalReasonTypeID, ModelName.FrequencyTypeID);
                AMIIntervalQaulityTypelist = meterquery.GetQualityTypes();
                AMIIntervalReadTypelist = meterquery.GetReadTypes();
            }
            Logger.Write("Cached devices.");
        }
        public bool ProcessInputFiles(string fpath)
        {
            IecCimFileReader reader;
            using (reader = new IecCimFileReader(fpath))
            {
                HeaderRecord hdr = reader.GetHeaderRecord();
                TrailerRecord trl = reader.GetTrailerRecord();
                if (hdr == null || trl == null)
                {
                    throw new Exception("Header OR Trailor record is null ---- " + fpath);
                }
                else
                {
                    long count = reader.GetDetailRecordCount();
                    if (trl.TotalRecordCount != count)
                    {
                        Logger.Write(" Count does not match with trailer record and detail record count", TraceEventType.Warning);
                    }
                    //expectedDevices = count;
                    while (!reader.EndOfStream)
                    {
                        IList<DetailRecord> dtlrc = reader.GetDetailRecordByDevice();
                        if(dtlrc.Count > 0)
                            ProcessDetailRecord(dtlrc);
                    }
                }
            }
            return true;
        }

        public void ProcessDetailRecord(IList<DetailRecord> dtlrc)
        {
            List<IntervalReadingmodel> interreadList = new List<IntervalReadingmodel>();

            foreach (DetailRecord dr in dtlrc)
            {
                //Validate detail record
                bool isDetailRecordValid = DetailRecordIsValid(dr);
                if (isDetailRecordValid == true)
                {
                    LGIntervalLoad_GetMeterInfo_Result channel = GetMeterInfo(dr);

                    if (channel != null)
                    {
                        ValidateCustomerType(channel);

                        var readType = GetAMIIntervalReadType(dr);
                        var qualityType = GetAMIIntervalQualityType(dr);

                        if(readType != null && qualityType != null)
                        {
                            IntervalReadingmodel model = new IntervalReadingmodel();
                            AMIIntervalQualityType newQualityType = new AMIIntervalQualityType
                            {
                                AMIIntervalQualityTypeId = qualityType.AMIIntervalQualityTypeId,
                                AMISystemId = qualityType.AMISystemId,
                                Description = qualityType.Description,
                                Type = qualityType.Type
                            };

                            model.QualityType = newQualityType;
                            model.IntervalChanelId = channel.IntervalChannelId;
                            model.ReadingValue = Convert.ToDecimal(dr.ReadingValue);
                            model.DST = false;
                            model.ReadDateTime = dr.ReadingDateTime.Value;
                            interreadList.Add(model);
                        }
                    }
                }
            }

            //return interreadList;

            if (interreadList.Count > 0)
            {
                t = z.GetDaylightChanges(interreadList[0].ReadDateTime.Year);
                if (interreadList.Where(d => d.ReadDateTime.Date == t.End.Date).Count() > 0)
                {
                    SetDstForReadings(interreadList);
                    ValidateAndRemoveDstReadings(interreadList);
                }

                using (var context = new DAL.AMROpen_CISJustinEntities())
                {
                    var query = new DAL.IntervalLoadQuery(context);
                    query.InsertIntervalreadingByDevice(interreadList);
                    
                    //query.InsertUpdateIntervalChannelTracking(interreadList[interreadList.Count - 1].IntervalChanelId, interreadList[interreadList.Count - 1].ReadDateTime, DateTime.Now);
                }
            }
        }

        /// <summary>
        /// Validates the passed detail record
        /// </summary>
        /// <param name="record">The detail record to validate</param>
        /// <returns>True if valid false if not valid</returns>
        public bool DetailRecordIsValid(DetailRecord record)
        {
            if (record == null)
                throw new ArgumentNullException("record");

            if (!record.ReadingDateTime.HasValue)
            {
                string readingDateTimeError = string.Format("ReadingDateTime is null.     Record:  {0}", record.IecCimRecord);
                Logger.Write(readingDateTimeError);
                Errors.Add(new IntervalLoadError(record.MeterId, record.ServicePointId, readingDateTimeError));
                return false;
            }

            if (!record.ReadingValue.HasValue)
            {
                string readingValueError = string.Format("ReadingValue is null.     Record:  {0}", record.IecCimRecord);
                Logger.Write(readingValueError);
                Errors.Add(new IntervalLoadError(record.MeterId, record.ServicePointId, readingValueError));
                return false;
            }

            if (record.ServicePointId.Length != 12)
            {
                string servicePointError = string.Format("Premise is not length 12.     Record:  {0}", record.IecCimRecord);
                Logger.Write(servicePointError);
                Errors.Add(new IntervalLoadError(record.MeterId, record.ServicePointId, servicePointError));
                return false;
            }

            Int64 outSPId;
            if (!Int64.TryParse(record.ServicePointId, out outSPId))
            {
                string servicePointError = string.Format("Premise is not an integer.     Record:  {0}", record.IecCimRecord);
                Logger.Write(servicePointError);
                Errors.Add(new IntervalLoadError(record.MeterId, record.ServicePointId, servicePointError));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Retrieves the meter info which matches the passed detail record
        /// </summary>
        /// <param name="record">Detail record to find the matching meter info for</param>
        /// <returns>MeterInfo object if found null if not found</returns>
        public LGIntervalLoad_GetMeterInfo_Result GetMeterInfo(DetailRecord record)
        {
            if (record == null)
                throw new ArgumentNullException("record");

            List<LGIntervalLoad_GetMeterInfo_Result> meterInfo = AllFilterMeterInfo.Where
            (m =>
                m.MeterNumber == record.MeterId
                && m.PremiseId == Convert.ToInt32(record.ServicePointId.Substring(0, 9))
                && m.PremiseServiceSequence == Convert.ToInt16(record.ServicePointId.Substring(9, 3))
            ).ToList();

            if(ModelName.LoadType == LoadTypeCode.GasChoice || ModelName.LoadType == LoadTypeCode.TransportGas)
            {
                short unitOfMeasure = 0;

                if (record.ReadingType == "0.0.7.4.1.7.58.0.0.0.0.0.0.0.0.2.121.0")
                    unitOfMeasure = 9;
                else if (record.ReadingType == "0.0.7.4.1.7.58.0.0.0.0.0.0.0.0.2.120.0")
                    unitOfMeasure = 1;

                meterInfo = meterInfo.Where(m => m.UnitOfMeasureTypeId == unitOfMeasure).ToList();
            }

            if (meterInfo.Count == 0)
            {
                string meterInfoError = string.Format("MeterInfo not found.     Record:  {0}", record.IecCimRecord);
                Logger.Write(meterInfoError);
                Errors.Add(new IntervalLoadError(record.MeterId, record.ServicePointId, meterInfoError));
                return null;
            }

            if (meterInfo.Count > 1)
            {
                string meterInfoError = string.Format("Multiple MeterInfo found.     Record:  {0}", record.IecCimRecord);
                Logger.Write(meterInfoError);
                Errors.Add(new IntervalLoadError(record.MeterId, record.ServicePointId, meterInfoError));
                return null;
            }

            return meterInfo.First();
        }

        public void ValidateCustomerType(LGIntervalLoad_GetMeterInfo_Result channel)
        {
            if (channel == null)
                throw new ArgumentNullException("channel");

            if(ModelName.LoadType == LoadTypeCode.GasChoice && channel.CustomerType != "GC")
            {
                string CompanyOffTypeError = string.Format("Customer type is not GC.");
                Logger.Write(CompanyOffTypeError);
                Errors.Add(new IntervalLoadError(channel.MeterNumber, channel.PremiseId.ToString(), CompanyOffTypeError));
            }

            if (ModelName.LoadType == LoadTypeCode.TransportGas && (channel.CustomerType != "TG" && channel.CustomerType != "IN"))
            {
                string CompanyOffTypeError = string.Format("Customer type is not TG or IN.");
                Logger.Write(CompanyOffTypeError);
                Errors.Add(new IntervalLoadError(channel.MeterNumber, channel.PremiseId.ToString(), CompanyOffTypeError));
            }
        }

        /// <summary>
        /// Retrieves the ami quality type that matches the passed detail record
        /// </summary>
        /// <param name="record">Detail record to find the matching ami quality type for</param>
        /// <returns>AmiQualityType object if found null if not found</returns>
        public AMIIntervalQualityType GetAMIIntervalQualityType(DetailRecord record)
        {
            if (record == null)
                throw new ArgumentNullException("record");

            List<AMIIntervalQualityType> qualityType = AMIIntervalQaulityTypelist.Where(q => q.Type == record.ReadingQuality).ToList();
            if (qualityType.Count == 0)
            {
                string qualityTypeError = string.Format("QualityType not found.     Record:  {0}", record.IecCimRecord);
                Logger.Write(qualityTypeError);
                Errors.Add(new IntervalLoadError(record.MeterId, record.ServicePointId, qualityTypeError));
                return null;
            }

            if (qualityType.Count > 1)
            {
                string qualityTypeError = string.Format("Multiple QualityType found.     Record:  {0}", record.IecCimRecord);
                Logger.Write(qualityTypeError);
                Errors.Add(new IntervalLoadError(record.MeterId, record.ServicePointId, qualityTypeError));
                return null;
            }

            return qualityType.First();
        }

        /// <summary>
        /// Retrieves the ami read type that matches the passed detail record
        /// </summary>
        /// <param name="record">Detail record to find the matching ami quality type for</param>
        /// <returns>AmiQualityType object if found null if not found</returns>
        public AMIIntervalReadType GetAMIIntervalReadType(DetailRecord record)
        {
            if (record == null)
                throw new ArgumentNullException("record");

            List<AMIIntervalReadType> readTypes = AMIIntervalReadTypelist.Where(r => r.Type == record.ReadingType).ToList();

            if (readTypes.Count == 0)
            {
                string readTypeError = string.Format("ReadType not found.     Record:  {0}", record.IecCimRecord);
                Logger.Write(readTypeError);
                Errors.Add(new IntervalLoadError(record.MeterId, record.ServicePointId, readTypeError));
                return null;
            }

            if (readTypes.Count > 1)
            {
                string readTypeError = string.Format("ReadType not found.     Record:  {0}", record.IecCimRecord);
                Logger.Write(readTypeError);
                Errors.Add(new IntervalLoadError(record.MeterId, record.ServicePointId, readTypeError));
                return null;
            }

            return readTypes.First();
        }

        public void ValidateAndRemoveDstReadings(IList<IntervalReadingmodel> records)
        {
            //Get count of 1:00AM's, 1:15AM's, 1:30AM's, 1:45AM's
            //  If 0 do nothing
            //  If 1 remove it from the list and log and report
            //  If 2 do nothing
            //  If greater than 2 remove from the list and log and report

            List<IntervalReadingmodel> OneAMRecordCount = records.Where(pairs => pairs.ReadDateTime.TimeOfDay == interval00).ToList();
            if (OneAMRecordCount.Count == 1  || OneAMRecordCount.Count > 2)
            {
                foreach (IntervalReadingmodel record in OneAMRecordCount)
                {
                    records.Remove(record);
                    string PairtypeError = "DST 1:00 AM record readings not found.";
                    Logger.Write(PairtypeError);
                }
            }
            if(ModelName.LoadType == LoadTypeCode.BillingElectric15Min)
            {
                List<IntervalReadingmodel> FifteenAMRecordCount = records.Where(pairs => pairs.ReadDateTime.TimeOfDay == interval15).ToList();
                if (FifteenAMRecordCount.Count == 1 || FifteenAMRecordCount.Count > 2)
                {
                    foreach (IntervalReadingmodel record in FifteenAMRecordCount)
                    {
                        records.Remove(record);
                        string PairtypeError = "DST 01:15 AM record readings not found.";
                        Logger.Write(PairtypeError);                       
                    }
                }
                List<IntervalReadingmodel> ThirtyAMRecordCount = records.Where(pairs => pairs.ReadDateTime.TimeOfDay == interval30).ToList();
                if (ThirtyAMRecordCount.Count == 1 || ThirtyAMRecordCount.Count > 2)
                {
                    foreach (IntervalReadingmodel record in ThirtyAMRecordCount)
                    {
                        records.Remove(record);
                        string PairtypeError = "DST 01:30 AM record readings not found.";
                        Logger.Write(PairtypeError);
                    }
                }
                List<IntervalReadingmodel> FortyfiveAMRecordCount = records.Where(pairs => pairs.ReadDateTime.TimeOfDay == interval45).ToList();
                if (FortyfiveAMRecordCount.Count == 1 || FortyfiveAMRecordCount.Count > 2)
                {
                    foreach (IntervalReadingmodel record in FortyfiveAMRecordCount)
                    {
                        records.Remove(record);
                        string PairtypeError = "DST 01:45 AM record readings not found.";
                        Logger.Write(PairtypeError);
                    }
                }
            }            
        }
           
        public void SetDstForReadings(IList<IntervalReadingmodel> readings)
        {
            var timeZoneInfo = TimeZoneInfo.Local;
            var timeZone = TimeZone.CurrentTimeZone;
            var dayLightChange = timeZone.GetDaylightChanges(readings[0].ReadDateTime.Year);

            var isAmbigious00 = false;
            var isAmbigious15 = false;
            var isAmbigious30 = false;
            var isAmbigious45 = false;
            var dstSecondHourHit = false;

            foreach (var reading in readings)
            {
                if (timeZoneInfo.IsAmbiguousTime(reading.ReadDateTime))
                {
                    if (reading.ReadDateTime.Minute == 0 && isAmbigious00 == false)
                    {
                        reading.DST = true;
                        isAmbigious00 = true;
                    }


                    if (reading.ReadDateTime.Minute == 15 && isAmbigious15 == false)
                    {
                        reading.DST = true;
                        isAmbigious15 = true;
                    }


                    if (reading.ReadDateTime.Minute == 30 && isAmbigious30 == false)
                    {
                        reading.DST = true;
                        isAmbigious30 = true;
                    }


                    if (reading.ReadDateTime.Minute == 45 && isAmbigious45 == false)
                    {
                        reading.DST = true;
                        isAmbigious45 = true;
                    }
                }
                else if (reading.ReadDateTime.Date == dayLightChange.End.Date)
                {
                    if (reading.ReadDateTime.Hour == 2 && !dstSecondHourHit)
                    {
                        reading.ReadDateTime = reading.ReadDateTime.AddHours(-1);
                        reading.DST = false;
                        dstSecondHourHit = true;
                    }
                    else if (reading.ReadDateTime.Hour == 2 && dstSecondHourHit)
                        reading.DST = false;
                    else
                        reading.DST = timeZoneInfo.IsDaylightSavingTime(reading.ReadDateTime);
                }
                else if (reading.ReadDateTime.Date == dayLightChange.Start.Date)
                {
                    if (reading.ReadDateTime.Hour == 2 && !dstSecondHourHit)
                    {
                        reading.ReadDateTime = reading.ReadDateTime.AddHours(1);
                        reading.DST = true;
                        dstSecondHourHit = true;
                    }
                    else
                        reading.DST = timeZoneInfo.IsDaylightSavingTime(reading.ReadDateTime);
                }
                else
                    reading.DST = timeZoneInfo.IsDaylightSavingTime(reading.ReadDateTime);
           }
        }
    }
}
