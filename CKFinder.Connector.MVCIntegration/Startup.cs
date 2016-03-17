[assembly: Microsoft.Owin.OwinStartup(typeof(CKSource.CKFinder.Connector.MVCIntegration.Startup))]
namespace CKSource.CKFinder.Connector.MVCIntegration
{
    using CKSource.CKFinder.Connector.Config;
    using CKSource.CKFinder.Connector.Core.Builders;
    using CKSource.CKFinder.Connector.Core.Logs;
    using CKSource.CKFinder.Connector.Host.Owin;
    using CKSource.CKFinder.Connector.KeyValue.EntityFramework;
    using CKSource.CKFinder.Connector.Logs.NLog;
    using CKSource.FileSystem.Local;

    using Owin;

    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
            /*
             * Start the logger.
             */
            LoggerManager.LoggerAdapterFactory = new NLogLoggerAdapterFactory();
            /*
             * Register the "local" type backend file system.
             */
            FileSystemFactory.RegisterFileSystem<LocalStorage>();
            /*
             * Map the CKFinder connector service under given path. By default CKFinder JavaScript client
             * expect the .NET connector to be accessible under "/ckfinder/connector" route.
             */
            app.Map("/ckfinder/connector", SetupConnector);
        }
        private static void SetupConnector(IAppBuilder app)
        {
            /*
             * Create a key-value store provider to be used for saving CKFinder cache data.
             */
            var keyValueStoreProvider = new EntityFrameworkKeyValueStoreProvider("CKFinderCacheConnection");
            /*
             * Create connector instance using ConnectorBuilder. The call to LoadConfig() method
             * will configure the connector using CKFinder configuration options defined in Web.config.
             */
            var connectorFactory = new OwinConnectorFactory();
            var connectorBuilder = new ConnectorBuilder();
            var customAuthenticator = new CustomCKFinderAuthenticator();
            var connector = connectorBuilder
                .LoadConfig()
                .SetRequestConfiguration(
                    (request, config) =>
                    {
                        config.LoadConfig();
                        config.SetKeyValueStoreProvider(keyValueStoreProvider);
                    })
                .SetAuthenticator(customAuthenticator)
                .Build(connectorFactory);

            /*
             * Add the CKFinder connector middleware to web application pipeline.
             */
            app.UseConnector(connector);
        }
    }
}