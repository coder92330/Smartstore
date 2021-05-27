﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smartstore.Admin.Models.Logging;
using Smartstore.Core.Common.Services;
using Smartstore.Core.Common.Settings;
using Smartstore.Core.Data;
using Smartstore.Core.Localization;
using Smartstore.Core.Logging;
using Smartstore.Core.Security;
using Smartstore.Data.Batching;
using Smartstore.Web.Controllers;
using Smartstore.Web.Modelling.DataGrid;
using Smartstore.Web.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Smartstore.Admin.Controllers
{
    public class LogController : AdminControllerBase
    {
        private readonly SmartDbContext _db;
        private readonly IDbLogService _dbLogService;
        private readonly IDateTimeHelper _dateTimeHelper;
        private readonly ILocalizationService _localizationService;
        private readonly AdminAreaSettings _adminAreaSettings;

        private static readonly Dictionary<LogLevel, string> _logLevelHintMap = new()
        {
            { LogLevel.Fatal, "dark" },
            { LogLevel.Error, "danger" },
            { LogLevel.Warning, "warning" },
            { LogLevel.Information, "info" },
            { LogLevel.Debug, "secondary" }
        };

        public LogController(
            SmartDbContext db,
            IDbLogService dbLogService,
            IDateTimeHelper dateTimeHelper,
            ILocalizationService localizationService,
            AdminAreaSettings adminAreaSettings)
        {
            _db = db;
            _dbLogService = dbLogService;
            _dateTimeHelper = dateTimeHelper;
            _localizationService = localizationService;
            _adminAreaSettings = adminAreaSettings;
        }

        public ActionResult Index()
        {
            return RedirectToAction(nameof(List));
        }

        [Permission(Permissions.System.Log.Read)]
        public ActionResult List()
        {
            var model = new LogListModel
            {
                AvailableLogLevels = LogLevel.Debug.ToSelectList(false).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [Permission(Permissions.System.Log.Read)]
        public async Task<IActionResult> LogList(GridCommand command, LogListModel model)
        {
            DateTime? createdOnFrom = model.CreatedOnFrom != null
                ? _dateTimeHelper.ConvertToUtcTime(model.CreatedOnFrom.Value, _dateTimeHelper.CurrentTimeZone)
                : null;

            DateTime? createdOnTo = model.CreatedOnTo != null
                ? _dateTimeHelper.ConvertToUtcTime(model.CreatedOnTo.Value, _dateTimeHelper.CurrentTimeZone).AddDays(1)
                : null;

            LogLevel? logLevel = model.LogLevelId > 0 ? (LogLevel?)model.LogLevelId : null;

            var query = _db.Logs.AsNoTracking()
                .ApplyDateFilter(createdOnFrom, createdOnTo)
                .ApplyLoggerFilter(model.Logger)
                .ApplyMessageFilter(model.Message)
                .ApplyLevelFilter(logLevel)
                .ApplyGridCommand(command, false)
                .OrderByDescending(x => x.CreatedOnUtc);

            var logItems = await query.ToPagedList(command.Page - 1, command.PageSize).LoadAsync();

            var gridModel = new GridModel<LogModel>
            {
                Rows = logItems.Select(x =>
                {
                    var logModel = PrepareLogModel(x);
                    return logModel;
                }),

                Total = logItems.TotalCount
            };

            return Json(gridModel);
        }

        [HttpPost]
        [Permission(Permissions.System.Log.Delete)]
        public async Task<IActionResult> LogDelete(GridSelection selection)
        {
            var ids = selection.GetEntityIds().ToList();
            var numDeleted = 0;
            if (ids.Any())
            {
                numDeleted = await _db.Logs
                    .Where(x => ids.Contains(x.Id))
                    .BatchDeleteAsync();
            }

            return Json(new { Success = true, Count = numDeleted });
        }

        [HttpPost, ActionName("List")]
        [FormValueRequired("clearall")]
        [Permission(Permissions.System.Log.Delete)]
        public async Task<IActionResult> LogClear()
        {
            await _dbLogService.ClearLogsAsync();
            NotifySuccess(_localizationService.GetResource("Admin.System.Log.Cleared"));
            return RedirectToAction(nameof(List));
        }

        [Permission(Permissions.System.Log.Read)]
        public async Task<IActionResult> View(int id)
        {
            var log = await _db.Logs.FindByIdAsync(id);
            if (log == null)
            {
                // No log found with the specified id.
                return RedirectToAction(nameof(List));
            }

            var model = PrepareLogModel(log);

            return View(model);
        }

        [NonAction]
        private static string TruncateLoggerName(string loggerName)
        {
            if (loggerName.IndexOf('.') < 0)
            {
                return loggerName;
            }

            var name = string.Empty;
            var tokens = loggerName.Split('.');
            for (int i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                name += i == tokens.Length - 1
                    ? token
                    : token.Substring(0, 1) + "...";
            }

            return name;
        }

        [NonAction]
        private LogModel PrepareLogModel(Log log)
        {
            var model = new LogModel
            {
                Id = log.Id,
                LogLevelHint = _logLevelHintMap[log.LogLevel],
                LogLevel = log.LogLevel.GetLocalizedEnum(),
                ShortMessage = log.ShortMessage,
                FullMessage = log.FullMessage,
                IpAddress = log.IpAddress,
                CustomerId = log.CustomerId,
                CustomerEmail = log.Customer?.Email,
                PageUrl = log.PageUrl,
                ReferrerUrl = log.ReferrerUrl,
                CreatedOn = _dateTimeHelper.ConvertToUserTime(log.CreatedOnUtc, DateTimeKind.Utc),
                Logger = log.Logger,
                LoggerShort = TruncateLoggerName(log.Logger),
                HttpMethod = log.HttpMethod,
                UserName = log.UserName,
                ViewUrl = Url.Action("View", "Log", new { id = log.Id })
            };

            return model;
        }
    }
}
