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
    
    public partial class T_L_NeedField
    {
        public T_L_NeedField()
        {
            this.T_CallNeed = new HashSet<T_CallNeed>();
        }
    
        public int NeedFieldID { get; set; }
        public int NeedID { get; set; }
        public string FieldName { get; set; }
        public Nullable<int> Sequence { get; set; }
        public string DisplayAs { get; set; }
        public string FieldPrompt { get; set; }
        public string DataType { get; set; }
        public Nullable<int> Length { get; set; }
        public Nullable<bool> IsRequired { get; set; }
        public string ValidOptions { get; set; }
        public string Comment { get; set; }
        public Nullable<System.DateTime> EnteredOn { get; set; }
        public string EnteredBy { get; set; }
        public byte[] Concurrency { get; set; }
        public bool IsRequiredForSale { get; set; }
    
        public virtual ICollection<T_CallNeed> T_CallNeed { get; set; }
        public virtual T_L_Need T_L_Need { get; set; }
    }
}
