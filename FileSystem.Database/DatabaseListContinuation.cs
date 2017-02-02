namespace FileSystem.Database
{
    using CKSource.FileSystem;

    internal class DatabaseListContinuation : IFolderListContinuation, IFileListContinuation
    {
        public DatabaseListContinuation(string path, int lastIndex)
        {
            Path = path;
            LastIndex = lastIndex;
        }

        public string Path { get; }

        public int LastIndex { get; }
    }
}