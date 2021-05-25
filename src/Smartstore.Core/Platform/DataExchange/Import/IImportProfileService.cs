﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Smartstore.IO;

namespace Smartstore.Core.DataExchange.Import
{
    public partial interface IImportProfileService
    {
        /// <summary>
        /// Gets a temporary directory for an import profile.
        /// </summary>
        /// <param name="profile">Import profile.</param>
        /// <param name="subpath">Optional subpath, e.g. "Content" to get the content subfolder.</param>
        /// <param name="createIfNotExists">A value indicating whether the folder should be created if it does not exist.</param>
        /// <returns>Import directory.</returns>
        Task<IDirectory> GetImportDirectoryAsync(ImportProfile profile, string subpath = null, bool createIfNotExists = false);

        /// <summary>
        /// Gets import files for an import profile.
        /// </summary>
        /// <param name="profile">Import profile.</param>
        /// <param name="includeRelatedFiles">
        /// A value indicating whether to include related data files.
        /// A related data file contains, for example, tier price data that is to be imported together with the associated products.
        /// </param>
        /// <returns>List of import files.</returns>
        Task<IList<ImportFile>> GetImportFilesAsync(ImportProfile profile, bool includeRelatedFiles = true);

        /// <summary>
        /// Gets a new profile name.
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <returns>Suggestion for a new profile name.</returns>
        Task<string> GetNewProfileNameAsync(ImportEntityType entityType);

        /// <summary>
        /// Inserts an import profile.
        /// </summary>
        /// <param name="fileName">Name of the import file</param>
        /// <param name="name">Profile name</param>
        /// <param name="entityType">Entity type</param>
        /// <returns>Inserted import profile</returns>
        Task<ImportProfile> InsertImportProfileAsync(string fileName, string name, ImportEntityType entityType);

        /// <summary>
        /// Deletes an import profile.
        /// </summary>
        /// <param name="profile">Import profile.</param>
        Task DeleteImportProfileAsync(ImportProfile profile);
    }
}
