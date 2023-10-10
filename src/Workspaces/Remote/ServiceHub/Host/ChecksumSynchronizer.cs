﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Serialization;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Remote
{
    internal readonly struct ChecksumSynchronizer(AssetProvider assetProvider)
    {
        // make sure there is always only 1 bulk synchronization
        private static readonly SemaphoreSlim s_gate = new SemaphoreSlim(initialCount: 1);

        private readonly AssetProvider _assetProvider = assetProvider;

        public async ValueTask SynchronizeAssetsAsync(HashSet<Checksum> checksums, CancellationToken cancellationToken)
        {
            using (await s_gate.DisposableWaitAsync(cancellationToken).ConfigureAwait(false))
            {
                // get children of solution checksum object at once
                await _assetProvider.SynchronizeAssetsAsync(checksums, cancellationToken).ConfigureAwait(false);
            }
        }

        public async ValueTask SynchronizeSolutionAssetsAsync(Checksum solutionChecksum, CancellationToken cancellationToken)
        {
            SolutionStateChecksums solutionChecksumObject;
            using (await s_gate.DisposableWaitAsync(cancellationToken).ConfigureAwait(false))
            {
                // this will make 4 round trip to data source (VS) to get all assets that belong to the given solution checksum

                // first, get solution checksum object for the given solution checksum
                solutionChecksumObject = await _assetProvider.GetAssetAsync<SolutionStateChecksums>(solutionChecksum, cancellationToken).ConfigureAwait(false);

                // second, get direct children of the solution
                {
                    using var pooledObject = SharedPools.Default<HashSet<Checksum>>().GetPooledObject();
                    var checksums = pooledObject.Object;

                    solutionChecksumObject.AddAllTo(checksums);
                    checksums.Remove(solutionChecksumObject.Checksum);
                    await _assetProvider.SynchronizeAssetsAsync(checksums, cancellationToken).ConfigureAwait(false);
                }
            }

            // third and last get direct children for all projects and documents in the solution 
            foreach (var project in solutionChecksumObject.Projects)
            {
                var projectStateChecksums = _assetProvider.GetRequiredAsset<ProjectStateChecksums>(project);
                await SynchronizeProjectAssetsAsync(projectStateChecksums, cancellationToken).ConfigureAwait(false);
            }
        }

        public async ValueTask SynchronizeProjectAssetsAsync(ProjectStateChecksums projectChecksum, CancellationToken cancellationToken)
        {
            using (await s_gate.DisposableWaitAsync(cancellationToken).ConfigureAwait(false))
            {
                await SynchronizeProjectAssets_NoLockAsync(projectChecksum, cancellationToken).ConfigureAwait(false);
            }
        }

        private async ValueTask SynchronizeProjectAssets_NoLockAsync(ProjectStateChecksums projectChecksum, CancellationToken cancellationToken)
        {
            // get children of project checksum objects at once
            using var pooledObject = SharedPools.Default<HashSet<Checksum>>().GetPooledObject();
            var checksums = pooledObject.Object;

            AddIfNeeded(checksums, projectChecksum.Info);
            AddIfNeeded(checksums, projectChecksum.CompilationOptions);
            AddIfNeeded(checksums, projectChecksum.ParseOptions);
            AddIfNeeded(checksums, projectChecksum.Documents);
            AddIfNeeded(checksums, projectChecksum.ProjectReferences);
            AddIfNeeded(checksums, projectChecksum.MetadataReferences);
            AddIfNeeded(checksums, projectChecksum.AnalyzerReferences);
            AddIfNeeded(checksums, projectChecksum.AdditionalDocuments);
            AddIfNeeded(checksums, projectChecksum.AnalyzerConfigDocuments);

            await _assetProvider.SynchronizeAssetsAsync(checksums, cancellationToken).ConfigureAwait(false);

            checksums.Clear();

            CollectChecksumChildren(this, projectChecksum.Documents);
            CollectChecksumChildren(this, projectChecksum.AdditionalDocuments);
            CollectChecksumChildren(this, projectChecksum.AnalyzerConfigDocuments);

            await _assetProvider.SynchronizeAssetsAsync(checksums, cancellationToken).ConfigureAwait(false);

            void CollectChecksumChildren(ChecksumSynchronizer @this, ChecksumCollection collection)
            {
                foreach (var checksum in collection)
                {
                    // These DocumentStateChecksums must be here due to the synchronizing step that just happened above. 
                    var checksumObject = @this._assetProvider.GetRequiredAsset<DocumentStateChecksums>(checksum);
                    @this.AddIfNeeded(checksums, checksumObject.Info);
                    @this.AddIfNeeded(checksums, checksumObject.Text);
                }
            }
        }

        private void AddIfNeeded(HashSet<Checksum> checksums, ChecksumCollection checksumCollection)
        {
            foreach (var checksum in checksumCollection)
                AddIfNeeded(checksums, checksum);
        }

        private void AddIfNeeded(HashSet<Checksum> checksums, Checksum checksum)
        {
            if (checksum != Checksum.Null && !_assetProvider.EnsureCacheEntryIfExists(checksum))
                checksums.Add(checksum);
        }
    }
}
