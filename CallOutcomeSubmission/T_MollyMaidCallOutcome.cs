//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Perceptionist.Services.CallOutcomeSubmission
{
    using System;
    using System.Collections.Generic;
    
    public partial class T_MollyMaidCallOutcome
    {
        public int CallOutcomeID { get; set; }
        public int CallID { get; set; }
        public Nullable<int> LeadVendorEmailID { get; set; }
        public Nullable<int> MollyMaidLeadActionID { get; set; }
        public string LeadAction { get; set; }
        public System.DateTime EnteredOn { get; set; }
        public Nullable<System.DateTime> ProcessedOn { get; set; }
    
        public virtual T_Call T_Call { get; set; }
        public virtual T_LeadVendorEmailHeader T_LeadVendorEmailHeader { get; set; }
    }
}
