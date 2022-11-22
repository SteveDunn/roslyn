﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.SQLite.v2.Interop;
using Microsoft.CodeAnalysis.Storage;

namespace Microsoft.CodeAnalysis.SQLite.v2
{
    using static SQLitePersistentStorageConstants;

    internal partial class SQLitePersistentStorage
    {
        protected override Task<bool> ChecksumMatchesAsync(ProjectKey projectKey, Project? project, string name, Checksum checksum, CancellationToken cancellationToken)
            => _projectAccessor.ChecksumMatchesAsync((projectKey, name), checksum, cancellationToken);

        protected override Task<Stream?> ReadStreamAsync(ProjectKey projectKey, Project? project, string name, Checksum? checksum, CancellationToken cancellationToken)
            => _projectAccessor.ReadStreamAsync((projectKey, name), checksum, cancellationToken);

        protected override Task<bool> WriteStreamAsync(ProjectKey projectKey, Project? project, string name, Stream stream, Checksum? checksum, CancellationToken cancellationToken)
            => _projectAccessor.WriteStreamAsync((projectKey, name), stream, checksum, cancellationToken);

        private readonly record struct ProjectPrimaryKey(int ProjectPathId, int ProjectNameId);

        /// <summary>
        /// <see cref="Accessor{TKey, TDatabaseId}"/> responsible for storing and
        /// retrieving data from <see cref="ProjectDataTableName"/>.
        /// </summary>
        private sealed class ProjectAccessor : Accessor<
            (ProjectKey projectKey, string name),
            (ProjectPrimaryKey projectKeyId, int dataNameId)>
        {
            public ProjectAccessor(SQLitePersistentStorage storage)
                : base(Table.Project,
                      storage,
                      (ProjectPathIdColumnName, SQLiteIntegerType),
                      (ProjectNameIdColumnName, SQLiteIntegerType),
                      (DataNameIdColumnName, SQLiteIntegerType))
            {
            }

            protected override (ProjectPrimaryKey projectKeyId, int dataNameId)? TryGetDatabaseId(SqlConnection connection, (ProjectKey projectKey, string name) key, bool allowWrite)
                => Storage.TryGetProjectDataId(connection, key.projectKey, key.name, allowWrite);

            protected override void BindPrimaryKeyParameters(SqlStatement statement, (ProjectPrimaryKey projectKeyId, int dataNameId) dataId)
            {
                var ((projectPathId, projectNameId), dataNameId) = dataId;

                statement.BindInt64Parameter(parameterIndex: 1, projectPathId);
                statement.BindInt64Parameter(parameterIndex: 2, projectNameId);
                statement.BindInt64Parameter(parameterIndex: 3, dataNameId);
            }
        }
    }
}
