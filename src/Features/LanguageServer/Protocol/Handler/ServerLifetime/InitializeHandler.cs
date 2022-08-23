﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.LanguageServer.Handler;

[ExportGeneralStatelessLspService(typeof(InitializeHandler)), Shared]
[Method(Methods.InitializeName)]
internal class InitializeHandler : ILspServiceRequestHandler<InitializeParams, InitializeResult>
{
    [ImportingConstructor]
    [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public InitializeHandler()
    {
    }

    public bool MutatesSolutionState => true;

    public object? GetTextDocumentIdentifier(InitializeParams request)
    {
        return null;
    }

    public async Task<InitializeResult> HandleRequestAsync(InitializeParams request, RequestContext context, CancellationToken cancellationToken)
    {
        var logger = context.GetRequiredLspService<ILspServiceLogger>();
        try
        {
            await logger.LogStartContextAsync("Initialize", cancellationToken).ConfigureAwait(false);

            var clientCapabilitiesManager = context.GetRequiredLspService<IClientCapabilitiesManager>();
            var clientCapabilities = clientCapabilitiesManager.TryGetClientCapabilities();
            if (clientCapabilities != null)
            {
                throw new InvalidOperationException($"{nameof(Methods.InitializeName)} called multiple times");
            }

            clientCapabilities = request.Capabilities;
            clientCapabilitiesManager.SetClientCapabilities(clientCapabilities);

            var capabilitiesProvider = context.GetRequiredLspService<ICapabilitiesProvider>();
            var serverCapabilities = capabilitiesProvider.GetCapabilities(clientCapabilities);

            return new InitializeResult
            {
                Capabilities = serverCapabilities,
            };
        }
        finally
        {
            await logger.LogEndContextAsync("Initialize", cancellationToken).ConfigureAwait(false);
        }
    }
}
