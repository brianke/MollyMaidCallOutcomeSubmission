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
    
    public partial class T_CallNeed
    {
        public int CallNeedID { get; set; }
        public int CallID { get; set; }
        public int NeedID { get; set; }
        public int NeedFieldID { get; set; }
        public string ValueData { get; set; }
        public System.DateTime EnteredOn { get; set; }
        public string EnteredBy { get; set; }
        public byte[] Concurrency { get; set; }
        public bool Visible { get; set; }
        public bool HasBeenVisited { get; set; }
    
        public virtual T_Call T_Call { get; set; }
        public virtual T_L_Need T_L_Need { get; set; }
        public virtual T_L_NeedField T_L_NeedField { get; set; }
    }
}
