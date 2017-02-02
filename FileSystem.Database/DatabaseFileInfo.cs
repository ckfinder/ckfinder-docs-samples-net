namespace FileSystem.Database
{
    using System.Threading;
    using System.Threading.Tasks;

    using CKSource.FileSystem;

    internal class DatabaseFileInfo : FileInfo
    {
        public DatabaseFileInfo(DatabaseNode node)
            : base(Path.GetFileName(node.Path))
        {
            Size = node.Size;
            UpdateDate = node.Timestamp;
            MimeType = node.MimeType;
        }

        protected override Task LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }
}