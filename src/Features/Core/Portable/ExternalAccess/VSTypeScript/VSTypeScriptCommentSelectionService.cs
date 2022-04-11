﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CommentSelection;
using Microsoft.CodeAnalysis.ExternalAccess.VSTypeScript.Api;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.ExternalAccess.VSTypeScript
{
    [ExportLanguageService(typeof(ICommentSelectionService), InternalLanguageNames.TypeScript), Shared]
    internal sealed class VSTypeScriptCommentSelectionService : ICommentSelectionService
    {
        private readonly IVSTypeScriptCommentSelectionServiceImplementation _impl;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public VSTypeScriptCommentSelectionService(IVSTypeScriptCommentSelectionServiceImplementation impl)
            => _impl = impl;

        public async Task<CommentSelectionInfo> GetInfoAsync(Document document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            var info = await _impl.GetInfoAsync(document, textSpan, cancellationToken).ConfigureAwait(false);
            return info.UnderlyingObject;
        }

        public Task<Document> FormatAsync(Document document, ImmutableArray<TextSpan> changes, SyntaxFormattingOptions formattingOptions, CancellationToken cancellationToken)
            => _impl.FormatAsync(document, changes, cancellationToken);
    }
}
