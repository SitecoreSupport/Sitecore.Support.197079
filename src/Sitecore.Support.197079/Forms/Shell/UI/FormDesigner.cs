using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Web;
using Sitecore.Collections;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Form.Core.Configuration;
using Sitecore.Form.Core.ContentEditor.Data;
using Sitecore.Form.Core.Data;
using Sitecore.Form.Core.Utility;
using Sitecore.Form.Core.Visual;
using Sitecore.Forms.Core.Data;
using Sitecore.Forms.Shell.UI.Controls;
using Sitecore.Forms.Shell.UI.Dialogs;
using Sitecore.Globalization;
using Sitecore.Shell.Controls.Splitters;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.WebControls;
using Sitecore.Web.UI.WebControls.Ribbons;
using Sitecore.Web.UI.XmlControls;
using Sitecore.WFFM.Abstractions.Analytics;
using Sitecore.WFFM.Abstractions.ContentEditor;
using Sitecore.WFFM.Abstractions.Dependencies;
using Action = System.Action;
using HtmlUtil = Sitecore.Web.HtmlUtil;
using ItemUtil = Sitecore.Form.Core.Utility.ItemUtil;
using Version = Sitecore.Data.Version;
using WebUtil = Sitecore.Web.WebUtil;
using XmlControl = Sitecore.Web.UI.XmlControls.XmlControl;
using Sitecore.SecurityModel;
using Sitecore.Support.Form.Core.Visual;

namespace Sitecore.Support.Forms.Shell.UI
{
    public class FormDesigner : ApplicationForm
    {
        #region start of modified part

        private void RenameInitialFieldItemName()
        {
            ID initialFieldItemId = null;

            //Database masterDb = Factory.GetDatabase("master");

            Item formItem = this.GetCurrentItem();

            if (formItem != null && formItem.HasChildren && HasInitialFieldItem(formItem, out initialFieldItemId))
            {
                string initialFieldTitle = GetInitialFieldTitle(initialFieldItemId);

                if (!string.IsNullOrWhiteSpace(initialFieldTitle))
                {
                    Item itemToEdit = formItem.Database.GetItem(initialFieldItemId);

                    using (new EditContext(itemToEdit))
                    {
                        itemToEdit.Name = initialFieldTitle;
                    }
                }
            }
        }


        private bool HasInitialFieldItem(Item formItem, out ID initialFieldItemId)
        {
            initialFieldItemId = new ID();

            Item[] formItemDescendants = formItem.Axes.GetDescendants();

            for (int i = 0; i < formItemDescendants.Length; i++)
            {
                if (formItemDescendants[i].Name == "InitialFieldItemName")
                {
                    initialFieldItemId = formItemDescendants[i].ID;
                    return true;
                }
            }

            return false;
        }

        private string GetInitialFieldTitle(ID initialFieldItemId)
        {
            string initialFieldTitle = null;

            foreach (SectionDefinition section in this.builder.FormStucture.Sections)
            {
                foreach (FieldDefinition field in section.Fields)
                {
                    if (field.FieldID.Equals(initialFieldItemId.ToString()))
                    {
                        initialFieldTitle = field.Name;
                        break;
                    }
                }
            }

            return initialFieldTitle;
        }

        #endregion

        #region Constants

        public static readonly string FormBuilderID = "FormBuilderID";

        public static readonly string RibbonPath =
           "/sitecore/content/Applications/Modules/Web Forms for Marketers/Form Designer/Ribbon";

        #endregion

        #region Protected Fields

        public static Action saveCallback;
        public static FormDesigner savedDesigner;

        public static readonly string DefautSubmitCommand = "{745D9CF0-B189-4EAD-8D1B-8CAB68B5C972}";
        protected FormBuilder builder;
        protected GridPanel DesktopPanel;
        protected Literal FieldsLabel;
        protected XmlControl Footer;

        protected RichTextBorder FooterGrid;

        protected VSplitterXmlControl FormsSpliter;
        protected GenericControl FormSubmit;
        protected Border FormTablePanel;
        protected Literal FormTitle;

        protected XmlControl Intro;
        protected RichTextBorder IntroGrid;
        protected Border RibbonPanel;
        protected FormSettingsDesigner SettingsEditor;
        protected Border TitleBorder;

        #endregion

        private readonly IAnalyticsSettings analyticsSettings;

        #region Methods

        public FormDesigner()
        {
            this.analyticsSettings = DependenciesManager.Resolve<IAnalyticsSettings>();
        }

        protected override void OnLoad(EventArgs e)
        {
            if (!Context.ClientPage.IsEvent)
            {
                this.Localize();
                this.BuildUpClientDictionary();

                string str = Registry.GetString("/Current_User/VSplitters/FormsSpliter");
                if (string.IsNullOrEmpty(str))
                {
                    Registry.SetString("/Current_User/VSplitters/FormsSpliter", "412,");
                }

                this.LoadControls();

                if (this.builder.IsEmpty)
                {
                    this.SettingsEditor.ShowEmptyForm();
                }
            }
            else
            {
                this.builder = this.FormTablePanel.FindControl(FormBuilderID) as FormBuilder;
                this.builder.UriItem = this.GetCurrentItem().Uri.ToString();
            }
        }

        private void Localize()
        {
            this.FormTitle.Text = DependenciesManager.ResourceManager.Localize("TITLE_CAPTION");
        }

        protected virtual void BuildUpClientDictionary()
        {
            var scriptManager = Context.ClientPage.ClientScript;
            var script = new StringBuilder();
            script.AppendFormat("Sitecore.FormBuilder.dictionary['tagDescription'] = '{0}';", DependenciesManager.ResourceManager.Localize("TAG_PROPERTY_DESCRIPTION"));
            script.AppendFormat("Sitecore.FormBuilder.dictionary['tagLabel'] = '{0}';", DependenciesManager.ResourceManager.Localize("TAG_LABEL_COLON"));
            script.AppendFormat("Sitecore.FormBuilder.dictionary['analyticsLabel']= '{0}';", DependenciesManager.ResourceManager.Localize("ANALYTICS"));
            script.AppendFormat("Sitecore.FormBuilder.dictionary['editButton']= '{0}';", DependenciesManager.ResourceManager.Localize("EDIT"));
            script.AppendFormat("Sitecore.FormBuilder.dictionary['conditionRulesLiteral']= '{0}';", DependenciesManager.ResourceManager.Localize("RULES"));
            script.AppendFormat("Sitecore.FormBuilder.dictionary['noConditions']= '{0}';", DependenciesManager.ResourceManager.Localize("THERE_IS_NO_RULES_FOR_THIS_ELEMENT"));
            scriptManager.RegisterClientScriptBlock(this.GetType(), "sc-webform-dict", script.ToString(), true);
        }

        private void ExportToAscx()
        {
            Run.ExportToAscx(this, this.GetCurrentItem().Uri);
        }

        private void AddFirstFieldIfNeeded()
        {
            Item item = this.GetCurrentItem();

            if (!item.HasChildren)
            {
                using (new SecurityDisabler())
                {
                    TemplateItem template = item.Database.GetTemplate(Sitecore.Form.Core.Configuration.IDs.FieldTemplateID);
                    item.Add("InitialFieldItemName", template);
                }
            }
        }

        private void LoadControls()
        {
            AddFirstFieldIfNeeded();

            var item = new FormItem(this.GetCurrentItem());

            this.builder = new FormBuilder();
            this.builder.ID = FormBuilderID;
            this.builder.UriItem = item.Uri.ToString();
            this.FormTablePanel.Controls.Add(this.builder);

            this.FormTitle.Text = item.FormName;
            if (string.IsNullOrEmpty(this.FormTitle.Text))
            {
                this.FormTitle.Text = DependenciesManager.ResourceManager.Localize("UNTITLED_FORM");
            }

            this.TitleBorder.Controls.Add(new Literal("<input ID=\"ShowTitle\" Type=\"hidden\"/>"));
            if (!item.ShowTitle)
            {
                this.TitleBorder.Style.Add("display", "none");
            }
            this.SettingsEditor.TitleName = this.FormTitle.Text;

            SettingsEditor.TitleTags = StaticSettings.TitleTagsRoot.Children.Select(ch => ch.Name).ToArray();
            SettingsEditor.SelectedTitleTag = item.TitleTag;
            this.Intro.Controls.Add(new Literal("<input ID=\"ShowIntro\" Type=\"hidden\"/>"));
            this.IntroGrid.Value = item.Introduction;

            if (string.IsNullOrEmpty(this.IntroGrid.Value))
            {
                this.IntroGrid.Value = DependenciesManager.ResourceManager.Localize("FORM_INTRO_EMPTY");
            }
            if (!item.ShowIntroduction)
            {
                this.Intro.Style.Add("display", "none");
            }

            this.IntroGrid.FieldName = item.IntroductionFieldName;
            this.SettingsEditor.FormID = this.CurrentItemID;
            this.SettingsEditor.Introduce = this.IntroGrid.Value;
            this.SettingsEditor.SaveActionsValue = item.SaveActions;
            this.SettingsEditor.CheckActionsValue = item.CheckActions;
            this.SettingsEditor.TrackingXml = item.Tracking.ToString();
            this.SettingsEditor.SuccessRedirect = item.SuccessRedirect;
            if (item.SuccessPage.TargetItem != null)
            {
                Language lang;
                if (!Language.TryParse(WebUtil.GetQueryString("la"), out lang))
                {
                    lang = Context.Language;
                }

                this.SettingsEditor.SubmitPage = ItemUtil.GetItemUrl(item.SuccessPage.TargetItem, Configuration.Settings.Rendering.SiteResolving, lang);
            }
            else
            {
                this.SettingsEditor.SubmitPage = item.SuccessPage.Url;
            }

            if (!ID.IsNullOrEmpty(item.SuccessPageID))
            {
                this.SettingsEditor.SubmitPageID = item.SuccessPageID.ToString();
            }

            this.Footer.Controls.Add(new Literal("<input ID=\"ShowFooter\" Type=\"hidden\"/>"));
            this.FooterGrid.Value = item.Footer;

            if (string.IsNullOrEmpty(this.FooterGrid.Value))
            {
                this.FooterGrid.Value = DependenciesManager.ResourceManager.Localize("FORM_FOOTER_EMPTY");
            }

            if (!item.ShowFooter)
            {
                this.Footer.Style.Add("display", "none");
            }

            this.FooterGrid.FieldName = item.FooterFieldName;
            this.SettingsEditor.Footer = this.FooterGrid.Value;

            this.SettingsEditor.SubmitMessage = item.SuccessMessage;

            string name = string.IsNullOrEmpty(item.SubmitName) ?
              DependenciesManager.ResourceManager.Localize("NO_BUTTON_NAME") :
              Sitecore.Form.Core.Configuration.Translate.TextByItemLanguage(item.SubmitName, item.Language.GetDisplayName());

            this.FormSubmit.Attributes["value"] = name;

            this.SettingsEditor.SubmitName = name;

            this.UpdateRibbon();
        }

        private void UpdateRibbon()
        {
            Item current = this.GetCurrentItem();
            var ctl = new Ribbon();
            ctl.ID = "FormDesigneRibbon";
            ctl.CommandContext = new CommandContext(current);
            Item item = Context.Database.GetItem(RibbonPath);
            Error.AssertItemFound(item, RibbonPath);
            ctl.CommandContext.Parameters.Add("title", (!string.IsNullOrEmpty(this.SettingsEditor.TitleName)).ToString());
            ctl.CommandContext.Parameters.Add("intro", (!string.IsNullOrEmpty(this.SettingsEditor.Introduce)).ToString());
            ctl.CommandContext.Parameters.Add("footer", (!string.IsNullOrEmpty(this.SettingsEditor.Footer)).ToString());
            ctl.CommandContext.Parameters.Add("id", current.ID.ToString());
            ctl.CommandContext.Parameters.Add("la", current.Language.Name);
            ctl.CommandContext.Parameters.Add("vs", current.Version.Number.ToString());
            ctl.CommandContext.Parameters.Add("db", current.Database.Name);
            ctl.CommandContext.RibbonSourceUri = item.Uri;
            ctl.ShowContextualTabs = false;
            this.RibbonPanel.InnerHtml = HtmlUtil.RenderControl(ctl);
        }

        protected virtual void SaveFormStructure()
        {
            SheerResponse.Eval("Sitecore.FormBuilder.SaveData();");
        }

        protected virtual void SaveFormStructure(bool refresh, Action callback)
        {
            #region start of modified part
            RenameInitialFieldItemName();
            #endregion

            FormDefinition definition = this.builder.FormStucture;

            bool isAsked = false;
            foreach (SectionDefinition section in definition.Sections)
            {
                if (section.Name == string.Empty && section.Deleted != "1" && section.IsHasOnlyEmptyField)
                {
                    isAsked = true;
                    break;
                }

                foreach (FieldDefinition field in section.Fields)
                {
                    if (string.IsNullOrEmpty(field.Name) && field.Deleted != "1")
                    {
                        isAsked = true;
                        break;
                    }
                }

                if (isAsked)
                {
                    break;
                }
            }

            if (isAsked)
            {
                saveCallback = callback;
                savedDesigner = this;
                ClientDialogs.Confirmation(DependenciesManager.ResourceManager.Localize("EMPTY_FIELD_NAME"), new ClientDialogCallback().SaveConfirmation);
            }
            else
            {
                this.Save(refresh);
                if (callback != null)
                {
                    callback();
                }
            }
        }

        private void Save(bool refresh)
        {
            FormItem.UpdateFormItem(this.GetCurrentItem().Database, this.CurrentLanguage, this.builder.FormStucture);

            this.SaveFormsText();

            Context.ClientPage.Modified = false;

            if (refresh)
            {
                this.Refresh(string.Empty);
            }
        }

        protected virtual void SaveFormStructureAndClose()
        {
            Context.ClientPage.Modified = false;
            this.SettingsEditor.IsModifiedActions = false;
            this.SaveFormStructure(false, this.CloseFormWebEdit);
        }

        protected virtual void CloseFormWebEdit()
        {
            if (this.CheckModified(true))
            {
                object mode = WebUtil.GetSessionValue(StaticSettings.Mode);
                bool isDesignMode = mode == null ? string.IsNullOrEmpty(WebUtil.GetQueryString("formId")) : string.Compare(mode.ToString(), StaticSettings.DesignMode, true) == 0;
                if (Context.PageMode.IsExperienceEditor)
                {
                    //SaveFieldValue(GetCurrentItem());
                }
                SheerResponse.SetDialogValue(WebUtil.GetQueryString("hdl"));
                if (this.IsWebEditForm || !isDesignMode)
                {
                    if (!string.IsNullOrEmpty(this.BackUrl))
                    {
                        SheerResponse.Eval("window.top.location.href='" + MainUtil.DecodeName(this.BackUrl) + "'");
                    }
                    else
                    {
                        //SheerResponse.Eval("if(window.parent!=null&&window.parent.parent!=null&&window.parent.parent.scManager!= null){window.parent.parent.scManager.closeWindow(window.parent);}else{window.close()}");
                        SheerResponse.Eval("if(window.parent!=null&&window.parent.parent!=null&&window.parent.parent.scManager!= null){window.parent.parent.scManager.closeWindow(window.parent);}else{}");
                        SheerResponse.CloseWindow();
                    }
                }
                else
                {
                    SheerResponse.CloseWindow();
                }
            }
        }

        private void SaveFormsText()
        {
            Item form = this.GetCurrentItem();
            var formItem = new FormItem(form);
            form.Editing.BeginEdit();
            form.Fields[Sitecore.Form.Core.Configuration.FieldIDs.FormTitleID].Value = this.SettingsEditor.TitleName;
            form.Fields[Sitecore.Form.Core.Configuration.FieldIDs.FormTitleTagID].Value = SettingsEditor.SelectedTitleTag.ToString();

            form.Fields[Sitecore.Form.Core.Configuration.FieldIDs.ShowFormTitleID].Value = Context.ClientPage.ClientRequest.Form["ShowTitle"];

            form.Fields[Sitecore.Form.Core.Configuration.FieldIDs.FormIntroductionID].Value = this.SettingsEditor.Introduce;
            form.Fields[Sitecore.Form.Core.Configuration.FieldIDs.ShowFormIntroID].Value = Context.ClientPage.ClientRequest.Form["ShowIntro"];
            form.Fields[Sitecore.Form.Core.Configuration.FieldIDs.FormFooterID].Value = this.SettingsEditor.Footer;
            form.Fields[Sitecore.Form.Core.Configuration.FieldIDs.ShowFormFooterID].Value = Context.ClientPage.ClientRequest.Form["ShowFooter"];

            form.Fields[Sitecore.Form.Core.Configuration.FieldIDs.FormSubmitID].Value =
               this.SettingsEditor.SubmitName == string.Empty
                  ? DependenciesManager.ResourceManager.Localize("NO_BUTTON_NAME")
                  : this.SettingsEditor.SubmitName;

            form.Fields[Sitecore.Form.Core.Configuration.FieldIDs.SaveActionsID].Value = this.SettingsEditor.SaveActions.ToXml();
            form.Fields[Sitecore.Form.Core.Configuration.FieldIDs.CheckActionsID].Value = this.SettingsEditor.CheckActions.ToXml();

            form.Fields[Sitecore.Form.Core.Configuration.FieldIDs.SuccessMessageID].Value = this.SettingsEditor.SubmitMessage;

            form.Fields[Sitecore.Form.Core.Configuration.FieldIDs.SuccessModeID].Value = this.SettingsEditor.SuccessRedirect ?
                  WFFM.Abstractions.Constants.Core.Constants.RedirectMode : WFFM.Abstractions.Constants.Core.Constants.ShowMessageMode;

            var link = formItem.SuccessPage;
            link.TargetID = MainUtil.GetID(this.SettingsEditor.SubmitPageID, ID.Null);
            if (link.TargetItem != null)
            {
                link.Url = link.TargetItem.Paths.Path;
            }

            form.Fields[Sitecore.Form.Core.Configuration.FieldIDs.SuccessPageID].Value = link.Xml.OuterXml;
            form.Editing.EndEdit();
        }

        private void SaveFormAnalyticsText()
        {
            Item form = this.GetCurrentItem();
            form.Editing.BeginEdit();

            if (form.Fields["__Tracking"] != null)
            {
                form.Fields["__Tracking"].Value = this.SettingsEditor.TrackingXml;
            }

            form.Editing.EndEdit();
        }

        protected void Refresh(string url)
        {
            this.builder.ReloadForm();
        }

        public void CompareTypes(string id, string newTypeID, string oldTypeID, string propValue)
        {
            var escapedPropValue = HttpUtility.UrlDecode(propValue);
            Item item = this.GetCurrentItem();
            IEnumerable<Pair<string, string>> properties = ParametersUtil.XmlToPairArray(escapedPropValue);
            #region start of modified part
            var result = new List<string>(SupportPropertiesFactory.SupportCompareTypes(properties,
                                                                         item.Database.GetItem(newTypeID),
                                                                         item.Database.GetItem(oldTypeID),
                                                                         Sitecore.Form.Core.Configuration.FieldIDs.FieldTypeAssemblyID,
                                                                         Sitecore.Form.Core.Configuration.FieldIDs.FieldTypeClassID));
            #endregion

            if (result.Count > 0)
            {
                string message = string.Format(DependenciesManager.ResourceManager.Localize("CHANGE_TYPE"), "\n\n", string.Join(",\n\t", result.ToArray()), "\t");
                ClientDialogs.Confirmation(message, (new ClientDialogCallback(id, oldTypeID, newTypeID)).Execute);
            }
            else
            {
                SheerResponse.Eval(GetUpdateTypeScript("yes", id, oldTypeID, newTypeID));
            }
        }

        private static string GetUpdateTypeScript(string res, string id, string oldTypeID, string newTypeID)
        {
            var script = new StringBuilder();
            script.Append("Sitecore.PropertiesBuilder.changeType('");
            script.Append(res);
            script.Append("','");
            script.Append(id);
            script.Append("','");
            script.Append(newTypeID);
            script.Append("','");
            script.Append(oldTypeID);
            script.Append("')");

            return script.ToString();
        }

        private void UpdateSubmit()
        {
            this.SettingsEditor.FormID = this.CurrentItemID;
            this.SettingsEditor.UpdateCommands(this.SettingsEditor.SaveActions, this.builder.FormStucture.ToXml(), true);
        }

        private void LoadPropertyEditor(string typeID, string id)
        {
            Item item = this.GetCurrentItem();
            Item type = item.Database.GetItem(typeID);

            if (!string.IsNullOrEmpty(typeID))
            {
                try
                {
                    string html = PropertiesFactory.RenderPropertiesSection(type, Sitecore.Form.Core.Configuration.FieldIDs.FieldTypeAssemblyID, Sitecore.Form.Core.Configuration.FieldIDs.FieldTypeClassID);

                    var tracking = new Tracking(this.SettingsEditor.TrackingXml, item.Database);

                    if (!this.analyticsSettings.IsAnalyticsAvailable || tracking.Ignore || type["Deny Tag"] == "1")
                    {
                        html += "<input id='denytag' type='hidden'/>";
                    }

                    if (!string.IsNullOrEmpty(html))
                    {
                        this.SettingsEditor.PropertyEditor = html;
                    }
                }
                catch
                {
                }
            }
            else if (id == "Welcome")
            {
                this.SettingsEditor.ShowEmptyForm();
            }
        }

        private void WarningEmptyForm()
        {
            this.builder.ShowEmptyForm();
            Control message = this.SettingsEditor.ShowEmptyForm();
            Context.ClientPage.ClientResponse.SetOuterHtml(message.ID, message);
        }

        private void AddNewSection(string id, string index)
        {
            this.builder.AddToSetNewSection(id, int.Parse(index));
        }

        private void AddNewField()
        {
            this.builder.AddToSetNewField();
            SheerResponse.Eval("Sitecore.FormBuilder.updateStructure(true);");
            SheerResponse.Eval("$j('#f1 input:first').trigger('focus'); $j('.v-splitter').trigger('change')");
        }

        private void AddNewField(string parent, string id, string index)
        {
            this.builder.AddToSetNewField(parent, id, int.Parse(index));
        }

        private void UpgradeToSection(string parent, string id)
        {
            this.builder.UpgradeToSection(id);
        }

        [HandleMessage("forms:validatetext", true)]
        private void ValidateText(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (!args.IsPostBack)
            {
                this.SettingsEditor.Validate(args.Parameters["ctrl"]);
            }
        }

        [HandleMessage("item:save", true)]
        private void SaveMessage(ClientPipelineArgs args)
        {
            this.SaveFormStructure(true, null);
            SheerResponse.Eval("Sitecore.FormBuilder.updateStructure(true);");
        }

        [HandleMessage("item:selectlanguage", true)]
        private void SelectLanguage(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            Run.SetLanguage(this, this.GetCurrentItem().Uri);
        }

        [HandleMessage("item:load", true)]
        private void ChangeLanguage(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            if (!string.IsNullOrEmpty(args.Parameters["language"]))
            {
                if (this.CheckModified(true))
                {
                    var url = new UrlString(HttpUtility.UrlDecode(HttpContext.Current.Request.RawUrl.Replace("&amp;", "&")));
                    url["la"] = args.Parameters["language"];

                    Context.ClientPage.ClientResponse.SetLocation(url.ToString());
                }
            }
        }

        private bool CheckModified(bool checkIfActionsModified)
        {
            if (checkIfActionsModified && this.SettingsEditor.IsModifiedActions)
            {
                //SheerResponse.SetModified(true);
                Context.ClientPage.Modified = true;
                this.SettingsEditor.IsModifiedActions = false;
            }
            return SheerResponse.CheckModified();
        }

        [HandleMessage("forms:editsuccess", true)]
        private void EditSuccess(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (this.CheckModified(false))
            {
                if (args.IsPostBack)
                {
                    if (args.HasResult)
                    {
                        var collection = ParametersUtil.XmlToNameValueCollection(args.Result);

                        var form = new FormItem(this.GetCurrentItem());
                        LinkField field = form.SuccessPage;

                        var item = form.Database.GetItem(collection["page"]);

                        if (!string.IsNullOrEmpty(collection["page"]))
                        {
                            field.TargetID = MainUtil.GetID(collection["page"], null);

                            if (item != null)
                            {
                                Language lang;
                                if (!Language.TryParse(WebUtil.GetQueryString("la"), out lang))
                                {
                                    lang = Context.Language;
                                }
                                field.Url = ItemUtil.GetItemUrl(item, Configuration.Settings.Rendering.SiteResolving, lang);
                            }
                        }

                        this.SettingsEditor.UpdateSuccess(collection["message"], collection["page"], field.Url, collection["choice"] == "1");
                    }
                }
                else
                {
                    var str = new UrlString(UIUtil.GetUri("control:SuccessForm.Editor"));

                    UrlHandle handle = new UrlHandle();
                    handle["message"] = this.SettingsEditor.SubmitMessage;
                    if (!string.IsNullOrEmpty(this.SettingsEditor.SubmitPageID))
                    {
                        handle["page"] = this.SettingsEditor.SubmitPageID;
                    }
                    handle["choice"] = this.SettingsEditor.SuccessRedirect ? "1" : "0";

                    handle.Add(str);

                    Context.ClientPage.ClientResponse.ShowModalDialog(str.ToString(), true);
                    args.WaitForPostBack();
                }
            }
        }

        /// <summary>
        /// Opens the set submit actions.
        /// </summary>
        /// <param name="args">The arguments.</param>
        [HandleMessage("forms:addaction", true)]
        private void OpenSetSubmitActions(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (this.CheckModified(false))
            {
                if (args.IsPostBack)
                {
                    UrlString str = new UrlString(args.Parameters["url"]);
                    var handle = UrlHandle.Get(str);

                    this.SettingsEditor.TrackingXml = handle["tracking"];
                    this.SettingsEditor.FormID = this.CurrentItemID;
                    if (args.HasResult)
                    {
                        ListDefinition definition = ListDefinition.Parse(args.Result == "-" ? string.Empty : args.Result);
                        this.SettingsEditor.UpdateCommands(definition, this.builder.FormStucture.ToXml(), args.Parameters["mode"] == "save");
                    }
                }
                else
                {
                    string key = ID.NewID.ToString();
                    HttpContext.Current.Session.Add(key, args.Parameters["mode"] == "save" ? this.SettingsEditor.SaveActions : this.SettingsEditor.CheckActions);

                    var str = new UrlString(UIUtil.GetUri("control:SubmitCommands.Editor"));
                    str.Append("definition", key);
                    str.Append("db", this.GetCurrentItem().Database.Name);
                    str.Append("id", this.CurrentItemID);
                    str.Append("la", this.CurrentLanguage.Name);
                    str.Append("root", args.Parameters["root"]);
                    str.Append("system", args.Parameters["system"] ?? string.Empty);

                    args.Parameters.Add("params", key);

                    UrlHandle handle = new UrlHandle();
                    handle["title"] = DependenciesManager.ResourceManager.Localize(args.Parameters["mode"] == "save" ?
                                                               "SELECT_SAVE_TITLE" : "SELECT_CHECK_TITLE");

                    handle["desc"] = DependenciesManager.ResourceManager.Localize(args.Parameters["mode"] == "save" ?
                                                               "SELECT_SAVE_DESC" : "SELECT_CHECK_DESC");

                    handle["actions"] = DependenciesManager.ResourceManager.Localize(args.Parameters["mode"] == "save" ?
                                                               "SAVE_ACTIONS" : "CHECK_ACTIONS");

                    handle["addedactions"] = DependenciesManager.ResourceManager.Localize(args.Parameters["mode"] == "save" ?
                                                               "ADDED_SAVE_ACTIONS" : "ADDED_CHECK_ACTIONS");

                    handle["tracking"] = this.SettingsEditor.TrackingXml;

                    handle["structure"] = this.builder.FormStucture.ToXml();

                    handle.Add(str);

                    args.Parameters["url"] = str.ToString();

                    Context.ClientPage.ClientResponse.ShowModalDialog(str.ToString(), true);
                    args.WaitForPostBack();
                }
            }
        }

        /// <summary>
        /// Configures the goal.
        /// </summary>
        /// <param name="args">The arguments.</param>
        [HandleMessage("forms:configuregoal", true)]
        protected void ConfigureGoal(ClientPipelineArgs args)
        {
            var database = Factory.GetDatabase(this.CurrentDatabase);
            var tracking = new Tracking(this.SettingsEditor.TrackingXml, database);

            var goal = tracking.Goal;
            if (goal != null)
            {
                var realContext = new CommandContext(new[] { goal });

                var command = CommandManager.GetCommand("item:personalize");
                command.Execute(realContext);
                return;
            }
            SheerResponse.Alert(DependenciesManager.ResourceManager.Localize("CHOOSE_GOAL_AT_FIRST"));
        }

        [HandleMessage("forms:analytics", true)]
        protected void CustomizeAnalytics(ClientPipelineArgs args)
        {
            if (args.IsPostBack)
            {
                if (args.HasResult)
                {
                    this.SettingsEditor.FormID = this.CurrentItemID;
                    this.SettingsEditor.TrackingXml = args.Result;
                    this.SettingsEditor.UpdateCommands(this.SettingsEditor.SaveActions, this.builder.FormStucture.ToXml(), true);

                    SheerResponse.Eval("Sitecore.PropertiesBuilder.editors = [];");
                    SheerResponse.Eval("Sitecore.PropertiesBuilder.setActiveProperties(Sitecore.FormBuilder, null)");
                    this.SaveFormAnalyticsText();

                }
            }
            else
            {
                UrlString url = new UrlString(UIUtil.GetUri("control:Forms.CustomizeAnalyticsWizard"));
                UrlHandle handle = new UrlHandle();
                handle["tracking"] = this.SettingsEditor.TrackingXml;

                handle.Add(url);

                Context.ClientPage.ClientResponse.ShowModalDialog(url.ToString(), true);
                args.WaitForPostBack();
            }
        }


        [HandleMessage("forms:edititem", true)]
        protected void Edit(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            if (this.CheckModified(false))
            {
                bool isSaveAction = true;
                ListDefinition list = this.SettingsEditor.SaveActions;

                if (list.Groups.Any())
                {
                    IListItemDefinition li = list.Groups.First().GetListItem(args.Parameters["unicid"]);

                    if (li == null)
                    {
                        isSaveAction = false;
                        list = this.SettingsEditor.CheckActions;
                        if (list.Groups.Any())
                        {
                            li = list.Groups.First().GetListItem(args.Parameters["unicid"]);
                        }
                    }

                    if (li != null)
                    {
                        if (args.IsPostBack)
                        {
                            var str = new UrlString(args.Parameters["url"]);
                            var handle = UrlHandle.Get(str);

                            this.SettingsEditor.FormID = this.CurrentItemID;
                            this.SettingsEditor.TrackingXml = handle["tracking"];

                            if (args.HasResult)
                            {
                                #region start of modified part
                                li.Parameters = args.Result == "-" ? string.Empty : PatchHelper.Expand(args.Result, false);
                                #endregion
                                this.SettingsEditor.UpdateCommands(list, this.builder.FormStucture.ToXml(), isSaveAction);
                            }
                        }
                        else
                        {
                            string key = ID.NewID.ToString();
                            HttpContext.Current.Session.Add(key, li.Parameters);

                            var action = new ActionItem(StaticSettings.ContextDatabase.GetItem(li.ItemID));

                            UrlString str;

                            if (action.Editor.Contains("~/xaml/"))
                            {
                                str = new UrlString(action.Editor);
                            }
                            else
                            {
                                str = new UrlString(UIUtil.GetUri(action.Editor));
                            }

                            str.Append("params", key);
                            str.Append("id", this.CurrentItemID);
                            str.Append("actionid", li.ItemID);
                            str.Append("la", this.CurrentLanguage.Name);
                            str.Append("uniqid", li.Unicid);
                            str.Append("db", this.CurrentDatabase);

                            var handle = new UrlHandle();
                            handle["tracking"] = this.SettingsEditor.TrackingXml;
                            handle["actiondefinition"] = this.SettingsEditor.SaveActions.ToXml();
                            handle.Add(str);

                            args.Parameters["url"] = str.ToString();

                            string query = action.QueryString;

                            ModalDialog.Show(str, query);

                            args.WaitForPostBack();
                        }
                    }
                }
            }
        }

        [HandleMessage("list:edit", true)]
        protected void ListEdit(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            if (!args.IsPostBack)
            {
                var str = new UrlString("/sitecore/shell/~/xaml/Sitecore.Forms.Shell.UI.Dialogs.ListItemsEditor.aspx");

                string key = ID.NewID.ToString();

                string value = HttpUtility.UrlDecode(args.Parameters["value"]);
                if (value.StartsWith(StaticSettings.SourceMarker))
                {
                    value = new QuerySettings("root", value.Substring(StaticSettings.SourceMarker.Length)).ToString();
                }

                var collection = new NameValueCollection();
                collection["queries"] = value;

                HttpContext.Current.Session.Add(key, ParametersUtil.NameValueCollectionToXml(collection, true));

                str.Append("params", key);
                str.Append("id", this.CurrentItemID);
                str.Append("db", this.CurrentDatabase);
                str.Append("la", this.CurrentLanguage.Name);
                str.Append("vs", this.CurrentVersion.Number.ToString());
                str.Append("target", args.Parameters["target"]);

                Context.ClientPage.ClientResponse.ShowModalDialog(str.ToString(), true);
                args.WaitForPostBack();
            }
            else
            {
                if (args.HasResult)
                {
                    if (args.Result == "-")
                    {
                        args.Result = string.Empty;
                    }

                    #region start of modified part
                    var collection = ParametersUtil.XmlToNameValueCollection(PatchHelper.Expand(args.Result, true), true);
                    #endregion

                    SheerResponse.SetAttribute(args.Parameters["target"], "value", HttpUtility.UrlEncode(collection["queries"]));
                    SheerResponse.Eval("Sitecore.FormBuilder.executeOnChange($('" + args.Parameters["target"] + "'));");

                    if (HttpUtility.UrlDecode(args.Parameters["value"]) != collection["queries"])
                    {
                        SheerResponse.SetModified(true);
                    }
                }
            }
        }

        public override void HandleMessage(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            base.HandleMessage(message);
            string name = message.Name;
            if (name != null)
            {
                if (name == "forms:save")
                {
                    this.SaveFormStructure(true, null);
                    return;
                }

                if (!string.IsNullOrEmpty(message["id"]))
                {
                    var args = new ClientPipelineArgs();
                    args.Parameters.Add("id", message["id"]);

                    if (name == "richtext:edit")
                    {
                        Context.ClientPage.Start(this.SettingsEditor, "EditText", args);
                    }
                    else if (name == "richtext:edithtml")
                    {
                        Context.ClientPage.Start(this.SettingsEditor, "EditHtml", args);
                    }
                    else if (name == "richtext:fix")
                    {
                        Context.ClientPage.Start(this.SettingsEditor, "Fix", args);
                    }
                }
            }
        }

        public Item GetCurrentItem()
        {
            var uri = new ItemUri(this.CurrentItemID, this.CurrentLanguage, this.CurrentVersion, this.CurrentDatabase);

            return Database.GetItem(uri);
        }

        #endregion

        #region Properties

        public bool IsWebEditForm
        {
            get { return !string.IsNullOrEmpty(WebUtil.GetQueryString("webform")); }
        }

        public Language CurrentLanguage
        {
            get { return Language.Parse(WebUtil.GetQueryString("la")); }
        }

        public string CurrentDatabase
        {
            get { return WebUtil.GetQueryString("db"); }
        }

        public Version CurrentVersion
        {
            get { return Version.Parse(WebUtil.GetQueryString("vs")); }
        }

        public string BackUrl
        {
            get { return WebUtil.GetQueryString("backurl"); }
        }

        public string CurrentItemID
        {
            get
            {
                string queryString = WebUtil.GetQueryString("formid");

                if (string.IsNullOrEmpty(queryString))
                {
                    queryString = WebUtil.GetQueryString("webform");
                }
                if (string.IsNullOrEmpty(queryString))
                {
                    queryString = WebUtil.GetQueryString("id");
                }
                if (string.IsNullOrEmpty(queryString))
                {
                    queryString = Sitecore.Form.Core.Utility.Utils.GetDataSource(WebUtil.GetQueryString());
                }
                return queryString;
            }
        }

        #endregion

        [Serializable]
        public class ClientDialogCallback
        {
            public delegate void Action();
            private string id;
            private string oldTypeID;
            private string newTypeID;

            public ClientDialogCallback()
            {
            }

            public ClientDialogCallback(string id, string oldTypeID, string newTypeID)
            {
                this.id = id;
                this.oldTypeID = oldTypeID;
                this.newTypeID = newTypeID;
            }

            public void Execute(string res)
            {
                SheerResponse.Eval(GetUpdateTypeScript(res, this.id, this.oldTypeID, this.newTypeID));
            }

            public void SaveConfirmation(string result)
            {
                if (result == "yes")
                {
                    savedDesigner.Save(true);

                    if (saveCallback != null)
                    {
                        saveCallback();
                    }
                }
            }
        }

    }
}
