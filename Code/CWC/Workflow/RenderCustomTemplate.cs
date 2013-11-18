using System.Linq;
using CWC.Util;
using Sitecore;
using Sitecore.Buckets.Managers;
using Sitecore.Collections;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Layouts;
using Sitecore.SecurityModel;
using Sitecore.Shell;
using Sitecore.Shell.Applications.ContentEditor;
using Sitecore.Shell.Applications.WebEdit;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI.Sheer;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;

namespace CWC.Workflow
{
    public class RenderCustomTemplate
    {
        /// <summary>
        /// The current pipeline arguments.
        /// 
        /// </summary>
        private ClientPipelineArgs args;
        /// <summary>
        /// The name of the handle.
        /// 
        /// </summary>
        private string handleName;

        /// <summary>
        /// Gets or sets the args.
        /// 
        /// </summary>
        /// 
        /// <value>
        /// The args.
        /// 
        /// </value>
        public ClientPipelineArgs Args
        {
            get
            {
                return this.args;
            }
            set
            {
                Assert.ArgumentNotNull((object)value, "value");
                this.args = value;
            }
        }

        /// <summary>
        /// Gets or sets the  item id.
        /// 
        /// </summary>
        /// 
        /// <value>
        /// The item id.
        /// </value>
        public ID ItemID { private get; set; }

        /// <summary>
        /// Gets or sets the  custom template id.
        /// 
        /// </summary>
        /// 
        /// <value>
        /// The custom template id.
        /// </value>
        public ID CustomTemplateID { private get; set; }

        /// <summary>
        /// Gets or sets the  item language.
        /// 
        /// </summary>
        /// 
        /// <value>
        /// The item language.
        /// </value>
        public Language Language { private get; set; }

        /// <summary>
        /// Gets or sets the  item version.
        /// 
        /// </summary>
        /// 
        /// <value>
        /// The item version.
        /// </value>
        public Version Version { private get; set; }

        /// <summary>
        /// Gets or sets the name of the handle.
        /// 
        /// </summary>
        /// 
        /// <value>
        /// The name of the handle.
        /// 
        /// </value>
        public string HandleName
        {
            get
            {
                return this.handleName ?? "SC_DEVICEEDITOR";
            }
            set
            {
                Assert.ArgumentNotNull((object)value, "value");
                this.handleName = value;
            }
        }

        /// <summary>
        /// Shows this instance.
        /// 
        /// </summary>
        /// 
        /// <returns>
        /// The boolean.
        /// 
        /// </returns>
        public bool Show()
        {   
            if (!this.Args.IsPostBack)
            {
                //get the fields from the custom template
                RenderingParametersFieldEditorOptions fieldEditorOptions = new RenderingParametersFieldEditorOptions((IEnumerable<FieldDescriptor>)RenderCustomTemplate.GetFields(CustomTemplateID));
                fieldEditorOptions.DialogTitle = Consts.Strings.DialogTitle;
                fieldEditorOptions.HandleName = this.HandleName;
                fieldEditorOptions.PreserveSections = true;
                RenderingParametersFieldEditorOptions options = fieldEditorOptions;

                //create url based on options
                UrlString urlString = options.ToUrlString();

                //display UI
                SheerResponse.ShowModalDialog(urlString.ToString(), "720", "480", string.Empty, true);
                this.args.WaitForPostBack();
            }
            return false;
        }

        /// <summary>
        /// Gets the fields.
        /// 
        /// </summary>
        /// <param name="customTemplateID">The custom template id to be used for this UI.
        /// <returns>
        /// The fields.
        /// 
        /// </returns>
        public static List<FieldDescriptor> GetFields(ID customTemplateID)
        {
            List<FieldDescriptor> fieldDescriptors = new List<FieldDescriptor>();
            
            Item standardValuesItem;
            using (new SecurityDisabler())
            {
                //get the customTemplateID template
                TemplateItem templateItem = (TemplateItem)Client.ContentDatabase.GetItem(customTemplateID);
                if (templateItem == null)
                    return fieldDescriptors;
                else
                    standardValuesItem = templateItem.StandardValues ?? templateItem.CreateStandardValues(); //if item doesnt have any standard values, create them
            }
            //make sure we got the Item
            if (standardValuesItem == null)
                return fieldDescriptors;

            //get all fields
            FieldCollection fields = standardValuesItem.Fields;
            fields.ReadAll();
            fields.Sort();
            //for each field make sure they are not standard system fields and then add the to the List
            foreach (Field field in fields)
            {
                //weed out system fields
                if (!(field.Name == "Additional Parameters") && (!(field.Name == "Personalization") || UserOptions.View.ShowPersonalizationSection) && ((!(field.Name == "Tests") || UserOptions.View.ShowTestLabSection) && RenderingItem.IsAvalableNotBlobNotSystemField(field)))
                {
                    FieldDescriptor fieldDescriptor = new FieldDescriptor(standardValuesItem, field.Name)
                    {
                        Value = field.Value
                    };
                    fieldDescriptors.Add(fieldDescriptor);
                }
            }
            return fieldDescriptors;
        }
    }
}
