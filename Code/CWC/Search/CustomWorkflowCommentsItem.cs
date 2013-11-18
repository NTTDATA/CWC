using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.Data;


namespace CWC.Search
{
    public class CustomWorkflowCommentsItem: SearchResultItem
    {
        [IndexField("_name")]
        public string ItemName { get; set; }

        public virtual string WorkflowItemID { get; set; }
        public virtual string Language { get; set; }
        public virtual string Version { get; set; }

        [IndexField("_template")]
        public string Template { get; set; }

        [IndexField(BuiltinFields.ID)]
        public virtual ID ID { get; set; }
    }
}
