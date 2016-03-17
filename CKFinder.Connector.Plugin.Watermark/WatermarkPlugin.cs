namespace CKSource.CKFinder.Connector.Plugin.Watermark
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Drawing;
    using System.Linq;
    using System.Threading.Tasks;

    using CKSource.CKFinder.Connector.Core;
    using CKSource.CKFinder.Connector.Core.Events;
    using CKSource.CKFinder.Connector.Core.Events.Messages;
    using CKSource.CKFinder.Connector.Core.Nodes;
    using CKSource.CKFinder.Connector.Core.Plugins;
    using CKSource.CKFinder.Connector.Core.ResizedImages;

    using ImageProcessor;
    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging;

    [Export(typeof(IPlugin))]
    public class WatermarkPlugin : IPlugin, IDisposable
    {
        private Image _watermarkImage;

        private object _fileUploadSubscription;

        private IEventAggregator _eventAggregator;

        public void Initialize(IComponentResolver componentResolver, IReadOnlyDictionary<string, IReadOnlyCollection<string>> options)
        {
            var watermarkImagePath = options["watermarkPath"].First();

            _watermarkImage = Image.FromFile(watermarkImagePath);

            _eventAggregator = componentResolver.Resolve<IEventAggregator>();
            _fileUploadSubscription = _eventAggregator.Subscribe<FileUploadEvent>(next => async messageContext => await OnFileUpload(messageContext, next));
        }

        public void Dispose()
        {
            _eventAggregator.Unsubscribe(_fileUploadSubscription);
            _watermarkImage.Dispose();
        }

        private async Task OnFileUpload(MessageContext<FileUploadEvent> messageContext, EventHandlerFunc<FileUploadEvent> next)
        {
            var stream = messageContext.Message.Stream;
            var imageSettings = messageContext.ComponentResolver.Resolve<IImageSettings>();

            var originalPosition = stream.Position;

            using (var imageFactory = new ImageFactory())
            {
                try
                {
                    imageFactory.Load(stream);
                    stream.Position = originalPosition;
                }
                catch (ImageFormatException)
                {
                    // This is not an image or the format is not supported.
                    await next(messageContext);
                    return;
                }

                imageFactory.Overlay(new ImageLayer
                {
                    Image = _watermarkImage, Opacity = 50, Size = imageFactory.Image.Size
                });

                var format = ImageFormats.GetFormatForFile(messageContext.Message.File);
                format.Quality = imageSettings.Quality;

                imageFactory.Format(format);
                imageFactory.Save(stream);
            }

            await next(messageContext);
        }
    }
}
