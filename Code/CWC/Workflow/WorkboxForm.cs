using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CWC.Search;
using CWC.Util;
using Sitecore;
using Sitecore.Buckets.Extensions;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Data.Templates;
using Sitecore.Diagnostics;
using Sitecore.Exceptions;
using Sitecore.Globalization;
using Sitecore.Links;
using Sitecore.Resources;
using Sitecore.SecurityModel;
using Sitecore.Shell.Data;
using Sitecore.Shell.Feeds;
using Sitecore.Shell.Framework;
using Sitecore.Shell.Framework.CommandBuilders;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.WebControls.Ribbons;
using Sitecore.Web.UI.XmlControls;
using Sitecore.Workflows;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.UI;
using Sitecore.Shell.Applications.WebEdit;
using Sitecore.Buckets.Managers;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.SearchTypes;
using System.Data.Linq;

namespace CWC.Workflow
{
    /// <summary>
    /// Displays the workbox.
    /// </summary>
    public class WorkboxForm : BaseForm
    {
        /// <summary>
        /// The pager.
        /// </summary>
        protected Border Pager;

        /// <summary>
        /// The ribbon panel.
        /// </summary>
        protected Border RibbonPanel;

        /// <summary>
        /// The states.
        /// </summary>
        protected Border States;

        /// <summary>
        /// The view menu.
        /// </summary>
        protected Toolmenubutton ViewMenu;

        /// <summary>
        /// The _state names.
        /// </summary>
        private NameValueCollection stateNames;

        /// <summary>
        /// Gets a value indicating whether page is reloads by reload button on the ribbon.
        /// </summary>
        /// <value><c>true</c> if this instance is reload; otherwise, <c>false</c>.</value>
        protected virtual bool IsReload
        {
            get
            {
                UrlString urlString = new UrlString(WebUtil.GetRawUrl());
                return urlString["reload"] == "1";
            }
        }

        /// <summary>
        /// Gets or sets the size of the page.
        /// </summary>
        /// <value>The size of the page.</value>
        public int PageSize
        {
            get
            {
                return Registry.GetInt("/Current_User/Workbox/Page Size", 10);
            }
            set
            {
                Registry.SetInt("/Current_User/Workbox/Page Size", value);
            }
        }

        public WorkboxForm()
        {
        }

        /// <summary>
        /// Comments the specified args.
        /// </summary>
        /// <param name="args">
        /// The arguments.
        /// </param>
        public void Comment(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (!args.IsPostBack)
            {
                bool showCustom = false;
                //get workflow item
                Item workflowItem = Context.ContentDatabase.GetItem(Context.ClientPage.ServerProperties["workflowid"] as string);

                Template customTemplate = null;
                //check if the custom template field has a value
                if (workflowItem != null && !string.IsNullOrEmpty(workflowItem[new ID(Consts.FieldIDs.CustomTemplate)]))
                {
                    //load custom template based on the field value
                    customTemplate = TemplateManager.GetTemplate(new ID(workflowItem[new ID(Consts.FieldIDs.CustomTemplate)].ToString()),
                        Context.ContentDatabase);

                    //check if it inherits Custom Workflow Comments Base template
                    if (customTemplate != null && customTemplate.InheritsFrom(new ID(Consts.TemplateIDs.CustomWorkflowCommentsBase)))
                            showCustom = true;
                }

                //decide if we show the standard comments dialog or custom dialog
                if (showCustom)
                {
                    if (!new RenderCustomTemplate() { Args = args, ItemID = new ID(Context.ClientPage.ServerProperties["id"].ToString()), CustomTemplateID = customTemplate.ID, Language = Language.Parse(Context.ClientPage.ServerProperties["language"] as string), Version = Sitecore.Data.Version.Parse(Context.ClientPage.ServerProperties["version"] as string) }.Show()) return;
                }
                else
                {
                    //display regular comments dialog
                    Context.ClientPage.ClientResponse.Input("Enter a comment:", string.Empty);
                    args.WaitForPostBack();
                }
                return;
            }
            if (args.Result.Length > 2000)
            {
                Context.ClientPage.ClientResponse.ShowError(new Exception(string.Format("The comment is too long.\n\nYou have entered {0} characters.\nA comment cannot contain more than 2000 characters.", args.Result.Length)));
                Context.ClientPage.ClientResponse.Input("Enter a comment:", string.Empty);
                args.WaitForPostBack();
                return;
            }
            if (args.Result != null && args.Result != "null" && args.Result != "undefined")
            {
                string language = Context.ClientPage.ServerProperties["language"].ToString();
                string version = Context.ClientPage.ServerProperties["version"].ToString();

                //check if the return is text comments or from a custom template
                IEnumerable<FieldDescriptor> resultingFields = (IEnumerable<FieldDescriptor>)RenderingParametersFieldEditorOptions.Parse(args.Result).Fields;

                string comments = "";
                comments = args.Result;

                //if the result is from a custom template
                if (resultingFields.Count() > 0)
                {
                    //get the item bucket
                    var bucketItem = Context.ContentDatabase.GetItem(Consts.ItemIDs.WorkflowCommentsItemBucket);
                    if (bucketItem != null && BucketManager.IsBucket(bucketItem))
                    {
                        //get workflow item
                        Item workflowItem =
                            Context.ContentDatabase.GetItem(Context.ClientPage.ServerProperties["workflowid"] as string);

                        //check if the custom template field has a value
                        if (workflowItem != null && !string.IsNullOrEmpty(workflowItem[new ID(Consts.FieldIDs.CustomTemplate)]))
                        {
                            // locate item in bucket by name
                            using (
                                var searchContext =
                                    ContentSearchManager.GetIndex(bucketItem as IIndexable).CreateSearchContext())
                            {
                                //ShortID.Encode(item.ID).ToLowerInvariant())
                                string searchCustomWorkflowCommentsItemTemplate = ShortID.Encode(Consts.TemplateIDs.CustomWorkflowCommentsItem).ToLowerInvariant(); 
                                string searchWorkflowItemID = ShortID.Encode(Context.ClientPage.ServerProperties["id"].ToString()).ToLowerInvariant();
                                var result =
                                    searchContext.GetQueryable<CustomWorkflowCommentsItem>()
                                        .Where(
                                            x =>
                                                x.Template.Equals(searchCustomWorkflowCommentsItemTemplate) && x.WorkflowItemID == searchWorkflowItemID
                                                && x.Language == language
                                                && x.Version == version) 
                                        .FirstOrDefault();

                                //generate valid unique name
                                string validItemName = ItemUtil.ProposeValidItemName(DateUtil.IsoNow.ToString());
                                
                                Item parent = null;
                                //based on the result, assign parent as the item found or the item bucket
                                if (result != null)
                                {
                                    parent = Context.ContentDatabase.GetItem(result.ID);
                                }

                                //load templates for Custom template and the Custom Workflow Comments Item
                                TemplateItem customTemplate = Context.ContentDatabase.GetTemplate(new ID(workflowItem[new ID(Consts.FieldIDs.CustomTemplate)].ToString()));
                                TemplateItem customWorkflowCommentsItem = Context.ContentDatabase.GetTemplate(new ID(Consts.TemplateIDs.CustomWorkflowCommentsItem));
                                using (new SecurityDisabler())
                                {
                                    if (parent == null)
                                    {
                                        //create an item based on Custom Workflow Comments Item and store the workflow item values
                                        Item newItem = bucketItem.Add(validItemName, customWorkflowCommentsItem);
                                        newItem.Editing.BeginEdit();
                                        newItem.Fields[new ID(Consts.FieldIDs.CustomWorkflowCommentsItem.WorkflowItemID)].Value =
                                            Context.ClientPage.ServerProperties["id"].ToString(); //workflow item id
                                        newItem.Fields[new ID(Consts.FieldIDs.CustomWorkflowCommentsItem.Language)].Value =
                                            language; //language
                                        newItem.Fields[new ID(Consts.FieldIDs.CustomWorkflowCommentsItem.Version)].Value =
                                            version; //version
                                        newItem.Editing.AcceptChanges();

                                       parent = newItem;
                                    }


                                    //generate another valid unique name
                                    validItemName = ItemUtil.ProposeValidItemName(DateUtil.IsoNow.ToString());

                                    //create an item based on the custom template you chose under the Custom Workflow Comments Item
                                    Item customItem = parent.Add(validItemName, customTemplate);
                                    customItem.Editing.BeginEdit();
                                    foreach (FieldDescriptor fieldDescriptor in resultingFields)
                                        customItem.Fields[fieldDescriptor.FieldID].Value = fieldDescriptor.Value;
                                    customItem.Editing.AcceptChanges();

                                    //set comments to the guid of the newly created item
                                    comments = customItem.ID.ToString();
                                }
                            }

                        }
                    }
                }

                IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
                if (workflowProvider != null)
                {
                    IWorkflow workflow = workflowProvider.GetWorkflow(Context.ClientPage.ServerProperties["workflowid"] as string);
                    if (workflow != null)
                    {
                        ItemRecords items = Context.ContentDatabase.Items;
                        object item = Context.ClientPage.ServerProperties["id"];
                        object empty = item;
                        if (item == null)
                        {
                            empty = string.Empty;
                        }
                        Item item1 = items[empty.ToString(), Language.Parse(Context.ClientPage.ServerProperties["language"] as string), Sitecore.Data.Version.Parse(Context.ClientPage.ServerProperties["version"] as string)];
                        if (item1 != null)
                        {
                            try
                            {
                                workflow.Execute(Context.ClientPage.ServerProperties["command"] as string, item1, comments, true, new object[0]);
                                //workflow.Execute(Context.ClientPage.ServerProperties["command"] as string, item1, args.Result, true, new object[0]);
                            }
                            catch (WorkflowStateMissingException workflowStateMissingException)
                            {
                                SheerResponse.Alert("One or more items could not be processed because their workflow state does not specify the next step.", new string[0]);
                            }
                            UrlString urlString = new UrlString(WebUtil.GetRawUrl());
                            urlString["reload"] = "1";
                            Context.ClientPage.ClientResponse.SetLocation(urlString.ToString());
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates the command.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <param name="command">
        /// The command.
        /// </param>
        /// <param name="item">
        /// The item.
        /// </param>
        /// <param name="workboxItem">
        /// The workbox item.
        /// </param>
        private void CreateCommand(IWorkflow workflow, WorkflowCommand command, Item item, XmlControl workboxItem)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(command, "command");
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(workboxItem, "workboxItem");
            XmlControl webControl = Resource.GetWebControl("WorkboxCommand") as XmlControl;
            Assert.IsNotNull(webControl, "workboxCommand is null");
            webControl["Header"] = command.DisplayName;
            webControl["Icon"] = command.Icon;
            CommandBuilder commandBuilder = new CommandBuilder("workflow:send");
            commandBuilder.Add("id", item.ID.ToString());
            commandBuilder.Add("la", item.Language.Name);
            commandBuilder.Add("vs", item.Version.ToString());
            commandBuilder.Add("command", command.CommandID);
            commandBuilder.Add("wf", workflow.WorkflowID);
            commandBuilder.Add("ui", command.HasUI);
            commandBuilder.Add("suppresscomment", command.SuppressComment);
            webControl["Command"] = commandBuilder.ToString();
            workboxItem.AddControl(webControl);
        }

        /// <summary>
        /// Creates the item.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <param name="item">
        /// The item.
        /// </param>
        /// <param name="control">
        /// The control.
        /// </param>
        private void CreateItem(IWorkflow workflow, Item item, System.Web.UI.Control control)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(control, "control");
            XmlControl webControl = Resource.GetWebControl("WorkboxItem") as XmlControl;
            Assert.IsNotNull(webControl, "workboxItem is null");
            control.Controls.Add(webControl);
            StringBuilder stringBuilder = new StringBuilder(" - (");
            Language language = item.Language;
            stringBuilder.Append(language.CultureInfo.DisplayName);
            stringBuilder.Append(", ");
            stringBuilder.Append(Translate.Text("version"));
            stringBuilder.Append(' ');
            stringBuilder.Append(item.Version.ToString());
            stringBuilder.Append(")");
            Assert.IsNotNull(webControl, "workboxItem");
            webControl["Header"] = item.DisplayName;
            webControl["Details"] = stringBuilder.ToString();
            webControl["Icon"] = item.Appearance.Icon;
            webControl["ShortDescription"] = item.Help.ToolTip;
            webControl["History"] = this.GetHistory(workflow, item);
            webControl["HistoryMoreID"] = Sitecore.Web.UI.HtmlControls.Control.GetUniqueID("ctl");
            object[] d = new object[] { "workflow:showhistory(id=", item.ID, ",la=", item.Language.Name, ",vs=", item.Version, ",wf=", workflow.WorkflowID, ")" };
            webControl["HistoryClick"] = string.Concat(d);
            object[] objArray = new object[] { "Preview(\"", item.ID, "\", \"", item.Language, "\", \"", item.Version, "\")" };
            webControl["PreviewClick"] = string.Concat(objArray);
            object[] d1 = new object[] { "Open(\"", item.ID, "\", \"", item.Language, "\", \"", item.Version, "\")" };
            webControl["Click"] = string.Concat(d1);
            object[] objArray1 = new object[] { "Diff(\"", item.ID, "\", \"", item.Language, "\", \"", item.Version, "\")" };
            webControl["DiffClick"] = string.Concat(objArray1);
            webControl["Display"] = "none";
            string uniqueID = Sitecore.Web.UI.HtmlControls.Control.GetUniqueID(string.Empty);
            webControl["CheckID"] = string.Concat("check_", uniqueID);
            webControl["HiddenID"] = string.Concat("hidden_", uniqueID);
            object[] d2 = new object[] { item.ID, ",", item.Language, ",", item.Version };
            webControl["CheckValue"] = string.Concat(d2);
            WorkflowCommand[] workflowCommandArray = WorkflowFilterer.FilterVisibleCommands(workflow.GetCommands(item));
            for (int i = 0; i < (int)workflowCommandArray.Length; i++)
            {
                WorkflowCommand workflowCommand = workflowCommandArray[i];
                this.CreateCommand(workflow, workflowCommand, item, webControl);
            }
        }

        /// <summary>
        /// Creates the navigator.
        /// </summary>
        /// <param name="section">
        /// The section.
        /// </param>
        /// <param name="id">
        /// The id.
        /// </param>
        /// <param name="count">
        /// The count.
        /// </param>
        private void CreateNavigator(Section section, string id, int count)
        {
            Assert.ArgumentNotNull(section, "section");
            Assert.ArgumentNotNull(id, "id");
            Navigator navigator = new Navigator();
            section.Controls.Add(navigator);
            navigator.ID = id;
            navigator.Offset = 0;
            navigator.Count = count;
            navigator.PageSize = this.PageSize;
        }

        /// <summary>
        /// Diffs the specified id.
        /// </summary>
        /// <param name="id">
        /// The id.
        /// </param>
        /// <param name="language">
        /// The language.
        /// </param>
        /// <param name="version">
        /// The version.
        /// </param>
        protected void Diff(string id, string language, string version)
        {
            Assert.ArgumentNotNull(id, "id");
            Assert.ArgumentNotNull(language, "language");
            Assert.ArgumentNotNull(version, "version");
            UrlString urlString = new UrlString(UIUtil.GetUri("control:Diff"));
            urlString.Append("id", id);
            urlString.Append("la", language);
            urlString.Append("vs", version);
            urlString.Append("wb", "1");
            Context.ClientPage.ClientResponse.ShowModalDialog(urlString.ToString());
        }

        /// <summary>
        /// Displays the state.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <param name="state">
        /// The state.
        /// </param>
        /// <param name="items">
        /// The items.
        /// </param>
        /// <param name="control">
        /// The control.
        /// </param>
        /// <param name="offset">
        /// The offset.
        /// </param>
        /// <param name="pageSize">
        /// Size of the page.
        /// </param>
        protected virtual void DisplayState(IWorkflow workflow, WorkflowState state, DataUri[] items, System.Web.UI.Control control, int offset, int pageSize)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(state, "state");
            Assert.ArgumentNotNull(items, "items");
            Assert.ArgumentNotNull(control, "control");
            if ((int)items.Length > 0)
            {
                int length = offset + pageSize;
                if (length > (int)items.Length)
                {
                    length = (int)items.Length;
                }
                for (int i = offset; i < length; i++)
                {
                    DataUri dataUri = items[i];
                    Item item = Context.ContentDatabase.Items[dataUri];
                    if (item != null)
                    {
                        this.CreateItem(workflow, item, control);
                    }
                }
                Border border = new Border();
                border.Background = "#e9e9e9";
                Border border1 = border;
                control.Controls.Add(border1);
                border1.Margin = "0px 4px 0px 16px";
                border1.Padding = "2px 8px 2px 8px";
                border1.Border = "1px solid #999999";
                WorkflowCommand[] workflowCommandArray = WorkflowFilterer.FilterVisibleCommands(workflow.GetCommands(state.StateID));
                for (int j = 0; j < (int)workflowCommandArray.Length; j++)
                {
                    WorkflowCommand workflowCommand = workflowCommandArray[j];
                    XmlControl webControl = Resource.GetWebControl("WorkboxCommand") as XmlControl;
                    Assert.IsNotNull(webControl, "workboxCommand is null");
                    webControl["Header"] = string.Concat(workflowCommand.DisplayName, " ", Translate.Text("(selected)"));
                    webControl["Icon"] = workflowCommand.Icon;
                    string[] commandID = new string[] { "workflow:sendselected(command=", workflowCommand.CommandID, ",ws=", state.StateID, ",wf=", workflow.WorkflowID, ")" };
                    webControl["Command"] = string.Concat(commandID);
                    border1.Controls.Add(webControl);
                    webControl = Resource.GetWebControl("WorkboxCommand") as XmlControl;
                    Assert.IsNotNull(webControl, "workboxCommand is null");
                    webControl["Header"] = string.Concat(workflowCommand.DisplayName, " ", Translate.Text("(all)"));
                    webControl["Icon"] = workflowCommand.Icon;
                    string[] strArrays = new string[] { "workflow:sendall(command=", workflowCommand.CommandID, ",ws=", state.StateID, ",wf=", workflow.WorkflowID, ")" };
                    webControl["Command"] = string.Concat(strArrays);
                    border1.Controls.Add(webControl);
                }
            }
        }

        /// <summary>
        /// Displays the states.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <param name="placeholder">
        /// The placeholder.
        /// </param>
        protected virtual void DisplayStates(IWorkflow workflow, XmlControl placeholder)
        {
            string str;
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(placeholder, "placeholder");
            this.stateNames = null;
            WorkflowState[] states = workflow.GetStates();
            for (int i = 0; i < (int)states.Length; i++)
            {
                WorkflowState workflowState = states[i];
                WorkflowCommand[] workflowCommandArray = WorkflowFilterer.FilterVisibleCommands(workflow.GetCommands(workflowState.StateID));
                if ((int)workflowCommandArray.Length > 0)
                {
                    DataUri[] items = this.GetItems(workflowState, workflow);
                    Assert.IsNotNull(items, "items is null");
                    string str1 = string.Concat(ShortID.Encode(workflow.WorkflowID), "_", ShortID.Encode(workflowState.StateID));
                    Section section = new Section();
                    section.ID = string.Concat(str1, "_section");
                    Section icon = section;
                    placeholder.AddControl(icon);
                    int length = (int)items.Length;
                    if (length > 0)
                    {
                        str = (length != 1 ? string.Format("{0} {1}", length, Translate.Text("items")) : string.Format("1 {0}", Translate.Text("item")));
                    }
                    else
                    {
                        str = "none";
                    }
                    str = string.Format("<span style=\"font-weight:normal\"> - ({0})</span>", str);
                    icon.Header = string.Concat(workflowState.DisplayName, str);
                    icon.Icon = workflowState.Icon;
                    if (Settings.ClientFeeds.Enabled)
                    {
                        FeedUrlOptions feedUrlOption = new FeedUrlOptions("/sitecore/shell/~/feed/workflowstate.aspx");
                        feedUrlOption.UseUrlAuthentication = true;
                        FeedUrlOptions workflowID = feedUrlOption;
                        workflowID.Parameters["wf"] = workflow.WorkflowID;
                        workflowID.Parameters["st"] = workflowState.StateID;
                        icon.FeedLink = workflowID.ToString();
                    }
                    icon.Collapsed = length <= 0;
                    Border border = new Border();
                    icon.Controls.Add(border);
                    border.ID = string.Concat(str1, "_content");
                    this.DisplayState(workflow, workflowState, items, border, 0, this.PageSize);
                    this.CreateNavigator(icon, string.Concat(str1, "_navigator"), length);
                }
            }
        }

        /// <summary>
        /// Displays the workflow.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        protected virtual void DisplayWorkflow(IWorkflow workflow)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Context.ClientPage.ServerProperties["WorkflowID"] = workflow.WorkflowID;
            XmlControl webControl = Resource.GetWebControl("Pane") as XmlControl;
            Error.AssertXmlControl(webControl, "Pane");
            this.States.Controls.Add(webControl);
            Assert.IsNotNull(webControl, "pane");
            webControl["PaneID"] = this.GetPaneID(workflow);
            webControl["Header"] = workflow.Appearance.DisplayName;
            webControl["Icon"] = workflow.Appearance.Icon;
            FeedUrlOptions feedUrlOption = new FeedUrlOptions("/sitecore/shell/~/feed/workflow.aspx");
            feedUrlOption.UseUrlAuthentication = true;
            FeedUrlOptions workflowID = feedUrlOption;
            workflowID.Parameters["wf"] = workflow.WorkflowID;
            webControl["FeedLink"] = workflowID.ToString();
            this.DisplayStates(workflow, webControl);
            if (Context.ClientPage.IsEvent)
            {
                SheerResponse.Insert(this.States.ClientID, "append", HtmlUtil.RenderControl(webControl));
            }
        }

        /// <summary>
        /// Gets the history.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <param name="item">
        /// The item.
        /// </param>
        /// <returns>
        /// The get history.
        /// </returns>
        private string GetHistory(IWorkflow workflow, Item item)
        {
            string str;
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(item, "item");
            WorkflowEvent[] history = workflow.GetHistory(item);
            if ((int)history.Length <= 0)
            {
                str = Translate.Text("No changes have been made.");
            }
            else
            {
                WorkflowEvent workflowEvent = history[(int)history.Length - 1];
                string user = workflowEvent.User;
                string name = Context.Domain.Name;
                if (user.StartsWith(string.Concat(name, "\\"), StringComparison.OrdinalIgnoreCase))
                {
                    user = StringUtil.Mid(user, name.Length + 1);
                }
                string[] strArrays = new string[] { user, Translate.Text("Unknown") };
                user = StringUtil.GetString(strArrays);
                string stateName = this.GetStateName(workflow, workflowEvent.OldState);
                string stateName1 = this.GetStateName(workflow, workflowEvent.NewState);
                object[] objArray = new object[] { user, stateName, stateName1, DateUtil.FormatDateTime(workflowEvent.Date, "D", Context.User.Profile.Culture) };
                str = string.Format(Translate.Text("{0} changed from <b>{1}</b> to <b>{2}</b> on {3}."), objArray);
            }
            return str;
        }

        /// <summary>
        /// Gets the items.
        /// </summary>
        /// <param name="state">
        /// The state.
        /// </param>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <returns>
        /// Array of item DataUri.
        /// </returns>
        private DataUri[] GetItems(WorkflowState state, IWorkflow workflow)
        {
            Assert.ArgumentNotNull(state, "state");
            Assert.ArgumentNotNull(workflow, "workflow");
            ArrayList arrayLists = new ArrayList();
            DataUri[] items = workflow.GetItems(state.StateID);
            if (items != null)
            {
                DataUri[] dataUriArray = items;
                for (int i = 0; i < (int)dataUriArray.Length; i++)
                {
                    DataUri dataUri = dataUriArray[i];
                    Item item = Context.ContentDatabase.Items[dataUri];
                    if (item != null && item.Access.CanRead() && item.Access.CanReadLanguage() && item.Access.CanWriteLanguage() && (Context.IsAdministrator || item.Locking.CanLock() || item.Locking.HasLock()))
                    {
                        arrayLists.Add(dataUri);
                    }
                }
            }
            return arrayLists.ToArray(typeof(DataUri)) as DataUri[];
        }

        /// <summary>
        /// Gets the pane ID.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <returns>
        /// The get pane id.
        /// </returns>
        private string GetPaneID(IWorkflow workflow)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            return string.Concat("P", Regex.Replace(workflow.WorkflowID, "\\W", string.Empty));
        }

        /// <summary>
        /// Gets the name of the state.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <param name="stateID">
        /// The state ID.
        /// </param>
        /// <returns>
        /// The get state name.
        /// </returns>
        private string GetStateName(IWorkflow workflow, string stateID)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(stateID, "stateID");
            if (this.stateNames == null)
            {
                this.stateNames = new NameValueCollection();
                WorkflowState[] states = workflow.GetStates();
                for (int i = 0; i < (int)states.Length; i++)
                {
                    WorkflowState workflowState = states[i];
                    this.stateNames.Add(workflowState.StateID, workflowState.DisplayName);
                }
            }
            string[] item = new string[] { this.stateNames[stateID], "?" };
            return StringUtil.GetString(item);
        }

        /// <summary>
        /// Handles the message.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        public override void HandleMessage(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            string name = message.Name;
            string str = name;
            if (name != null)
            {
                switch (str)
                {
                    case "workflow:send":
                    {
                        this.Send(message);
                        return;
                    }
                    case "workflow:sendselected":
                    {
                        this.SendSelected(message);
                        return;
                    }
                    case "workflow:sendall":
                    {
                        this.SendAll(message);
                        return;
                    }
                    case "window:close":
                    {
                        Windows.Close();
                        return;
                    }
                    case "workflow:showhistory":
                    {
                        WorkboxForm.ShowHistory(message, Context.ClientPage.ClientRequest.Control);
                        return;
                    }
                    case "workbox:hide":
                    {
                        Context.ClientPage.SendMessage(this, string.Concat("pane:hide(id=", message["id"], ")"));
                        Context.ClientPage.ClientResponse.SetAttribute(string.Concat("Check_Check_", message["id"]), "checked", "false");
                        break;
                    }
                    case "pane:hidden":
                    {
                        Context.ClientPage.ClientResponse.SetAttribute(string.Concat("Check_Check_", message["paneid"]), "checked", "false");
                        break;
                    }
                    case "workbox:show":
                    {
                        Context.ClientPage.SendMessage(this, string.Concat("pane:show(id=", message["id"], ")"));
                        Context.ClientPage.ClientResponse.SetAttribute(string.Concat("Check_Check_", message["id"]), "checked", "true");
                        break;
                    }
                    case "pane:showed":
                    {
                        Context.ClientPage.ClientResponse.SetAttribute(string.Concat("Check_Check_", message["paneid"]), "checked", "true");
                        break;
                    }
                }
            }
            base.HandleMessage(message);
            string item = message["id"];
            if (!string.IsNullOrEmpty(item))
            {
                string[] strArrays = new string[] { message["language"] };
                string str1 = StringUtil.GetString(strArrays);
                string[] item1 = new string[] { message["version"] };
                string str2 = StringUtil.GetString(item1);
                Item item2 = Context.ContentDatabase.Items[item, Language.Parse(str1), Sitecore.Data.Version.Parse(str2)];
                if (item2 != null)
                {
                    Dispatcher.Dispatch(message, item2);
                }
            }
        }

        /// <summary>
        /// Jumps the specified sender.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="offset">
        /// The offset.
        /// </param>
        private void Jump(object sender, Message message, int offset)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(message, "message");
            string control = Context.ClientPage.ClientRequest.Control;
            string str = ShortID.Decode(control.Substring(0, 32));
            string str1 = ShortID.Decode(control.Substring(33, 32));
            control = control.Substring(0, 65);
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            Assert.IsNotNull(workflowProvider, string.Concat("Workflow provider for database \"", Context.ContentDatabase.Name, "\" not found."));
            IWorkflow workflow = workflowProvider.GetWorkflow(str);
            Error.Assert(workflow != null, string.Concat("Workflow \"", str, "\" not found."));
            Assert.IsNotNull(workflow, "workflow");
            WorkflowState state = workflow.GetState(str1);
            Assert.IsNotNull(state, string.Concat("Workflow state \"", str1, "\" not found."));
            Border border = new Border();
            border.ID = string.Concat(control, "_content");
            Border border1 = border;
            DataUri[] items = this.GetItems(state, workflow);
            WorkboxForm workboxForm = this;
            IWorkflow workflow1 = workflow;
            WorkflowState workflowState = state;
            DataUri[] dataUriArray = items;
            DataUri[] dataUriArray1 = dataUriArray;
            if (dataUriArray == null)
            {
                dataUriArray1 = new DataUri[0];
            }
            workboxForm.DisplayState(workflow1, workflowState, dataUriArray1, border1, offset, this.PageSize);
            Context.ClientPage.ClientResponse.SetOuterHtml(string.Concat(control, "_content"), border1);
        }

        /// <summary>
        /// Raises the load event.
        /// </summary>
        /// <param name="e">
        /// The <see cref="T:System.EventArgs" /> instance containing the event data.
        /// </param>
        /// <remarks>
        /// This method notifies the server control that it should perform actions common to each HTTP
        /// request for the page it is associated with, such as setting up a database query. At this
        /// stage in the page lifecycle, server controls in the hierarchy are created and initialized,
        /// view state is restored, and form controls reflect client-side data. Use the IsPostBack
        /// property to determine whether the page is being loaded in response to a client postback,
        /// or if it is being loaded and accessed for the first time.
        /// </remarks>
        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            base.OnLoad(e);
            if (!Context.ClientPage.IsEvent)
            {
                IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
                if (workflowProvider != null)
                {
                    IWorkflow[] workflows = workflowProvider.GetWorkflows();
                    IWorkflow[] workflowArray = workflows;
                    for (int i = 0; i < (int)workflowArray.Length; i++)
                    {
                        IWorkflow workflow = workflowArray[i];
                        string str = string.Concat("P", Regex.Replace(workflow.WorkflowID, "\\W", string.Empty));
                        if (!this.IsReload && (int)workflows.Length == 1 && string.IsNullOrEmpty(Registry.GetString(string.Concat("/Current_User/Panes/", str))))
                        {
                            Registry.SetString(string.Concat("/Current_User/Panes/", str), "visible");
                        }
                        string str1 = Registry.GetString(string.Concat("/Current_User/Panes/", str));
                        string empty = str1;
                        if (str1 == null)
                        {
                            empty = string.Empty;
                        }
                        if (empty == "visible")
                        {
                            this.DisplayWorkflow(workflow);
                        }
                    }
                }
                this.UpdateRibbon();
            }
            this.WireUpNavigators(Context.ClientPage);
        }

        /// <summary>
        /// Called when the view menu is clicked.
        /// </summary>
        protected void OnViewMenuClick()
        {
            string str;
            Menu menu = new Menu();
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                IWorkflow[] workflows = workflowProvider.GetWorkflows();
                for (int i = 0; i < (int)workflows.Length; i++)
                {
                    IWorkflow workflow = workflows[i];
                    string paneID = this.GetPaneID(workflow);
                    string str1 = Registry.GetString(string.Concat("/Current_User/Panes/", paneID));
                    str = (str1 != "hidden" ? "workbox:hide" : "workbox:show");
                    string str2 = str;
                    menu.Add(Sitecore.Web.UI.HtmlControls.Control.GetUniqueID("ctl"), workflow.Appearance.DisplayName, workflow.Appearance.Icon, string.Empty, string.Concat(str2, "(id=", paneID, ")"), str1 != "hidden", string.Empty, MenuItemType.Check);
                }
                if (menu.Controls.Count > 0)
                {
                    menu.AddDivider();
                }
                menu.Add("Refresh", "Applications/16x16/refresh.png", "Refresh");
            }
            Context.ClientPage.ClientResponse.ShowPopup("ViewMenu", "below", menu);
        }

        /// <summary>
        /// Opens the specified item.
        /// </summary>
        /// <param name="id">
        /// The id.
        /// </param>
        /// <param name="language">
        /// The language.
        /// </param>
        /// <param name="version">
        /// The version.
        /// </param>
        protected void Open(string id, string language, string version)
        {
            Assert.ArgumentNotNull(id, "id");
            Assert.ArgumentNotNull(language, "language");
            Assert.ArgumentNotNull(version, "version");
            string sectionID = RootSections.GetSectionID(id);
            UrlString urlString = new UrlString();
            urlString.Append("ro", sectionID);
            urlString.Append("fo", id);
            urlString.Append("id", id);
            urlString.Append("la", language);
            urlString.Append("vs", version);
            Windows.RunApplication("Content editor", urlString.ToString());
        }

        /// <summary>
        /// Called with the pages size changes.
        /// </summary>
        protected void PageSize_Change()
        {
            string item = Context.ClientPage.ClientRequest.Form["PageSize"];
            int num = MainUtil.GetInt(item, 10);
            this.PageSize = num;
            this.Refresh();
        }

        /// <summary>
        /// Toggles the pane.
        /// </summary>
        /// <param name="id">
        /// The id.
        /// </param>
        protected void Pane_Toggle(string id)
        {
            Assert.ArgumentNotNull(id, "id");
            string str = string.Concat("P", Regex.Replace(id, "\\W", string.Empty));
            string str1 = Registry.GetString(string.Concat("/Current_User/Panes/", str));
            if (Context.ClientPage.FindControl(str) == null)
            {
                IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
                if (workflowProvider == null)
                {
                    return;
                }
                IWorkflow workflow = workflowProvider.GetWorkflow(id);
                this.DisplayWorkflow(workflow);
            }
            if (string.IsNullOrEmpty(str1) || str1 == "hidden")
            {
                Registry.SetString(string.Concat("/Current_User/Panes/", str), "visible");
                Context.ClientPage.ClientResponse.SetStyle(str, "display", string.Empty);
            }
            else
            {
                Registry.SetString(string.Concat("/Current_User/Panes/", str), "hidden");
                Context.ClientPage.ClientResponse.SetStyle(str, "display", "none");
            }
            SheerResponse.SetReturnValue(true);
        }

        /// <summary>
        /// Previews the specified item.
        /// </summary>
        /// <param name="id">
        /// The id.
        /// </param>
        /// <param name="language">
        /// The language.
        /// </param>
        /// <param name="version">
        /// The version.
        /// </param>
        protected void Preview(string id, string language, string version)
        {
            Assert.ArgumentNotNull(id, "id");
            Assert.ArgumentNotNull(language, "language");
            Assert.ArgumentNotNull(version, "version");
            string[] strArrays = new string[] { "item:preview(id=", id, ",language=", language, ",version=", version, ")" };
            Context.ClientPage.SendMessage(this, string.Concat(strArrays));
        }

        /// <summary>
        /// Refreshes the page.
        /// </summary>
        protected void Refresh()
        {
            UrlString urlString = new UrlString(WebUtil.GetRawUrl());
            urlString["reload"] = "1";
            Context.ClientPage.ClientResponse.SetLocation(urlString.ToString());
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        private void Send(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                string item = message["wf"];
                IWorkflow workflow = workflowProvider.GetWorkflow(item);
                if (workflow != null)
                {
                    Item item1 = Context.ContentDatabase.Items[message["id"], Language.Parse(message["la"]), Sitecore.Data.Version.Parse(message["vs"])];
                    if (item1 != null)
                    {
                        if (message["ui"] != "1" && message["suppresscomment"] != "1")
                        {
                            Context.ClientPage.ServerProperties["id"] = message["id"];
                            Context.ClientPage.ServerProperties["language"] = message["la"];
                            Context.ClientPage.ServerProperties["version"] = message["vs"];
                            Context.ClientPage.ServerProperties["command"] = message["command"];
                            Context.ClientPage.ServerProperties["workflowid"] = item;
                            Context.ClientPage.Start(this, "Comment");
                            return;
                        }
                        try
                        {
                            workflow.Execute(message["command"], item1, string.Empty, true, new object[0]);
                        }
                        catch (WorkflowStateMissingException workflowStateMissingException)
                        {
                            SheerResponse.Alert("One or more items could not be processed because their workflow state does not specify the next step.", new string[0]);
                        }
                        UrlString urlString = new UrlString(WebUtil.GetRawUrl());
                        urlString["reload"] = "1";
                        Context.ClientPage.ClientResponse.SetLocation(urlString.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Sends all.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        private void SendAll(Message message)
        {
            string str;
            Assert.ArgumentNotNull(message, "message");
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                string item = message["wf"];
                string item1 = message["ws"];
                IWorkflow workflow = workflowProvider.GetWorkflow(item);
                if (workflow != null)
                {
                    WorkflowState state = workflow.GetState(item1);
                    DataUri[] items = this.GetItems(state, workflow);
                    Assert.IsNotNull(items, "uris is null");
                    str = (state != null ? state.DisplayName : string.Empty);
                    string str1 = str;
                    bool flag = false;
                    DataUri[] dataUriArray = items;
                    for (int i = 0; i < (int)dataUriArray.Length; i++)
                    {
                        DataUri dataUri = dataUriArray[i];
                        Item item2 = Context.ContentDatabase.Items[dataUri];
                        if (item2 != null)
                        {
                            try
                            {
                                workflow.Execute(message["command"], item2, str1, true, new object[0]);
                            }
                            catch (WorkflowStateMissingException workflowStateMissingException)
                            {
                                flag = true;
                            }
                        }
                    }
                    if (flag)
                    {
                        SheerResponse.Alert("One or more items could not be processed because their workflow state does not specify the next step.", new string[0]);
                    }
                    UrlString urlString = new UrlString(WebUtil.GetRawUrl());
                    urlString["reload"] = "1";
                    Context.ClientPage.ClientResponse.SetLocation(urlString.ToString());
                }
            }
        }

        /// <summary>
        /// Sends the selected.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        private void SendSelected(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                string item = message["wf"];
                string str = message["ws"];
                IWorkflow workflow = workflowProvider.GetWorkflow(item);
                if (workflow != null)
                {
                    int num = 0;
                    bool flag = false;
                    foreach (string key in Context.ClientPage.ClientRequest.Form.Keys)
                    {
                        if (key == null || !key.StartsWith("check_", StringComparison.InvariantCulture))
                        {
                            continue;
                        }
                        string str1 = string.Concat("hidden_", key.Substring(6));
                        string item1 = Context.ClientPage.ClientRequest.Form[str1];
                        char[] chrArray = new char[] { ',' };
                        string[] strArrays = item1.Split(chrArray);
                        Item item2 = Context.ContentDatabase.Items[strArrays[0], Language.Parse(strArrays[1]), Sitecore.Data.Version.Parse(strArrays[2])];
                        if (item2 == null)
                        {
                            continue;
                        }
                        WorkflowState state = workflow.GetState(item2);
                        if (state.StateID != str)
                        {
                            continue;
                        }
                        try
                        {
                            workflow.Execute(message["command"], item2, state.DisplayName, true, new object[0]);
                        }
                        catch (WorkflowStateMissingException workflowStateMissingException)
                        {
                            flag = true;
                        }
                        num++;
                    }
                    if (flag)
                    {
                        SheerResponse.Alert("One or more items could not be processed because their workflow state does not specify the next step.", new string[0]);
                    }
                    if (num == 0)
                    {
                        Context.ClientPage.ClientResponse.Alert("There are no selected items.");
                        return;
                    }
                    UrlString urlString = new UrlString(WebUtil.GetRawUrl());
                    urlString["reload"] = "1";
                    Context.ClientPage.ClientResponse.SetLocation(urlString.ToString());
                }
            }
        }

        /// <summary>
        /// Shows the history.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="control">
        /// The control.
        /// </param>
        private static void ShowHistory(Message message, string control)
        {
            Assert.ArgumentNotNull(message, "message");
            Assert.ArgumentNotNull(control, "control");
            XmlControl webControl = Resource.GetWebControl("WorkboxHistory") as XmlControl;
            Assert.IsNotNull(webControl, "history is null");
            webControl["ItemID"] = message["id"];
            webControl["Language"] = message["la"];
            webControl["Version"] = message["vs"];
            webControl["WorkflowID"] = message["wf"];
            Context.ClientPage.ClientResponse.ShowPopup(control, "below", webControl);
        }

        /// <summary>
        /// Updates the ribbon.
        /// </summary>
        private void UpdateRibbon()
        {
            Ribbon ribbon = new Ribbon();
            ribbon.ID = "WorkboxRibbon";
            ribbon.CommandContext = new CommandContext();
            Ribbon uri = ribbon;
            Item item = Context.Database.GetItem("/sitecore/content/Applications/Workbox/Ribbon");
            Error.AssertItemFound(item, "/sitecore/content/Applications/Workbox/Ribbon");
            uri.CommandContext.RibbonSourceUri = item.Uri;
            uri.CommandContext.CustomData = this.IsReload;
            this.RibbonPanel.Controls.Add(uri);
        }

        /// <summary>
        /// Wires the up navigators.
        /// </summary>
        /// <param name="control">
        /// The control.
        /// </param>
        private void WireUpNavigators(System.Web.UI.Control control)
        {
            foreach (System.Web.UI.Control control1 in control.Controls)
            {
                Navigator navigator = control1 as Navigator;
                if (navigator != null)
                {
                    navigator.Jump += new Navigator.NavigatorDelegate(this.Jump);
                    navigator.Previous += new Navigator.NavigatorDelegate(this.Jump);
                    navigator.Next += new Navigator.NavigatorDelegate(this.Jump);
                }
                this.WireUpNavigators(control1);
            }
        }
    }
}

