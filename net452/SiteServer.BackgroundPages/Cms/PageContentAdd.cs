﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web.UI.WebControls;
using SiteServer.Utils;
using SiteServer.BackgroundPages.Ajax;
using SiteServer.BackgroundPages.Controls;
using SiteServer.BackgroundPages.Core;
using SiteServer.BackgroundPages.Utils;
using SiteServer.CMS.Caches;
using SiteServer.CMS.Caches.Content;
using SiteServer.CMS.Core;
using SiteServer.CMS.Core.Create;
using SiteServer.CMS.Core.Enumerations;
using SiteServer.CMS.Core.Office;
using SiteServer.CMS.Database.Attributes;
using SiteServer.CMS.Database.Core;
using SiteServer.CMS.Database.Models;
using SiteServer.CMS.Fx;
using SiteServer.CMS.Plugin;
using SiteServer.Plugin;

namespace SiteServer.BackgroundPages.Cms
{
    public class PageContentAdd : BasePageCms
    {
        public Literal LtlPageTitle;

        public TextBox TbTitle;
        public Literal LtlTitleHtml;
        public AuxiliaryControl AcAttributes;
        public CheckBoxList CblContentAttributes;
        public CheckBoxList CblContentGroups;
        public Button BtnContentGroupAdd;
        public DropDownList DdlContentLevel;
        public TextBox TbTags;
        public Literal LtlTags;
        public PlaceHolder PhTranslate;
        public Button BtnTranslate;
        public DropDownList DdlTranslateType;
        public PlaceHolder PhStatus;
        public TextBox TbLinkUrl;
        public DateTimeTextBox TbAddDate;
        public Button BtnSubmit;

        private ChannelInfo _channelInfo;
        private List<TableStyleInfo> _styleInfoList;
        private string _tableName;

        protected override bool IsSinglePage => true;

        public string PageContentAddHandlerUrl => PageContentAddHandler.GetRedirectUrl(SiteId, AuthRequest.GetQueryInt("channelId"), AuthRequest.GetQueryInt("id"));

        public string AjaxGetDetectionReplaceUrl => AjaxCmsService.GetDetectionReplaceUrl(SiteId);

        public static string GetRedirectUrlOfAdd(int siteId, int channelId, string returnUrl)
        {
            return FxUtils.GetCmsUrl(siteId, nameof(PageContentAdd), new NameValueCollection
            {
                {"channelId", channelId.ToString()},
                {"returnUrl", StringUtils.ValueToUrl(returnUrl)}
            });
        }

        public static string GetRedirectUrlOfEdit(int siteId, int channelId, int id, string returnUrl)
        {
            return FxUtils.GetCmsUrl(siteId, nameof(PageContentAdd), new NameValueCollection
            {
                {"channelId", channelId.ToString()},
                {"id", id.ToString()},
                {"returnUrl", StringUtils.ValueToUrl(returnUrl)}
            });
        }

        public void Page_Load(object sender, EventArgs e)
        {
            if (IsForbidden) return;

            WebPageUtils.CheckRequestParameter("siteId", "channelId");

            var channelId = AuthRequest.GetQueryInt("channelId");
            var contentId = AuthRequest.GetQueryInt("id");
            ReturnUrl = StringUtils.ValueFromUrl(AuthRequest.GetQueryString("returnUrl"));
            if (string.IsNullOrEmpty(ReturnUrl))
            {
                ReturnUrl = AdminPagesUtils.Cms.GetContentsUrl(SiteId, channelId);
            }

            _channelInfo = ChannelManager.GetChannelInfo(SiteId, channelId);
            _tableName = ChannelManager.GetTableName(SiteInfo, _channelInfo);
            ContentInfo contentInfo = null;
            _styleInfoList = TableStyleManager.GetContentStyleInfoList(SiteInfo, _channelInfo);

            if (!IsPermissions(contentId)) return;

            if (contentId > 0)
            {
                contentInfo = ContentManager.GetContentInfo(SiteInfo, _channelInfo, contentId);
            }

            var titleFormat = IsPostBack ? Request.Form[ContentAttribute.GetFormatStringAttributeName(ContentAttribute.Title)] : contentInfo?.Get<string>(ContentAttribute.GetFormatStringAttributeName(ContentAttribute.Title));
            LtlTitleHtml.Text = ContentUtility.GetTitleHtml(titleFormat, AjaxCmsService.GetTitlesUrl(SiteId, _channelInfo.Id));

            AcAttributes.SiteInfo = SiteInfo;
            AcAttributes.ChannelId = _channelInfo.Id;
            AcAttributes.ContentId = contentId;
            AcAttributes.StyleInfoList = _styleInfoList;

            if (!IsPostBack)
            {
                var pageTitle = contentId == 0 ? "添加内容" : "编辑内容";

                LtlPageTitle.Text = pageTitle;

                if (HasChannelPermissions(_channelInfo.Id, ConfigManager.ChannelPermissions.ContentTranslate))
                {
                    PhTranslate.Visible = true;
                    BtnTranslate.Attributes.Add("onclick", ModalChannelMultipleSelect.GetOpenWindowString(SiteId, true));

                    SystemWebUtils.AddListItemsToETranslateContentType(DdlTranslateType, true);
                    SystemWebUtils.SelectSingleItem(DdlTranslateType, ETranslateContentTypeUtils.GetValue(ETranslateContentType.Copy));
                }
                else
                {
                    PhTranslate.Visible = false;
                }

                CblContentAttributes.Items.Add(new ListItem("置顶", ContentAttribute.IsTop));
                CblContentAttributes.Items.Add(new ListItem("推荐", ContentAttribute.IsRecommend));
                CblContentAttributes.Items.Add(new ListItem("热点", ContentAttribute.IsHot));
                CblContentAttributes.Items.Add(new ListItem("醒目", ContentAttribute.IsColor));
                TbAddDate.DateTime = DateTime.Now;
                TbAddDate.Now = true;

                var contentGroupNameList = ContentGroupManager.GetGroupNameList(SiteId);
                foreach (var groupName in contentGroupNameList)
                {
                    var item = new ListItem(groupName, groupName);
                    CblContentGroups.Items.Add(item);
                }
                
                BtnContentGroupAdd.Attributes.Add("onclick", ModalContentGroupAdd.GetOpenWindowString(SiteId));

                LtlTags.Text = ContentUtility.GetTagsHtml(AjaxCmsService.GetTagsUrl(SiteId));

                if (HasChannelPermissions(_channelInfo.Id, ConfigManager.ChannelPermissions.ContentCheck))
                {
                    PhStatus.Visible = true;
                    int checkedLevel;
                    var isChecked = CheckManager.GetUserCheckLevel(AuthRequest.AdminPermissionsImpl, SiteInfo, _channelInfo.Id, out checkedLevel);
                    if (AuthRequest.IsQueryExists("contentLevel"))
                    {
                        checkedLevel = TranslateUtils.ToIntWithNegative(AuthRequest.GetQueryString("contentLevel"));
                        if (checkedLevel != CheckManager.LevelInt.NotChange)
                        {
                            isChecked = checkedLevel >= SiteInfo.CheckContentLevel;
                        }
                    }

                    SystemWebUtils.LoadContentLevelToCheckEdit(DdlContentLevel, SiteInfo, contentInfo, isChecked, checkedLevel);
                }
                else
                {
                    PhStatus.Visible = false;
                }

                //自动检测敏感词
                BtnSubmit.Attributes.Add("onclick",
                    SiteInfo.IsAutoCheckKeywords
                        ? InputParserUtils.GetValidateSubmitOnClickScript("myForm", true, "autoCheckKeywords()")
                        : InputParserUtils.GetValidateSubmitOnClickScript("myForm", true, string.Empty));

                if (contentId == 0)
                {
                    var attributes = TableStyleManager.GetDefaultAttributes(_styleInfoList);

                    if (AuthRequest.IsQueryExists("isUploadWord"))
                    {
                        var isFirstLineTitle = AuthRequest.GetQueryBool("isFirstLineTitle");
                        var isFirstLineRemove = AuthRequest.GetQueryBool("isFirstLineRemove");
                        var isClearFormat = AuthRequest.GetQueryBool("isClearFormat");
                        var isFirstLineIndent = AuthRequest.GetQueryBool("isFirstLineIndent");
                        var isClearFontSize = AuthRequest.GetQueryBool("isClearFontSize");
                        var isClearFontFamily = AuthRequest.GetQueryBool("isClearFontFamily");
                        var isClearImages = AuthRequest.GetQueryBool("isClearImages");
                        var contentLevel = AuthRequest.GetQueryInt("contentLevel");
                        var fileName = AuthRequest.GetQueryString("fileName");

                        var formCollection = WordUtils.GetWordNameValueCollection(SiteId, isFirstLineTitle, isFirstLineRemove, isClearFormat, isFirstLineIndent, isClearFontSize, isClearFontFamily, isClearImages, fileName);

                        if (formCollection != null)
                        {
                            foreach (string name in formCollection)
                            {
                                var value = formCollection[name];
                                attributes[name] = value;
                            }
                        }

                        TbTitle.Text = formCollection[ContentAttribute.Title];
                    }

                    AcAttributes.Attributes = attributes;
                }
                else if (contentInfo != null)
                {
                    TbTitle.Text = contentInfo.Title;

                    TbTags.Text = contentInfo.Tags;

                    var list = new List<string>();
                    if (contentInfo.Top)
                    {
                        list.Add(ContentAttribute.IsTop);
                    }
                    if (contentInfo.Recommend)
                    {
                        list.Add(ContentAttribute.IsRecommend);
                    }
                    if (contentInfo.Hot)
                    {
                        list.Add(ContentAttribute.IsHot);
                    }
                    if (contentInfo.Color)
                    {
                        list.Add(ContentAttribute.IsColor);
                    }
                    SystemWebUtils.SelectMultiItems(CblContentAttributes, list);
                    TbLinkUrl.Text = contentInfo.LinkUrl;
                    if (contentInfo.AddDate.HasValue)
                    {
                        TbAddDate.DateTime = contentInfo.AddDate.Value;
                    }

                    SystemWebUtils.SelectMultiItems(CblContentGroups, TranslateUtils.StringCollectionToStringList(contentInfo.GroupNameCollection));

                    AcAttributes.Attributes = contentInfo.ToDictionary();
                }
            }
            else
            {
                AcAttributes.Attributes = TranslateUtils.ToDictionary(Request.Form);
            }
            //DataBind();
        }

        public override void Submit_OnClick(object sender, EventArgs e)
        {
            if (!Page.IsPostBack || !Page.IsValid) return;

            var contentId = AuthRequest.GetQueryInt("id");
            string redirectUrl;

            if (contentId == 0)
            {
                try
                {
                    var tagCollection = TagUtils.ParseTagsString(TbTags.Text);
                    var dict = BackgroundInputTypeParser.SaveAttributes(SiteInfo, _styleInfoList, Request.Form, ContentAttribute.AllAttributes.Value);

                    var contentInfo = new ContentInfo(dict)
                    {
                        ChannelId = _channelInfo.Id,
                        SiteId = SiteId,
                        AdminId = AuthRequest.AdminId,
                        AddUserName = AuthRequest.AdminName,
                        LastEditUserName = AuthRequest.AdminName,
                        LastEditDate = DateTime.Now,
                        GroupNameCollection = SystemWebUtils.SelectedItemsValueToStringCollection(CblContentGroups.Items),
                        Title = TbTitle.Text
                    };

                    var formatString = TranslateUtils.ToBool(Request.Form[ContentAttribute.Title + "_formatStrong"]);
                    var formatEm = TranslateUtils.ToBool(Request.Form[ContentAttribute.Title + "_formatEM"]);
                    var formatU = TranslateUtils.ToBool(Request.Form[ContentAttribute.Title + "_formatU"]);
                    var formatColor = Request.Form[ContentAttribute.Title + "_formatColor"];
                    var theFormatString = ContentUtility.GetTitleFormatString(formatString, formatEm, formatU, formatColor);
                    contentInfo.Set(ContentAttribute.GetFormatStringAttributeName(ContentAttribute.Title), theFormatString);

                    foreach (ListItem listItem in CblContentAttributes.Items)
                    {
                        var value = listItem.Selected.ToString();
                        var attributeName = listItem.Value;
                        contentInfo.Set(attributeName, value);
                    }
                    contentInfo.LinkUrl = TbLinkUrl.Text;
                    contentInfo.AddDate = TbAddDate.DateTime;
                    if (contentInfo.AddDate.Value.Year <= DateUtils.SqlMinValue.Year)
                    {
                        contentInfo.AddDate = DateTime.Now;
                    }

                    contentInfo.CheckedLevel = TranslateUtils.ToIntWithNegative(DdlContentLevel.SelectedValue);
                    contentInfo.Checked = contentInfo.CheckedLevel >= SiteInfo.CheckContentLevel;
                    contentInfo.Tags = TranslateUtils.ObjectCollectionToString(tagCollection, " ");

                    foreach (var service in PluginManager.Services)
                    {
                        try
                        {
                            service.OnContentFormSubmit(new ContentFormSubmitEventArgs(SiteId, _channelInfo.Id,
                                contentInfo.Id, TranslateUtils.ToDictionary(Request.Form), contentInfo));
                        }
                        catch (Exception ex)
                        {
                            LogUtils.AddErrorLog(service.PluginId, ex, nameof(IService.ContentFormSubmit));
                        }
                    }
                    
                    //判断是不是有审核权限
                    var isCheckedOfUser = CheckManager.GetUserCheckLevel(AuthRequest.AdminPermissionsImpl, SiteInfo, contentInfo.ChannelId, out var checkedLevelOfUser);
                    if (CheckManager.IsCheckable(contentInfo.Checked, contentInfo.CheckedLevel, isCheckedOfUser, checkedLevelOfUser))
                    {
                        if (contentInfo.Checked)
                        {
                            contentInfo.CheckedLevel = 0;
                        }

                        contentInfo.Set(ContentAttribute.CheckUserName, AuthRequest.AdminName);
                        contentInfo.Set(ContentAttribute.CheckDate, DateUtils.GetDateAndTimeString(DateTime.Now));
                        contentInfo.Set(ContentAttribute.CheckReasons, string.Empty);
                    }

                    contentInfo.Id = DataProvider.ContentRepository.Insert(_tableName, SiteInfo, _channelInfo, contentInfo);

                    TagUtils.AddTags(tagCollection, SiteId, contentInfo.Id);

                    CreateManager.CreateContent(SiteId, _channelInfo.Id, contentInfo.Id);
                    CreateManager.TriggerContentChangedEvent(SiteId, _channelInfo.Id);

                    AuthRequest.AddSiteLog(SiteId, _channelInfo.Id, contentInfo.Id, "添加内容",
                        $"栏目:{ChannelManager.GetChannelNameNavigation(SiteId, contentInfo.ChannelId)},内容标题:{contentInfo.Title}");

                    ContentManager.Translate(SiteInfo, _channelInfo.Id, contentInfo.Id, Request.Form["translateCollection"], ETranslateContentTypeUtils.GetEnumType(DdlTranslateType.SelectedValue), AuthRequest.AdminName);

                    redirectUrl = PageContentAddAfter.GetRedirectUrl(SiteId, _channelInfo.Id, contentInfo.Id,
                        ReturnUrl);
                }
                catch (Exception ex)
                {
                    LogUtils.AddErrorLog(ex);
                    FailMessage($"内容添加失败：{ex.Message}");
                    return;
                }
            }
            else
            {
                var contentInfo = ContentManager.GetContentInfo(SiteInfo, _channelInfo, contentId);
                try
                {
                    var tagsLast = contentInfo.Tags;

                    contentInfo.LastEditUserName = AuthRequest.AdminName;
                    contentInfo.LastEditDate = DateTime.Now;
                    if (contentInfo.AdminId == 0)
                    {
                        contentInfo.AdminId = AuthRequest.AdminId;
                    }

                    var dict = BackgroundInputTypeParser.SaveAttributes(SiteInfo, _styleInfoList, Request.Form, ContentAttribute.AllAttributes.Value);
                    foreach (var o in dict)
                    {
                        contentInfo.Set(o.Key, o.Value);
                    }

                    contentInfo.GroupNameCollection = SystemWebUtils.SelectedItemsValueToStringCollection(CblContentGroups.Items);
                    var tagCollection = TagUtils.ParseTagsString(TbTags.Text);

                    contentInfo.Title = TbTitle.Text;
                    var formatString = TranslateUtils.ToBool(Request.Form[ContentAttribute.Title + "_formatStrong"]);
                    var formatEm = TranslateUtils.ToBool(Request.Form[ContentAttribute.Title + "_formatEM"]);
                    var formatU = TranslateUtils.ToBool(Request.Form[ContentAttribute.Title + "_formatU"]);
                    var formatColor = Request.Form[ContentAttribute.Title + "_formatColor"];
                    var theFormatString = ContentUtility.GetTitleFormatString(formatString, formatEm, formatU, formatColor);
                    contentInfo.Set(ContentAttribute.GetFormatStringAttributeName(ContentAttribute.Title), theFormatString);
                    foreach (ListItem listItem in CblContentAttributes.Items)
                    {
                        var value = listItem.Selected.ToString();
                        var attributeName = listItem.Value;
                        contentInfo.Set(attributeName, value);
                    }
                    contentInfo.LinkUrl = TbLinkUrl.Text;
                    contentInfo.AddDate = TbAddDate.DateTime;

                    var checkedLevel = TranslateUtils.ToIntWithNegative(DdlContentLevel.SelectedValue);
                    if (checkedLevel != CheckManager.LevelInt.NotChange)
                    {
                        contentInfo.Checked = checkedLevel >= SiteInfo.CheckContentLevel;
                        contentInfo.CheckedLevel = checkedLevel;
                    }
                    contentInfo.Tags = TranslateUtils.ObjectCollectionToString(tagCollection, " ");

                    foreach (var service in PluginManager.Services)
                    {
                        try
                        {
                            service.OnContentFormSubmit(new ContentFormSubmitEventArgs(SiteId, _channelInfo.Id,
                                contentInfo.Id, TranslateUtils.ToDictionary(Request.Form), contentInfo));
                        }
                        catch (Exception ex)
                        {
                            LogUtils.AddErrorLog(service.PluginId, ex, nameof(IService.ContentFormSubmit));
                        }
                    }

                    DataProvider.ContentRepository.Update(SiteInfo, _channelInfo, contentInfo);

                    TagUtils.UpdateTags(tagsLast, contentInfo.Tags, tagCollection, SiteId, contentId);

                    ContentManager.Translate(SiteInfo, _channelInfo.Id, contentInfo.Id, Request.Form["translateCollection"], ETranslateContentTypeUtils.GetEnumType(DdlTranslateType.SelectedValue), AuthRequest.AdminName);

                    CreateManager.CreateContent(SiteId, _channelInfo.Id, contentId);
                    CreateManager.TriggerContentChangedEvent(SiteId, _channelInfo.Id);

                    AuthRequest.AddSiteLog(SiteId, _channelInfo.Id, contentId, "修改内容",
                        $"栏目:{ChannelManager.GetChannelNameNavigation(SiteId, contentInfo.ChannelId)},内容标题:{contentInfo.Title}");

                    redirectUrl = ReturnUrl;

                    //更新引用该内容的信息
                    //如果不是异步自动保存，那么需要将引用此内容的content修改
                    //var sourceContentIdList = new List<int>
                    //{
                    //    contentInfo.Id
                    //};
                    //var tableList = DataProvider.TableDao.GetTableCollectionInfoListCreatedInDb();
                    //foreach (var table in tableList)
                    //{
                    //    var targetContentIdList = DataProvider.ContentRepository.GetReferenceIdList(table.TableName, sourceContentIdList);
                    //    foreach (var targetContentId in targetContentIdList)
                    //    {
                    //        var targetContentInfo = DataProvider.ContentRepository.GetContentInfo(table.TableName, targetContentId);
                    //        if (targetContentInfo == null || targetContentInfo.GetString(ContentAttribute.TranslateContentType) != ETranslateContentType.ReferenceContent.ToString()) continue;

                    //        contentInfo.Id = targetContentId;
                    //        contentInfo.SiteId = targetContentInfo.SiteId;
                    //        contentInfo.ChannelId = targetContentInfo.ChannelId;
                    //        contentInfo.SourceId = targetContentInfo.SourceId;
                    //        contentInfo.ReferenceId = targetContentInfo.ReferenceId;
                    //        contentInfo.Taxis = targetContentInfo.Taxis;
                    //        contentInfo.Set(ContentAttribute.TranslateContentType, targetContentInfo.GetString(ContentAttribute.TranslateContentType));
                    //        DataProvider.ContentRepository.UpdateObject(table.TableName, contentInfo);

                    //        //资源：图片，文件，视频
                    //        var targetSiteInfo = SiteManager.GetSiteInfo(targetContentInfo.SiteId);
                    //        var bgContentInfo = contentInfo as BackgroundContentInfo;
                    //        var bgTargetContentInfo = targetContentInfo as BackgroundContentInfo;
                    //        if (bgTargetContentInfo != null && bgContentInfo != null)
                    //        {
                    //            if (bgContentInfo.ImageUrl != bgTargetContentInfo.ImageUrl)
                    //            {
                    //                //修改图片
                    //                var sourceImageUrl = PathUtility.MapPath(SiteInfo, bgContentInfo.ImageUrl);
                    //                CopyReferenceFiles(targetSiteInfo, sourceImageUrl);
                    //            }
                    //            else if (bgContentInfo.GetString(ContentAttribute.GetExtendAttributeName(ContentAttribute.ImageUrl)) != bgTargetContentInfo.GetString(ContentAttribute.GetExtendAttributeName(ContentAttribute.ImageUrl)))
                    //            {
                    //                var sourceImageUrls = TranslateUtils.StringCollectionToStringList(bgContentInfo.GetString(ContentAttribute.GetExtendAttributeName(ContentAttribute.ImageUrl)));

                    //                foreach (string imageUrl in sourceImageUrls)
                    //                {
                    //                    var sourceImageUrl = PathUtility.MapPath(SiteInfo, imageUrl);
                    //                    CopyReferenceFiles(targetSiteInfo, sourceImageUrl);
                    //                }
                    //            }
                    //            if (bgContentInfo.FileUrl != bgTargetContentInfo.FileUrl)
                    //            {
                    //                //修改附件
                    //                var sourceFileUrl = PathUtility.MapPath(SiteInfo, bgContentInfo.FileUrl);
                    //                CopyReferenceFiles(targetSiteInfo, sourceFileUrl);

                    //            }
                    //            else if (bgContentInfo.GetString(ContentAttribute.GetExtendAttributeName(ContentAttribute.FileUrl)) != bgTargetContentInfo.GetString(ContentAttribute.GetExtendAttributeName(ContentAttribute.FileUrl)))
                    //            {
                    //                var sourceFileUrls = TranslateUtils.StringCollectionToStringList(bgContentInfo.GetString(ContentAttribute.GetExtendAttributeName(ContentAttribute.FileUrl)));

                    //                foreach (var fileUrl in sourceFileUrls)
                    //                {
                    //                    var sourceFileUrl = PathUtility.MapPath(SiteInfo, fileUrl);
                    //                    CopyReferenceFiles(targetSiteInfo, sourceFileUrl);
                    //                }
                    //            }
                    //        }
                    //    }
                    //}
                }
                catch (Exception ex)
                {
                    LogUtils.AddErrorLog(ex);
                    FailMessage($"内容修改失败：{ex.Message}");
                    return;
                }
            }

            WebPageUtils.Redirect(redirectUrl);
        }

        private bool IsPermissions(int contentId)
        {
            if (contentId == 0)
            {
                if (_channelInfo == null || _channelInfo.IsContentAddable == false)
                {
                    WebPageUtils.RedirectToErrorPage("此栏目不能添加内容！");
                    return false;
                }

                if (!HasChannelPermissions(_channelInfo.Id, ConfigManager.ChannelPermissions.ContentAdd))
                {
                    if (!AuthRequest.IsAdminLoggin)
                    {
                        WebPageUtils.RedirectToLoginPage();
                        return false;
                    }

                    WebPageUtils.RedirectToErrorPage("您无此栏目的添加内容权限！");
                    return false;
                }
            }
            else
            {
                if (!HasChannelPermissions(_channelInfo.Id, ConfigManager.ChannelPermissions.ContentEdit))
                {
                    if (!AuthRequest.IsAdminLoggin)
                    {
                        WebPageUtils.RedirectToLoginPage();
                        return false;
                    }

                    WebPageUtils.RedirectToErrorPage("您无此栏目的修改内容权限！");
                    return false;
                }
            }
            return true;
        }

        public string ReturnUrl { get; private set; }
    }
}