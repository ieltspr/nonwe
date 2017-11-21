using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Teg.Its.Metering.Core;
using Wec.Its.Metering.LGIntervalLoadCommon;
using Wec.Its.Metering.LGIntervalLoadCommon.Logging;

namespace Wec.Its.Metering.LGIntervalLoadDomain.Manager
{
    public class ErrorReportManager
    {
        public ErrorReportManager() { }

        public void CreateReport(List<IntervalLoadError> errors, decimal PercExpected, decimal PercActual)
        {
            if (errors == null)
            {
                throw new ArgumentNullException("errors");
            }

            if (errors.Count == 0)
            {
                Logger.Write("Error list passed to Report is empty.");
                return;
            }

            if (string.IsNullOrEmpty(PercExpected.ToString()) || string.IsNullOrEmpty(PercActual.ToString()))
            {
                Logger.Write("No Expected or Actual Percentage values passed to Report.");
            }

            decimal percentDevices = 100;
            if (PercExpected == 0)
            {
                Logger.Write("Cannot calc Percentage since the report was passed an Expected value = 0.");
            }
            else
            {
                percentDevices = PercActual / PercExpected;
            }

            string calcThreshold = "";
            if (percentDevices < AppConfigSettings.ReadingThreshold)
            {
                calcThreshold = "LOW";
            }
            else
            {
                calcThreshold = "ACCEPTABLE";
            }

            string rptLine1 = "LGIntervalLoad.exe encountered some discrepancies/errors when processing today's file.";
            string rptLine3 = "% of meters received in L&G file is:  " + calcThreshold;
            string rptLine4 = "Expected = " + PercExpected.ToString() + "% VS Actual = " + PercActual.ToString() + "%";
            string rptLine6 = "The following meters have errors:  ";
            string rptBlankLine = " ";
            string rptColHdg1 = "Meter";
            string rptColHdg2 = "Premise";
            string rptColHdg3 = "Error Details";

            string fileName = @"\LGIntervalLoadErrRpt-" + DateTime.Now.ToString("MMddyyyyhhmmss") + ".xlsx";
            FileInfo newFile = new FileInfo(AppConfigSettings.ErrorReportFilePathRoot + fileName);

            using (ExcelPackage errorReport = new ExcelPackage(newFile))
            {
                errorReport.Workbook.Worksheets.Add("LGIntervalLoadErrors");
                ExcelWorksheet intervalLoadErrors = errorReport.Workbook.Worksheets[1];
                intervalLoadErrors.Name = "LGIntervalLoadErrors";

                int rowIndex = 1;

                //upfront lines at top
                intervalLoadErrors.Cells[rowIndex, 1].Value = rptLine1;
                rowIndex++;
                intervalLoadErrors.Cells[rowIndex, 1].Value = rptBlankLine;
                rowIndex++;
                intervalLoadErrors.Cells[rowIndex, 1].Value = rptLine3;
                rowIndex++;
                intervalLoadErrors.Cells[rowIndex, 1].Value = rptLine4;
                rowIndex++;
                intervalLoadErrors.Cells[rowIndex, 1].Value = rptBlankLine;
                rowIndex++;
                intervalLoadErrors.Cells[rowIndex, 1].Value = rptLine6;
                rowIndex++;

                //column headings
                intervalLoadErrors.Cells[rowIndex, 1].Value = rptColHdg1;
                intervalLoadErrors.Cells[rowIndex, 1].Style.Font.Bold = true;
                intervalLoadErrors.Cells[rowIndex, 2].Value = rptColHdg2;
                intervalLoadErrors.Cells[rowIndex, 2].Style.Font.Bold = true;
                intervalLoadErrors.Cells[rowIndex, 3].Value = rptColHdg3;
                intervalLoadErrors.Cells[rowIndex, 3].Style.Font.Bold = true;
                rowIndex++;

                //write out list of cached errors
                foreach (IntervalLoadError Error in errors)
                {
                    intervalLoadErrors.Cells[rowIndex, 1].Value = Error.MeterNumber;
                    intervalLoadErrors.Cells[rowIndex, 2].Value = Error.PremiseId;
                    intervalLoadErrors.Cells[rowIndex, 3].Value = Error.ErrorMessage;
                    rowIndex++;
                }

                //save the excel
                errorReport.Save();
            }

            //if (!CommandLineOptions.EmailReport)
            //    return;
            
            List<string> emailList = new List<string>();

            if (!string.IsNullOrEmpty(AppConfigSettings.ErrorReportEmailTo))
            {
                emailList.AddRange(AppConfigSettings.ErrorReportEmailTo.Split(',').ToList());
            }

            if (emailList.Count > 0)
            {
                MailMessage newMailMessage = new MailMessage();

                foreach (string email in emailList)
                {
                    newMailMessage.To.Add(email);
                }

                newMailMessage.From = new MailAddress(AppConfigSettings.MailFromAddress);
                newMailMessage.Subject = AppConfigSettings.ErrorReportEmailSubject;
                newMailMessage.Body = AppConfigSettings.ErrorReportEmailBody + System.Environment.NewLine + AppConfigSettings.ErrorReportFilePathRoot + fileName;
                newMailMessage.IsBodyHtml = true;

                Attachment reportAttachment = new Attachment(AppConfigSettings.ErrorReportFilePathRoot + fileName);
                newMailMessage.Attachments.Add(reportAttachment);

                SmtpClient smtp = new SmtpClient(AppConfigSettings.MailRelayServer);
                smtp.Send(newMailMessage);
            }
        }
    }
}
