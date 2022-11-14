﻿using Smartstore.Core.Content.Media;

namespace Smartstore.Web.Api.Controllers.OData
{
    /// <summary>
    /// The endpoint for operations on Download entity.
    /// </summary>
    public class DownloadsController : WebApiController<Download>
    {
        [HttpGet, ApiQueryable]
        [Permission(Permissions.Media.Download.Read)]
        public IQueryable<Download> Get()
        {
            return Entities.AsNoTracking();
        }

        [HttpGet, ApiQueryable]
        [Permission(Permissions.Media.Download.Read)]
        public SingleResult<Download> Get(int key)
        {
            return GetById(key);
        }

        [HttpGet, ApiQueryable]
        [Permission(Permissions.Media.Download.Read)]
        public SingleResult<MediaFile> GetMediaFile(int key)
        {
            return GetRelatedEntity(key, x => x.MediaFile);
        }

        [HttpPost]
        [Permission(Permissions.Media.Download.Create)]
        public Task<IActionResult> Post([FromBody] Download entity)
        {
            return PostAsync(entity);
        }

        [HttpPut]
        [Permission(Permissions.Media.Download.Update)]
        public Task<IActionResult> Put(int key, Delta<Download> model)
        {
            return PutAsync(key, model);
        }

        [HttpPatch]
        [Permission(Permissions.Media.Download.Update)]
        public Task<IActionResult> Patch(int key, Delta<Download> model)
        {
            return PatchAsync(key, model);
        }

        [HttpDelete]
        [Permission(Permissions.Media.Download.Delete)]
        public Task<IActionResult> Delete(int key)
        {
            return DeleteAsync(key);
        }
    }
}
