﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Smartstore.ComponentModel;
using Smartstore.Core.Data;
using Smartstore.Core.Localization;
using Smartstore.Core.Rules.Filters;
using Smartstore.Core.Search;
using Smartstore.Core.Search.Facets;
using Smartstore.Core.Security;
using Smartstore.Core.Stores;
using Smartstore.Forums.Domain;
using Smartstore.Forums.Models;
using Smartstore.Web.Controllers;
using Smartstore.Web.Modelling.Settings;
using Smartstore.Web.Models.DataGrid;
using Smartstore.Web.Rendering;

namespace Smartstore.Forums.Controllers
{
    [Area("Admin")]
    [Route("[area]/forum/[action]/{id?}")]
    public class ForumAdminController : AdminController
    {
        private readonly SmartDbContext _db;
        private readonly ILocalizedEntityService _localizedEntityService;
        private readonly ILanguageService _languageService;
        private readonly StoreDependingSettingHelper _settingHelper;

        public ForumAdminController(
            SmartDbContext db,
            ILocalizedEntityService localizedEntityService,
            ILanguageService languageService,
            StoreDependingSettingHelper settingHelper)
        {
            _db = db;
            _localizedEntityService = localizedEntityService;
            _languageService = languageService;
            _settingHelper = settingHelper;
        }

        [Permission(ForumPermissions.Read)]
        public IActionResult List()
        {
            var model = new ForumGroupListModel();

            //...

            ViewBag.IsSingleStoreMode = Services.StoreContext.IsSingleStoreMode();

            return View(model);
        }

        [HttpPost]
        [Permission(ForumPermissions.Read)]
        public async Task<IActionResult> ForumGroupList(GridCommand command, ForumGroupListModel model)
        {
            var mapper = MapperFactory.GetMapper<ForumGroup, ForumGroupModel>();
            var query = _db.ForumGroups()
                .Include(x => x.Forums)
                .ApplyStoreFilter(model.SearchStoreId)
                .AsNoTracking();

            if (model.SearchName.HasValue())
            {
                query = query.ApplySearchFilterFor(x => x.Name, model.SearchName);
            }

            var groups = await query
                .OrderBy(x => x.DisplayOrder)
                .ApplyGridCommand(command, false)
                .ToListAsync();

            var rows = groups
                .Select(x => new ForumGroupModel
                {
                    Id = x.Id,
                    Name = x.GetLocalized(x => x.Name),
                    DisplayOrder = x.DisplayOrder,
                    LimitedToStores = x.LimitedToStores,
                    SubjectToAcl = x.SubjectToAcl,
                    CreatedOn = Services.DateTimeHelper.ConvertToUserTime(x.CreatedOnUtc, DateTimeKind.Utc),
                    EditUrl = Url.Action("EditForumGroup", "Forum", new { id = x.Id, area = "Admin" })
                })
                .ToList();

            return Json(new GridModel<ForumGroupModel>
            {
                Rows = rows,
                Total = groups.Count
            });
        }

        #region Settings

        [Permission(ForumPermissions.Read)]
        [LoadSetting]
        public IActionResult ForumSettings(ForumSettings settings, int storeScope)
        {
            var model = MiniMapper.Map<ForumSettings, ForumSettingsModel>(settings);

            AddLocales(model.Locales, (locale, languageId) =>
            {
                locale.MetaTitle = settings.GetLocalizedSetting(x => x.MetaTitle, languageId, storeScope, false, false);
                locale.MetaDescription = settings.GetLocalizedSetting(x => x.MetaDescription, languageId, storeScope, false, false);
                locale.MetaKeywords = settings.GetLocalizedSetting(x => x.MetaKeywords, languageId, storeScope, false, false);
            });

            return View(model);
        }

        [Permission(ForumPermissions.Update)]
        [HttpPost, SaveSetting]
        public async Task<IActionResult> ForumSettings(ForumSettingsModel model, ForumSettings settings, int storeScope)
        {
            if (!ModelState.IsValid)
            {
                return ForumSettings(settings, storeScope);
            }

            ModelState.Clear();
            MiniMapper.Map(model, settings);

            foreach (var localized in model.Locales)
            {
                await _localizedEntityService.ApplyLocalizedSettingAsync(settings, x => x.MetaTitle, localized.MetaTitle, localized.LanguageId, storeScope);
                await _localizedEntityService.ApplyLocalizedSettingAsync(settings, x => x.MetaDescription, localized.MetaDescription, localized.LanguageId, storeScope);
                await _localizedEntityService.ApplyLocalizedSettingAsync(settings, x => x.MetaKeywords, localized.MetaKeywords, localized.LanguageId, storeScope);
            }

            return RedirectToAction("ForumSettings");
        }

        [Permission(ForumPermissions.Read)]
        public async Task<IActionResult> ForumSearchSettings()
        {
            // INFO: set HtmlFieldPrefix early because StoreDependingSettingHelper use it to create override key names.
            ViewData.TemplateInfo.HtmlFieldPrefix = "CustomProperties[ForumSearchSettings]";

            var i = 0;
            var storeScope = GetActiveStoreScopeConfiguration();
            var languages = await _languageService.GetAllLanguagesAsync(true);
            var megaSearchDescriptor = Services.ApplicationContext.ModuleCatalog.GetModuleByName("Smartstore.MegaSearch");
            var megaSearchPlusDescriptor = Services.ApplicationContext.ModuleCatalog.GetModuleByName("Smartstore.MegaSearchPlus");
            var settings = await Services.SettingFactory.LoadSettingsAsync<ForumSearchSettings>(storeScope);

            var model = MiniMapper.Map<ForumSearchSettings, ForumSearchSettingsModel>(settings);

            model.ForumFacet.Disabled = settings.ForumDisabled;
            model.ForumFacet.DisplayOrder = settings.ForumDisplayOrder;
            model.CustomerFacet.Disabled = settings.CustomerDisabled;
            model.CustomerFacet.DisplayOrder = settings.CustomerDisplayOrder;
            model.DateFacet.Disabled = settings.DateDisabled;
            model.DateFacet.DisplayOrder = settings.DateDisplayOrder;

            await _settingHelper.GetOverrideKeysAsync(settings, model, storeScope);

            foreach (var language in _languageService.GetAllLanguages(true))
            {
                var forumKey = FacetUtility.GetFacetAliasSettingKey(FacetGroupKind.Forum, language.Id, "Forum");
                var customerKey = FacetUtility.GetFacetAliasSettingKey(FacetGroupKind.Customer, language.Id, "Forum");
                var dateKey = FacetUtility.GetFacetAliasSettingKey(FacetGroupKind.Date, language.Id, "Forum");

                await _settingHelper.GetOverrideKeyAsync($"CustomProperties[ForumSearchSettings].ForumFacet.Locales[{i}].Alias", forumKey, storeScope);
                await _settingHelper.GetOverrideKeyAsync($"CustomProperties[ForumSearchSettings].CustomerFacet.Locales[{i}].Alias", forumKey, storeScope);
                await _settingHelper.GetOverrideKeyAsync($"CustomProperties[ForumSearchSettings].DateFacet.Locales[{i}].Alias", forumKey, storeScope);

                model.ForumFacet.Locales.Add(new ForumFacetSettingsLocalizedModel
                {
                    LanguageId = language.Id,
                    Alias = Services.Settings.GetSettingByKey<string>(forumKey, null, storeScope)
                });
                model.CustomerFacet.Locales.Add(new ForumFacetSettingsLocalizedModel
                {
                    LanguageId = language.Id,
                    Alias = Services.Settings.GetSettingByKey<string>(customerKey, null, storeScope)
                });
                model.DateFacet.Locales.Add(new ForumFacetSettingsLocalizedModel
                {
                    LanguageId = language.Id,
                    Alias = Services.Settings.GetSettingByKey<string>(dateKey, null, storeScope)
                });

                i++;
            }

            foreach (var prefix in new string[] { "Forum", "Customer", "Date" })
            {
                await _settingHelper.GetOverrideKeyAsync($"CustomProperties[ForumSearchSettings].{prefix}Facet.Disabled", prefix + "Disabled", settings, storeScope);
                await _settingHelper.GetOverrideKeyAsync($"CustomProperties[ForumSearchSettings].{prefix}Facet.DisplayOrder", prefix + "DisplayOrder", settings, storeScope);
            }


            ViewBag.IsMegaSearchInstalled = megaSearchDescriptor != null;

            ViewBag.SearchModes = settings.SearchMode.ToSelectList()
                .Where(x => megaSearchDescriptor != null || x.Value.ToInt() != (int)SearchMode.ExactMatch)
                .ToList();

            ViewBag.SearchFields = new List<SelectListItem>
            {
                new SelectListItem { Text = T("Admin.Customers.Customers.Fields.Username"), Value = "username" },
                new SelectListItem { Text = T("Forum.PostText"), Value = "text" },
            };

            return PartialView(model);
        }

        #endregion
    }
}
