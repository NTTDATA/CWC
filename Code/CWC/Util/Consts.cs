using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CWC.Util
{
    public static class Consts
    {
        public static class FieldIDs
        {
            public const string CustomTemplate = "{EDB716F9-811E-4520-BC59-BEF08B343CD3}";

            public static class CustomWorkflowCommentsItem
            {
                public const string WorkflowItemID = "{6A920413-913E-421F-8895-E1CFD5521540}";
                public const string Language = "{DD1626EF-5FFA-4EF2-96F1-BD364220537C}";
                public const string Version = "{D55A4933-2489-4338-A6E7-99FFF97DE495}";
            }
        }

        public static class ItemIDs
        {
            public const string WorkflowCommentsItemBucket = "{F093F9FC-13D3-4D0E-87C8-5006B861F319}";
        }

        public static class Strings
        {
            public const string DialogTitle = "Custom Workflow Comments Module";
        }

        public static class TemplateIDs
        {
            public const string CustomWorkflowCommentsBase = "{0273184E-8F0A-4B93-8869-73191E77C3E9}";
            public const string CustomWorkflowCommentsItem = "{3F8A5EAD-42BA-4C33-9205-8D189A7F7C65}";
        }
    }
}
