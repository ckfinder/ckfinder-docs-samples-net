/*
 * CKFinder
 * ========
 * http://cksource.com/ckfinder
 * Copyright (C) 2007-2016, CKSource - Frederico Knabben. All rights reserved.
 *
 * The software, this file and its contents are subject to the MIT License.
 * Please read the LICENSE.md file before using, installing, copying,
 * modifying or distribute this file or part of its contents.
 */

[assembly: Microsoft.Owin.OwinStartup(typeof(CKSource.CKFinder.Connector.WebApp.Startup))]

namespace CKSource.CKFinder.Connector.WebApp
{
    using System.Configuration;

    using CKSource.CKFinder.Connector.Config;
    using CKSource.CKFinder.Connector.Core.Builders;
    using CKSource.CKFinder.Connector.Core.Logs;
    using CKSource.CKFinder.Connector.Host.Owin;
    using CKSource.CKFinder.Connector.Logs.NLog;
    using CKSource.CKFinder.Connector.KeyValue.EntityFramework;
    using CKSource.FileSystem.Dropbox;
    using CKSource.FileSystem.Local;
    using CKSource.FileSystem.Amazon;
    using CKSource.FileSystem.Azure;

    using Microsoft.Owin.Security;
    using Microsoft.Owin.Security.Cookies;

    using Owin;

    public class Startup
    {
        public void Configuration(IAppBuilder builder)
        {
            LoggerManager.LoggerAdapterFactory = new NLogLoggerAdapterFactory();

            RegisterFileSystems();

            builder.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = "ApplicationCookie",
                AuthenticationMode = AuthenticationMode.Active
            });

            var route = ConfigurationManager.AppSettings["ckfinderRoute"];
            builder.Map(route, SetupConnector);
        }

        private static void RegisterFileSystems()
        {
            FileSystemFactory.RegisterFileSystem<LocalStorage>();
            FileSystemFactory.RegisterFileSystem<DropboxStorage>();
            FileSystemFactory.RegisterFileSystem<AmazonStorage>();
            FileSystemFactory.RegisterFileSystem<AzureStorage>();
        }

        private static void SetupConnector(IAppBuilder builder)
        {
            var keyValueStoreProvider = new EntityFrameworkKeyValueStoreProvider("CacheConnectionString");

            var allowedRoleMatcherTemplate = ConfigurationManager.AppSettings["ckfinderAllowedRole"];
            var authenticator = new RoleBasedAuthenticator(allowedRoleMatcherTemplate);
            
            var connectorFactory = new OwinConnectorFactory();
            var connectorBuilder = new ConnectorBuilder();
            var connector = connectorBuilder
                .LoadConfig()
                .SetAuthenticator(authenticator)
                .SetRequestConfiguration(
                    (request, config) =>
                    {
                        config.LoadConfig();
                        config.SetKeyValueStoreProvider(keyValueStoreProvider);
                    })
                .Build(connectorFactory);

            builder.UseConnector(connector);
        }
    }
}
