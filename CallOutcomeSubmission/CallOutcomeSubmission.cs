using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Perceptionist.Services.CallOutcomeSubmission.ServiceReference;

namespace Perceptionist.Services.CallOutcomeSubmission
{
    public partial class CallOutcomeSubmission : ServiceBase
    {
        private static Timer submissionTimer;
        double submissionInterval = .25;  // Enter time in minutes 
        static LeadServiceClient client;
//        static ServiceReference.ListItem[] actionList;
        private static int? leadActionResult = -1;


        // List of available actions
        private static string ACTION_TYPE_CALL_ATTEMPT = "Call Attempt";
        private static string ACTION_TYPE_LEFT_MESSAGE = "Left Voice Message";
        private static string ACTION_TYPE_ESTIMATE_BOOKED = "Estimate Booked";
        private static string ACTION_TYPE_ONE_TIME_CLEAN_BOOKED = "One-Time Clean Booked";
        private static string ACTION_TYPE_OTHER = "Other";

        // Call Attempt, Lead Forwarded, Transfer Lead, Pursuing, Waiting for Response, Other, Dead Lead,
        // Export, Recurring Customer, Occasional Customer, Estimate Performed, Estimate Booked, Letter Sent, 
        // Meeting, Left Voice Message, Left Message, Phone Conversation, Email Sent, Lead Received 

        public CallOutcomeSubmission()
        {
            InitializeComponent();

            // BKE Implement Windows logging
            if (!System.Diagnostics.EventLog.SourceExists("CallSubmissionEventLogSource"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "CallSubmissionEventLogSource", "CallSubmissionEventLog");
            }
            eventLog.Source = "CallSubmissionEventLogSource";
            eventLog.Log = "CallSubmissionEventLog";
        }

        protected override void OnStart(string[] args)
        {
            eventLog.WriteEntry("CallOutcomeSubmission.OnStart: service started");

            submissionTimer = new Timer(submissionInterval * (1000 * 60));      // (converts ms to minutes)
            submissionTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            submissionTimer.Enabled = true;
        }


        protected override void OnStop()
        {
            eventLog.WriteEntry("CallOutcomeSubmission service stopped");
        }


        private static void OnTimedEvent(object source, ElapsedEventArgs e) 
        {
            CallOutcomeSubmission los = new CallOutcomeSubmission();
            client = connectToService();
//            actionList = client.GetLookupList("ActionTypes");

            submissionTimer.Enabled = false;
            try
            {
                using (var context = new CallOutcomeContext())
                {
                    string[] _testFranchiseIDs = { "1050", "1417", "1240", "2157", "1473", "2340", "2215", "2270", "2336", "2153", "9999" };
                    DateTime tenDaysAgo = DateTime.Now.AddHours(-240);
                    var queryLead = from co in context.T_MollyMaidCallOutcome
                                    join ca in context.T_Call on co.CallID equals ca.CallID
                                    join cp in context.T_Company on ca.CompanyID equals cp.CompanyID
                                    join lv in context.T_LeadVendorEmailHeader on co.LeadVendorEmailID equals lv.LeadVendorEmailID into oj
                                    from sublv in oj.DefaultIfEmpty()
                                    where co.EnteredOn > tenDaysAgo && co.MollyMaidLeadActionID == null
                                        && _testFranchiseIDs.Contains(cp.FranchiseID)  // remove this line to send info for all franchises
                                    select new
                                    {
                                        co.CallOutcomeID,
                                        co.CallID,
                                        co.LeadVendorEmailID,
                                        MollyMaidLeadID = (sublv == null ? String.Empty : sublv.email_text),
                                        ca.OutcomeID,
                                        cp.FranchiseID,
                                        co.MollyMaidLeadActionID,
                                        co.LeadAction,
                                        co.ProcessedOn
                                    };

                    // if any results found for query attempt to send result to Molly Maid
                    if (queryLead.Any())
                    {
                        foreach (var call in queryLead.ToList())
                        {
                            // if the franchsie exists on Molly Maid database
                            if (client.FranchiseExists(int.Parse(call.FranchiseID)))
                            {
                                los.eventLog.WriteEntry("CallOutcomeSubmission.OnTimedEvent: Call ID " + call.CallID + " found with result of " + call.OutcomeID);

                                // if LeadVendorEmailID found then process as lead
                                if (call.LeadVendorEmailID.Value > 0)
                                {
                                    // Get the specific call for updating
                                    var _leadCall = context.T_MollyMaidCallOutcome.Find(call.CallOutcomeID);

                                    _leadCall.MollyMaidLeadActionID = SubmitOutcomeFromLead(los, context, call.CallOutcomeID, call.OutcomeID,
                                                                                        Convert.ToInt32(call.LeadVendorEmailID), int.Parse(call.MollyMaidLeadID));
                                    _leadCall.LeadAction = ACTION_TYPE_CALL_ATTEMPT;
                                    _leadCall.ProcessedOn = DateTime.Now;
                                }

                                // if LeadvendorEmailID = -1 then this is a inbound call
                                else if (call.LeadVendorEmailID.Value == -1)
                                {
                                    // Get the specific call for updating
                                    var _leadCall = context.T_MollyMaidCallOutcome.Find(call.CallOutcomeID);

                                    _leadCall.MollyMaidLeadActionID = SubmitOutcomeFromNewCall(los, context, call.CallOutcomeID, call.OutcomeID);
                                    _leadCall.LeadAction = ACTION_TYPE_CALL_ATTEMPT;
                                    _leadCall.ProcessedOn = DateTime.Now;
                                }

                                // if LeadVendorEmailID is null or 0 then something screwed up
                                else
                                {
                                    los.eventLog.WriteEntry("CallOutcomeSubmission.OnTimedEvent: Call with OutcomeID " + call.OutcomeID.ToString() + ", not processed correctly");
                                }  // end switch

                            }

                            // if no franchsie found
                            else
                            {
                                los.eventLog.WriteEntry("CallOutcomeSubmission.OnTimedEvent: No franchise found with franchise ID " + call.FranchiseID);
                            }

                            // Save any changes currently on context
                            context.SaveChanges();

                        }  // end foreach

                    }
                    // if no results found from query write system log stating such
                    else
                    {
                        // los.eventLog.WriteEntry("CallOutcomeSubmission.OnTimedEvent: No new entries found");
                    }


                }  // end using

                client.Close();
            }

            catch (System.TimeoutException exception)
            {
                los.eventLog.WriteEntry("CallOutcomeSubmission.OnTimedEvent:" + exception.ToString());
                client.Abort();
            }
            catch (System.ServiceModel.CommunicationException exception)
            {
                los.eventLog.WriteEntry("CallOutcomeSubmission.OnTimedEvent:" + exception.ToString());
                client.Abort();
            }
            finally
            {
                submissionTimer.Enabled = true;
            }

        }


        private static int? SubmitOutcomeFromLead(CallOutcomeSubmission _los, 
                                              CallOutcomeContext _context, 
                                              int _callOutcomeID,
                                              int? _outcomeID,
                                              int _leadVendorEmailID,
                                              int _mollyMaidLeadID)
        {
//            _los.eventLog.WriteEntry("CallOutcomeSubmission.SubmitOutcomeFromLead: Entered method");
            var _call = (from co in _context.T_MollyMaidCallOutcome
                        join ca in _context.T_Call on co.CallID equals ca.CallID
                        join cn in _context.T_CallNeed on ca.CallID equals cn.CallID
                        join tln in _context.T_L_Need on ca.NeedID equals tln.NeedID
                        join tlnf in _context.T_L_NeedField on cn.NeedFieldID equals tlnf.NeedFieldID
                        join tlo in _context.T_L_Outcome on ca.OutcomeID equals tlo.OutcomeID
                        join lveh in _context.T_LeadVendorEmailHeader on co.LeadVendorEmailID equals lveh.LeadVendorEmailID
                         where co.CallOutcomeID == _callOutcomeID && cn.NeedID == ca.NeedID
                        select new {
                            co.MollyMaidLeadActionID,
                            co.LeadAction,
                            tln.Need,
                            tlo.Outcome,
                            tlnf.FieldName,
                            cn.Visible,
                            cn.ValueData,
                            lveh.LeadVendorEmailID,
                            lveh.CallbackOn
                        }).ToList();

            var _callCount = from co in _context.T_MollyMaidCallOutcome
                             join lveh in _context.T_LeadVendorEmailHeader on co.LeadVendorEmailID equals lveh.LeadVendorEmailID
                             where co.LeadVendorEmailID == _leadVendorEmailID
                             select new { co, lveh };

            int _count = _callCount.ToList().Count();

            LeadAction leadAction = new LeadAction();
            leadAction.ActionDate = DateTime.Now;
            leadAction.ActionType = ACTION_TYPE_CALL_ATTEMPT;
            switch (_outcomeID)
            {
                case 36:    // Sale               
                    leadAction.Notes = "Call Attempt " + _count + ": Scheduled Appointment for " +
                        (_call.Any(a => a.FieldName.Equals("AppointmentDateAndTime") && a.Visible && !String.IsNullOrEmpty(a.ValueData))
                        ? _call       // if appointment found
                        .Where(a => a.FieldName.Equals("AppointmentDateAndTime") && a.Visible).Single().ValueData.ToString()
                        : "<Appointment not found>");
                    leadAction.ActionType =
                        (_call.FirstOrDefault().Need.Contains("Estimate"))
                        ? ACTION_TYPE_ESTIMATE_BOOKED          // if Estimate is found
                        : ACTION_TYPE_ONE_TIME_CLEAN_BOOKED;   // else, must be One Time Clean
                    break;
                case 37:    // No Sale             
                    leadAction.Notes = "Call Attempt " + _count + ": No appointment scheduled - " +
                        (_call.Any(a => a.FieldName.Equals("DetailedOutcome") && a.Visible && !String.IsNullOrEmpty(a.ValueData))
                        ? _call       // if detailed outcome found
                        .Where(a => a.FieldName.Equals("DetailedOutcome") && a.Visible).Single().ValueData.ToString()
                        : "<Detailed Outcome not found>");
                    break;
                case 39:    // Not Answered
                    leadAction.Notes = "Call Attempt " + _count + ": Not Answered";
                    break;
                case 40:    // Not In Service/Wrong Number
                    leadAction.Notes = "Call Attempt " + _count + ": Not In Service/Wrong Number";
                    break;
                case 42:    // LeftMessage
                    leadAction.ActionType = ACTION_TYPE_LEFT_MESSAGE;
                    leadAction.Notes = "Call Attempt " + _count + ": Left Message";
                    break;
                case 46:    // Scheduled Callback               
                    leadAction.Notes = "Call Attempt " + _count + ": Scheduled Callback for " + 
                        (_call.FirstOrDefault().CallbackOn.HasValue 
                        ? "Unknown" 
                        : _call.FirstOrDefault().CallbackOn.ToString());
                    break;
                default:
                    leadAction.Notes = "Call Attempt " + _count + ": Unknown Outcome";
                    break;
            }
                

            Outcome outcome = new Outcome();
            outcome.NeedType = _call.FirstOrDefault().Need.ToString();
            outcome.CallOutcome = _call.FirstOrDefault().Outcome.ToString();
            try
            {
                outcome.CallOutcome = _call.FirstOrDefault().Outcome.ToString();
                // ternary check for null
                outcome.DetailedCallOutcome = _call.Any(a => a.FieldName.Equals("DetailedOutcome") && a.Visible && !String.IsNullOrEmpty(a.ValueData))
                    ? _call.Where(a => a.FieldName.Equals("DetailedOutcome") && a.Visible).Single().ValueData.ToString()
                    : "<Detailed Outcome not found>";         // if null check is false
                    
                // ternary check for null
                outcome.AdditionalNotes = _call.Any(a => a.FieldName.Equals("OtherInfo") && a.Visible && !String.IsNullOrEmpty(a.ValueData))
                    ? _call.Where(a => a.FieldName.Equals("OtherInfo") && a.Visible).Single().ValueData.ToString()
                    : "<Additional Notes not found>";
//                _los.eventLog.WriteEntry("CallOutcomeSubmission.SubmitNotFinalOutcome: " + outcome.DetailedCallOutcome + " :: " + outcome.AdditionalNotes);
            }
            catch (Exception e)
            {
                _los.eventLog.WriteEntry("CallOutcomeSubmission.SubmitNotFinalOutcome: " + e.Message);
            }

            leadAction.Outcome = outcome;
            leadActionResult = client.SubmitLeadAction(_mollyMaidLeadID, leadAction);
//            _los.eventLog.WriteEntry("CallOutcomeSubmission.SubmitNotFinalOutcome: leadActionResult = " + leadActionResult);

            return leadActionResult;

        }


        private static int? SubmitOutcomeFromNewCall(CallOutcomeSubmission _los,
                                                     CallOutcomeContext _context,
                                                     int _callOutcomeID,
                                                     int? _outcomeID)
        {
//            _los.eventLog.WriteEntry("CallOutcomeSubmission.SubmitOutcomeFromNewCall: Entered method");
            var _call = (from co in _context.T_MollyMaidCallOutcome
                         join ca in _context.T_Call on co.CallID equals ca.CallID
                         join cn in _context.T_CallNeed on ca.CallID equals cn.CallID
//                            new { _callID = ca.CallID, _needID = ca.NeedID } equals
//                            new { _callID = cn.CallID, _needID = cn.NeedID }
                         join cp in _context.T_Company on ca.CompanyID equals cp.CompanyID
                         join tln in _context.T_L_Need on ca.NeedID equals tln.NeedID
                         join tlo in _context.T_L_Outcome on ca.OutcomeID equals tlo.OutcomeID
                         join tlnf in _context.T_L_NeedField on cn.NeedFieldID equals tlnf.NeedFieldID
                         where co.CallOutcomeID == _callOutcomeID && cn.NeedID == ca.NeedID
                         select new
                         {
                             co.MollyMaidLeadActionID,
                             co.LeadAction,
                             ca.NeedID,
                             cp.FranchiseID,
                             tln.Need,
                             tlo.Outcome,
                             tlnf.FieldName,
                             cn.Visible,
                             cn.ValueData
                         }).ToList();

            Lead _lead = new Lead();
            _lead.FranNum = int.Parse(_call.FirstOrDefault().FranchiseID);

            LeadAction leadAction = new LeadAction();
            leadAction.ActionDate = DateTime.Now;
            leadAction.ActionType = ACTION_TYPE_CALL_ATTEMPT;
            switch (_outcomeID)
            {
                case 36:    // Sale               
                    leadAction.Notes = "New Inbound Call: Scheduled Appointment for " +
                        (_call.Any(a => a.FieldName.Equals("AppointmentDateAndTime") && a.Visible && !String.IsNullOrEmpty(a.ValueData))
                        ? _call.Where(a => a.FieldName.Equals("AppointmentDateAndTime") && a.Visible).Single().ValueData.ToString()
                        : "<Appointment data not found>");           // if null check is false, set to empty string;
                    break;
                case 37:    // No Sale             
                    leadAction.Notes = "New Inbound Call: No appointment scheduled - " +
                        (_call.Any(a => a.FieldName.Equals("DetailedOutcome") && a.Visible && !String.IsNullOrEmpty(a.ValueData))
                        ? _call.Where(a => a.FieldName.Equals("DetailedOutcome") && a.Visible).Single().ValueData.ToString()
                        : "<Detailed Outcome data not found>");           // if null check is false, set to empty string;
                    break;
                default:
                    leadAction.Notes = "New Inbound Call: Unknown Outcome";
                    break;
            }

            // set up the lead detail
            LeadDetail _detail = new LeadDetail();
            _detail.LeadDate = DateTime.Now;
            string[] _nameParts = _call.Any(a => a.FieldName.Equals("Name") && a.Visible && !String.IsNullOrEmpty(a.ValueData)) ?
                _call.Single(a => a.FieldName.Equals("Name") && a.Visible).ValueData.ToString().Split(' ')
                : new string[]{"<First Name not found>", "<Last name not found>"};
            _detail.FirstName = String.IsNullOrEmpty(_nameParts[0])
                ? "<First Name not found>"
                : _nameParts[0];
            // put any remaining _nameParts into LastName field
            for (int i = 1; i < _nameParts.Length; i++)
            {
                _detail.LastName += _nameParts[i] + " ";
            }
            if (String.IsNullOrEmpty(_detail.LastName))
            {
                _detail.LastName = "<Last Name not found>";
            }
            else
            {
                _detail.LastName.Trim();
            }
            _detail.Address1 = _call.Any(a => a.FieldName.Equals("Address") && a.Visible && !String.IsNullOrEmpty(a.ValueData)) ?
                _call.FirstOrDefault(a => a.FieldName.Equals("Address") && a.Visible).ValueData.ToString() : "Value not found";
            _detail.City = _call.Any(a => a.FieldName.Equals("City") && a.Visible && !String.IsNullOrEmpty(a.ValueData)) ?
                _call.FirstOrDefault(a => a.FieldName.Equals("City") && a.Visible).ValueData.ToString() : "Value not found";
            _detail.State = _call.Any(a => a.FieldName.Equals("State") && a.Visible && !String.IsNullOrEmpty(a.ValueData)) ?
                _call.FirstOrDefault(a => a.FieldName.Equals("State") && a.Visible).ValueData.ToString() : "Value not found";
            _detail.Zip = _call.Any(a => a.FieldName.Equals("ZipCode") && a.Visible && !String.IsNullOrEmpty(a.ValueData)) ?
                _call.FirstOrDefault(a => a.FieldName.Equals("ZipCode") && a.Visible).ValueData.ToString() : "Value not found";
            _detail.Phone1 = _call.Any(a => a.FieldName.Equals("Phone") && a.Visible && !String.IsNullOrEmpty(a.ValueData)) ?
                _call.FirstOrDefault(a => a.FieldName.Equals("Phone") && a.Visible).ValueData.ToString() : "Value not found";
            _detail.Email = _call.Any(a => a.FieldName.Equals("Email") && a.Visible && !String.IsNullOrEmpty(a.ValueData)) ?
                _call.FirstOrDefault(a => a.FieldName.Equals("Email") && a.Visible).ValueData.ToString() : "Value not found";
            _detail.Frequency = _call.Any(a => a.FieldName.Equals("ServiceFrequency") && a.Visible && !String.IsNullOrEmpty(a.ValueData)) ?
                ConvertServiceFrequency(_call.FirstOrDefault(a => a.FieldName.Equals("ServiceFrequency") && a.Visible).ValueData.ToString(),
                                        _call.FirstOrDefault().Need.ToString())
                : ConvertServiceFrequency("", _call.FirstOrDefault().Need.ToString());
            _detail.SquareFeet = _call.Any(a => a.FieldName.Equals("SquareFootage") && a.Visible && !String.IsNullOrEmpty(a.ValueData)) ?
                _call.FirstOrDefault(a => a.FieldName.Equals("SquareFootage") && a.Visible).ValueData.ToString() : "Value not found"; ;
            string[] _roomParts = _call.Any(a => a.FieldName.Equals("BedroomsBathroom") && a.Visible && !String.IsNullOrEmpty(a.ValueData)) ?
                _call.FirstOrDefault(a => a.FieldName.Equals("BedroomsBathroom") && a.Visible).ValueData.ToString().Split('/') :
                "Value not found/Value Not Found".Split('/');
            _detail.Bedrooms = _roomParts[0];
            _detail.Bathrooms = _roomParts[1];
            _detail.Pets = _call.Any(a => a.FieldName.Equals("Pets") && a.Visible && !String.IsNullOrEmpty(a.ValueData)) ?
                _call.FirstOrDefault(a => a.FieldName.Equals("Pets") && a.Visible).ValueData.ToString() : "Value not found";
            _detail.LeadSource = "Other";
            _detail.LeadStatus = "New Lead";
            _detail.NextAction = "Initial Contact";
            _detail.Comments = _call.Any(a => a.FieldName.Equals("OtherInfo") && a.Visible && !String.IsNullOrEmpty(a.ValueData)) ?
                _call.FirstOrDefault(a => a.FieldName.Equals("OtherInfo") && a.Visible).ValueData.ToString() : "";
            _detail.Phone1Type = "Home";
            _lead.LeadDetails = _detail;

            Outcome outcome = new Outcome();
            outcome.NeedType = _call.FirstOrDefault().Need.ToString();
            outcome.CallOutcome = _call.FirstOrDefault().Outcome.ToString();
            try
            {
                outcome.CallOutcome = _call.FirstOrDefault().Outcome.ToString();
                // ternary check for null
                outcome.DetailedCallOutcome = _call.Any(a => a.FieldName.Equals("DetailedOutcome") && a.Visible && !String.IsNullOrEmpty(a.ValueData))
                    ? _call       // if value is not null
                    .Where(a => a.FieldName.Equals("DetailedOutcome") && a.Visible)
                    .Single().ValueData.ToString()
                    : "";         // if value is null, set to empty string
                // ternary check for null
                outcome.AdditionalNotes = _call.Any(a => a.FieldName.Equals("OtherInfo") && a.Visible && !String.IsNullOrEmpty(a.ValueData)) 
                    ? _call       // if value is not null
                    .Where(a => a.FieldName.Equals("OtherInfo") && a.Visible)
                    .Single().ValueData.ToString()
                    : "";         // if value is null, set to empty string

//                _los.eventLog.WriteEntry("CallOutcomeSubmission.SubmitFinalOutcome: " + outcome.DetailedCallOutcome + " :: " + outcome.AdditionalNotes);
            }
            catch (Exception e)
            {
                _los.eventLog.WriteEntry("CallOutcomeSubmission.SubmitFinalOutcome: " + e.Message);
            }

            leadAction.Outcome = outcome;
            _lead.LeadAction = leadAction;

            leadActionResult = client.SubmitLead(_lead);
            _los.eventLog.WriteEntry("CallOutcomeSubmission.SubmitFinalOutcome: leadActionResult = " + leadActionResult);

            return leadActionResult;

        }

        private static string ConvertServiceFrequency(string _freq, string _need)
        {
            // Convert PErceptionist values to Molly Maid values
            if (_need.ToLower().Contains("onetime"))
            {
                return "One Time";
            }
            else
            {
                if (_freq == "Weekly")
                {
                    return "Weekly";
                }
                else if (_freq == "Every Two Weeks")
                {
                    return "Alternate Weekly";
                }
                else if ((_freq == "Every Four Weeks") || (_freq == "Monthly"))
                {
                    return "Monthly";
                }

                return "Occasional";
            }
        }


        private static LeadServiceClient connectToService()
        {
            try
            {
                // "wsHttpBinding" is the name of the client endpoint found in app.config
                // client endpoints are auto-gernerated when importing the service reference
                client = new LeadServiceClient("wsHttpBinding");
                //ServicePointManager.ServerCertificateValidationCallback = CertificatePolicy.CertificateValidationCallBack;

                client.ClientCredentials.UserName.UserName = "MollyMaid";
                client.ClientCredentials.UserName.Password = "M1ll2M34d";
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return client;
        }
    }
}
