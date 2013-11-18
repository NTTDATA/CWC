using System.Text;
using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Resources;
using Sitecore.Security;
using Sitecore.Security.Accounts;
using Sitecore.Security.Domains;
using Sitecore.Shell;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.XmlControls;
using Sitecore.Workflows;
using System;
using System.Collections.Specialized;
using System.Web.UI;
using System.Web.UI.WebControls;


namespace CWC.Workflow
{
    /// <summary>
    /// Represents a WorkboxHistoryXmlControl.
    /// </summary>
    public class WorkboxHistoryXmlControl : XmlControl
    {
        private string m_itemID;

        private string m_language;

        private string m_version;

        private string m_workflowID;

        /// <summary></summary>
        protected Border History;

        /// <summary>
        /// Gets or sets the item ID.
        /// </summary>
        /// <value>The item ID.</value>
        public string ItemID
        {
            get
            {
                return this.m_itemID;
            }
            set
            {
                this.m_itemID = value;
            }
        }

        /// <summary>
        /// Gets or sets the language.
        /// </summary>
        /// <value>The language.</value>
        public string Language
        {
            get
            {
                return this.m_language;
            }
            set
            {
                this.m_language = value;
            }
        }

        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        /// <value>The version.</value>
        public string Version
        {
            get
            {
                return this.m_version;
            }
            set
            {
                this.m_version = value;
            }
        }

        /// <summary>
        /// Gets or sets the workflow ID.
        /// </summary>
        /// <value>The workflow ID.</value>
        public string WorkflowID
        {
            get
            {
                return this.m_workflowID;
            }
            set
            {
                this.m_workflowID = value;
            }
        }

        public WorkboxHistoryXmlControl()
        {
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load"></see> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs"></see> object that contains the event data.</param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.WriteLanguageCssClass();
            if (!Sitecore.Context.ClientPage.IsEvent)
            {
                IWorkflowProvider workflowProvider = Sitecore.Context.ContentDatabase.WorkflowProvider;
                if (workflowProvider != null)
                {
                    IWorkflow workflow = workflowProvider.GetWorkflow(this.WorkflowID);
                    Error.Assert(workflow != null, string.Concat("Workflow \"", this.WorkflowID, "\" not found."));
                    Item item = Sitecore.Context.ContentDatabase.Items[this.ItemID, Sitecore.Globalization.Language.Parse(this.Language), Sitecore.Data.Version.Parse(this.Version)];
                    if (item != null)
                    {
                        NameValueCollection nameValueCollection = new NameValueCollection();
                        NameValueCollection nameValueCollection1 = new NameValueCollection();
                        WorkflowState[] states = workflow.GetStates();
                        for (int i = 0; i < (int)states.Length; i++)
                        {
                            WorkflowState workflowState = states[i];
                            nameValueCollection.Add(workflowState.StateID, workflowState.DisplayName);
                            nameValueCollection1.Add(workflowState.StateID, workflowState.Icon);
                        }
                        WorkflowEvent[] history = workflow.GetHistory(item);
                        string name = Sitecore.Context.Domain.Name;
                        WorkflowEvent[] workflowEventArray = history;
                        for (int j = 0; j < (int)workflowEventArray.Length; j++)
                        {
                            WorkflowEvent workflowEvent = workflowEventArray[j];
                            string user = workflowEvent.User;
                            if (user.StartsWith(string.Concat(name, "\\"), StringComparison.OrdinalIgnoreCase))
                            {
                                user = StringUtil.Mid(user, name.Length + 1);
                            }
                            string[] strArrays = new string[] { user, Translate.Text("Unknown") };
                            user = StringUtil.GetString(strArrays);
                            string str = nameValueCollection1[workflowEvent.NewState];
                            string[] item1 = new string[] { nameValueCollection[workflowEvent.OldState], "?" };
                            string str1 = StringUtil.GetString(item1);
                            string[] strArrays1 = new string[] { nameValueCollection[workflowEvent.NewState], "?" };
                            string str2 = StringUtil.GetString(strArrays1);
                            string str3 = DateUtil.FormatDateTime(workflowEvent.Date, "D", Sitecore.Context.User.Profile.Culture);
                            XmlControl webControl = Resource.GetWebControl("WorkboxHistoryEntry") as XmlControl;
                            this.History.Controls.Add(webControl);
                            webControl["User"] = user;
                            webControl["Icon"] = str;
                            webControl["Date"] = str3;
                            webControl["Action"] = string.Format(Translate.Text("Changed from <b>{0}</b> to <b>{1}</b>."), str1, str2);

                            //check if the value in the Text field is an ID
                            Sitecore.Data.ID customWorkFlowItemID;
                            if (Sitecore.Data.ID.TryParse(workflowEvent.Text, out customWorkFlowItemID))
                            {
                                //get the rendered text from the fields of the custom template item from the item bucket
                                webControl["Text"] = GetWorkflowItemDetails(customWorkFlowItemID);                                
                            }
                            else
                                webControl["Text"] = workflowEvent.Text;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the output with all the fields and their values associated with the custom template item from the item bucket
        /// </summary>
        /// <param name="customWorkFlowItemID">The ID of the custom template item.</param>
        private string GetWorkflowItemDetails(Sitecore.Data.ID customWorkFlowItemID)
        {
            StringBuilder returnString = new StringBuilder();
            //get the item and make sure its not null
            Item customWorkflowItem = Sitecore.Context.ContentDatabase.GetItem(customWorkFlowItemID);
            if (customWorkflowItem != null)
            {
                //loop through all the item fields
                foreach (Field field in customWorkflowItem.Fields)
                {
                    //only extract the template fields and their values
                    if (!(field.Name == "Additional Parameters") && (!(field.Name == "Personalization") || UserOptions.View.ShowPersonalizationSection) && ((!(field.Name == "Tests") || UserOptions.View.ShowTestLabSection) && RenderingItem.IsAvalableNotBlobNotSystemField(field)))
                        returnString.AppendLine(string.Format("<b>{0}</b>: {1}<BR>", field.DisplayName, field.Value));
                }
            }
            return returnString.ToString();
        }

        /// <summary>
        /// Writes the language CSS class.
        /// </summary>
        private void WriteLanguageCssClass()
        {
            string item = this.History.Attributes["class"];
            if (!string.IsNullOrEmpty(item))
            {
                item = string.Concat(item, " ");
            }
            this.History.Attributes["class"] = string.Concat(item, UIUtil.GetLanguageCssClassString());
        }
    }
}